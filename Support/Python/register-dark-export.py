"""Register Dark's raw geometry for the Resonite GLB2 bridge; new output only.

Legacy glTF blend enums cannot express multiplication. The retained UnlitHull
GLSL fallback is only a transport placeholder, NOT a faithful Dark web shader.
The customized SDK explicitly reads the Dark GUID and restores its shader math.
"""
import copy,json,re,sys
from pathlib import Path
source,destination=map(Path,sys.argv[1:])
if destination.exists():raise FileExistsError(destination)
text=source.read_text(encoding='utf-8-sig');data=json.loads(text)
guid='61820308-bae0-48d4-8288-a7d4e6734d2f'
if guid in data['brushes']:raise ValueError('Dark already registered; inspect before changing it')
entry=copy.deepcopy(data['brushes']['a8fea537-da7c-4d4b-817f-24f074725d6d'])
entry.update(guid=guid,name='Dark',blendMode=3,enableCull=False,textures={},textureSizes={},
             floatParams={'_Opacity':1,'_AlphaMultiply':.33,'_SoftMultiply':1,'_EdgeFadeoff':.157})
data['brushes'][guid]=entry
end=re.search(r'\n  }\n}\s*\Z',text)
if end is None:raise ValueError('Unexpected manifest layout')
lines=json.dumps({guid:entry},indent=2).splitlines()[1:-1]
result=text[:end.start()]+',\n'+'\n'.join('  '+line for line in lines)+text[end.start():]
assert json.loads(result)==data
destination.write_text(result,encoding='utf-8')
print(destination)
