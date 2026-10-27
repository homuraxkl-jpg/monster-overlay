import bpy,re,math
from mathutils import Vector
from pathlib import Path
r=Path('D:/homura/codex/MHW')
bpy.ops.wm.open_mainfile(filepath=str(r/'assets/working/whale-material-v1-lit-preview.blend'))
s=bpy.context.scene;s.cycles.samples=12;s.view_settings.exposure=-1.5
for group,label in [(56024,'sad'),(56040,'relaxed')]:
 for o in s.objects:
  if o.type=='MESH':
   m=re.search(r'Group_(\d+)',o.name)
   if m:o.hide_render=int(m.group(1)) not in (0,group)
 for angle,name in [(-28,'left'),(0,'front'),(28,'right')]:
  c=Vector((0,-.15,.44));a=math.radians(angle);s.camera.location=c+Vector((3*math.sin(a),-3*math.cos(a),.3));s.camera.rotation_euler=(c-s.camera.location).to_track_quat('-Z','Y').to_euler()
  s.render.filepath=str(r/f'research/face/material-v1-lit-{label}-{name}.png');bpy.ops.render.render(write_still=True)
