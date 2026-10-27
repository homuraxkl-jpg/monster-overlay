"""Cache the original Iceborne research workbook for local numeric research."""
from pathlib import Path
import re,json,urllib.request,concurrent.futures,html
root=Path(__file__).resolve().parents[1]/'research/weapons'
base='https://docs.google.com/spreadsheets/d/e/2PACX-1vTEYb4wGpijtIpFVopiYl1V83m48d7g1AHmTwOBKJ5RXdlz1sfxCyEmnhbgHLWQsGiXnodyBsUlPzc3'
text=(root/'mhwi-motion-sheet.html').read_text(encoding='utf-8-sig')
sheets=re.findall(r'items.push\(\{name: "([^"]+)".*?gid: "(\d+)"',text)
out=root/'motion-source';out.mkdir(exist_ok=True)
def fetch(pair):
    name,gid=pair
    if '(Old)' in name:return None
    name=name.replace('\\x26','&')
    url=base+'/pubhtml/sheet?headers=false&gid='+gid
    data=urllib.request.urlopen(url,timeout=40).read()
    (out/(gid+'.html')).write_bytes(data)
    return dict(name=name,gid=gid,url=url,bytes=len(data))
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:rows=[r for r in pool.map(fetch,sheets) if r]
(out/'index.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(rows,ensure_ascii=False))
