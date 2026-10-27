param([switch]$Restore)
$ErrorActionPreference='Stop'
$project='D:\homura\codex\MHW'
$native='D:\steam\steamapps\common\Monster Hunter World\nativePC'
$backup=Join-Path $project 'backups\pot-toggle-v1'
$manifest=Join-Path $backup 'manifest.json'
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" -ErrorAction Stop | Where-Object ThreadCount -gt 0).Count -gt 0){throw 'Close Monster Hunter World first; no files changed.'}
function Target([string]$rel) {
 $p=[IO.Path]::GetFullPath((Join-Path $native $rel))
 if(!$p.StartsWith($native+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Outside nativePC'}
 return $p
}
function RefreshModelManifest {
 $p=Join-Path $project 'backups\model-p3-first\manifest.json'
 $entries=Get-Content -LiteralPath $p -Raw | ConvertFrom-Json
 $row=@($entries | Where-Object Relative -eq 'otomo\ot000\mod\otomo000\otomo000.mod3')
 if($row.Count -ne 1){throw 'Original model manifest missing'}
 $row[0].InstalledHash=(Get-FileHash -LiteralPath (Target $row[0].Relative)).Hash
 $entries | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $p -Encoding utf8
}
if($Restore){
 $rows=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
 foreach($row in $rows){
  if($row.PreserveOnRestore){continue}
  $t=Target $row.Relative
  if(Test-Path -LiteralPath $t){$h=(Get-FileHash -LiteralPath $t).Hash;if($h -ne $row.InstalledHash -and $h -ne $row.OriginalHash -and !($row.Relative -eq 'otomo\ot000\mod\otomo000\otomo000.mod3' -and $h -eq '98BE1CE67C1E167CE5787C0FE027F0A72D2874CAB01B5F6017429F95701D88A2')){throw "Changed file: $t"}}
  if($row.Existed -and (Get-FileHash -LiteralPath (Join-Path $backup $row.Relative)).Hash -ne $row.OriginalHash){throw 'Backup hash mismatch'}
 }
 foreach($row in $rows){if($row.PreserveOnRestore){continue};$t=Target $row.Relative;if($row.Existed){Copy-Item -LiteralPath (Join-Path $backup $row.Relative) -Destination $t -Force}elseif(Test-Path -LiteralPath $t){Remove-Item -LiteralPath $t}}
 RefreshModelManifest
 'P7.0 plugin and model restored; configuration retained.';exit
}
if(Test-Path -LiteralPath $manifest){throw 'Backup already exists; inspect before reinstalling'}
$dllRel='plugins\CSharp\WhaleCompanion\WhaleCompanion.dll'
$baseline=@(Get-Content (Join-Path $project 'backups\chat-configuration-v1\manifest.json') -Raw | ConvertFrom-Json | Where-Object Relative -eq $dllRel)
if($baseline.Count -ne 1 -or (Get-FileHash -LiteralPath (Target $dllRel)).Hash -ne $baseline[0].InstalledHash){throw 'Expected installed P7.0 plugin'}
$items=@(@{Relative=$dllRel;Source=(Join-Path $project 'dist\WhaleCompanion\WhaleCompanion.dll')},@{Relative='plugins\CSharp\WhaleCompanion\P8-POT-TOGGLE.md';Source=(Join-Path $project 'dist\WhaleCompanion\P8-POT-TOGGLE.md')})
$modelRel='otomo\ot000\mod\otomo000\otomo000.mod3'
if((Get-FileHash -LiteralPath (Target $modelRel)).Hash -ne '25117018F896F8506D779DC6AC1A3C4877B9D407C487A806D4544526101677A4'){throw 'Expected P7.0 model'}
$build=Get-Content (Join-Path $project 'research\face\pot-v1-build.json') -Raw | ConvertFrom-Json
if(!$build.all_other_meshes_byte_identical){throw 'Body lock verification missing'}
$modelSource=Join-Path $project 'assets\prototype\pot-v1\otomo000.mod3'
if((Get-FileHash -LiteralPath $modelSource).Hash -ne '4AF7D138E38BAF53D528FD6E38B59C10BC9C59A89097432B44D2A1426EFC09A7'){throw 'Pot candidate hash mismatch'}
$items+=@{Relative=$modelRel;Source=$modelSource}
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
RefreshModelManifest
'P8.0 installed; file hashes match. Game validation pending. Only pot geometry changed.'
