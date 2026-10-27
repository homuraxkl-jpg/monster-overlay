from pathlib import Path
import json,hashlib,shutil
root=Path(__file__).resolve().parents[1]
section='''
## 2026-10-25 实机反馈修订 2

本轮依据 17:25—17:34 测试日志修正，尚待新版本实机确认；未提及的问题沿用已有结果，不要求重测全部武器。

- 登龙：下劈动作 91 开始重新取伤害基线，排除起手刺击。离开招式阶段就封存累计值，后续攻击不能挽救这次空登龙。
- 居合：动作 101 的确在日志中出现在特殊纳刀 98/99 之后；旧版三秒窗口和回复延后容易让评价看起来对应后面的气刃连段。现在离开居合即结算，不累计后续招式。小居合只评价伤害；大居合仍要求刃色不降与伤害。
- 盾斧：119/120 是可转分支，出现 118 后改按高解结算；109 SHIELD_BUFF 和 148 分支取消攻击评价。普通剑连段不继承未完成超解窗口。
- 大锤：补入日志反复出现的 131 FESTA7 强化三蓄。敲打终结仍保留，不要求在训练场强行打空连段。
- 见切：旧版只有动作陪伴。本版增加动作 86 直接接 69 大回旋、且见切窗口有伤害的宽松成功判据；不是精确回避帧接口。未接大回旋不能据此宣称见切成功，可能漏报不接派生的成功见切。
- 软化：新增 HunterPie 部位软化计时读取（monster+0x1C458，10 条、64 字节步长，计时+8、最大计时+12、部位+48）。仅记录 TENDERIZE_TRANSITION，不触发 AI，不从“已软化部位伤害”标志倒推主人完成软化。原生通知的发送者/归属仍未确认；首次读到已有伤口保持静默。
- 退出确认：本次插件日志在正常采样中结束，没有异常或原生退出原因；加载器日志无对应异常，当前可读取的 Windows 应用事件未找到对应条目。源码未找到退出/结束进程或修改退出确认的调用，但原生 hook 导致的问题不一定有托管异常，因此不能据此排除模组影响。新增 PLUGIN_UNLOAD_BEGIN 记录供以后定位，不声称已修复退出问题。

离线验证：228 项武器/软化检查、3698 项表情断言、空 Location 构造烟雾检查通过；这些不等于游戏内验证。仍使用基础装备动态估计与候选伤害来源，随从/环境混入的边界保持公开。

软化数据参考：[HunterPie MHWMonster](https://github.com/HunterPie/HunterPie/blob/main/HunterPie.Integrations/Datasources/MonsterHunterWorld/Entity/Enemy/MHWMonster.cs) 与同目录 Definitions/MHWTenderizeInfoStructure.cs，Apache-2.0；没有复制其整个程序或注入额外伤害 hook。
'''
p=root/'P11-5-DYNAMIC-COMBAT.md'
s=p.read_text(encoding='utf-8-sig')
assert '## 2026-10-25 实机反馈修订 2' not in s
s=s.replace('窗口内的后续输出可能混入。','本次修订在动作阶段结束时封存计数，仍不能保证排除同阶段内的随从/环境伤害。')
p.write_text(s+section,encoding='utf-8')
for name in ['PROJECT-STATUS.md','README.md']:
    p=root/name;s=p.read_text(encoding='utf-8-sig')
    marker='\n\n'
    first=s.index(marker)
    note='\n\n2026-10-25 P11.5 反馈修订 2：修正登龙起手/后续连段伤害混入、盾斧取消分支与强化三蓄遗漏；补实用见切判据，软化暂只读记录。228 项武器检查、3698 项表情断言通过，游戏内结果待确认。退出确认问题尚不能归因。详见 [P11.5](P11-5-DYNAMIC-COMBAT.md)。'
    p.write_text(s[:first]+note+s[first:],encoding='utf-8')
stage=root/'dist/DynamicCombatPreview'
for name,source in [('WhaleCompanion.dll',root/'src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll'),('P11-5-DYNAMIC-COMBAT.md',root/'P11-5-DYNAMIC-COMBAT.md'),('THIRD-PARTY.md',root/'THIRD-PARTY.md')]:
    shutil.copyfile(source,stage/name)
manifest=stage/'upgrade-manifest.json';rows=json.loads(manifest.read_text(encoding='utf-8-sig'))
for row in rows:row['NewHash']=hashlib.sha256((stage/row['Source']).read_bytes()).hexdigest().upper()
manifest.write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print('Staged feedback revision 2; existing original hashes preserved for installer rollback.')
