import lzma, hashlib
from pathlib import Path
p=Path('D:/homura/codex/MHW/tools/vendor/oodle9.lzma')
b=lzma.decompress(p.read_bytes())
assert hashlib.md5(b).hexdigest().upper()=='F2DB01967705B62AECEF3CD3E5A28E4D'
Path('D:/homura/codex/MHW/tools/ChunkExtract/oo2core_8_win64.dll').write_bytes(b)
print('OFFICIAL_DEPENDENCY_HASH_OK')
