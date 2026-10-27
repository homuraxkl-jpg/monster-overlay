# 大肥鱼 AI艾露猫陪伴模组 — P11.5 RC1

这是用于收集玩家反馈的发布候选，不代表所有武器、多人模式和模组组合都已实机验收。

## 新手先看

本模组需要另外准备的前置只有两项：

| 名称 | 是什么 | 是否必装 |
| --- | --- | --- |
| SharpPluginLoader | 让游戏加载本模组C#程序的加载器，完整发行包解压到游戏根目录 | 是，已有兼容版本无需重复安装 |
| .NET 8 Desktop Runtime x64 | 微软提供的程序运行环境，下载后运行安装程序即可；不是编程软件 | 是，已有8.x桌面版x64无需重复安装 |
| Stracker’s Loader | 另一套MHW原生插件加载器 | 本模组不要求；其他模组可能需要 |
| Lua Engine／Lua插件 | 运行Lua脚本的扩展环境 | 本模组不要求 |
| Performance Booster and Plugin Extender | 其他插件可能使用的性能及扩展组件 | 本模组不要求；其他模组可能需要 |

不会编程也能安装：安装微软运行环境 → 解压SharpPluginLoader到游戏根目录 → 解压本包，双击Install.cmd并选择MonsterHunterWorld.exe。不要直接在压缩包浏览窗口运行，先完整解压。本包包含模组文件及安装脚本，不捆绑上述前置，也不是需要编译的源码包。

## 前置与安装

