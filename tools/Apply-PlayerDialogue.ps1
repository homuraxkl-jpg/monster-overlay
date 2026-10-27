param([switch]$Restore)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
try {
 if(@(Get-CimInstance Win32_Process -Filter "Name='MonsterHunterWorld.exe'" -ErrorAction Stop | Where-Object ThreadCount -gt 0).Count -gt 0){throw '请先退出怪物猎人世界，再双击应用修改。'}
 $plugin=Split-Path -Parent $PSScriptRoot
 $languageText=Get-Content -LiteralPath (Join-Path $PSScriptRoot '台词语言.txt') -Raw -Encoding UTF8
 $language=switch($languageText.Trim()){'简体中文'{'chS'} '简中'{'chS'} 'chS'{'chS'} '繁體中文'{'chT'} '繁体中文'{'chT'} '繁中'{'chT'} 'chT'{'chT'} 'English'{'eng'} 'eng'{'eng'} 'en'{'eng'} default{throw 'Supported: 简体中文 / 繁體中文 / English.'}}
 $mode=if($Restore){'restore'}else{'build'}
 & (Join-Path $plugin 'OfflineTools\OfflineDialogueTool.exe') $mode $plugin
 if($LASTEXITCODE -ne 0){throw '台词校验未通过，游戏中的台词没有更改。请检查上方提示。'}
 $current=Get-Content -LiteralPath (Join-Path $PSScriptRoot '台词语言.txt') -Raw -Encoding UTF8
 if($current -ne $languageText){throw '处理期间语言发生变化，请重新应用。'}
 & (Join-Path $plugin 'Apply-OfflineDialogue.ps1') -Language $language
 if(!$?){throw '未能应用台词。'}
 Write-Host '应用成功！现在可以启动游戏了。' -ForegroundColor Green
} catch {Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
