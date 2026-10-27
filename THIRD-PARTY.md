# 第三方来源与工具记录

- SharpPluginLoader：Fexty12573，https://github.com/Fexty12573/SharpPluginLoader ，现用插件框架。
- 武器动作表：AniBullet Wiki，数据提取者Fexty，https://github.com/AniBullet/MonsterHunterWorldModding/wiki/Player-Weapon-Action-IDs ，研究数据；未据此假定任意数据可自由再分发。
- MHW Model Editor：chikichikibangbang，基于NSACloud/RE-Mesh-Editor，https://github.com/chikichikibangbang/MHW_Model_Editor ，GPL-3.0-or-later（见下载源码LICENSE.GPL与manifest）。源码zip及解压目录位于tools/vendor，许可证保留。当前下载评估，尚未安装/执行该插件。
- VRM Add-on for Blender：saturday06，https://github.com/saturday06/VRM-Addon-for-Blender ，候选VRM专用工具，仓库含MIT与GPL许可，具体使用组件需核对文件级许可；尚未下载。
- MHW-FreeKinetics：Dimcirui移植，原作者AsteriskAmpersand，https://github.com/Dimcirui/MHW-FreeKinetics ，候选动画工具；尚未下载及核实全文许可，尚未复用代码。要求Blender4.3/4.4，不支持5.x。

工具用于资产生产与把工具代码编入插件是不同情形；实际发布时按使用内容保留所需许可证与版权声明，不能只写作者名字替代许可要求。

- JodoZT/MHWNoChunk：https://github.com/JodoZT/MHWNoChunk ，MIT / Copyright (c) 2020 Jodo；复用Chunk.cs/FileNode.cs/Utils.cs并新增CLI适配层，上游源码及LICENSE位于tools/vendor/MHWNoChunk-master。Chunk代码同时注明参考MHVuze WorldChunkTool，后续分发工具需保留相关通知。
- Blender4.3.2官方便携版：https://download.blender.org/release/Blender4.3/blender-4.3.2-windows-x64.zip ，独立工具环境，不随游戏模组发布。

- FreeKinetics已本地使用导入原版LMT，GPLv3，源目录tools/vendor/MHW-FreeKinetics-main保留LICENSE；添加了headless空context.area回调保护。用于离线动画验证，不编入游戏插件。

## 2026-10-24 表情执行实验来源
- Mi1ice/MHWI-VisualControllerExtended，https://github.com/Mi1ice/MHWI-VisualControllerExtended ，研究固定到 a05ad4755ee89e80eab4dd28294d2a885402255a。实际git记录为 tools/vendor/MHWI-VisualControllerExtended HEAD。复用/适配其四寄存器查询ABI、queryObject+4组号读取及15.23.00签名字节；项目MIT许可，全文随插件保存在 LICENSE.VisualControllerExtended.txt。没有链接其SafetyHook、Zydis或状态规则实现；执行hook使用SPL自身后端。
- Dimcirui/Modding-Toolkit，https://github.com/Dimcirui/Modding-Toolkit ，57ac77dd3c701190bd4a4efce81a4d3b46960a95，GPLv3。只阅读骨骼和权重转换策略，未把其代码或猎人参考资产编入插件。
- SharpPluginLoader源码研究版本 c16380a398c3252309ba94d1c154ad58f6ab5413；游戏插件依赖仍为Core2.0.0。没有更换已安装加载器。

- eigeen/more-auto-shoutouts，https://github.com/eigeen/more-auto-shoutouts ，e433fcac2ac380d278f921d0baa5a1105ea512b0；eigeen/mhw-toolkit，https://github.com/eigeen/mhw-toolkit ，0a593484976e58358bbb801a821af2f99ed0037e。均Apache-2.0，源码和LICENSE保留在tools/vendor对应目录。本轮仅研究状态字段/事件，未编译、安装或将实现复制进插件。

- P8.1药瓶候选ID来源：Ezekial711/MonsterHunterWorldModding Wiki，Accessory-and-Specialized-Tool-IDs，原表致谢Deported、Jodo、Ezekial。https://github.com/Ezekial711/MonsterHunterWorldModding/wiki/Accessory-and-Specialized-Tool-IDs 。仅作ID研究。铁盆使用既有获授权VRM的TiePen形态，米饭几何为本项目程序生成；原药瓶骨骼来自用户本地游戏提取原件。


## P10.0 状态字段参考

P11.4累计伤害：参考HunterPie既有421810地图与MHWPlayer.GetParty，以及27-exe/monster-overlay的World readParty和存档名读取；后者固定提交c482f3309f59bee03e59cd3a6eca1320a12e8098，https://github.com/27-exe/monster-overlay ，Apache-2.0，Copyright 2024 a27exe。采用本项目独立C#只读适配，不引入覆盖层或Rise的攻击者Hook。许可与上游通知随包为LICENSE.MonsterOverlay.txt、NOTICE.MonsterOverlay.txt；未使用其图标、字体或UI资产。名称相同存在歧义时由玩家选择席位。

