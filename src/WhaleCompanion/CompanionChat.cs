using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImGuiNET;

namespace WhaleCompanion;

public sealed class CompanionChat : IDisposable
{
    public static string SecretPath => Path.Combine(ConfigurationDirectory, "deepseek.dpapi");
    public static string ConfigurationDirectory => Path.Combine(AppContext.BaseDirectory, "nativePC", "plugins", "CSharp", "WhaleCompanion", "config");
    private ChatConfiguration configuration = ChatConfiguration.Default;
    private LongTermMemory? memories;
    private string memoryDraft = "", memorySource = "";
    private string? memoryEditId;
    private bool confirmMemoryClear;
    private static string MemoryPath => Path.Combine(ConfigurationDirectory, "saved-memories.json");
    private string gameContext = "未提供实时状态。";
    private string peerContext = "";
    public void UpdatePeerContext(string value){lock(gate)peerContext=value;}
    public CompanionChat() { ReloadConfiguration(); }
    public bool ReloadConfiguration()
    {
        lock (gate)
        {
            try
            {
                var candidate = ChatConfiguration.Load(ConfigurationDirectory);
                var candidateMemories = LongTermMemory.Load(MemoryPath);
                ResetCore(); configuration = candidate; memories = candidateMemories;
                Status = "Configuration loaded; conversation cleared"; return true;
            }
            catch { Status = "Configuration invalid/unreadable; previous settings retained"; return false; }
        }
    }
    public void UpdateGameContext(string text) { lock (gate) gameContext = text; }
    private readonly object gate = new();
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly List<ChatMessage> history = new();
    private Task<ChatResult>? pending;
    private CancellationTokenSource? cancellation;
    private bool automatic;
    private Action<string?>? automaticCompletion;
    private bool pendingNativeDecision;
    public bool CanAcceptNative => !Busy || automatic;
    public event Action<bool, bool>? NativeDecisionMade;
    public event Action<string>? NativeTrace;
    public string NativeStatus { get; private set; } = "等待主人消息";
    private Func<bool>? relevant;
    public event Action<CompanionReply>? ReplyReceived;
    public event Action<string>? LineReady;
    public event Action<string>? ConversationReplyReady;
    public event Action? ConversationReset;
    public string LastLine { get; private set; } = "";
    private string input = "", keyInput = "";
    private string modelDraft="",endpointDraft="",personaDraft="";
    private bool editingConnection;
    public string Status { get; private set; } = "Ready";
    public IReadOnlyList<ChatMessage> History => history;
    public bool Busy => pending != null;

