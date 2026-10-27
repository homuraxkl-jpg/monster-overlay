param([switch]$Restore)
$ErrorActionPreference='Stop'
$project='D:\homura\codex\MHW'
$native='D:\steam\steamapps\common\Monster Hunter World\nativePC'
$backup=Join-Path $project 'backups\rice-prop-v2'
$manifest=Join-Path $backup 'manifest.json'
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" -ErrorAction Stop | Where-Object ThreadCount -gt 0).Count -gt 0){throw 'Close Monster Hunter World first; no files changed.'}
function Target([string]$rel){
 if(!$rel.StartsWith('otomo\accessory\ot_acc000\mod\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected resource path'}
 $p=[IO.Path]::GetFullPath((Join-Path $native $rel))
 if(!$p.StartsWith((Join-Path $native 'otomo\accessory\ot_acc000\mod\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Target outside accessory'}
 return $p
}
if($Restore){
 $rows=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
 foreach($row in $rows){
  $t=Target $row.Relative
  if(Test-Path -LiteralPath $t){$h=(Get-FileHash -LiteralPath $t).Hash;if($h -ne $row.InstalledHash -and $h -ne $row.OriginalHash){throw "Changed file: $t"}}
  if($row.Existed -and (Get-FileHash -LiteralPath (Join-Path $backup $row.Relative)).Hash -ne $row.OriginalHash){throw 'Backup hash mismatch'}
 }
 foreach($row in $rows){$t=Target $row.Relative;if($row.Existed){Copy-Item -LiteralPath (Join-Path $backup $row.Relative) -Destination $t -Force}elseif(Test-Path -LiteralPath $t){Remove-Item -LiteralPath $t}}
 'Previous 1x rice prop restored; head-pot, companion plugin and config untouched.';exit
}
if(Test-Path -LiteralPath $manifest){throw 'Backup already exists; inspect before reinstalling'}
$roundtrip=Get-Content (Join-Path $project 'research\rice\rice-large-roundtrip.json') -Raw | ConvertFrom-Json
if(!$roundtrip.original_skeleton_bytes_identical -or !$roundtrip.all_vertices_root_weight_1 -or !$roundtrip.all_vertex_positions_scaled_3_5){throw 'Prop verification missing'}
$items=Get-Content (Join-Path $project 'research\rice\rice-large-bundle.json') -Raw | ConvertFrom-Json
if(@($items).Count -ne 1){throw 'Expected one scaled model'}
foreach($item in $items){$null=Target $item.Relative;$source=Join-Path $project ('assets\prototype\rice-v2\'+$item.Relative);if((Get-FileHash -LiteralPath $source).Hash -ne $item.Hash){throw 'Staged hash mismatch'}}
if((Get-FileHash -LiteralPath (Target 'otomo\accessory\ot_acc000\mod\ot_acc000.mod3')).Hash -ne 'C6C5E781D202275E71A6CAC6C7EF36DE632DB47E3AF4263F386148E35158562C'){throw 'Expected installed rice v1 model'}
$protected=@('otomo\ot000\mod\otomo000\otomo000.mod3','otomo\ot000\mod\otomo000\otomo000.mrl3','plugins\CSharp\WhaleCompanion\WhaleCompanion.dll')
$before=@{};foreach($rel in $protected){$before[$rel]=(Get-FileHash -LiteralPath (Join-Path $native $rel)).Hash}
New-Item -ItemType Directory -Force $backup | Out-Null
$rows=@()
foreach($item in $items){
 $t=Target $item.Relative;$exists=Test-Path -LiteralPath $t;$old=$null
 if($exists){$old=(Get-FileHash -LiteralPath $t).Hash;$b=Join-Path $backup $item.Relative;New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null;Copy-Item -LiteralPath $t -Destination $b}
 $rows+=[pscustomobject]@{Relative=$item.Relative;Existed=$exists;OriginalHash=$old;InstalledHash=$item.Hash}
}
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding utf8
foreach($item in $items){$t=Target $item.Relative;New-Item -ItemType Directory -Force (Split-Path $t) | Out-Null;Copy-Item -LiteralPath (Join-Path $project ('assets\prototype\rice-v2\'+$item.Relative)) -Destination $t -Force}
foreach($row in $rows){if((Get-FileHash -LiteralPath (Target $row.Relative)).Hash -ne $row.InstalledHash){throw 'Installed hash mismatch'}}
foreach($rel in $protected){if((Get-FileHash -LiteralPath (Join-Path $native $rel)).Hash -ne $before[$rel]){throw 'Unexpected companion asset change'}}
[pscustomobject]@{Installed=$true;Files=1;CompanionAssetsUnchanged=$true;GameValidation='pending'} | ConvertTo-Json | Set-Content (Join-Path $project 'research\rice\rice-large-installation.json') -Encoding utf8
'Rice 3.5x model installed; model hash matches. Palico self-heal resource usage and hand placement require game validation.'
