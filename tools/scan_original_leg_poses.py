import bpy,sys,json,math
from pathlib import Path
root=Path('D:/homura/codex/MHW');sys.path.insert(0,str(root/'tools/vendor'))
import addon_utils
addon_utils.enable('MHW-FreeKinetics-main',default_set=True,persistent=False)
bpy.ops.wm.open_mainfile(filepath=str(root/'assets/working/whale-original-animations.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');arm.animation_data_create()
print('ACTION_PATHS',[(a.name,a.fcurves[0].data_path) for a in bpy.data.actions if a.name.startswith('LMT')][:3],flush=True)
report=[]
for action in list(bpy.data.actions):
 if not action.name.startswith('LMT::'):continue
 arm.animation_data.action=action
 for t in [action.frame_range[0],sum(action.frame_range)/2,action.frame_range[1]]:
  bpy.context.scene.frame_set(int(t));bpy.context.view_layer.update()
  positions={name:list((arm.matrix_world@arm.pose.bones[name].matrix).translation) for name in ['MhBone_013','MhBone_014','MhBone_015','MhBone_016','MhBone_017']}
  points=[arm.pose.bones[name].matrix.translation for name in ['MhBone_014','MhBone_015','MhBone_016','MhBone_017']]
  lengths=[(points[i+1]-points[i]).length for i in range(3)]
  report.append({'action':action.name,'frame':t,'positions':positions,'segment_lengths':lengths,'leg_span':(points[-1]-points[0]).length})
report.sort(key=lambda r:r['leg_span'],reverse=True)
(root/'research/model/original-leg-poses.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('POSE_SCAN_OK',len(report),report[:2],flush=True)
