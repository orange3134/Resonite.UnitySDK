"""Add audited membrane aliases to an Open Brush export manifest (new output only).

MatteMembrane, ShinyMembrane and DiamondMembrane use the exact normal m_Material assets of the
corresponding hull brushes. Keep existing hosted shader paths; only brush identity
changes. Install the result in Open Brush's Support folder before starting it.
"""
import argparse,copy,json,re
from pathlib import Path
ALIASES={
 '528fc9e8-5b95-48f3-b3f0-67ac5236f010':('MatteMembrane','79348357-432d-4746-8e29-0e25c112e3aa'),
 '63b09da0-a6a6-4f86-a48a-28cc53fd64de':('ShinyMembrane','faaa4d44-fcfb-4177-96be-753ac0421ba3'),
 '4f070b71-da25-48c4-9322-e9216d921562':('DiamondMembrane','c8313697-2563-47fc-832e-290f4c04b901'),
}
def extend(source,destination):
 source,destination=Path(source),Path(destination)
 if destination.exists():raise FileExistsError('Use a new output manifest')
 original_text=source.read_text(encoding='utf-8-sig')
 data=json.loads(original_text);brushes=data['brushes'];added=[];new_entries={}
 for guid,(name,base) in ALIASES.items():
  if guid in brushes:continue
  original=brushes[base]
  diamond=name=='DiamondMembrane'
  textures={'MainTex':'DiamondHull-c8313697-2563-47fc-832e-290f4c04b901-v10.0-MainTex.png'} if diamond else {}
  if original['guid']!=base or original['textures']!=textures or original['blendMode']!=(2 if diamond else 0) or original['enableCull']:
   raise ValueError('Audited source hull material changed: '+base)
  entry=copy.deepcopy(original);entry['guid']=guid;entry['name']=name;brushes[guid]=entry;new_entries[guid]=entry;added.append(name)
 # Preserve existing formatting and numeric literals; a material registration
 # must not create unrelated changes throughout the manifest.
 result=original_text
 if new_entries:
  ending=re.search(r'\n  }\n}\s*\Z',original_text)
  if list(data)[-1]!='brushes' or ending is None:raise ValueError('Unexpected manifest layout')
  entries=json.dumps(new_entries,indent=2,ensure_ascii=False).splitlines()[1:-1]
  result=original_text[:ending.start()]+',\n'+'\n'.join('  '+line for line in entries)+original_text[ending.start():]
 if json.loads(result)!=data:raise ValueError('Manifest insertion did not preserve content')
 destination.write_text(result,encoding='utf-8')
 return added
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('source');p.add_argument('destination');a=p.parse_args();print(extend(a.source,a.destination))
