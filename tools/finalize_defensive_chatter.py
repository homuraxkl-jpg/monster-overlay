from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/WhaleCompanion/ActionCompanion.cs';s=p.read_text(encoding='utf-8')
s=s.replace('private double nextAllowed;','private double nextAllowed,nextSpinAllowed;')
s=s.replace('nextAllowed = 0; lastKind','nextAllowed = nextSpinAllowed = 0; lastKind')
s=s.replace(' || now < nextAllowed) return null;',') return null;')
needle='''        if (sample.Set != 3) return null;'''
assert s.count(needle)==1
s=s.replace(needle,needle+'''
        // Spin has its own cooldown so a recent power-charge comment does not swallow it.
        if(IncludeAllWeapons&&sample.Weapon=="Hammer"&&sample.Id==95)
        {
            if(now<nextSpinAllowed)return null;nextSpinAllowed=now+18;
            return new("hammer_spin","转圈圈，转圈圈！本鱼也要转晕啦！","relaxed",.5f,
                "玩家已进入大锤回转动作，跟着可爱地说转圈圈，一句就够。不评价命中、伤害或成功失败。");
        }
        if(now<nextAllowed)return null;''')
p.write_text(s,encoding='utf-8')
p=root/'src/WhaleCompanion/Class1.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('P11.5 feedback revision 2: phase-isolated damage, CB final branch, hammer FESTA7, foresight branch; tenderize observation only','P11.5 feedback revision 3: small Iai preparation, defensive success-only, safe-exit heuristic and hammer spin chatter')
p.write_text(s,encoding='utf-8')
p=root/'P11-5-DYNAMIC-COMBAT.md';s=p.read_text(encoding='utf-8-sig')
section='''
## 2026-10-25 太刀防御语义修订 3

用户实测盾斧、强化三蓄已正常，保留现有实现。新增大锤地面回转动作95的“转圈圈”陪伴台词，独立18秒冷却，避免刚说力量蓄力就吞掉回转回应，不按伤害评价回转。

小居合的官方名为居合拔刀斩，大居合为居合拔刀气刃斩（原生 weaponTutorial_chS）。小居合常用于命中后获取练气槽自动上升效果：已移出伤害成绩系统，只保留回气准备/衔接类陪伴；给AI明确禁用反击“大成功”的要求，不把动作本身当作已获得buff的证明。

见切保留现有直接接大回旋且有伤害的成功判据，不符合时静默，不再嘲笑空见切。大居合命中且不掉刃仍可夸奖；未观察到伤害、刃色未降、全段可用HP采样无下降时，生成独立 iai_safe_exit 候选，台词为安全脱身类陪伴。其他未成功大居合保持安静。

HP通过只读系统内存接口读取SPL Player.Health同一组件（player+7630 -> +64，MaxHealth+60）。初值缺失、中途读取缺失、HP下降后回满、刃色下降后恢复都不能拿来夸安全脱身。不读取或修改无敌/霸体标志，也未检测敌方攻击轨迹；空放、采样间隔内伤害与治疗抵消等仍可能符合启发式，不能宣称确定躲过怪物攻击。伤害显示候选仍有归属限制。

验证：241项武器/软化/防御语义检查通过，另验证表情与构造回归；这不是游戏内验收。安装保留修订2备份、玩家配置与模型。
'''
assert '太刀防御语义修订 3' not in s
p.write_text(s+section,encoding='utf-8')
for name in ['README.md','PROJECT-STATUS.md']:
    p=root/name;s=p.read_text(encoding='utf-8-sig');at=s.index('\n\n')
    p.write_text(s[:at]+'\n\nP11.5 修订3：盾斧/大锤已获玩家验证；太刀小居合改为回气衔接、见切仅夸成功、大居合新增不掉刃不掉血且无伤害的安全脱身候选，空刀不贬低；新增大锤转圈圈陪伴。241项离线武器检查通过，新规则待实机确认。'+s[at:],encoding='utf-8')
print('Finalized defensive chatter and revision 3 documentation.')
