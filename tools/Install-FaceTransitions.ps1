param([switch]$Restore)
$ErrorActionPreference='Stop'
$project='D:\homura\codex\MHW'
$game='D:\steam\steamapps\common\Monster Hunter World'
$native=Join-Path $game 'nativePC'
$backup=Join-Path $project 'backups\face-runtime-v2'
$manifestFile=Join-Path $backup 'manifest.json'
$firstManifest=Join-Path $project 'backups\model-p3-first\manifest.json'
$modelRel='otomo\ot000\mod\otomo000\otomo000.mod3'
$neutralHash='98BE1CE67C1E167CE5787C0FE027F0A72D2874CAB01B5F6017429F95701D88A2'
$previousHash='2CBE8ADDCF6E40A39266D55AD43FCC47E486C383EC290A109EBDF4F60ADC804D'
$runtimeHash='FF0B7F39528888E3EA1DD013760132FA168D272D2329C63B9697E0DB5D92B3F3'
if(Get-Process MonsterHunterWorld -ErrorAction SilentlyContinue){throw 'Close Monster Hunter World before installing/restoring.'}
function Target([string]$relative) {
 $path=[IO.Path]::GetFullPath((Join-Path $native $relative))
 if(!$path.StartsWith($native+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Target outside nativePC'}
 return $path
}
function RefreshModelManifest {
 $entries=Get-Content -LiteralPath $firstManifest -Raw | ConvertFrom-Json
 $entry=@($entries | Where-Object Relative -eq $modelRel)
 if($entry.Count -ne 1){throw 'Expected model entry in original manifest'}
 $entry[0].InstalledHash=(Get-FileHash -LiteralPath (Target $modelRel)).Hash
 $entries | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $firstManifest -Encoding utf8
}
if($Restore) {
 $rows=Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
 # Validate every target and backup before making any mutation.
 foreach($row in $rows) {
  $target=Target $row.Relative
  if(Test-Path -LiteralPath $target) {
   $hash=(Get-FileHash -LiteralPath $target).Hash
   if($hash -ne $row.InstalledHash -and $hash -ne $row.OriginalHash -and !($row.Relative -eq $modelRel -and $hash -eq $neutralHash)){throw "Changed file: $target"}
  }
  if($row.Existed -and (Get-FileHash -LiteralPath (Join-Path $backup $row.Relative)).Hash -ne $row.OriginalHash){throw 'Backup hash mismatch'}
 }
 foreach($row in $rows) {
  $target=Target $row.Relative
  if($row.Existed){Copy-Item -LiteralPath (Join-Path $backup $row.Relative) -Destination $target -Force}
  elseif(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}
 }
 RefreshModelManifest
 'Restored previous P5.1 plugin and face model. Body is unchanged. Backup retained.'
 exit
}
if(Test-Path (Join-Path $project 'backups\face-runtime-v3\manifest.json')){throw 'A newer face release is backed up. Use Install-FaceEmotions.ps1.'}
if(Test-Path -LiteralPath $manifestFile){throw 'Experiment already backed up; inspect/restore it before reinstalling.'}
if((Get-FileHash -LiteralPath (Target $modelRel)).Hash -ne $previousHash){throw 'Installed model differs from the previously tested P5.1 face model.'}
$verification=Get-Content (Join-Path $project 'research\face\runtime-v2-roundtrip.json') -Raw | ConvertFrom-Json
if(!$verification.body_unchanged){throw 'Body verification missing'}
if((Get-FileHash (Join-Path $project 'assets\prototype\face-runtime-v2\otomo000.mod3')).Hash -ne $runtimeHash){throw 'Candidate hash mismatch'}
$items=@(
 @{Relative=$modelRel;Source=(Join-Path $project 'assets\prototype\face-runtime-v2\otomo000.mod3')},
 @{Relative='plugins\CSharp\WhaleCompanion\face-neutral.mod3';Source=(Join-Path $project 'assets\prototype\otomo000.mod3')}
)
foreach($name in @('WhaleCompanion.dll','WhaleCompanion.deps.json','System.Security.Cryptography.ProtectedData.dll','FACE-RUNTIME-TEST.md','THIRD-PARTY.md','LICENSE.VisualControllerExtended.txt')) {
 $items+=@{Relative=('plugins\CSharp\WhaleCompanion\'+$name);Source=(Join-Path $project ('dist\WhaleCompanion\'+$name))}
}
foreach($item in $items){if(!(Test-Path -LiteralPath $item.Source)){throw "Missing staged file: $($item.Source)"};$null=Target $item.Relative}
New-Item -ItemType Directory -Force $backup | Out-Null
Copy-Item -LiteralPath $firstManifest -Destination (Join-Path $backup 'original-model-manifest.json')
$rows=@()
foreach($item in $items) {
 $target=Target $item.Relative;$exists=Test-Path -LiteralPath $target;$hash=$null
 if($exists) {
  $hash=(Get-FileHash -LiteralPath $target).Hash
  $saved=Join-Path $backup $item.Relative
  New-Item -ItemType Directory -Force (Split-Path $saved) | Out-Null
  Copy-Item -LiteralPath $target -Destination $saved
 }
 $rows+=[pscustomobject]@{Relative=$item.Relative;Existed=$exists;OriginalHash=$hash;InstalledHash=(Get-FileHash -LiteralPath $item.Source).Hash}
}
$rows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestFile -Encoding utf8
foreach($item in $items){$target=Target $item.Relative;New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null;Copy-Item -LiteralPath $item.Source -Destination $target -Force}
RefreshModelManifest
foreach($row in $rows){if((Get-FileHash -LiteralPath (Target $row.Relative)).Hash -ne $row.InstalledHash){throw 'Installed hash mismatch'}}
'Face runtime experiment installed with hash-checked backup and neutral fallback. In-game validation pending.'
'Rollback after exiting game: powershell -File D:\homura\codex\MHW\tools\Install-FaceTransitions.ps1 -Restore'
