param([string]$GameRoot='D:\steam\steamapps\common\Monster Hunter World')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$stage=Join-Path $project 'dist\DynamicCombatPreview'
$backup=Join-Path $project 'backups\dynamic-combat-preview'
$native=Join-Path ([IO.Path]::GetFullPath($GameRoot)) 'nativePC'
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" -ErrorAction Stop | Where-Object ThreadCount -gt 0).Count -gt 0){throw '游戏正在运行，未更新。'}
$stateFile=Join-Path $backup 'manifest.json'
$state=Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest=Get-Content -LiteralPath (Join-Path $stage 'upgrade-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
function Target([string]$relative){$p=[IO.Path]::GetFullPath((Join-Path $native $relative));if(!$p.StartsWith($native+'\',[StringComparison]::OrdinalIgnoreCase)){throw '目标越界'};return $p}
foreach($row in $state){
 if($row.Target -notin @($manifest.Target)){throw '清单不一致'}
 if((Get-FileHash -LiteralPath (Target $row.Target)).Hash -ne $row.NewHash){throw '实际安装文件发生变化，未覆盖'}
 if($row.Existed -and (Get-FileHash -LiteralPath (Join-Path $backup $row.Target)).Hash -ne $row.OldHash){throw '原始备份校验失败'}
 $item=$manifest | Where-Object Target -eq $row.Target
 if((Get-FileHash -LiteralPath (Join-Path $stage $item.Source)).Hash -ne $item.NewHash){throw '候选文件哈希不符'}
}
$changed=@($state | Where-Object { $row=$_;($manifest | Where-Object Target -eq $row.Target).NewHash -ne $row.NewHash })
if($changed.Count -eq 0){'候选已安装，无需更新。';exit}
$revision=Join-Path $backup ('revision-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Force $revision | Out-Null
Copy-Item -LiteralPath $stateFile -Destination (Join-Path $revision 'manifest.json')
foreach($row in $changed){$p=Join-Path $revision $row.Target;New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null;Copy-Item -LiteralPath (Target $row.Target) -Destination $p}
try{
 foreach($row in $changed){$item=$manifest | Where-Object Target -eq $row.Target;Copy-Item -LiteralPath (Join-Path $stage $item.Source) -Destination (Target $row.Target) -Force;if((Get-FileHash -LiteralPath (Target $row.Target)).Hash -ne $item.NewHash){throw '安装校验失败'};$row.NewHash=$item.NewHash}
 $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding UTF8
}catch{
 foreach($row in $changed){Copy-Item -LiteralPath (Join-Path $revision $row.Target) -Destination (Target $row.Target) -Force}
 Copy-Item -LiteralPath (Join-Path $revision 'manifest.json') -Destination $stateFile -Force
 throw
}
'P11.5最终候选已更新并核对哈希；保留原升级前备份和中间版本，配置与模型未覆盖。'