P11.1扩展：HunterPie/HunterPie-legacy，https://github.com/HunterPie/HunterPie-legacy ，固定提交612ef2bc4b29537acff41eca8bd9daf4484eb1de，MIT，Copyright (c) 2020 Haato。参考HunterPie.Core/Core/Local/Player.cs的Get*Information和Definitions中的弹药/音符队列布局；采用独立C#只读适配，不安装旧版覆盖层或其Native DLL。完整许可随包为LICENSE.HunterPieLegacy.txt。原始动作目录沿用research/actions/README.md注明的Fexty提取来源；动作名是候选映射，不冒充伤害attackId。伤害九参数ABI和签名参考MAS/mhw-toolkit，Apache许可随包；不复制其阻塞通道或来源推断。

P11.0-A：HunterPie/HunterPie，https://github.com/HunterPie/HunterPie ，固定提交ef654889658684848cb465176b676b9b553ea102，Apache-2.0。参考World的MHWDualBladesStructure、MHWChargeBladeStructure、MHWSwitchAxeStructure、MHWInsectGlaiveStructure及421810映射。以独立C#安全读取实现适配结构字段，不链接HunterPie或启用其Hook；许可证随包为LICENSE.HunterPie.txt，原源码/版权声明保留tools/vendor/HunterPie-research。
读取链参考more-auto-shoutouts e433fcac2ac380d278f921d0baa5a1105ea512b0（src/game.rs）及mhw-toolkit 0a593484976e58358bbb801a821af2f99ed0037e（utils/util.rs、game_export.rs）。均Apache-2.0。现以自身C#代码/Windows只读API适配数值字段，不链接Rust代码；许可随附LICENSE.MoreAutoShoutouts.txt。实机语义尚待验证。

P10.5离线GMD格式参考：Fusion86/Cirilla，MIT，Copyright (c) 2018 Wouter Visser。固定提交a1e065435e11b0fd102ad0395c1c63a1c86b04af；源码和LICENSE.md保留在tools/vendor/Cirilla；只读研究工具inspect_palico_dialogue.py采用自己的解析实现，不链接或分发Cirilla二进制及游戏GMD。原生显示使用既有SPL Gui.DisplayMessage；mhw-toolkit game_util.rs仅用于核对本地显示和网络发送分离，未复制或调用其网络发送代码。

P10.5-D正常聊天输入发送布局参考：eigeen/mhw-toolkit，固定提交0a593484976e58358bbb801a821af2f99ed0037e，Apache-2.0，game_util.rs SendChatMessage与game_export.rs GUI偏移。采用自己的C#适配并验证SPL对象关联，不复制固定全局地址或构造未验证的网络包。完整LICENSE随包为LICENSE.MhwToolkit.txt，原源码和许可保留tools/vendor/mhw-toolkit。


P11.5动态战斗数字目录：本机游戏wp_dat/wp_dat_g最小攻击/属性元数据；二进制字段格式参考Synthlight/MHW-Editor（https://github.com/Synthlight/MHW-Editor），独立解析，不分发其编辑器或游戏原表。攻击数值参考MoonBunnie与Deathcream的MHWI Weapon Attack Tables（Fatalis v15.01.00），https://docs.google.com/spreadsheets/d/e/2PACX-1vTEYb4wGpijtIpFVopiYl1V83m48d7g1AHmTwOBKJ5RXdlz1sfxCyEmnhbgHLWQsGiXnodyBsUlPzc3/pubhtml 。只纳入数值事实及招式标签，不复制整张表HTML或说明文字。装备行号使用既有HunterPie/monster-overlay候选字段。铳枪追加等级编码参考原作者测试 https://github.com/Synthlight/MHW-Editor/issues/19 并与MHW数据库炮击字段交叉核对。来源哈希与版本保留research/weapons/COMBAT-CATALOG-MANIFEST.json；运行时规则为本项目实现，不宣称这些上游提供了本插件的GP/命中接口。

P11.5单人备用：复用既有MAS/mhw-toolkit九参数显示观察器与HunterPie World按local计数的路线，只在明确单人时累计候选，来源仍标为display_local_candidate，不宣称逐击归属已验证。未运行或安装任何CE表脚本。

2026-10-27怪物状态诊断：部位布局参考HunterPie World MHWMonster.GetMonsterParts / MHWMonsterPartStructure，monster-break-thresholds.json仅保留MonsterData.xml的怪物ID、普通部位序号/ID与数字破坏阈值事实；不含文本、图像或完整XML。来源与现有HunterPie Apache-2.0许可相同，尾部切断路线未纳入。

2026-10-27整场怪物状态诊断：异常表布局、状态编号和当前动作读取参考既有HunterPie World GetMonsterAilments/MHWMonsterAilmentStructure/MonsterData.xml，HP组件参考既有SharpPluginLoader Monster布局。独立OS只读适配，不分发原始日志或采样，不将倒地动作当作撞墙成功。


独立插件候选包附带 System.Security.Cryptography.ProtectedData 8.0.0（.NET Foundation and Contributors，MIT），配套 LICENSE.ProtectedData.txt 与 NOTICE.ProtectedData.txt 从本机构建所用NuGet包提取；不包含用户DPAPI加密密钥文件。


2026-10-27原生聊天就绪检测：沿用上述MAS/mhw-toolkit World 421810 PLAYER_BASE(+0x50139A0)及+0x50玩家链参考，以本项目保护性OS读取实现；已采样玩家作为备用，不在菜单中新增FindMasterPlayer/GetDti原生调用。近期对话/狩猎上下文与计数概览为本项目实现，未引入第三方记忆插件或新增外部服务。
