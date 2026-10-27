using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WhaleCompanion;

public sealed record SavedMemory(string Id, string Text, string Source, string SourceText, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, bool UserConfirmed);
public sealed record MemoryDocument
{
    public int Version { get; init; } = 1;
    public bool Enabled { get; init; }
    public List<SavedMemory> Entries { get; init; } = new();
}

// User-confirmed facts only. No network calls or automatic model-authored writes.
public sealed class LongTermMemory
{
    private const int MaxBytes = 256000;
    private readonly string path;
    private MemoryDocument document;
    private string? fingerprint;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public bool Enabled => document.Enabled;
    public IReadOnlyList<SavedMemory> Entries => document.Entries.ToArray();
    private LongTermMemory(string path, MemoryDocument document, string? fingerprint) { this.path = path; this.document = document; this.fingerprint = fingerprint; }
    private static byte[]? Read(string path)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("Memory file too large");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Memory file too large");
        return bytes;
    }
    private static string? Hash(byte[]? data) => data == null ? null : Convert.ToHexString(SHA256.HashData(data));
    public static LongTermMemory Load(string path)
    {
        var bytes = Read(path);
        var doc = bytes == null ? new MemoryDocument() : JsonSerializer.Deserialize<MemoryDocument>(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? bytes.AsSpan(3) : bytes.AsSpan(), Options) ?? throw new InvalidDataException("Invalid memory document");
        Validate(doc); return new(path, doc, Hash(bytes));
    }
    private static bool HasCredential(string text) => Regex.IsMatch(text, @"\bsk-[A-Za-z0-9_-]{12,}|\bBearer\s+[A-Za-z0-9._-]{12,}", RegexOptions.IgnoreCase);
    private static void Validate(MemoryDocument doc)
    {
        if (doc.Version != 1 || doc.Entries == null || doc.Entries.Count > 50) throw new InvalidDataException("Invalid memory version/count");
        var ids = new HashSet<string>();
        foreach (var e in doc.Entries)
        {
            if (e == null || !Guid.TryParse(e.Id, out _) || !ids.Add(e.Id) || string.IsNullOrWhiteSpace(e.Text) || e.Text.Length > 280 || e.Source is not ("manual" or "user_chat") || e.SourceText == null || e.SourceText.Length > 512 || HasCredential(e.Text) || HasCredential(e.SourceText)) throw new InvalidDataException("Invalid memory entry");
        }
    }
    private void Commit(MemoryDocument candidate)
    {
        Validate(candidate);
        if (Hash(Read(path)) != fingerprint) throw new IOException("Memory changed externally; reload first");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(candidate, Options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temp, bytes); File.Move(temp, path, true);
        document = candidate; fingerprint = Hash(bytes);
    }
    public void SetEnabled(bool enabled) => Commit(document with { Enabled = enabled });
    public void Save(string text, string sourceText = "", string? editId = null)
    {
        text = text.Trim();
        if (HasCredential(sourceText)) throw new InvalidDataException("Credential cannot be saved");
        var entries = document.Entries.ToList(); var now = DateTimeOffset.UtcNow;
        if (editId != null)
        {
            var index = entries.FindIndex(e => e.Id == editId); if (index < 0) throw new InvalidDataException("Unknown entry");
            entries[index] = entries[index] with { Text = text, UpdatedUtc = now, UserConfirmed = true };
        }
        else
        {
            if (entries.Any(e => e.Text == text)) throw new InvalidDataException("Duplicate memory");
            entries.Add(new(Guid.NewGuid().ToString(), text, sourceText.Length == 0 ? "manual" : "user_chat", sourceText[..Math.Min(512, sourceText.Length)], now, now, true));
        }
        Commit(document with { Entries = entries });
    }
    public void Delete(string id)
    {
        var entries = document.Entries.Where(e => e.Id != id).ToList();
        if (entries.Count == document.Entries.Count) throw new InvalidDataException("Unknown entry");
        Commit(document with { Entries = entries });
    }
    public void Clear() => Commit(document with { Entries = new() });
    public string Context()
    {
        if (!Enabled) return "未启用玩家保存的跨对话记忆。";
        var lines = new List<string>(); var count = 0;
        foreach (var entry in document.Entries.Where(e => e.UserConfirmed).Reverse())
        {
            var line = "- " + entry.Text;
            if (count + line.Length > 4000) break;
            lines.Add(line); count += line.Length + 1;
        }
        return lines.Count == 0 ? "暂无玩家确认的跨对话记忆。" : string.Join("\n", lines);
    }
}
