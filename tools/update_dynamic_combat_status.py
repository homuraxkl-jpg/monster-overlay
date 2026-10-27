from pathlib import Path
import json,hashlib,datetime
root=Path(__file__).resolve().parents[1]
def replace_paragraph(name,prefix,new):
    p=root/name;s=p.read_text(encoding='utf-8-sig');lines=s.splitlines()
    hits=[i for i,line in enumerate(lines) if line.startswith(prefix)]
    assert len(hits)==1,(name,prefix)
    lines[hits[0]]=new;p.write_text('\n'.join(lines)+'\n',encoding='utf-8-sig')
replace_paragraph('README.md','当前版本：','当前版本：P11.5。已安装14武器重点招式的动态伤害估算与资源判据，默认关闭。装备基础攻击/属性、招式倍率、瓶型/瓶数等参与门槛，不再统一400。居合气刃结合出刀前后刃色与伤害，登龙看下劈，零解区分完整终结。单人/训练场提供受保护的显示伤害候选备用，联机优先主人席位统计。212项专项、3698项回归、构造与安装/回滚检查通过；不是14武器实机验收。现在集中验证太刀、盾斧、大锤、斩斧，再整理公开测试包。详见[本轮完整说明与测试表](P11-5-DYNAMIC-COMBAT.md)。')
p=root/'PROJECT-STATUS.md';s=p.read_text(encoding='utf-8-sig');lines=s.splitlines();lines[0]='# 项目状态（2026-10-25，P11.5）';s='\n'.join(lines)+'\n'
paragraph='P11.5已安装：3704条有效武器基础数据、657条攻击/弹药参考记录，14武器重点动作规则；动态门槛随装备基础攻击/属性、招式倍率与资源变化；居合不降刃、登龙下劈、零解终结、JR节奏/末段、弹药/瓶/炮弹消耗与实际增益判据。单人/训练场按需启用受版本/签名/RVA保护的显示伤害候选，联机读主人席位累计；候选可能混入随从或环境，不是精确逐击归属。212项专项、3698项回归、构造及假目录安装/更新/回滚通过；最终实际文件哈希、已验收模型/材质哈希核对通过。运行时仍待集中四武器验收，其他武器按未实机验证保留。当前攻击buff、客制和技能未完整建模；不是精确面板模拟器。密钥位置已便携化，旧加密文件仅本地迁移、不打包；提供不含聊天/密钥的主动战斗摘要导出。详见P11-5-DYNAMIC-COMBAT.md。'
if 'P11.5已安装：' not in s:s=s.replace('\n\n','\n\n'+paragraph+'\n\n',1)
p.write_text(s,encoding='utf-8-sig')
p=root/'ROADMAP.md';s=p.read_text(encoding='utf-8-sig')
paragraph='最新P11.5：动态装备/招式估算、重点资源与动作终结判据、单人显示候选备用及联机个人统计已安装。212专项、3698回归、构造和安装/更新/回滚验证通过。下一步只集中验收用户会的太刀/盾斧/大锤/斩斧，修复后整理干净公开测试包、确认发布位置并发布，其余武器标记未实机验证并接受自愿反馈。不再为精确GP窗口或逐击归属停住陪伴功能；仍不把所有倍率记录冒称都已映射/实测。'
if '最新P11.5：' not in s:s=s.replace('\n\n','\n\n'+paragraph+'\n\n',1)
p.write_text(s,encoding='utf-8-sig')
stage=root/'dist/DynamicCombatPreview'
files=[dict(name=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper()) for p in sorted(stage.iterdir()) if p.is_file()]
report=dict(phase='P11.5',date_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),weapon_checks=212,regression_assertions=3698,constructor_smoke=True,build_warnings=0,build_errors=0,paid_api_calls=0,gameplay_validated=False,installed=True,default_enabled=False,model_and_material_hashes_retained=True,portable_secret_path=True,private_configuration_packaged=False,files=files,installation_checks=['fake install/rollback','fake refresh with original baseline retained','idempotence and external-edit refusal','actual install file hashes'],remaining=['four-weapon user acceptance','other weapons remain experimental','complete current-buff/augment/skill panel simulation not provided','clean public bundle and destination after acceptance'])
(root/'research/weapons/DYNAMIC-COMBAT-VALIDATION.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Updated README, status, roadmap and validation evidence; no game or private data read.')
