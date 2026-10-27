param([switch]$Restore)
$ErrorActionPreference='Stop'
$projectRoot='D:\homura\codex\MHW'
$gameRoot='D:\steam\steamapps\common\Monster Hunter World'
$nativeRoot=Join-Path $gameRoot 'nativePC'
$stageRoot=Join-Path $projectRoot 'dist\model-prototype\nativePC'
$backupRoot=Join-Path $projectRoot 'backups\model-p3-first'
$manifestPath=Join-Path $backupRoot 'manifest.json'
if(Get-Process MonsterHunterWorld -ErrorAction SilentlyContinue){throw 'Close Monster Hunter World before installing or restoring.'}
if($Restore){
 if(!(Test-Path -LiteralPath $manifestPath)){throw 'No installation manifest.'}
 $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 foreach($entry in $manifest){
  $target=[IO.Path]::GetFullPath((Join-Path $nativeRoot $entry.Relative))
  if(!$target.StartsWith($nativeRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside nativePC.'}
  if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $target).Hash -ne $entry.InstalledHash){throw "File changed since installation: $target"}}
  if($entry.Existed){Copy-Item -LiteralPath (Join-Path $backupRoot $entry.Relative) -Destination $target -Force}
  elseif(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}
 }
 'Model sample restored. Chat plugin unchanged.'
 exit
}
if(Test-Path -LiteralPath $manifestPath){throw 'Sample already installed or backed up. Inspect manifest before reinstalling.'}
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$manifest=@()
foreach($file in Get-ChildItem -LiteralPath $stageRoot -Recurse -File){
 $relative=[IO.Path]::GetRelativePath($stageRoot,$file.FullName)
 $target=[IO.Path]::GetFullPath((Join-Path $nativeRoot $relative))
 if(!$target.StartsWith($nativeRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside nativePC.'}
 $existed=Test-Path -LiteralPath $target
 if($existed){$backup=Join-Path $backupRoot $relative;New-Item -ItemType Directory -Path (Split-Path $backup) -Force | Out-Null;Copy-Item -LiteralPath $target -Destination $backup}
 $manifest+=[pscustomobject]@{Relative=$relative;Existed=$existed;InstalledHash=(Get-FileHash -LiteralPath $file.FullName).Hash}
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
foreach($entry in $manifest){$target=Join-Path $nativeRoot $entry.Relative;New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $stageRoot $entry.Relative) -Destination $target -Force}
'P3 experimental model sample installed with rollback manifest.'
'Equip Felyne Leather Cap and Felyne Leather Vest for the hiding files to match.'
'Rollback: powershell -File D:\homura\codex\MHW\tools\Install-ModelSample.ps1 -Restore'
