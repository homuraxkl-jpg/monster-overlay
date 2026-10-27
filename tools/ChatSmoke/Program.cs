using WhaleCompanion;
using var chat = new CompanionChat();
foreach (var text in new[] { "你好，我的猎人叫阿蓝，简单打个招呼。", "我的猎人叫什么名字？" })
{
    chat.Start(text);
    while(chat.Busy) { await Task.Delay(100); chat.Poll(); }
    if (chat.History.Count == 0 || chat.Status != "Reply received") throw new Exception(chat.Status);
    Console.WriteLine(chat.History.Last().content);
}
chat.Start("再说一句。"); chat.Cancel();
if(chat.Busy) throw new Exception("Cancel failed");
chat.Reset();
if(chat.History.Count != 0) throw new Exception("Reset failed");
Console.WriteLine("PASS: two live replies, cancellation and reset.");
