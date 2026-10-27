from pathlib import Path
import json,hashlib,shutil
r=Path('.');stage=r/'dist/OptionalAppearancePreview';stage.mkdir(parents=True,exist_ok=True)
report=json.loads((r/'research/face/appearance-v1-materials.json').read_text(encoding='utf8'))
for name in ['otomo000.mod3','original.mrl3','soft-rosy.mrl3','rosy.mrl3']:shutil.copyfile(r/'assets/prototype/appearance-v1'/name,stage/name)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
manifest={'ModelHash':sha(stage/'otomo000.mod3'),'PluginHash':sha(r/'src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll'),'Materials':{name:row['sha256'] for name,row in report.items()}}
(stage/'appearance-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
