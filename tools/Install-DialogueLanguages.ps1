param([string]$GameRoot='D:\steam\steamapps\common\Monster Hunter World',[switch]$Restore,[ValidateSet('DialogueLanguages','WeaponStateProbe','WeaponCombatPreview','NativeGuardPreview','ActionConfirmationPreview','SaedDamagePreview','DynamicCombatPreview')][string]$Package='DialogueLanguages')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$stage=Join-Path $project ('dist\'+$Package)
$native=Join-Path ([IO.Path]::GetFullPath($GameRoot)) 'nativePC'
$backup=Join-Path $project $(if($Package -eq 'DynamicCombatPreview'){'backups\dynamic-combat-preview'}elseif($Package -eq 'SaedDamagePreview'){'backups\saed-damage-preview'}elseif($Package -eq 'ActionConfirmationPreview'){'backups\action-confirmation-preview'}elseif($Package -eq 'NativeGuardPreview'){'backups\native-guard-preview'}elseif($Package -eq 'WeaponCombatPreview'){'backups\weapon-combat-preview'}elseif($Package -eq 'WeaponStateProbe'){'backups\weapon-state-probe'}else{'backups\dialogue-languages'})
$stateFile=Join-Path $backup 'manifest.json'
if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" -ErrorAction Stop | Where-Object ThreadCount -gt 0).Count -gt 0){throw '游戏正在运行，未安装。请在下次正常退出后运行本脚本。'}
function Target([string]$relative){
 $p=[IO.Path]::GetFullPath((Join-Path $native $relative))
 if(!$p.StartsWith($native+'\',[StringComparison]::OrdinalIgnoreCase)){throw '目标越界'}
 return $p
}
if($Restore){
 $rows=Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8 | ConvertFrom-Json
 foreach($row in $rows){
  if($row.Preserve){continue}
  $t=Target $row.Target
  if(!(Test-Path -LiteralPath $t) -or (Get-FileHash -LiteralPath $t).Hash -ne $row.NewHash){throw '文件已修改，请先保留玩家数据再回滚。'}
  if($row.Existed -and (Get-FileHash -LiteralPath (Join-Path $backup $row.Target)).Hash -ne $row.OldHash){throw '备份校验失败'}
 }
 foreach($row in $rows){
  if($row.Preserve){continue}
  $t=Target $row.Target
  if($row.Existed){Copy-Item -LiteralPath (Join-Path $backup $row.Target) -Destination $t -Force}else{Remove-Item -LiteralPath $t}
 }
 '已恢复升级前程序和简中覆盖；玩家文本、人设和目录保留。';exit
}
if(Test-Path -LiteralPath $stateFile){throw '本次升级已有备份，未重复安装。'}
$items=Get-Content -LiteralPath (Join-Path $stage 'upgrade-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$rows=@()
foreach($item in $items){
 $t=Target $item.Target
 if($item.Preserve -and (Test-Path -LiteralPath $t)){continue}
 $source=[IO.Path]::GetFullPath((Join-Path $stage $item.Source))
 if(!$source.StartsWith($stage+'\',[StringComparison]::OrdinalIgnoreCase)){throw '安装源越界'}
 if((Get-FileHash -LiteralPath $source).Hash -ne $item.NewHash){throw '候选包校验失败'}
 $exists=Test-Path -LiteralPath $t
 $old=if($exists){(Get-FileHash -LiteralPath $t).Hash}else{$null}
 if($old -ne $item.ExpectedHash){throw "文件被其他工具改动，停止安装：$($item.Target)"}
 $rows+=[pscustomobject]@{Target=$item.Target;Source=$source;Existed=$exists;OldHash=$old;NewHash=$item.NewHash;Preserve=$item.Preserve}
}
New-Item -ItemType Directory -Force $backup | Out-Null
foreach($row in $rows){if($row.Existed){$b=Join-Path $backup $row.Target;New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null;Copy-Item -LiteralPath (Target $row.Target) -Destination $b}}
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding UTF8
foreach($row in $rows){$t=Target $row.Target;New-Item -ItemType Directory -Force (Split-Path $t) | Out-Null;Copy-Item -LiteralPath $row.Source -Destination $t -Force;if((Get-FileHash -LiteralPath $t).Hash -ne $row.NewHash){throw '安装后校验失败，请保留备份'}}
if($Package -eq 'DynamicCombatPreview'){'P11.5动态装备/招式门槛与关键资源判据已安装；默认关闭，待集中实机验证。'}elseif($Package -eq 'SaedDamagePreview'){'P11.4主人累计伤害与超解双结果评价已安装；默认关闭，阈值可调，面板伤害自动估算尚未接入。'}elseif($Package -eq 'ActionConfirmationPreview'){'P11.3实际动作复核与防御反作用观察已安装；默认关闭，精确GP与单次伤害归属仍未知。'}elseif($Package -eq 'NativeGuardPreview'){'P11.2原生攻击守势结果读取已安装；默认关闭，GP与命中仍不推断。'}elseif($Package -eq 'WeaponCombatPreview'){'P11.1全武器候选状态/动作及伤害观察基础已安装；默认关闭，伤害来源尚未验证，不等于完整命中检测。'}elseif($Package -eq 'WeaponStateProbe'){'首批四武器只读诊断已安装，默认关闭，无新Hook或API请求；读取语义待实机验证。'}else{'简繁语言升级已安装。既有繁中台词、聊天配置、记忆、人设和模型保留；游戏显示仍需实机观察。'}
