# Extra authored VRM face shapes, using the exact same locked neutral transfer.
import sys
from pathlib import Path
root=Path('D:/homura/codex/MHW')
expression=sys.argv[sys.argv.index('--')+1]
assert expression in ('angry','sad','relaxed')
source=(root/'tools/build_face_blink.py').read_text(encoding='utf8')
source=source.replace("expression='blink'",f"expression='{expression}'").replace('face-blink',f'face-{expression}').replace('rig-prototype.json',f'face-{expression}-build.json')
exec(compile(source,str(root/'tools/build_face_blink.py'),'exec'))
