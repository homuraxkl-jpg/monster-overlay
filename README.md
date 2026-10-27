# 大肥鱼 AI 艾露猫陪伴模组

目标环境为 Windows x64、Steam《怪物猎人：世界／冰原》421810／15.23。尚未承诺其他版本、Proton 或所有模组组合兼容。

## 安装前置

1. **.NET 8 Desktop Runtime x64**。普通玩家无需安装 SDK、Python、Blender、HunterPie 或 Cheat Engine。
2. **SharpPluginLoader** 完整发行包，需与本插件构建依赖 Core 2.0.0 相匹配。把加载器文件放在 `MonsterHunterWorld.exe` 所在目录；不能只复制 NuGet Core.dll。官方Windows说明要求游戏根目录有 `winmm.dll`，插件目录为 `nativePC/plugins/CSharp`，F9 打开加载器菜单。[官方安装说明](https://fexty12573.github.io/SharpPluginLoader/Install/Installation.html)、[.NET Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)。
3. 本插件 DLL 与配套 `System.Security.Cryptography.ProtectedData.dll` 放到 `nativePC/plugins/CSharp/WhaleCompanion`；不要放进加载器的 `Loader` 子目录。依赖及其许可应随插件包提供。
4. 完整模型与离线台词属于另外的资源组件。本工程的 `CleanPluginPreview.zip` 只是独立插件候选，不能单独还原整套大肥鱼模型。安装完整模组时按最终资源清单部署，保留已有备份。

SharpPluginLoader 是本C#插件的直接前置。Stracker's Loader、Performance Booster、Lua插件并非本工程C#包引用的直接依赖；本机已有的这些组件继续保留，不能据此宣称干净环境组合已验证。原生文件读取的旧版说明不可直接当作15.23新增硬前置；Stracker上游文件记录注明NativePC enabler已不再需要。不要为此覆盖或卸载原有加载器。[Stracker文件说明](https://www.nexusmods.com/monsterhunterworld/mods/1982?tab=files)。

**只有启用AI功能才需要网络、模型服务账号和API密钥。** 离线固定回应、原生台词替换不需要AI调用。API可能收费，取决于所选服务。

安装、更新、应用原生台词、回滚都必须先退出游戏。安装脚本检查进程真实线程数；不把零线程残留当作仍在运行，也不会自动结束正常游戏。备份配置与现有文件后再更新。

## F9：日常使用

进入存档后打开F9。本插件顶部有 `English`、`简体中文`、`繁體中文`。首次按Windows用户界面文化选择简中/繁中；其他语言或无法识别时使用英文，选择保存于 `config/ui-language.txt`。界面语言不更改游戏语言，也不自动应用其他语言的原生GMD。高级诊断区保留英文原始动作名、字段和技术状态。

- `Enable all companion categories`：一键启用陪伴所需探针、动作/状态检测和全部类别。原有多个诊断开关不再是普通用户必须逐个开启的步骤。
- `Disable all companion categories`：关闭这些陪伴检测与回应，并取消尚未完成的自动AI请求。不会删除已固定台词、私人记忆或密钥。
- 单独启用某类别：先一键关闭，再在 `Response categories and fixed lines` 勾选需要的类别。启用类别会启动所需检测；未选类别不会发言。
- 类别包括生命、回避、防御后准备、太刀升刃、怪物状态、伤害评价、武器动作、实际增益、任务结果。
- `Dynamic AI rewriting (paid requests)`：所有上述运行时事件都可使用外置人设、该事件提示词与本次只读事实生成一句回复。不会先固定说一句、再AI重复一句。关闭时沿用离线固定文本；请求失败、忙碌或无密钥时回退固定文本。迟到且已失去相关性的结果丢弃。此开关不会替用户发送聊天消息。
- 类别 `Shared cooldown (seconds)` 是该类共享发言间隔；太刀升刃无CD，不把消耗刃色当升刃。检测去重、读数有效期、忙碌/场景保护与发言间隔不同。
- 点击某事件可编辑 `Offline fallback line` 和 `AI event wording instructions`，再 `Save selected event`。提示词决定话术和风格，不应把未确认事实写成成功。
- `Save last lines as offline defaults`：将本次运行各事件最后实际说出的句子固定到离线文本。只保存已触发、实际输出的事件；没见过的状态保留默认。普通聊天历史、密钥和私人记忆不会写入该表。此操作会把生成的台词写入本机配置，请勿把个人配置作为公开包上传。
- `Reload external response settings`：重新读取外置配置并取消旧自动请求。无效文件保留旧设置，阻止覆盖错误配置；修复后重新加载。

一键开关影响运行时陪伴，不修改游戏行为、判定、无敌帧或增益，不会开关原生资源台词。实验检测仍需最终实机回归。联网共享给队友看的输出有独立开关，不会随一键陪伴自动开启。

## 模型服务、人设与密钥窗口

展开 `AI model and persona`：

| 选项 | 作用 |
|---|---|
| Model ID | 服务提供的精确模型ID，可手工填写 |
| Native chat decision/reply model | 可独立选择原生聊天相关性判断与回复模型，可选轻量模型，一次判断并生成 |
| Chat completions URL | 完整的OpenAI兼容Chat Completions接口URL，不是Responses接口；默认DeepSeek |
| External persona | 多行人设输入框；保存到 `config/persona.md` |
| Save model and persona | 校验后保存到本机，重新加载并清空当前对话；不删除长期记忆 |
| API key (optional replacement) | 密码遮罩输入；不会写进普通JSON |
| Save encrypted key | 用当前Windows用户DPAPI加密保存；其他Windows账号/设备不能直接使用该加密文件 |
| Send / Cancel / New conversation | 手动发送、取消当前请求、开始新对话；保存的跨对话记忆另行管理 |

`Native chat decision/reply model` 可以独立填写原生聊天判断与回复所用模型（settings.json 的 NativeChatModel），战斗事件改写继续使用 Model ID。可以选择较轻量的模型处理聊天相关性：单次请求同时判断并生成，不必为每条消息先后调用两个模型。不同服务的兼容性与联网调用仍需实测；本轮没有代用户发送收费测试请求。

## 外置文件与默认值

位置：`nativePC/plugins/CSharp/WhaleCompanion/config`。

- `settings.json`：Endpoint、Model、NativeChatModel、上下文及回复长度等。没有API key字段。
- `persona.md`：AI外置人设；普通聊天与事件改写共用。
- `responses.json`：Enabled总开关、DynamicAi动态改写、Categories分类启用/共享CD、Events逐事件固定文本与提示词、Thresholds阈值。首次从源码默认目录补齐事件，已有编辑保留。未运行到的分支也可从预置事件中编辑；其他实际分支首次识别后加入事件表。
- `Events.<事件键>.OfflineText` 为空：沿用当前程序的原有固定句或轮换池；填写文字后：使用这句固定文字。ReferenceText仅为编辑参考，不自动取代整个原轮换池。Prompt是此事件AI话术要求。
- `combat-response.json`：原有动态伤害参考、正面评价比例、人工门槛、一分钟输出窗口/倍数/冷却等；不是完整实时面板读取。
- `ui-language.txt`：chS / chT / eng。

阈值在 `responses.json/Thresholds`，可退出游戏后修改，或保存后重新加载：

| 参数 | 默认值与意义 |
|---|---|
| LowHealthRatio / LowHealthResetRatio | 0.25触发低血量、0.4解锁下一次 |
| HealthLossRatio / HealthLossWindow | 2秒累计掉血15% |
| PlayerHealRatio / PlayerHealMinimum / PlayerHealWindow | 6秒恢复至少max(最大HP20%,20HP)；期间掉血重置 |
| PlayerHealCooldown | 玩家恢复检测自身30秒间隔，另受生命类别共享CD影响 |
| PalicoArmRatio / PalicoHealRatio / PalicoHealWindow | 猫低于75%准备，10秒恢复阶段净增最大HP25% |
| PalicoHealCooldown | 猫恢复检测自身30秒，另受生命类别共享CD影响 |
| EvasionCount / EvasionWindow / BowDualEvasionWindow | 4次；其他武器60秒，弓/双刀30秒；发言后重新计数 |
| MantleCooldown | 衣装专属增益回应18秒，与有效回避计数是两条路线 |
| BowPowerCount / BowPowerWindow | 8秒内3次刚射，再评价第三次阶段 |
| SameActionExtraCooldown | 同类动作额外重复抑制12秒，另受武器类别共享CD影响 |

数值是可调的发言/评价规则，不会改变游戏原生判定。怪物归属不清时不说主人造成；猫和玩家回血都只说状态，不判断治疗来源。G01是后续动作准备回应，不宣称GP；狩猎笛伤害旋律和大剑肩撞按用户决定跳过。

## 原生离线台词：简中／繁中／英文

这组是游戏GMD资源，不是上面的运行时事件。简繁保留当前安装内容；新增英文独立目录和TXT。英文初始默认保留原生英文语义，**尚未统一做人设润色**，可以手写或用AI人设改写。

在插件 `玩家自定义` 目录编辑所选TXT：简中 `随从台词-简体中文.txt`、繁中 `随从台词.txt`、英文 `Palico-dialogue-English.txt`。先在 `台词语言.txt` 选择 `简体中文` / `繁體中文` / `English`，保留标题与名字/音符/参数标记。退出游戏后运行现有 `应用修改.cmd`；也可在F9原生编辑器按资源键改写，生成候选并保存，然后退出应用并重启。

外部AI可在启动前按TXT标题和占位符逐条改写；校验不通过不会应用。内置AI编辑器支持共用或独立人设、最多8条一批，付费请求只生成预览，经用户保存后才更新候选。两种方式均不自动更改游戏文本语言。内置编辑器使用已配置模型/API接口，不发送聊天记忆。

每种语言240项。回复蜜露台词已有用户实机确认，其余资源键与动作的完整对应关系尚未实机逐条确认。更新原生台词需要重启游戏；普通AI事件不需要重建GMD。

## 验证与尚未完成

源码覆盖和玩家实机反馈见 [CURRENT-FEATURE-COVERAGE.md](CURRENT-FEATURE-COVERAGE.md)。离线通过不等于游戏验证；新统一回应、设置面板和英文路线仍需最终实测。单人显示伤害可能混入随从/环境；部分武器映射、弓/双刀特殊回避、部位破坏输出未全部实测。

原VRM、身体、腿部、短裙和权重保持不变；可选描边与轻度脸部红润材质已有候选，原生渲染仍待实机确认。来源和许可见 [THIRD-PARTY.md](THIRD-PARTY.md)。个人密钥、记忆、聊天和运行配置禁止加入发布包或Git。


## 开发候选更新与回滚

当前开发安装分为插件刷新和界面/英文工具补充，两步都要在退出游戏后执行：

```powershell
./tools/Refresh-DynamicCombatPreview.ps1
./tools/Install-ResponseInterfacePreview.ps1
```

前者更新DLL及动态战斗说明，后者更新界面说明/离线工具与缺失的英文编辑资源。补充安装不覆盖已有英文自定义文件，不复制当前开发配置，不更改简繁资源或语言选择文件。文件哈希与原始文件保存在 `backups`，不是普通玩家必须运行的构建过程。

回退补充文件：`./tools/Install-ResponseInterfacePreview.ps1 -Restore`；回退整个动态战斗阶段：`./tools/Install-DialogueLanguages.ps1 -Package DynamicCombatPreview -Restore`。这是回到原阶段基线；需要回到最近一次更新前DLL时，应从刷新脚本保存的中间版本恢复，不能把阶段基线称为最近一版。脚本遇到安装后被用户编辑的文件会拒绝覆盖，先保留编辑再处理。


## 原生输出、多人联机与游戏内对话

进入存档后，在 `Game output, multiplayer and memory` 管理以下开关。刚载入存档时，可点 `Enable all companion categories` 或 `Resume game output and chat (loaded save)` 恢复就绪状态。

| 英文选项 | 作用 |
|---|---|
| Show replies in native game messages | 默认开启；单机将对白显示到本机游戏原生消息区域，统一以【大肥鱼】开头；关闭后才选择模组字幕窗口 |
| Show chat status overlay (diagnostic) | 默认关闭；只是额外的诊断状态浮窗，不是对白输出位置 |
| Multiplayer mode: send companion lines to game chat | 默认关闭；开启后对话回复、AI战斗回应和固定离线回应都进入玩家原生聊天，沿用当前非私聊频道；游戏显示当前猎人身份，正文用【大肥鱼】区别AI |
| Chat with AI using native game chat | 直接在游戏聊天输入消息，无需改到F9输入；名字与问题优先考虑回应；原消息仍由游戏正常发送，不会被插件改写 |
| Remember peer chat in multiplayer | 多人模式下读取已识别的队友聊天作为上下文；显示名不等于已验证身份 |
| AI may reply to relevant peer chat | 允许对合适的队友消息回应；不是每条必答；本机主人输入优先，AI自己的【大肥鱼】输出不会再次触发AI |
| Remember recent hunt observations | 将已检测的战斗事实保留为短期AI上下文，区别于当前HP/武器快照 |
| AI may add relevant proactive hunt comments | 可选；默认关闭。动态AI开启后，有新狩猎观察且到了间隔，才让原生聊天判断模型决定是否补一句主动陪伴；没有新信息不持续空转 |
| Clear recent hunt/chat context | 清空近期观察，不删除人工确认的长期记忆；会取消当前请求 |

本机原生提示不是艾露猫原生气泡或带猫头像的台词。多人模式使用游戏玩家聊天输入/发送路线，不冒充猫的原生发言者；127 UTF8字节上限包括【大肥鱼】，过长会安全省略。不会覆盖你正在输入的文本，也不主动改频道或发送私聊。检测到当前私聊时不向该频道自动共享；本机私聊文本也不作为自动AI输入。多人发送仅表示交给了游戏，仍需队友实机确认收到，不能以本机哈希代替联机验收。

原生界面暂时未就绪会排队；布局/调用异常时停止该路径并保留字幕回退，面板显示原因。旧版原生显示开关没有持久化且默认关闭，可能导致重启后回到模组字幕；现在输出偏好保存到 `config/conversation.json`。高级诊断不再承担普通用户开关入口。

这些聊天选项与“战斗台词动态改写”不同：关闭 Dynamic AI rewriting 只切回战斗/状态固定句，不关闭你明确启用的原生聊天AI功能。若不希望主动AI读聊天，请关闭对应聊天选项。

## 上下文、沉默与跨对话记忆

已有最近对话轮数/字符限额和人工确认的跨对话记忆。现在补充了独立的近期观察层：

- 当次对话历史按 `ContextTurns` / `MaxContextCharacters` 保留；原生聊天的相关性判断可返回 should_reply=false，沉默的消息仍留在近期观察，不会因此失忆。
- 近期狩猎事实和聊天观察各为有界队列，默认最多64条、20分钟，并按字符预算选择最近内容送入模型；只在内存中，不自动保存聊天文件。
- 狩猎概览另保留当前及上一段有记录遭遇的观察分类计数，避免近期队列淘汰后所有线索消失。它是记录次数，不是精确动作/命中总数、真实DPS或主人贡献。
- 怪物结果和伤害估计不据此获得主人归属；过往观察不当作当前状态；队友引用不能成为配置、权限或记忆授权。
- `New conversation` 清空对话历史与近期聊天引用，保留当前狩猎观察及人工确认的长期事实；换存档会清掉近期观察，长期事实仍由用户自行管理。
- 在 `Cross-conversation memory` 中启用 `Use saved memories in AI requests`，可从最近主人输入拟定或手工填写事实，再 `Save memory`。支持编辑/删除/清空；没有用户保存就不自动写永久记忆。手工 `memory.md` 与确认记忆 `saved-memories.json` 是分开的。

`conversation.json` 还可调 NativeChatCooldownSeconds（默认8秒）与 ProactiveIntervalSeconds（默认45秒）。主动判断可以用你选择的轻量模型，保留上下文、偶尔不回话是预期行为；不把“保持沉默”误判为“没有读到”。

### 私人上下文导出与对白语言

F9 → `Game output, multiplayer and memory` → 按需选择 `Export hunt context`、`Export recent dialogue`、`Export confirmed memories`，点击 `Export selected context to out`。只在点击时生成本机 ZIP，位置为 `nativePC/plugins/CSharp/WhaleCompanion/out/`；每次独立命名，不覆盖旧包，不自动上传。ZIP 包含 `summary.md`（最近对话压缩摘要）、`context.json`（所选详细资料）和说明。可以把摘要交给你自己的“大肥鱼”，需要细节时再给 JSON。这是确定性整理与 ZIP 压缩，不调用 AI；观察记录保留历史与来源限制，不代表当前战况。近期聊天与观察受现有容量/时限限制，不是全部历史记录；已确认记忆仅导出确认文本，不导出原始引用、人设、API配置或密钥文件。常见凭据形态会脱敏，但压缩包仍含你选择的私人内容，请自行保管。`out` 已排除在 Git 和公开发布白名单之外。

`AI model and persona` 中的 `Reply language` 独立于界面语言；默认 `Follow persona / local user`，也可指定简体、繁体或英文。离线固定台词不会因这个选项自动翻译。

### 可选描边与脸部红润

外观候选仅在原170个网格之外追加88个独立轮廓层，原网格、UV、法线、骨架与权重按字节保留；不修改原VRM或贴图。原表情材质的双面兼容块保持不变。F9 → `Appearance` → `Optional model outline` 默认关闭，可在游戏中切换；轮廓跟随41级表情与 `Show head pot`，不会单独露出铁盆轮廓。额外网格增加渲染负担，实际原生阴影及性能仍需实机检查；离线预览不是游戏画面。

脸部采用独立材质暖红倍率，**不是精确饱和度滑条**，影响head材质覆盖区域（包括眼部），不修改身体或服装。提供 `original` 原色、`soft-rosy` 轻度红润、`rosy` 较明显红润。默认候选使用 `soft-rosy`。切换脸部预设需退出游戏，运行 `tools/Install-OptionalAppearance.ps1 -Tint original`（或 `soft-rosy` / `rosy`）再启动；此脚本需本机候选 `dist/OptionalAppearancePreview` 与匹配插件，尚非独立公开发行安装器。恢复本次认可的旧外观：退出游戏运行 `tools/Install-OptionalAppearance.ps1 -Restore`。备份位于 `backups/optional-appearance-preview`；未知的外部改动会拒绝覆盖。脚本不重置私人配置。


### 描边实测修订：原生路线研究

玩家实测旧轮廓壳开关没有可见效果，不能将离线预览视为游戏通过。旧开关已标为 `Legacy outline shell (no visible effect confirmed)`，保留对照用途；原VRM的MToon参数不会自动成为MHW材质功能。旧生成器将新增网格绘制相关字段19改为0，这一语义未经原生验证，是候选原因，不是已定位的结论。

新的研究路线为MHW的 `cpModelOutline` / `uOutlineFilter`。公开类型资料仅说明类型存在，没有证明常驻卡通描边可用。新版在首次启动后于后台自动生成一次 `config/research/native-outline-421810-v1.json`；也可 F9 → `Appearance` → `Capture native outline renderer (read only)` 重采。它只在421810版本通过OS保护读取主程序映像，按静态类名、DTI大小、getter指令和虚表关系定位，导出有限代码片段与RVA，不调用未知原生函数、不读取堆对象、不写内存、不增加hook、不读取聊天或密钥。扫描失败仅显示状态，不阻止游戏。该JSON仅供本机研究，不进入Git或发行包。此入口不产生任何新的描边效果；需要加载后的代码进一步确认渲染条件和控制字段。


原生轮廓场景采样：进入存档后自动每5秒观察一次，共6次，保存 `config/research/native-outline-scene-0.json` 至 `-5.json`。只读检查现有原生轮廓实例的目标ID、颜色和深度模式，以及猫是否挂有对应组件。通过SPL已缓存的单例地址和OS保护读取遍历有界unit列表，不调用实体GetDti、不创建后处理器或写字段。无需武器诊断或战斗动作；没有加载存档就保持等待。猫来源不作主人归属认定；这些诊断不等于描边效果已经实现。
