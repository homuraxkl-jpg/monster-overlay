param([string]$GameRoot='D:\steam\steamapps\common\Monster Hunter World',[ValidateSet('original','soft-rosy','rosy')][string]$Tint='soft-rosy',[switch]$Restore,[string]$PackageRoot='', [string]$BackupRoot='')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
if(!$PackageRoot){$PackageRoot=Join-Path $project 'dist\OptionalAppearancePreview'}
if(!$BackupRoot){$BackupRoot=Join-Path $project 'backups\optional-appearance-preview'}
$native=Join-Path ([IO.Path]::GetFullPath($GameRoot)) 'nativePC'
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" | Where-Object ThreadCount -gt 0).Count -gt 0){throw 'Close Monster Hunter World before changing appearance resources.'}
function Hash([string]$p){if(Test-Path -LiteralPath $p){return (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash};return ''}
$model=Join-Path $native 'otomo\ot000\mod\otomo000\otomo000.mod3'
$material=[IO.Path]::ChangeExtension($model,'.mrl3')
$state=Join-Path $BackupRoot 'manifest.json'
$package=Get-Content -LiteralPath (Join-Path $PackageRoot 'appearance-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$baseModel='4AF7D138E38BAF53D528FD6E38B59C10BC9C59A89097432B44D2A1426EFC09A7'
$baseMaterial='E9716C92C557194F83D7309C9B0D9A08288EF3C9613D77F510CA4DA6468A64F9'
$allowedModels=@($baseModel,$package.ModelHash)
$allowedMaterials=@($baseMaterial)+@($package.Materials.PSObject.Properties | ForEach-Object {$_.Value})
if((Hash $model) -notin $allowedModels -or (Hash $material) -notin $allowedMaterials){throw 'Appearance files changed externally; refusing to overwrite.'}
if($Restore){
 if(!(Test-Path -LiteralPath $state)){throw 'No appearance baseline backup exists.'}
 $saved=Get-Content -LiteralPath $state -Raw -Encoding UTF8 | ConvertFrom-Json
 if($saved.ModelHash -ne $baseModel -or $saved.MaterialHash -ne $baseMaterial -or (Hash (Join-Path $BackupRoot 'otomo000.mod3')) -ne $baseModel -or (Hash (Join-Path $BackupRoot 'otomo000.mrl3')) -ne $baseMaterial){throw 'Appearance baseline backup mismatch.'}
 Copy-Item -LiteralPath (Join-Path $BackupRoot 'otomo000.mod3') -Destination $model -Force
 Copy-Item -LiteralPath (Join-Path $BackupRoot 'otomo000.mrl3') -Destination $material -Force
 if((Hash $model) -ne $baseModel -or (Hash $material) -ne $baseMaterial){throw 'Restore hash mismatch'}
 Write-Output 'Original accepted appearance restored. Private settings and original VRM retained.';return
}
$plugin=Join-Path $native 'plugins\CSharp\WhaleCompanion\WhaleCompanion.dll'
if((Hash $plugin) -ne $package.PluginHash){throw 'Install the matching plugin candidate before installing outline assets.'}
$sourceModel=Join-Path $PackageRoot 'otomo000.mod3'
$sourceMaterial=Join-Path $PackageRoot ($Tint+'.mrl3')
$wantedMaterial=$package.Materials.$Tint
if((Hash $sourceModel) -ne $package.ModelHash -or (Hash $sourceMaterial) -ne $wantedMaterial){throw 'Appearance package hash mismatch'}
if(!(Test-Path -LiteralPath $state)){
 if((Hash $model) -ne $baseModel -or (Hash $material) -ne $baseMaterial){throw 'A verified original baseline is required for first install.'}
 New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
 Copy-Item -LiteralPath $model -Destination (Join-Path $BackupRoot 'otomo000.mod3')
 Copy-Item -LiteralPath $material -Destination (Join-Path $BackupRoot 'otomo000.mrl3')
 @{ModelHash=$baseModel;MaterialHash=$baseMaterial} | ConvertTo-Json | Set-Content -LiteralPath $state -Encoding UTF8
}
if((Hash (Join-Path $BackupRoot 'otomo000.mod3')) -ne $baseModel -or (Hash (Join-Path $BackupRoot 'otomo000.mrl3')) -ne $baseMaterial){throw 'Baseline backup mismatch'}
Copy-Item -LiteralPath $sourceMaterial -Destination $material -Force
Copy-Item -LiteralPath $sourceModel -Destination $model -Force
if((Hash $model) -ne $package.ModelHash -or (Hash $material) -ne $wantedMaterial){throw 'Appearance install hash mismatch'}
Write-Output "Optional outline assets installed; tint=$Tint. Outline defaults OFF in F9 > Appearance. Restart game to load resource changes."
