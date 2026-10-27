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
| Chat completions URL | 完整的OpenAI兼容Chat Completions接口URL，不是Responses接口；默认DeepSeek |
| External persona | 多行人设输入框；保存到 `config/persona.md` |
| Save model and persona | 校验后保存到本机，重新加载并清空当前对话；不删除长期记忆 |
| API key (optional replacement) | 密码遮罩输入；不会写进普通JSON |
| Save encrypted key | 用当前Windows用户DPAPI加密保存；其他Windows账号/设备不能直接使用该加密文件 |
| Send / Cancel / New conversation | 手动发送、取消当前请求、开始新对话；保存的跨对话记忆另行管理 |

修改模型也会使原生聊天判断使用同一模型；需要不同模型可手工编辑 `settings.json` 的 `NativeChatModel`。不同服务的兼容性与联网调用仍需实测；本轮没有代用户发送收费测试请求。

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

原VRM、身体、腿部、短裙和权重保持不变；可选描边与脸部饱和度尚未修改。来源和许可见 [THIRD-PARTY.md](THIRD-PARTY.md)。个人密钥、记忆、聊天和运行配置禁止加入发布包或Git。


## 开发候选更新与回滚

当前开发安装分为插件刷新和界面/英文工具补充，两步都要在退出游戏后执行：

```powershell
./tools/Refresh-DynamicCombatPreview.ps1
./tools/Install-ResponseInterfacePreview.ps1
```

前者更新DLL及动态战斗说明，后者更新界面说明/离线工具与缺失的英文编辑资源。补充安装不覆盖已有英文自定义文件，不复制当前开发配置，不更改简繁资源或语言选择文件。文件哈希与原始文件保存在 `backups`，不是普通玩家必须运行的构建过程。

回退补充文件：`./tools/Install-ResponseInterfacePreview.ps1 -Restore`；回退整个动态战斗阶段：`./tools/Install-DialogueLanguages.ps1 -Package DynamicCombatPreview -Restore`。这是回到原阶段基线；需要回到最近一次更新前DLL时，应从刷新脚本保存的中间版本恢复，不能把阶段基线称为最近一版。脚本遇到安装后被用户编辑的文件会拒绝覆盖，先保留编辑再处理。
