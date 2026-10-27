param([switch]$Restore,[string]$GameRoot='',[ValidateSet('chS','chT')][string]$Language='chT')
$ErrorActionPreference='Stop'
if(!$GameRoot){
 if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'config\dialogue')){$GameRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))}
 else{$GameRoot='D:\steam\steamapps\common\Monster Hunter World'}
}
$native=Join-Path ([IO.Path]::GetFullPath($GameRoot)) 'nativePC'
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" -ErrorAction Stop | Where-Object ThreadCount -gt 0).Count -gt 0){throw '请先退出游戏，再应用离线台词。'}
$config=Join-Path $native 'plugins\CSharp\WhaleCompanion\config\dialogue'
$file='ot_auto_'+$Language+'.gmd'
$target=Join-Path $native ('common\text\'+$file)
$backup=Join-Path $config $(if($Language -eq 'chT'){'backups\applied-p108'}else{'backups\applied-chS'})
$statePath=Join-Path $backup 'manifest.json'
$baselineHash='B014B672C6E63802AFC0E31C2DBBD3F2E3B3CD0B2E271848FFAF35EEA9C14B54'
$state=$null
if(Test-Path -LiteralPath $statePath){$state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json}
$exists=Test-Path -LiteralPath $target
$simplifiedDefaultHash='F303990B641940D01CF3C2C2D4B1458A41A250B1B5EA16740372F54CE42C2C37'
$expected=if($state){$state.InstalledHash}elseif($Language -eq 'chT'){$baselineHash}elseif($exists -and (Get-FileHash -LiteralPath $target).Hash -eq $simplifiedDefaultHash){$simplifiedDefaultHash}else{$null}
if(($expected -and (!$exists -or (Get-FileHash -LiteralPath $target).Hash -ne $expected)) -or (!$expected -and $exists)){throw '游戏台词文件被其他工具修改，已停止应用以保留原文件。'}
if($Restore){
 if(!$state){throw '没有需要恢复的台词备份。'}
 $old=Join-Path $backup 'original.gmd'
 if($state.OriginalHash -and (Get-FileHash -LiteralPath $old).Hash -ne $state.OriginalHash){throw '备份校验失败，已停止。'}
 if($state.OriginalHash){Copy-Item -LiteralPath $old -Destination $target -Force}elseif(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}
 $state.InstalledHash=$state.OriginalHash
 $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
 '默认离线台词已恢复，编辑文件和人设保留。';exit
}
$source=Join-Path $config ('generated\'+$file)
$report=Get-Content -LiteralPath (Join-Path $config ('generated\ot_auto_'+$Language+'.validation.json')) -Raw | ConvertFrom-Json
$sourceHash=if($Language -eq 'chT'){'1167eab1388de4d74534f4563dfdd840f9581c777dbcee245d55aff7b408dc93'}else{'ac40e7e008395c9a30feb89bd50d09a9fda50d3b45d542276463ed6db0a837fd'}
if($report.source_file -ne $file -or $report.source_sha256 -ne $sourceHash){throw '台词源文件或语言不匹配。'}
$newHash=(Get-FileHash -LiteralPath $source).Hash
if($newHash -ne $report.output_sha256){throw '台词与校验报告不一致，请重新生成。'}
$bytes=[IO.File]::ReadAllBytes($source)
if($bytes.Length -lt 40 -or [Text.Encoding]::ASCII.GetString($bytes,0,3) -ne 'GMD' -or [BitConverter]::ToInt32($bytes,24) -ne 240){throw '生成的台词文件无效。'}
New-Item -ItemType Directory -Force $backup | Out-Null
if(!$state){if($exists){Copy-Item -LiteralPath $target -Destination (Join-Path $backup 'original.gmd')};$state=[pscustomobject]@{OriginalHash=$expected;InstalledHash=$expected}}
$state.InstalledHash=$newHash
$state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
Copy-Item -LiteralPath $source -Destination $target -Force
if((Get-FileHash -LiteralPath $target).Hash -ne $newHash){throw '应用后校验失败，请保留备份并联系作者。'}
'离线台词已应用，重启游戏后生效。'