    public void Render() { lock (gate) RenderCore(); }
    private void RenderCore()
    {
        ImGui.Separator();
        ImGui.TextUnformatted($"AI companion / {configuration.Settings.Model}");
        ImGui.TextWrapped($"Config: {ConfigurationDirectory}");
        ImGui.TextUnformatted($"Context: {configuration.Settings.ContextTurns} turns / {configuration.Settings.MaxContextCharacters} characters; replies {configuration.Settings.MaxReplyTokens} tokens");
        if (ImGui.Button(UiLanguage.Text("Reload configuration (clears conversation)","重新加载配置（清空当前对话）","重新載入設定（清空目前對話）"))) ReloadConfiguration();
        if(ImGui.CollapsingHeader(UiLanguage.Text("AI model and persona","AI模型与人设","AI模型與人設")))
        {
            if(!editingConnection){modelDraft=configuration.Settings.Model;endpointDraft=configuration.Settings.Endpoint;personaDraft=configuration.Persona;editingConnection=true;}
            ImGui.InputText(UiLanguage.Text("Model ID","模型ID","模型ID"),ref modelDraft,256);
            ImGui.InputText(UiLanguage.Text("Chat completions URL","聊天接口完整URL","聊天介面完整URL"),ref endpointDraft,1024);
            ImGui.InputTextMultiline(UiLanguage.Text("External persona","外置人设","外置人設"),ref personaDraft,64000,new System.Numerics.Vector2(650,160));
            if(ImGui.Button(UiLanguage.Text("Save model and persona","保存模型与人设","儲存模型與人設")))
            {
                try
                {
                    var settings=configuration.Settings with{Model=modelDraft.Trim(),NativeChatModel=modelDraft.Trim(),Endpoint=endpointDraft.Trim()};settings.Validate();
                    if(string.IsNullOrWhiteSpace(personaDraft)||personaDraft.Length>16000)throw new InvalidDataException();
                    Directory.CreateDirectory(ConfigurationDirectory);
                    OfflineDialogue.AtomicWrite(Path.Combine(ConfigurationDirectory,"settings.json"),JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
                    OfflineDialogue.AtomicWrite(Path.Combine(ConfigurationDirectory,"persona.md"),personaDraft);
                    ReloadConfiguration();editingConnection=false;
                }
                catch {Status="Model/persona not saved: invalid input or write failure";}
            }
        }
        RenderMemories();
        ImGui.TextUnformatted("API key is encrypted for the current Windows user.");
        ImGui.InputText(UiLanguage.Text("API key (optional replacement)","API密钥（可替换）","API金鑰（可替換）"), ref keyInput, 256, ImGuiInputTextFlags.Password);
        if (ImGui.Button(UiLanguage.Text("Save encrypted key","加密保存密钥","加密儲存金鑰")) && !string.IsNullOrWhiteSpace(keyInput))
        {
            try { SaveKey(keyInput); keyInput = ""; Status = "Key saved"; }
            catch { Status = "Could not save encrypted key"; }
        }
        ImGui.TextUnformatted(Status);
        foreach (var message in history) ImGui.TextWrapped($"{message.role}: {message.content}");
        ImGui.InputText(UiLanguage.Text("Message","聊天输入","聊天輸入"), ref input, 2048);
        if (ImGui.Button(UiLanguage.Text("Send","发送","傳送")) && (!Busy || automatic) && !string.IsNullOrWhiteSpace(input))
        {
            if (automatic) Cancel(); Start(input); input = "";
        }
        ImGui.SameLine();
        if (ImGui.Button(UiLanguage.Text("Cancel","取消","取消"))) Cancel();
        ImGui.SameLine();
        if (ImGui.Button(UiLanguage.Text("New conversation","新对话","新對話"))) Reset();
    }

    private void ChangeMemory(Action change, string status)
    {
        try { change(); ResetCore(); Status = status + "; conversation cleared"; }
        catch { Status = "Memory not saved: invalid/duplicate/full, file changed or write failed; reload if edited externally"; }
    }
    private void RenderMemories()
    {
        if (!ImGui.CollapsingHeader(UiLanguage.Text("Cross-conversation memory","跨对话记忆","跨對話記憶"))) return;
        if (memories == null) { ImGui.TextWrapped("Memory file unavailable; repair it and reload configuration."); return; }
        bool use = memories.Enabled;
        if (ImGui.Checkbox(UiLanguage.Text("Use saved memories in AI requests","AI请求使用已保存记忆","AI請求使用已儲存記憶"), ref use)) ChangeMemory(() => memories.SetEnabled(use), "Memory setting saved");
        ImGui.TextWrapped("Only Save writes a fact. New conversation keeps saved facts. memory.md is a separate manual file.");
        foreach (var entry in memories.Entries)
        {
            ImGui.PushID(entry.Id);
            ImGui.TextWrapped(entry.Text);
            ImGui.TextUnformatted($"{entry.Source} | confirmed={entry.UserConfirmed} | {entry.CreatedUtc.LocalDateTime:g}");
            if (ImGui.Button(UiLanguage.Text("Edit","编辑","編輯"))) { memoryDraft = entry.Text; memoryEditId = entry.Id; memorySource = entry.SourceText; }
            ImGui.SameLine();
            if (ImGui.Button(UiLanguage.Text("Delete","删除","刪除"))) ChangeMemory(() => memories.Delete(entry.Id), "Memory deleted");
            ImGui.PopID();
        }
        if (ImGui.Button(UiLanguage.Text("Draft from last user message","从最近输入拟定记忆","從最近輸入擬定記憶")))
        {
            var source = history.LastOrDefault(m => m.role == "user")?.content;
            if (source != null) { memorySource = source; memoryDraft = source[..Math.Min(280, source.Length)]; memoryEditId = null; }
            else Status = "No user message to use; enter a fact below";
        }
        ImGui.InputText(UiLanguage.Text("Memory fact (max 280 characters)","记忆内容（最多280字）","記憶內容（最多280字）"), ref memoryDraft, 1121);
        if (ImGui.Button(memoryEditId == null ? "Save memory" : "Save edited memory"))
            ChangeMemory(() => memories.Save(memoryDraft, memorySource, memoryEditId), "Memory saved");
        ImGui.SameLine();
        if (ImGui.Button(UiLanguage.Text("Discard draft","丢弃草稿","捨棄草稿"))) { memoryDraft = memorySource = ""; memoryEditId = null; }
        ImGui.Checkbox(UiLanguage.Text("Confirm clearing all saved memories","确认清空已保存记忆","確認清空已儲存記憶"), ref confirmMemoryClear);
        if (ImGui.Button(UiLanguage.Text("Clear saved memories","清空记忆","清空記憶")) && confirmMemoryClear) ChangeMemory(memories.Clear, "Saved memories cleared; memory.md retained");
    }

    public static void SaveKey(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretPath)!);
        var bytes = Encoding.UTF8.GetBytes(key.Trim());
        try { File.WriteAllBytes(SecretPath, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public void SayLocal(string text) { lock (gate) SayLocalCore(text); }
    private void SayLocalCore(string text) { LastLine = text; LineReady?.Invoke(text); }
    public void StartEvent(string text, Func<bool> isRelevant) { lock (gate) StartEventCore(text, isRelevant); }
    private void StartEventCore(string text, Func<bool> isRelevant)
    {
        if (Busy) return;
        Start(text); automatic = true; relevant = isRelevant;
    }
    public bool StartDynamicEvent(string prompt,Func<bool> isRelevant,Action<string?> complete)
    {
        lock(gate)
        {
            if(Busy)return false;
            StartCore(prompt);if(!Busy)return false;
            automatic=true;relevant=isRelevant;automaticCompletion=complete;cancellation?.CancelAfter(TimeSpan.FromSeconds(10));return true;
        }
    }
    public void CancelAutomatic() { lock (gate) CancelAutomaticCore(); }
    private void CancelAutomaticCore() { if (automatic) Cancel(); }
    public void Start(string text) { lock (gate) StartCore(text); }
    public bool StartNative(string text, Func<bool> isRelevant)
    {
        lock(gate)
        {
            if(automatic)Cancel();
            if(Busy)return false;
            StartCore(text, true); relevant=isRelevant; return Busy;
        }
    }
    public void CancelNative() { lock(gate) { if(pendingNativeDecision)Cancel(); } }
    private void StartCore(string text, bool decideNative = false)
    {
        if (Busy || string.IsNullOrWhiteSpace(text)) return;
        cancellation = new CancellationTokenSource();
        var messages = configuration.BuildMessages(history, text, gameContext, memories?.Context() ?? "跨对话记忆不可用。", peerContext:peerContext);
        messages[0]=new("system",messages[0].content+"\n"+UiLanguage.OutputInstruction);
        var settings = configuration.Settings;
        if(decideNative)
        {
            settings=settings with { Model=settings.NativeChatModel, UseJsonMode=false };
            NativeTrace?.Invoke($"request_peer_chars={peerContext.Length} quoted={(peerContext.Length>0)}");
            NativeChatDecision.PrepareMessages(messages);
            NativeStatus="正在判断并生成回复";
        }
        pendingNativeDecision=decideNative;
        Status = "Waiting for model...";
        pending = Request(messages, text, settings, cancellation.Token);
    }

    private async Task<ChatResult> Request(List<ChatMessage> messages, string user, ChatSettings settings, CancellationToken token)
    {
        try
        {
            if (!File.Exists(SecretPath)) return new(user, null, "Configure an API key first");
            var bytes = ProtectedData.Unprotect(await File.ReadAllBytesAsync(SecretPath, token), null, DataProtectionScope.CurrentUser);
            string key;
            try { key = Encoding.UTF8.GetString(bytes); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            var body = new Dictionary<string, object> { ["model"] = settings.Model, ["messages"] = messages, ["max_tokens"] = settings.MaxReplyTokens, ["stream"] = false };
            if(new Uri(settings.Endpoint).Host.Equals("api.deepseek.com",StringComparison.OrdinalIgnoreCase))body["thinking"]=new{type="disabled"};
            if (settings.UseJsonMode) body["response_format"] = new { type = "json_object" };
            request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(user, null, $"API error HTTP {(int)response.StatusCode}");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            var reply = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            var finish=json.RootElement.GetProperty("choices")[0].TryGetProperty("finish_reason",out var f) ? f.GetString() : "unknown";
            return string.IsNullOrWhiteSpace(reply) ? new(user, null, "Empty reply") : new(user, reply, "Reply received",finish is "stop" or "length" ? finish : "other");
        }
        catch (OperationCanceledException) { return new(user, null, "Request cancelled or timed out"); }
        catch { return new(user, null, "Request failed (network, credential or response error)"); }
    }

    public void Poll() { lock (gate) PollCore(); }
    private void PollCore()
    {
        if (pending is not { IsCompleted: true }) return;
        var result = pending.GetAwaiter().GetResult();
        pending = null; cancellation?.Dispose(); cancellation = null;
        Status = result.status;
        var wasAutomatic = automatic;
        var completion=automaticCompletion;automaticCompletion=null;
        var wasNativeDecision = pendingNativeDecision;
        var valid = relevant?.Invoke() ?? true;
        automatic = false; relevant = null; pendingNativeDecision=false;
        if (!valid) { Status = "Stale event reply discarded"; if(wasNativeDecision)NativeStatus="场景或会话改变：已取消旧回复"; return; }
        if (result.reply == null) { completion?.Invoke(null); if(wasNativeDecision)NativeStatus="请求未成功："+result.status; return; }
        CompanionReply reply;
        if(wasNativeDecision)
        {
            var decision=NativeChatDecision.Parse(result.reply);
            NativeTrace?.Invoke($"chars={result.reply.Length} finish={result.finish} shape={NativeChatDecision.Shape(result.reply)}");
            NativeDecisionMade?.Invoke(decision.Valid,decision.Speak);
            if(!decision.Valid){Status="Native chat: invalid decision discarded";NativeStatus="模型返回格式错误（并非主动沉默）";return;}
            if(!decision.Speak){Status="Native chat: chose to stay quiet";NativeStatus="模型选择暂不回应";return;}
            NativeStatus="回复已生成，等待原生显示";
            reply=decision.Reply!;
        }
        else reply = CompanionReply.Parse(result.reply);
        if (string.IsNullOrWhiteSpace(reply.Text)) { completion?.Invoke(null); Status = "Invalid structured reply discarded"; return; }
        if(completion!=null){ReplyReceived?.Invoke(reply);completion(reply.Text);return;}
        LastLine = reply.Text;
        LineReady?.Invoke(reply.Text);
        ReplyReceived?.Invoke(reply);
        if (wasAutomatic) return;
        ConversationReplyReady?.Invoke(reply.Text);
        history.Add(new("user", result.user)); history.Add(new("assistant", reply.Text));
        if (history.Count > 100) history.RemoveRange(0, history.Count - 100);
    }

    public void Cancel() { lock (gate) CancelCore(); }
    private void CancelCore()
    {
        if(pendingNativeDecision)NativeStatus="会话或设置改变：请求已取消";
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        automaticCompletion=null;pending = null; automatic = false; relevant = null; pendingNativeDecision=false; Status = "Cancelled";
    }
    public void Reset() { lock (gate) ResetCore(); }
    private void ResetCore() { Cancel(); history.Clear(); memoryDraft = memorySource = ""; memoryEditId = null; confirmMemoryClear = false; LastLine = ""; ConversationReset?.Invoke(); Status = "New conversation"; }
    public void Dispose() { Cancel(); client.Dispose(); }
    private record ChatResult(string user, string? reply, string status, string finish="unknown");
}

