using MHWNoChunk;
var input=args[0]; var root=Path.GetFullPath(args[1]); Directory.CreateDirectory(root);
var window=new MainWindow(); var chunk=new Chunk(); window.Chunks[input]=chunk;
var tree=chunk.AnalyzeChunk(input,window,new());
IEnumerable<FileNode> Walk(IEnumerable<FileNode> nodes) { foreach(var n in nodes){if(n.IsFile)yield return n;foreach(var c in Walk(n.Childern))yield return c;}}
var prefixArg=args.FirstOrDefault(a=>a.StartsWith("--prefix="));
var scope=prefixArg==null ? "otomo/" : prefixArg[9..].Replace('\\','/').TrimStart('/');
if(string.IsNullOrWhiteSpace(scope) || scope.Contains("..") || scope.Contains(':')) throw new Exception("Invalid extraction prefix");
var files=Walk(tree).Where(n=>n.EntireName.Replace('\\','/').TrimStart('/').StartsWith(scope,StringComparison.OrdinalIgnoreCase)).ToList();
File.WriteAllLines(Path.Combine(root,prefixArg==null ? "otomo-index.txt" : "prefix-index.txt"),files.Select(n=>$"{n.EntireName}\t{n.Size}"));
Console.WriteLine($"Otomo indexed: {files.Count}");
if(args.Contains("--index-only")) return;
var selected=files.Where(n=> n.EntireName.Replace('\\','/').Contains("otomo/common/mot/") || n.EntireName.Replace('\\','/').Contains("otomo/common/face_mot/") ||  n.EntireName.Replace('\\','/').Contains("otomo/ot000/mod/otomo000/") ||  n.EntireName.Replace('\\','/').Contains("otomo/equip/ot001/") || n.EntireName.EndsWith(".mod3") && !n.EntireName.Replace('\\','/').Contains("/equip/") && !n.EntireName.Replace('\\','/').Contains("/wp/")).ToList();
if(prefixArg!=null){var prefix=prefixArg[9..].Replace('\\','/').TrimStart('/');selected=files.Where(n=>n.EntireName.Replace('\\','/').TrimStart('/').StartsWith(prefix,StringComparison.OrdinalIgnoreCase)).ToList();}
var extensionArg=args.FirstOrDefault(a=>a.StartsWith("--extensions="));
if(extensionArg!=null){var extensions=extensionArg[13..].Split(',');selected=selected.Where(n=>extensions.Any(ext=>n.EntireName.EndsWith(ext,StringComparison.OrdinalIgnoreCase))).ToList();}
foreach(var n in selected){var path=Path.GetFullPath(Path.Combine(root,n.EntireName.TrimStart('\\','/')));if(!path.StartsWith(root+Path.DirectorySeparatorChar))throw new Exception("Unsafe path");Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,chunk.GetFileData(n)); Console.WriteLine("EXTRACTED "+n.EntireName);}
Console.WriteLine($"Extracted {selected.Count}");




