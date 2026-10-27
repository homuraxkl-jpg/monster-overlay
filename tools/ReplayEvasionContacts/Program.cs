using System.Text.Json;
using WhaleCompanion;
[assembly:System.Runtime.Versioning.SupportedOSPlatform("windows")]
if(args.Length!=1)throw new ArgumentException("Pass the local diagnostics log path");
var director=new EvasionContactDirector();int rows=0,responses=0,bodyControls=0,legacyRows=0,episodeRows=0;
double lastSample=double.NegativeInfinity,lastNow=double.NegativeInfinity;
foreach(var line in File.ReadLines(args[0]))
{
 const string marker=" EVASION_CONTACT_CANDIDATE ";int index=line.IndexOf(marker,StringComparison.Ordinal);if(index<0)continue;
 using var json=JsonDocument.Parse(line[(index+marker.Length)..]);var raw=json.RootElement.GetProperty("candidate");
 if(!raw.TryGetProperty("AttackKind",out _))continue;
 var value=raw.Deserialize<EvasionContactCandidate>()??throw new InvalidDataException("Missing sample");rows++;
 if(value.AttackKind==16&&value.TargetKind==16)bodyControls++;
 // Do not invent occurrence IDs for old collision samples: that would fabricate four evades.
 if(value.EvasionId<=0){legacyRows++;continue;}
 value=value with{Owner=1};episodeRows++;
 if(value.Time<lastSample-1){director.Reset();lastNow=double.NegativeInfinity;}
 lastSample=value.Time;double now=Math.Max(lastNow,value.Time+.05);lastNow=now;
 var reaction=director.Observe(1,value.Weapon,value,now);
 if(value.AttackKind==16&&value.TargetKind==16&&reaction!=null)throw new InvalidDataException("Body overlap produced a response");
 if(reaction!=null){if(reaction.Kind!="native_evade_streak"||director.Count!=0)throw new InvalidDataException("Batch not cleared");responses++;}
}
if(rows==0)throw new InvalidDataException("No candidate records");
Console.WriteLine(JsonSerializer.Serialize(new{offline_replay_only=true,rows,episodeRows,legacyRows,responses,body_controls=bodyControls,threshold_replay_verified=episodeRows>0&&responses>0,owner_identity="synthetic_for_privacy_preserving_telemetry",game_feedback_verified=false}));
