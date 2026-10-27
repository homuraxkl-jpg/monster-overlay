using System.Text.Json;
namespace WhaleCompanion;
public static class ResponseCatalog
{
    public static void Seed(ResponsePolicy policy)
    {
        using var stream=typeof(ResponseCatalog).Assembly.GetManifestResourceStream("WhaleCompanion.response-defaults.json");
        if(stream==null)return;
        var defaults=JsonSerializer.Deserialize<Dictionary<string,ResponseRule>>(stream)!;
        foreach(var (id,rule) in defaults)policy.Register(id,rule.Category,rule.ReferenceText,rule.Prompt);
    }
}