- Windows x64，Steam《怪物猎人：世界／冰原》421810／15.23，建议冰原环境。
- 安装完整兼容的 [SharpPluginLoader](https://fexty12573.github.io/SharpPluginLoader/Install/Installation.html)，本项目依赖 Core 2.0.0。仅下载一个Core DLL不够；按加载器说明部署winmm.dll与Loader目录。
- 安装.NET 8 Desktop Runtime x64。普通玩家不需要.NET SDK、Python、Blender、HunterPie或CE。
- Stracker、Lua、Performance Booster不是本C#插件的直接前置；已有其他模组请保留其必要依赖，不要为本模组盲目卸载或替换加载器。

退出游戏，解压候选包，双击 `Install.cmd`，选择游戏根目录的 `MonsterHunterWorld.exe`。也可运行 `Install.ps1 -GameRoot "你的游戏目录"`。脚本不结束游戏进程，只拦截有活动线程的游戏；零线程残留不算游戏仍在运行。安装会检查包内哈希，备份被替换的文件，并保留已有私人配置与可编辑台词。

完整包包含基础艾露猫替换、米饭回血道具替换及皮制套（ot001 Leather）的头、身隐藏补丁；请先使用皮制套外观，其他装备可能叠加。原始VRM、开发工具、解包库、游戏代码缓存、研究采样均不包含在包内。本次提供完整候选包；不要与旧开发预览压缩包混装。

## 首次使用与界面

进入存档后按F9。日常设置支持简体中文、繁體中文、English，初始跟随Windows界面语言，无法识别用英文；高级技术诊断保留英文。以下均使用英文界面实际标签。

- `Enable all companion categories`：启用现有武器、生命、怪物状态等陪伴检测。各类别可单独启停；`Disable all companion categories`取消自动陪伴，不删除私人资料。
- `Dynamic AI rewriting (paid requests)`：让运行时事件按人设和逐事件提示词生成话术。关闭时使用当前离线台词；缺少API密钥或请求失败时按规则回退。外置 `config/responses.json` 提供逐事件提示词、离线句、类别共享CD和阈值。
- `Save last lines as offline defaults`：将本次每种状态最后实际说出的一句保存为该事件的离线文本。不是保存全部聊天，也不修改游戏GMD。
- `AI model and persona`：设置 `Model ID`、`Native chat decision/reply model`、`Chat completions URL`、`External persona`，在密钥窗口填写自己的API key，本机DPAPI加密保存。
- `Reply language` 独立于界面语言；默认跟随人设／本机用户，也可选简体、繁体或英文。固定离线句不会因此自动翻译。

分类默认开关和检测总开关是不同层级，首次请点击一次Enable all。实验伤害评价不是精确招式归属；默认不启用一分钟输出评价，需提供人工基础攻击参考。

## 游戏输出、联机和记忆

`Game output, multiplayer and memory` 中：

- `Show replies in native game messages` 默认开启，回复进入本机游戏提示区。字幕和 `Show chat status overlay (diagnostic)` 可选；异常时可能回退。加载存档后若未恢复，点 `Resume game output and chat (loaded save)`。
- `Multiplayer mode: send companion lines to game chat` 默认关闭。开启后陪伴台词通过当前非私聊频道发送，统一前缀【大肥鱼】；不替你切换频道。提交成功不等于队友已收到，队友可见性仍需多人反馈。
- `Chat with AI using native game chat`：本机玩家可在原生聊天框与AI对话。`Remember peer chat in multiplayer`和`AI may reply to relevant peer chat`分别控制队友上下文与有意义的回复。队友显示名不是身份认证。
- `Remember recent hunt observations`保留近期战斗背景；`AI may add relevant proactive hunt comments`默认关闭。原生判断／回复可用独立便宜模型，允许AI保持沉默，消息仍可进入短期上下文。
- 短期观察各最多64条、20分钟，普通对话也有轮数／字符上限，不是无限聊天档案。New conversation清对话，不自动清狩猎概览；切存档清近期上下文。跨对话长期记忆必须由玩家确认保存，AI不会自行永久写入。
- `Export selected context to out`：手动选择聊天、狩猎和确认记忆，生成本机ZIP摘要与JSON，保存到插件 `out/`，供你自行交给外部AI。不会自动上传；包仍含你选择的私人内容，反馈时不要上传。

关闭Dynamic AI只关闭自动事件改写；主动评论、原生聊天及面板手动Send有各自入口。要停用这些请求，请同时关闭对应入口。AI服务费用由玩家自己的API账号承担。

## 原生离线台词编辑

完整包提供简体、繁体、英文三个ot_auto资源与TXT入口；英文默认保持原生英文语义，尚未统一润色。

退出游戏，在插件 `玩家自定义/台词语言.txt` 写 `简体中文`、`繁體中文`或`English`，编辑相应TXT，再双击 `应用修改.cmd`。参数标记必须保留；工具校验失败不会替换游戏文件。修改后重启游戏加载。`恢复默认台词.cmd`恢复该编辑流程保存的基线。可通过F9离线编辑区用自己的AI人设预览／改写，不自动处理全部资源。

每种语言240项，回复蜜露有玩家实机确认；不能承诺每个任务资源或全部台词都已覆盖。更新安装保留现有台词；卸载会保留用户改过的可编辑文件。

## 外观、日志与反馈

- `Appearance` → `Show head pot`控制铁盆。默认轻度红润；退出游戏运行 `Install.ps1 -FaceTint original`或`rosy`切换。身体、腿、短裙、权重及原始VRM不作调整。
- 模型描边按项目安排延期；本候选没有失效描边层、描边开关或自动原生轮廓采样。头发／衣服物理同样不在本次发布范围。
- 默认只留简要启动／错误日志，达到约1MiB后轮换，详细诊断默认关闭；按需要启用 `Detailed diagnostic logging (local only)`。不要上传整个config、saved-memories、聊天或out。
- 已通过的玩家测试包括部分盾斧／大锤、有效翻滚与雷球／激光、猫HP和恢复、若干怪物异常与陷阱／软化。弓和双刀特殊回避、其他武器新规则、全部多人路径等仍有未覆盖项。离线回归不等于游戏验证。
- 单人显示伤害可能混入随从／环境，基础攻击不是实时完整面板；大剑肩撞和伤害旋律分类按安排跳过。旧退出确认异常未归因，近期退出测试正常。

提交反馈前看 `docs/FEEDBACK.md`。完整触发提示词清单见 `docs/trigger-catalog.csv`，默认阈值见config示例；台词最终润色及描边可以结合玩家反馈再处理。

## 卸载与来源

退出游戏运行 `Uninstall.cmd`，选择同一游戏exe。会校验并恢复 `WhaleCompanion-backup/P11.5-RC1` 中的原文件；不删除密钥、私人记忆、out或修改过的台词。未知的托管文件改动会停止卸载，避免覆盖别人的修改。请保留安装包和备份目录。

模型原作者：木子恶魔；原作名：蓝色小肥鱼Deepseek。项目维护者已确认获得转换后模型及贴图的公开发布授权（2026-10-27）；本包不包含原始VRM，不代表授权他人提取、改作或单独再分发模型。模型与第三方代码的权利分别保留。项目原创部分未另行授予开源许可证；第三方许可证全文和来源说明随插件附带，不受这句话缩减。


## RC1反馈修订：输出、延迟与离线文字

`Game output, multiplayer and memory`提供`Test local game message (no API)`、多人模式下的`Check multiplayer sender (read-only)`和`Test multiplayer game chat (no API)`。本机原生消息与网络玩家聊天是两条路径；网络发送尚须实机确认队友收到，发送失败或忙碌会回退本机，不能把回退当成广播成功。

类别CD：health 12秒、guard 15秒、monster 12秒、weapon 18秒、buff 18秒；evasion/spirit/damage/quest默认0，升刃无CD。类别内共享CD，另有检测器自身阈值和全局安静时间；CD阻止触发，不会把事件排到CD结束再说。AI事件请求超过2.5秒回退当前离线句；事件3秒过期，本机提示间隔1秒、网络提交间隔2秒，仅保留最新一句，不积压多招评价。战斗结果待说超过2秒会丢弃。上述限制不等于所有事件都应该每秒触发；默认类别CD仍由responses.json控制。

太刀非升刃事件不允许AI把见切夸奖改成开刃公告，红刃刷新保持静默；真正黄刃升红刃仍可回应。简繁GMD按目标资源独立生成，生成时规范文字脚本并保留原生图标／名字参数；不混装简体文本进繁体GMD。

`Offline Palico dialogue / persona editor` → `Use current chat persona.md` → `Rewrite all editable lines with persona (paid API, preview only)`可分批改写全部可编辑台词，保留特殊说话者原句。先预览，再点`Save generated batch and build candidate`；退出游戏运行`应用修改.cmd`，重启后加载。一次按钮会顺序产生多次付费请求，可取消，失败不会自动保存，不发送聊天记忆或原始VRM。繁体目标会规范为繁体，简体目标规范为简体。

时序默认值可在`Response categories and fixed lines` → `Output timing (seconds)`调整，也对应`config/responses.json`的Thresholds：EventFreshnessSeconds、DynamicEventTimeoutSeconds、NativeOutputIntervalSeconds、SharedOutputIntervalSeconds、AiReplyQuietSeconds、CombatResultMaxAgeSeconds。调大有效期会容许旧招式评价延后出现。


## RC1反馈修订2：动作评价与表情

使用已转换的原模型形态键网格，不新增表情资产或改动原始VRM。`Automatic face animation (experimental)`默认开启；关闭后仍可手动预览，自动表情与眨眼停用。

- 正面战斗评价：微笑；负面评价：难过。检测确认时立即驱动表情，不等待AI或聊天发送，也不因台词CD排队延后表情。
- 安全脱身等中性结果：放松；不是负面评价。
- 玩家HP不高于25%：优先保持伤心；恢复到40%及以上解除危险状态，避免阈值附近跳脸。这两个比率沿用responses.json的LowHealthRatio和LowHealthResetRatio。残血时夸奖或AI聊天表情不能盖掉危险表情。
- 玩家恢复状态：放松；即使没有触发回血台词，从未满血恢复至满血也会放松约4秒，然后回到自然表情和眨眼。满血阈值为Thresholds.FullHealthRelaxRatio，默认1。换场景、初次读取或最大HP变化不会凭空生成回血表情。
- AI实时战斗改写只决定话术，不能用迟到的情绪字段覆盖已确认的战斗／生命表情；普通AI聊天仍可表达情绪。

本轮通过7119项表情／聊天／GMD断言、1130项武器检查、377项配置检查及构造检查；新增绑定仍待实机确认。此前的伤害来源和动作映射限制仍适用，不因表情变化变成精确命中评价。
