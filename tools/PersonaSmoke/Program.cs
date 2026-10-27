using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WhaleCompanion;
var root="D:/homura/codex/MHW";
var config=ChatConfiguration.Load(root+"/dist/WhaleCompanion/config");
using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};
var secret=ProtectedData.Unprotect(File.ReadAllBytes(root+"/private/deepseek.dpapi"),null,DataProtectionScope.CurrentUser);
string key;try{key=Encoding.UTF8.GetString(secret);}finally{CryptographicOperations.ZeroMemory(secret);}
if(args.Contains("offline")) {
 var liveConfig=ChatConfiguration.Load("D:/steam/steamapps/common/Monster Hunter World/nativePC/plugins/CSharp/WhaleCompanion/config");
 var catalog=JsonSerializer.Deserialize<OfflineCatalog>(File.ReadAllText(root+"/config/dialogue/ot_auto_chT.json"))!;
 var lines=catalog.Entries.Where(x=>x.Replacement!=null).Take(8).ToArray();
 var proposal=await OfflineDialogue.Generate(client,key,liveConfig.Settings.Model,liveConfig.Persona,lines,CancellationToken.None);
 foreach(var line in catalog.Entries)if(proposal.TryGetValue(line.Key,out var text))line.Replacement=text;
 var bytes=OfflineDialogue.Build(File.ReadAllBytes(root+"/research/chat/chunks/chunkG0/common/text/ot_auto_chT.gmd"),catalog);
 var output=root+"/research/chat/offline-ai-smoke";Directory.CreateDirectory(output);
 File.WriteAllText(output+"/ot_auto_chT.json",JsonSerializer.Serialize(catalog,OfflineDialogue.JsonOptions));
 File.WriteAllBytes(output+"/ot_auto_chT.gmd",bytes);
 File.WriteAllText(output+"/validation.json",JsonSerializer.Serialize(new{calls=1,model=liveConfig.Settings.Model,requested=lines.Length,generated=proposal.Count,compiler="passed",installed=false},OfflineDialogue.JsonOptions));
 Console.WriteLine($"PASS offline persona batch: {proposal.Count} validated lines, one API call, not installed.");return;
}
if(args.Contains("peer")) {
 var external=JsonSerializer.Serialize(new[]{new{source="remote_unverified",display_name="测试队友",text="我想用西兰花配白米饭。"}});
 var messages=config.BuildMessages(new List<ChatMessage>(),"刚才队友说想用什么配米饭？","状态未知",peerContext:external);
 NativeChatDecision.PrepareMessages(messages);
 using var req=new HttpRequestMessage(HttpMethod.Post,"https://api.deepseek.com/chat/completions");
 req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
 req.Content=JsonContent.Create(new{model=config.Settings.NativeChatModel,messages,thinking=new{type="disabled"},max_tokens=240,stream=false});
 using var res=await client.SendAsync(req);
 if(!res.IsSuccessStatusCode)throw new Exception($"Peer context smoke HTTP {(int)res.StatusCode}");
 using var json=JsonDocument.Parse(await res.Content.ReadAsStringAsync());
 var content=json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"";
 var decision=NativeChatDecision.Parse(content);
 var recalled=decision.Reply?.Text.Contains("西兰花")==true;
 File.WriteAllText(root+"/research/chat/p10-5-peer-context-smoke.json",JsonSerializer.Serialize(new{calls=1,model=config.Settings.NativeChatModel,valid=decision.Valid,speak=decision.Speak,recalled,tokens=json.RootElement.GetProperty("usage").GetProperty("total_tokens").GetInt32()},new JsonSerializerOptions{WriteIndented=true}));
 if(!decision.Valid||!decision.Speak||!recalled)throw new Exception("Peer quote recall failed; no retries");
 Console.WriteLine("PASS one owner-triggered request recalled synthetic peer quote; no real peer chat used.");return;
}
if(args.Contains("native")) {
 var checks=new List<object>(); var nativeHistory=new List<ChatMessage>();
 foreach(var sample in new[]{(Text:"大肥鱼，你今天想吃什么？回去我请你吃饭。",Speak:true),(Text:"那你想配什么菜？",Speak:true),(Text:"队友们，我去营地换装备，你们先打。不用回复，我在给队友报位置。",Speak:false)}) {
  var messages=config.BuildMessages(nativeHistory,sample.Text,"生命值100/100；武器LongSword；气刃等级无刃。");
  NativeChatDecision.PrepareMessages(messages);
  using var req=new HttpRequestMessage(HttpMethod.Post,"https://api.deepseek.com/chat/completions");
  req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
  req.Content=JsonContent.Create(new{model=config.Settings.NativeChatModel,messages,thinking=new{type="disabled"},max_tokens=240,stream=false});
  using var res=await client.SendAsync(req);
  if(!res.IsSuccessStatusCode)throw new Exception($"Native smoke HTTP {(int)res.StatusCode}");
  using var json=JsonDocument.Parse(await res.Content.ReadAsStringAsync());
  var content=json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"";
  File.WriteAllText(root+"/research/chat/p10-5-synthetic-last-response.json",json.RootElement.GetRawText());
  var decision=NativeChatDecision.Parse(content);
  checks.Add(new{valid=decision.Valid,speak=decision.Speak,expected=sample.Speak,tokens=json.RootElement.GetProperty("usage").GetProperty("total_tokens").GetInt32()});
  File.WriteAllText(root+"/research/chat/p10-5-native-decision-smoke-b2-history.json",JsonSerializer.Serialize(new{model=config.Settings.NativeChatModel,calls=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));
  if(!decision.Valid || decision.Speak!=sample.Speak)throw new Exception("Native decision smoke mismatch; no automatic retries");
  if(decision.Speak){nativeHistory.Add(new("user",sample.Text));nativeHistory.Add(new("assistant",decision.Reply!.Text));}
 }
 Console.WriteLine("PASS 3 native decision requests: owner question and context follow-up replied; team coordination silent. Credentials not logged.");
 return;
}
var history=new List<ChatMessage>();var evidence=new List<object>();
var inputs=new[]{"大肥鱼，来陪我狩猎。先记住我们这次的暗号：海盐饭团七号。","我刚才约定的暗号是什么？","新对话了。我在这轮对话里告诉过你暗号吗？没有就直说。"};
for(int n=0;n<(args.Contains("baseline")?1:inputs.Length);n++){
 if(n==2)history.Clear();
 using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.deepseek.com/chat/completions");
 request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
 var messages=config.BuildMessages(history,inputs[n],"生命值100/200；武器LongSword；动作3:98。不代表命中。");
 if(args.Contains("baseline")) messages=new List<ChatMessage>{new("system","你是陪伴怪物猎人世界玩家的鲸鱼娘。只输出JSON对象，例如{\"text\":\"我陪着你。\",\"emotion\":\"relaxed\",\"intensity\":0.5}。"),new("user","你好，回复一句话。")};
 request.Content=JsonContent.Create(new{model=config.Settings.Model,messages,thinking=new{type="disabled"},max_tokens=config.Settings.MaxReplyTokens,stream=false});
 using var response=await client.SendAsync(request);
 if(!response.IsSuccessStatusCode)throw new Exception($"Smoke HTTP {(int)response.StatusCode}");
 using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
 var content=json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"";
 File.WriteAllText(root+"/research/chat/p7-smoke-last-response.json",json.RootElement.GetRawText()); var parsed=CompanionReply.Parse(content);if(string.IsNullOrEmpty(parsed.Text))throw new Exception("Invalid JSON reply");
 if(n==1&&!parsed.Text.Contains("海盐饭团七号"))throw new Exception("Context recall check failed");
 if(n==2&&parsed.Text.Contains("海盐饭团七号"))throw new Exception("New conversation leaked removed context");
 evidence.Add(new{test=n+1,reply=parsed.Text,emotion=parsed.Emotion,intensity=parsed.Intensity,tokens=json.RootElement.GetProperty("usage").GetProperty("total_tokens").GetInt32()});
 history.Add(new("user",inputs[n]));history.Add(new("assistant",parsed.Text));
}
Directory.CreateDirectory(root+"/research/chat");
File.WriteAllText(root+"/research/chat/p7-persona-smoke"+(args.Contains("baseline")?"-baseline":"")+".json",JsonSerializer.Serialize(new{model=config.Settings.Model,calls=evidence.Count,tests=evidence},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {evidence.Count} paid requests; credentials not logged.");




