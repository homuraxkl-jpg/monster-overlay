import bpy,sys,importlib,json
from pathlib import Path
root=Path('D:/homura/codex/MHW');p=root/'tools/vendor/MHW_Model_Editor-main';sys.path.insert(0,str(p.parent))
import addon_utils
addon_utils.enable(p.name,default_set=True,persistent=False)
mod=importlib.import_module(p.name+'.addons.MHW_Model_Editor.modules.mrl3.file_mrl3')
report={}
for name,path in [('cat','otomo/ot000/mod/otomo000/otomo000.mrl3'),('armor','otomo/equip/ot001/body/mod/ot_body001.mrl3')]:
 m=mod.readMrl3File(str(root/'assets/original/chunkG0'/path));m.parser()
 report[name]={'textures':m.textureList,'materials':[{'info':vars(mat.matInfo),'resources':mat.resourceDict} for mat in m.materialList]}
(root/'research/model/material-analysis.json').write_text(json.dumps(report,ensure_ascii=False,indent=2,default=str),encoding='utf8')
print('MATERIAL_ANALYSIS_OK')
