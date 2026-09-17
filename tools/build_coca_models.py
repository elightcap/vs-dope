"""Deterministic coca shrub shapes. Run from any directory; no third-party dependencies."""
import json, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SHAPES=ROOT/'assets/vs-dope/shapes/plant'
TEXTURES={k:f'block/coca/coca-{k}-v4' for k in ('stem','leaf','flower','berry')}
def add(a,b): return tuple(x+y for x,y in zip(a,b))
def mul(a,s): return tuple(x*s for x in a)
def direction(yaw,pitch):
 a,b=map(math.radians,(yaw,pitch));return (math.cos(a)*math.cos(b),math.sin(b),math.sin(a)*math.cos(b))
def segment(es,name,start,length,width,yaw,pitch,texture='stem',blade=False):
 x,y,z=start
 vertical=blade and texture=='leaf' and sum(map(ord,name))%3!=0
 faces={f:{'texture':'#'+texture,'uv':[0,0,32,32]} for f in (('north','south') if vertical else ('up','down') if blade else ('north','south','east','west','up','down'))}
 e={'name':name,'from':[x,y-(.008 if blade else width/2),z-width/2], 'to':[x+length,y+(.008 if blade else width/2),z+width/2], 'rotationOrigin':list(start),'rotationY':-yaw,'rotationZ':pitch,'faces':faces}
 if vertical:
  e['from']=[x,y-width/2,z-.008];e['to']=[x+length,y+width/2,z+.008]
 es.append(e);return add(start,mul(direction(yaw,pitch),length))
def leaf(es,name,node,yaw,pitch,length):
 tip=segment(es,name+'-petiole',node,.32,.035,yaw,pitch)
 # Texture's first opaque pixel is inset: extend the petiole under the blade.
 segment(es,name+'-blade',add(tip,mul(direction(yaw,pitch),-.22)),length,length*.58,yaw,pitch,'leaf',True)
def model(stage):
 es=[];scale=[0,.23,.32,.43,.55,.67,.80,.88,.94,1][stage]
 # A continuous, slightly bent, tapered leader. All growth stages share its structure.
 leader=[(8,0,8),(7.9,3,8.05),(8.12,6,8.12),(7.95,9,8.20),(8.25,12,8.12),(8.15,13.9,8.22)]
 leader=[(8+(x-8)*scale,y*scale,8+(z-8)*scale) for x,y,z in leader]
 for i,(a,b) in enumerate(zip(leader,leader[1:])):
  dx,dy,dz=[b[j]-a[j] for j in range(3)];length=math.sqrt(dx*dx+dy*dy+dz*dz)
  segment(es,f'leader-{i}',a,length,(.22-i*.032)*scale,math.degrees(math.atan2(dz,dx)),math.degrees(math.atan2(dy,math.hypot(dx,dz))))
 def on_leader(height):
  for a,b in zip(leader,leader[1:]):
   if a[1]<=height<=b[1]:return add(a,mul(tuple(b[j]-a[j] for j in range(3)),(height-a[1])/(b[1]-a[1])))
 if stage<=3:
  for i in range([0,4,7,11][stage]):
   n=[0,4,7,11][stage];h=scale*(3+i*10/n)
   leaf(es,f'young-{i}',on_leader(h),i*137.5,15+(i%3)*12,.85+scale*1.4)
 else:
  count={4:5,5:7,6:9,7:10,8:11,9:12}[stage]
  for i in range(count):
   h=3.0+i*10/(count-1);yaw=22+i*137.508
   start=on_leader(h*scale);length=(4.6-.075*(h-6)**2)*scale
   pitch=19+(i%3)*5;axis=direction(yaw,pitch)
   middle=segment(es,f'branch-{i}-base',start,length*.56,.12*scale,yaw,pitch)
   segment(es,f'branch-{i}-tip',middle,length*.44,.075*scale,yaw+9,pitch+9)
   for j,t in enumerate((.27,.48,.68,.87)):
    # Two fine twigs per main branch, plus alternating leaves along its axis.
    node=add(start,mul(axis,length*t)) if t<=.56 else add(middle,mul(direction(yaw+9,pitch+9),length*(t-.56)))
    side=1 if j%2==0 else -1
    leaf(es,f'branch-{i}-leaf-{j}',node,yaw+side*61,8+(j%3)*12,(2.05+.14*(i%3))*scale)
    if j in (1,2):
     tyaw=yaw+side*48;tpitch=23+j*3;tl=(1.20+.12*(i%3))*scale
     segment(es,f'twig-{i}-{j}',node,tl,.05*scale,tyaw,tpitch)
     for k,t2 in enumerate((.28,.62,.96)):
      ln=add(node,mul(direction(tyaw,tpitch),tl*t2))
      leaf(es,f'twig-{i}-{j}-leaf-{k}',ln,tyaw+(-1 if k%2 else 1)*53,12+k*8,(1.90+.10*(i%2))*scale)
   if stage>=7 and i%3==0:
    node=add(start,mul(axis,length*.48))
    end=segment(es,f'flower-stalk-{i}',node,.24,.025,yaw,65)
    segment(es,f'flower-{i}',end,.30,.30,yaw,35,'flower',True)
   if stage>=8 and i%2==0:
    node=add(middle,mul(direction(yaw+9,pitch+9),length*.12))
    for k in range(2 if stage==9 else 1):
     end=segment(es,f'fruit-stalk-{i}-{k}',node,.25+k*.09,.025,yaw+k*35,-65)
     segment(es,f'fruit-{i}-{k}',end,.37,.23,yaw+k*35,-75,'berry',True)
 for i in range(3):leaf(es,f'crown-{i}',leader[-1],i*120+20,35+i*12,1.1*scale)
 # Keep readable numeric output and stable diffs.
 def rounded(x):
  if isinstance(x,float):return round(x,5)
  if isinstance(x,list):return [rounded(v) for v in x]
  if isinstance(x,dict):return {k:rounded(v) for k,v in x.items()}
  return x
 return rounded({'editor':{'allAngles':True},'textureWidth':32,'textureHeight':32,'textures':TEXTURES,'elements':es})
if __name__=='__main__':
 for stage in range(1,10):
  p=next(SHAPES.glob(f'coca_stage_{stage:02}_*.json'));d=model(stage)
  p.write_text(json.dumps(d,indent=2)+'\n')
  print(stage,len(d['elements']),sum(e['name'].endswith('-blade') for e in d['elements']))
