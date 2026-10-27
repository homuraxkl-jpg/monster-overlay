namespace MHWNoChunk {
public class MainWindow {
public static bool CNMode=false;
public static Stack<string> ErrorsStack=new();
public bool PauseFlag=>false; public bool TerminateFlag=>false;
public Dictionary<string,Chunk> Chunks=new();
public Chunk GetChunk(string path)=>Chunks[path];
public void PrintLog(string text)=>Console.WriteLine(text);
public void SetProgressbar(int a,int b){} public void updateExtractProgress(){}
}}
namespace MHWNoChunk.Properties { public static class Resources { public static byte[] chunk=>File.ReadAllBytes("D:/homura/codex/MHW/tools/vendor/MHWNoChunk-master/MHWNoChunk/Resources/chunk.key"); }}
