using System.Reflection;
using WhaleCompanion;

var original = typeof(ProbePlugin).Assembly;
var loaded = Assembly.Load(File.ReadAllBytes(original.Location));
if (!string.IsNullOrEmpty(loaded.Location)) throw new Exception("Expected empty assembly location");
var instance = Activator.CreateInstance(loaded.GetType("WhaleCompanion.ProbePlugin", true)!);
if (instance is null) throw new Exception("Constructor returned null");
Console.WriteLine("PASS: plugin constructed from byte-loaded assembly with empty Location.");
