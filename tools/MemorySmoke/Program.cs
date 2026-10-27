using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WhaleCompanion;
var root="D:/homura/codex/MHW";
var config=ChatConfiguration.Load("D:/steam/steamapps/common/Monster Hunter World/nativePC/plugins/CSharp/WhaleCompanion/config");
var fixture=root+"/research/chat/memory-smoke-"+Guid.NewGuid().ToString("N")+"/saved-memories.json";
var store=LongTermMemory.Load(fixture);store.SetEnabled(true);store.Save("玩家确认的长期暗号：霜鲸饭团六十二");
using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};
var secret=ProtectedData.Unprotect(File.ReadAllBytes(root+"/private/deepseek.dpapi"),null,DataProtectionScope.CurrentUser);
string key;try{key=Encoding.UTF8.GetString(secret);}finally{CryptographicOperations.ZeroMemory(secret);}
var evidence=new List<object>();
for(int n=0;n<2;n++){
 if(n==1){store=LongTermMemory.Load(fixture);store.Clear();}
 var reloaded=LongTermMemory.Load(fixture);
 var messages=config.BuildMessages(Array.Empty<ChatMessage>(),"我保存的长期暗号是什么？没有记录就直说。","未提供实时状态。",reloaded.Context());
 using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.deepseek.com/chat/completions");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
 var body=new Dictionary<string,object>{["model"]=config.Settings.Model,["messages"]=messages,["thinking"]=new{type="disabled"},["max_tokens"]=config.Settings.MaxReplyTokens,["stream"]=false};
 if(config.Settings.UseJsonMode)body["response_format"]=new{type="json_object"};
 request.Content=JsonContent.Create(body);using var response=await client.SendAsync(request);
 if(!response.IsSuccessStatusCode)throw new Exception($"Memory smoke HTTP {(int)response.StatusCode}");
 using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
 var parsed=CompanionReply.Parse(json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"");
 if(parsed.Text.Length==0)throw new Exception("Invalid structured reply");
 if(n==0&&!parsed.Text.Contains("霜鲸饭团六十二"))throw new Exception("Saved memory not recalled");
 if(n==1&&parsed.Text.Contains("霜鲸饭团六十二"))throw new Exception("Removed memory leaked to fresh conversation");
 evidence.Add(new{test=n+1,reply=parsed.Text,emotion=parsed.Emotion,tokens=json.RootElement.GetProperty("usage").GetProperty("total_tokens").GetInt32()});
}
File.WriteAllText(root+"/research/chat/p9-memory-smoke.json",JsonSerializer.Serialize(new{calls=2,model=config.Settings.Model,tests=evidence},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PASS 2 paid saved-memory/restart and clear/new-conversation requests. No credentials logged.");
