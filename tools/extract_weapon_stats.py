from pathlib import Path
import subprocess,json
root=Path(__file__).resolve().parents[1]
chunks=Path('D:/steam/steamapps/common/Monster Hunter World/chunk')
out=root/'research/weapons/equipment-source';out.mkdir(exist_ok=True)
report=[]
for chunk in sorted(chunks.glob('chunkG*.bin'),key=lambda p:int(p.stem[6:])):
    folder=out/chunk.stem
    result=subprocess.run(['dotnet',str(root/'tools/ChunkExtract/bin/Release/net8.0/ChunkExtract.dll'),str(chunk),str(folder),'--prefix=common/equip/','--extensions=.wp_dat,.wp_dat_g'],capture_output=True,text=True)
    (out/(chunk.stem+'.log')).write_text(result.stdout+result.stderr,encoding='utf-8')
    assert result.returncode==0,chunk
    files=list(folder.rglob('*.wp_dat'))+list(folder.rglob('*.wp_dat_g'))
    report.append(dict(chunk=chunk.name,files=[str(p.relative_to(folder)) for p in files]))
    print(chunk.name,len(files),flush=True)
(out/'index.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
