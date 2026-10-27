import bpy,sys,importlib,json,struct,numpy as np
from pathlib import Path
root=Path('D:/homura/codex/MHW');p=root/'tools/vendor/MHW_Model_Editor-main';sys.path.insert(0,str(p.parent))
import addon_utils
addon_utils.enable(p.name,default_set=True,persistent=False)
mod=importlib.import_module(p.name+'.addons.MHW_Model_Editor.modules.tex.file_tex')
b=(root/'蓝色小肥鱼.vrm').read_bytes();length=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+length]);binstart=20+length+8
out=root/'assets/prototype/textures';out.mkdir(parents=True,exist_ok=True)
report=[]
for i,img in enumerate(j['images']):
 view=j['bufferViews'][img['bufferView']];raw=b[binstart+view.get('byteOffset',0):binstart+view.get('byteOffset',0)+view['byteLength']]
 src=out/f'image_{i}.png';src.write_bytes(raw)
 image=bpy.data.images.load(str(src));image.colorspace_settings.name='Non-Color';w,h=image.size
 if max(w,h)>512:image.scale(max(1,int(w*512/max(w,h))),max(1,int(h*512/max(w,h))))
 w,h=image.size;pixels=np.empty(w*h*4,dtype=np.float32);image.pixels.foreach_get(pixels)
 # Image pixels are linear; convert RGB to sRGB before writing the color texture.
 pixels=pixels.reshape(h,w,4);rgb=pixels[:,:,:3];# Preserve stored color bytes; do not apply a second sRGB transfer.
 data=np.rint(np.clip(pixels[::-1],0,1)*255).astype(np.uint8).tobytes()
 tex=mod.MHWTex();tex.header.width=w;tex.header.height=h;tex.header.mipCount=1;tex.header.format=9;tex.mipOffsetList=[0xC0];tex.mipBuffer=bytearray(data)
 path=out/f'image_{i}.tex'
 with path.open('wb') as f:tex.write(f)
 test=mod.MHWTex()
 with path.open('rb') as f:test.read(f)
 assert test.header.width==w and test.header.height==h and bytes(test.mipBuffer)==data
 report.append({'index':i,'size':[w,h],'tex':str(path),'bytes':len(data),'roundtrip':True})
(root/'research/model/texture-build.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('TEXTURE_ROUNDTRIP_OK',len(report))

for name,rgba in {'flat_normal':(128,128,255,255),'flat_rmt':(200,0,0,0),'black':(0,0,0,0)}.items():
 tex=mod.MHWTex();tex.header.width=4;tex.header.height=4;tex.header.mipCount=1;tex.header.format=7;tex.mipOffsetList=[0xC0];tex.mipBuffer=bytearray(bytes(rgba)*16)
 with (out/(name+'.tex')).open('wb') as f:tex.write(f)

