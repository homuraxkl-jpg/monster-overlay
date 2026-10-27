param([switch]$Restore)
$ErrorActionPreference='Stop'
$project='D:\homura\codex\MHW'
$native='D:\steam\steamapps\common\Monster Hunter World\nativePC'
$backup=Join-Path $project 'backups\chat-configuration-v1'
$manifest=Join-Path $backup 'manifest.json'
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" -ErrorAction Stop | Where-Object ThreadCount -gt 0).Count -gt 0){throw 'Close Monster Hunter World first; no files changed.'}
function Target([string]$rel) {
 $p=[IO.Path]::GetFullPath((Join-Path $native $rel))
 if(!$p.StartsWith($native+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Outside nativePC'}
 return $p
}
if($Restore){
 $rows=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
 foreach($row in $rows){
  if($row.PreserveOnRestore){continue}
  $t=Target $row.Relative
  if(Test-Path -LiteralPath $t){$h=(Get-FileHash -LiteralPath $t).Hash;if($h -ne $row.InstalledHash -and $h -ne $row.OriginalHash){throw "Changed file: $t"}}
  if($row.Existed -and (Get-FileHash -LiteralPath (Join-Path $backup $row.Relative)).Hash -ne $row.OriginalHash){throw 'Backup hash mismatch'}
 }
 foreach($row in $rows){if($row.PreserveOnRestore){continue};$t=Target $row.Relative;if($row.Existed){Copy-Item -LiteralPath (Join-Path $backup $row.Relative) -Destination $t -Force}elseif(Test-Path -LiteralPath $t){Remove-Item -LiteralPath $t}}
 'P6.1 plugin restored; face assets untouched.';exit
}
if(Test-Path -LiteralPath $manifest){throw 'Backup already exists; inspect before reinstalling'}
$dllRel='plugins\CSharp\WhaleCompanion\WhaleCompanion.dll'
$baseline=@(Get-Content (Join-Path $project 'backups\action-companion-v2\manifest.json') -Raw | ConvertFrom-Json | Where-Object Relative -eq $dllRel)
if($baseline.Count -ne 1 -or (Get-FileHash -LiteralPath (Target $dllRel)).Hash -ne $baseline[0].InstalledHash){throw 'Expected installed P6.1 plugin'}
$items=@(@{Relative=$dllRel;Source=(Join-Path $project 'dist\WhaleCompanion\WhaleCompanion.dll')},@{Relative='plugins\CSharp\WhaleCompanion\P7-CONFIGURATION.md';Source=(Join-Path $project 'dist\WhaleCompanion\P7-CONFIGURATION.md')})
foreach($name in @('persona.md','memory.md','settings.json')) {
 $rel='plugins\CSharp\WhaleCompanion\config\'+$name
 if(!(Test-Path -LiteralPath (Target $rel))) {$items+=@{Relative=$rel;Source=(Join-Path $project ('dist\WhaleCompanion\config\'+$name));Preserve=$true}}
}
foreach($item in $items){if(!(Test-Path -LiteralPath $item.Source)){throw 'Missing staged file'};$null=Target $item.Relative}
New-Item -ItemType Directory -Force $backup | Out-Null
$rows=@()
foreach($item in $items){
 $t=Target $item.Relative;$exists=Test-Path -LiteralPath $t;$old=$null
 if($exists){$old=(Get-FileHash -LiteralPath $t).Hash;$b=Join-Path $backup $item.Relative;New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null;Copy-Item -LiteralPath $t -Destination $b}
 $rows+=[pscustomobject]@{Relative=$item.Relative;Existed=$exists;OriginalHash=$old;PreserveOnRestore=[bool]$item.Preserve;InstalledHash=(Get-FileHash -LiteralPath $item.Source).Hash}
}
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding utf8
foreach($item in $items){$t=Target $item.Relative;New-Item -ItemType Directory -Force (Split-Path $t) | Out-Null;Copy-Item -LiteralPath $item.Source -Destination $t -Force}
foreach($row in $rows){if((Get-FileHash -LiteralPath (Target $row.Relative)).Hash -ne $row.InstalledHash){throw 'Installed hash mismatch'}}
'P7.0 installed; file hashes match. Game validation pending. Face assets untouched.'
