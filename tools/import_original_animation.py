import bpy,sys,json
from pathlib import Path
root=Path('D:/homura/codex/MHW');vendor=root/'tools/vendor';sys.path.insert(0,str(vendor))
import addon_utils
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/whale-before-leg-segments.blend'))
addon_utils.enable('MHW-FreeKinetics-main',default_set=True,persistent=False)
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');bpy.context.view_layer.objects.active=arm
bpy.context.scene.freehk_tether=arm
f=root/'assets/original/chunkG0/otomo/common/mot/otomo_00/otomo_00.lmt'
result=bpy.ops.freehk.import_lmt(filepath=str(f),anim_skeleton=True)
assert 'FINISHED' in result
report=[{'name':a.name,'range':list(a.frame_range),'curves':len(a.fcurves)} for a in bpy.data.actions]
(root/'research/model/original-animation-index.json').write_text(json.dumps(report,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'assets/working/whale-original-animations.blend'))
print('ANIMATION_IMPORT_OK',len(report))
