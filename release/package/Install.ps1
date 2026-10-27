param([string]$GameRoot='', [ValidateSet('original','soft-rosy','rosy')][string]$FaceTint='soft-rosy',[switch]$Uninstall)
$ErrorActionPreference='Stop'
if(!$GameRoot){
 Add-Type -AssemblyName System.Windows.Forms
 $picker=New-Object System.Windows.Forms.OpenFileDialog
 $picker.Title='Select MonsterHunterWorld.exe';$picker.Filter='Monster Hunter World|MonsterHunterWorld.exe'
 if($picker.ShowDialog() -ne 'OK'){return};$GameRoot=Split-Path -Parent $picker.FileName
}
$GameRoot=[IO.Path]::GetFullPath($GameRoot)
if(!(Test-Path -LiteralPath (Join-Path $GameRoot 'MonsterHunterWorld.exe'))){throw 'Select the folder containing MonsterHunterWorld.exe.'}
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" -ErrorAction Stop | Where-Object ThreadCount -gt 0).Count -gt 0){throw 'Close Monster Hunter World before installing or uninstalling.'}
function Safe([string]$root,[string]$relative){
 if([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or @($relative.Split([char[]]'/\') | Where-Object {$_ -eq '..'}).Count){throw 'Unsafe package path'}
 $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
 if(!$path.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside intended directory'}
 $cursor=$path
 while($cursor -and $cursor.Length -ge $root.Length){if((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked paths are not supported by the installer'};$cursor=Split-Path -Parent $cursor}
 return $path
}
function Hash([string]$path){if(Test-Path -LiteralPath $path){$stream=[IO.File]::OpenRead($path);$algorithm=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$algorithm.Dispose()}};return $null}
$package=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$native=Join-Path $GameRoot 'nativePC'
$backup=Join-Path $GameRoot 'WhaleCompanion-backup\P11.5-RC1'
$state=Join-Path $backup 'state.json'
$rows=@();if(Test-Path -LiteralPath $state){$rows=Get-Content -LiteralPath $state -Raw -Encoding UTF8 | ConvertFrom-Json}
function ValidateTarget([string]$relative){
 $allowed=$relative.StartsWith('nativePC/otomo/accessory/ot_acc000/mod/',[StringComparison]::OrdinalIgnoreCase) -or $relative.StartsWith('nativePC/plugins/CSharp/WhaleCompanion/',[StringComparison]::OrdinalIgnoreCase) -or $relative.StartsWith('nativePC/otomo/ot000/mod/otomo000/',[StringComparison]::OrdinalIgnoreCase) -or $relative -in @('nativePC/otomo/equip/ot001/body/mod/ot_body001.mod3','nativePC/otomo/equip/ot001/helm/mod/ot_helm001.mod3','nativePC/common/text/ot_auto_chS.gmd','nativePC/common/text/ot_auto_chT.gmd','nativePC/common/text/ot_auto_eng.gmd')
 if(!$allowed){throw 'Unexpected install target'};return Safe $GameRoot $relative
}
if($Uninstall){
 if(!(Test-Path -LiteralPath $state)){throw 'No release installation backup was found.'}
 foreach($row in $rows){
  $target=ValidateTarget $row.Target;if($row.KeepOnUninstall){continue}
  $current=Hash $target
  if($row.Mode -eq 'seed' -and $current -ne $row.InstalledHash){continue}
  $accepted=@($row.InstalledHash,$row.PreviousInstalledHash,$row.OriginalHash)
  if($row.Target -eq 'nativePC/otomo/ot000/mod/otomo000/otomo000.mod3'){$accepted+='98BE1CE67C1E167CE5787C0FE027F0A72D2874CAB01B5F6017429F95701D88A2'}
  if($current -notin $accepted){throw "Modified managed file retained; uninstall stopped: $($row.Target)"}
  if($row.OriginalHash -and (Hash (Safe $backup $row.Target)) -ne $row.OriginalHash){throw 'Backup hash mismatch'}
 }
 foreach($row in $rows){
  $target=ValidateTarget $row.Target;if($row.KeepOnUninstall){continue}
  if($row.Mode -eq 'seed' -and (Hash $target) -ne $row.InstalledHash){continue}
  if($row.OriginalHash){Copy-Item -LiteralPath (Safe $backup $row.Target) -Destination $target -Force}elseif(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}
 }
 Write-Output 'Release uninstalled. Previous managed files restored; private configuration, memories, edited text and out retained.';return
}
if(!(Test-Path -LiteralPath (Join-Path $GameRoot 'winmm.dll')) -or !(Test-Path -LiteralPath (Join-Path $native 'plugins\CSharp\Loader\SharpPluginLoader.Core.dll'))){throw 'Install the complete compatible SharpPluginLoader before this mod; see README.'}
$checks=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SHA256SUMS.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($item in $checks.PSObject.Properties){if((Hash (Safe $PSScriptRoot $item.Name)) -ne $item.Value){throw "Package checksum mismatch: $($item.Name)"}}
$plan=@()
foreach($file in $package.Files){
 $target=ValidateTarget $file.Target;$source=Safe $PSScriptRoot $file.Source;$wanted=$file.Hash
 if($file.Target -eq 'nativePC/otomo/ot000/mod/otomo000/otomo000.mrl3'){$variant=$package.FaceTints.$FaceTint;$source=Safe $PSScriptRoot $variant.Source;$wanted=$variant.Hash}
 if((Hash $source) -ne $wanted){throw 'Payload hash mismatch'}
 $existing=@($rows | Where-Object Target -eq $file.Target)
 if($file.Mode -eq 'seed' -and (Test-Path -LiteralPath $target)){continue}
 if($existing.Count -gt 1){throw 'Duplicate state target'}
 $current=Hash $target
 if($existing.Count -eq 1){
  $row=$existing[0]
  if($current -notin @($row.InstalledHash,$row.PreviousInstalledHash,$row.OriginalHash)){throw "Managed file changed; upgrade refused: $($file.Target)"}
  $row.PreviousInstalledHash=$row.InstalledHash;$row.InstalledHash=$wanted
 }else{
  $row=[pscustomobject]@{Target=$file.Target;Mode=$file.Mode;KeepOnUninstall=[bool]$file.KeepOnUninstall;OriginalHash=$current;InstalledHash=$wanted;PreviousInstalledHash=$null};$rows+=$row
 }
 $plan+=[pscustomobject]@{Source=$source;Target=$target;Row=$row}
}
# Validate all paths, current hashes and package sources before backing up or replacing any target.
New-Item -ItemType Directory -Force $backup | Out-Null
foreach($entry in $plan){$row=$entry.Row;$saved=Safe $backup $row.Target;if($row.OriginalHash -and !(Test-Path -LiteralPath $saved)){New-Item -ItemType Directory -Force (Split-Path -Parent $saved) | Out-Null;Copy-Item -LiteralPath $entry.Target -Destination $saved};if($row.OriginalHash -and (Hash $saved) -ne $row.OriginalHash){throw "Original backup mismatch: $($row.Target)"}}
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $state -Encoding UTF8
foreach($entry in $plan){New-Item -ItemType Directory -Force (Split-Path -Parent $entry.Target) | Out-Null;Copy-Item -LiteralPath $entry.Source -Destination $entry.Target -Force;if((Hash $entry.Target) -ne $entry.Row.InstalledHash){throw 'Installed hash mismatch; keep backup and run -Uninstall after closing game.'}}
Write-Output "P11.5 RC1 installed, face tint=$FaceTint. Existing configuration retained. Read README and open F9 after loading a save."
