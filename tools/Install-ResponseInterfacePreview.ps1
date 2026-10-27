param([string]$GameRoot='D:\steam\steamapps\common\Monster Hunter World',[switch]$Restore,[string]$BackupRoot='')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$stage=Join-Path $project 'dist\ResponseInterfacePreview'
$native=[IO.Path]::GetFullPath((Join-Path $GameRoot 'nativePC'))
if(!$BackupRoot){$BackupRoot=Join-Path $project 'backups\response-interface-preview'}
$BackupRoot=[IO.Path]::GetFullPath($BackupRoot)
$statePath=Join-Path $BackupRoot 'manifest.json'
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" | Where-Object ThreadCount -gt 0).Count -gt 0){throw 'Close Monster Hunter World before updating/restoring.'}
function Target([string]$relative){
 $path=[IO.Path]::GetFullPath((Join-Path $native $relative))
 if(!$path.StartsWith((Join-Path $native 'plugins\CSharp\WhaleCompanion')+'\',[StringComparison]::OrdinalIgnoreCase) -or !$relative.StartsWith('plugins/CSharp/WhaleCompanion/',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected plugin target'}
 return $path
}
function Hash([string]$path){if(Test-Path -LiteralPath $path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash};return $null}
if($Restore){
 if(!(Test-Path -LiteralPath $statePath)){throw 'No interface backup exists'}
 $rows=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
 foreach($row in $rows){$target=Target $row.Target;if((Hash $target) -ne $row.InstalledHash){throw 'Installed file changed; restore refused'};if($row.OriginalHash -and (Hash (Join-Path $BackupRoot $row.Target)) -ne $row.OriginalHash){throw 'Backup mismatch'}}
 foreach($row in $rows){$target=Target $row.Target;if($row.OriginalHash){Copy-Item -LiteralPath (Join-Path $BackupRoot $row.Target) -Destination $target -Force}elseif(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target};$row.InstalledHash=$row.OriginalHash}
 $rows | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding utf8
 'Interface files restored; runtime config and Chinese dialogue retained.';return
}
$planned=Get-Content -LiteralPath (Join-Path $stage 'supplement-manifest.json') -Raw | ConvertFrom-Json
$existing=@();if(Test-Path -LiteralPath $statePath){$existing=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json}
$work=@()
foreach($row in $planned){
 $target=Target $row.Target
 $source=[IO.Path]::GetFullPath((Join-Path $stage $row.Source))
 if(!$source.StartsWith($stage+'\',[StringComparison]::OrdinalIgnoreCase) -or (Hash $source) -ne $row.NewHash){throw 'Staged file mismatch'}
 if($row.Preserve -and (Test-Path -LiteralPath $target)){continue}
 $old=@($existing | Where-Object Target -eq $row.Target)
 if($old.Count -gt 1){throw 'Duplicate backup target'}
 if($old.Count -eq 1){if((Hash $target) -ne $old[0].InstalledHash){throw 'User changed installed interface file; update refused'};$saved=$old[0]}
 else{$saved=[pscustomobject]@{Target=$row.Target;OriginalHash=(Hash $target);InstalledHash=$null}}
 $work+=@{Row=$row;Saved=$saved;Target=$target;Source=$source}
}
New-Item -ItemType Directory -Force $BackupRoot | Out-Null
# Validate all inputs before making any changes; save every original first.
foreach($entry in $work){$backup=Join-Path $BackupRoot $entry.Row.Target;if($entry.Saved.OriginalHash -and !(@($existing | Where-Object Target -eq $entry.Row.Target).Count)){New-Item -ItemType Directory -Force (Split-Path -Parent $backup)|Out-Null;Copy-Item -LiteralPath $entry.Target -Destination $backup}}
foreach($entry in $work){New-Item -ItemType Directory -Force (Split-Path -Parent $entry.Target)|Out-Null;Copy-Item -LiteralPath $entry.Source -Destination $entry.Target -Force;if((Hash $entry.Target) -ne $entry.Row.NewHash){throw 'Installed hash mismatch'};$entry.Saved.InstalledHash=$entry.Row.NewHash;$existing=@($existing | Where-Object Target -ne $entry.Row.Target)+@($entry.Saved)}
$existing | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding utf8
'Interface supplements verified; Chinese dialogue, credentials and private configuration retained.'
