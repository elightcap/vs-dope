"""Editable connected voxel flora and item meshes; original ImageGen atlas kept separately."""
import json, math
from pathlib import Path
from build_coca_models import add, mul, direction
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'assets/vs-dope'
UV={'leaf':[0,0,32,32], 'bud':[32,0,64,32], 'paper':[0,32,32,64], 'stem':[32,32,64,64]}
TEXTURES={k:'block/cannabis/cannabis-atlas' for k in UV}
NAMES=['sprout','seedling','young','established','branching','vegetative','early_flower','budding','harvestable']
def box(es,name,lo,hi,tex,faces=None,origin=None,yaw=0,pitch=0):
 e={'name':name,'from':list(lo),'to':list(hi),'faces':{f:{'texture':'#'+tex,'uv':UV[tex]} for f in (faces or ['north','south','east','west','up','down'])}}
 if origin is not None:e.update(rotationOrigin=list(origin),rotationY=-yaw,rotationZ=pitch)
 es.append(e)
def rod(es,name,a,b,w,tex='stem'):
 dx,dy,dz=[b[j]-a[j] for j in range(3)];l=math.sqrt(dx*dx+dy*dy+dz*dz)
 box(es,name,(a[0],a[1]-w/2,a[2]-w/2),(a[0]+l,a[1]+w/2,a[2]+w/2),tex,origin=a,yaw=math.degrees(math.atan2(dz,dx)),pitch=math.degrees(math.atan2(dy,math.hypot(dx,dz))))
def blade(es,name,node,yaw,pitch,length,vertical=False):
 # Five connected steps create a narrow pointed, toothed leaflet silhouette.
 widths=[.14,.30,.23,.25,.10]
 for j,w in enumerate(widths):
  p=add(node,mul(direction(yaw,pitch),length*j/5));x,y,z=p
  if vertical:
   box(es,f'{name}-{j}',(x,y-w*length/2,z-.012),(x+length/5+.008,y+w*length/2,z+.012),'leaf',origin=p,yaw=yaw,pitch=pitch)
  else:
   box(es,f'{name}-{j}',(x,y-.012,z-w*length/2),(x+length/5+.008,y+.012,z+w*length/2),'leaf',origin=p,yaw=yaw,pitch=pitch)
def fan(es,name,node,yaw,pitch,length,count=7):
 hub=add(node,mul(direction(yaw,pitch),length*.22))
 rod(es,name+'-petiole',node,hub,.038)
 for j in range(count):
  angle=(j-(count-1)/2)*25
  # Leaflets all start at the same hub; no disconnected/floating leaves.
  vertical=sum(map(ord,name))%3!=0
  blade(es,f'{name}-finger-{j}',hub,yaw if vertical else yaw+angle,40+angle if vertical else pitch+abs(angle)*.08,length*(1-abs(angle)/140),vertical)
def bud(es,name,p,r,h):
 x,y,z=p
 for j,(low,high,scale) in enumerate([(0,.20,.60),(.16,.45,.90),(.4,.70,1),(.65,.86,.80),(.82,1,.45)]):
  for k,(sx,sz) in enumerate([(1,.60),(.60,1)]):
   box(es,f'{name}-{j}-{k}',(x-r*scale*sx,y+h*low,z-r*scale*sz),(x+r*scale*sx,y+h*high,z+r*scale*sz),'bud')
 # Short sugar leaves emerge from the bud volume.
 for j in range(3):blade(es,f'{name}-sugar-{j}',(x,y+h*.45,z),j*120+25,20,r*1.6)
def shape(es):
 def clean(v):
  if isinstance(v,float):return round(v,5)
  if isinstance(v,list):return [clean(x) for x in v]
  if isinstance(v,dict):return {k:clean(x) for k,x in v.items()}
  return v
 return clean({'textureWidth':64,'textureHeight':64,'textures':TEXTURES,'elements':es})
def model(stage):
 es=[];h=[0,2.0,3.0,4.2,5.7,7,9.4,11.4,12.8,13.5][stage]
 def at(t):return (8+.15*math.sin(t*5),h*t,8+.10*t)
 for i in range(5):rod(es,f'leader-{i}',at(i/5),at((i+1)/5),.13+.02*stage-i*.026)
 for i in range(2):blade(es,f'cotyledon-{i}',at(.65 if stage<4 else .07),i*180+25,8,.65 if stage<3 else .85)
 if stage==1:return shape(es)
 count={2:2,3:4,4:6,5:8,6:10,7:12,8:12,9:12}[stage]
 for i in range(count):
  t=.24+.63*i/max(1,count-1);node=at(t);yaw=25+(i//2)*91+(i%2)*180
  length=(1.15+min(stage,6)*.28)*(1-.35*t)
  reach=0 if stage<4 else (1.5+stage*.15)*(1-.55*t)
  tip=add(node,mul(direction(yaw,28+i%3*5),reach))
  if reach:rod(es,f'branch-{i}',node,tip,.07+.008*stage)
  fan(es,f'fan-{i}',tip,yaw,10+i%3*7,length,3 if stage==2 else 5 if stage<5 else 7)
  if stage>=5 and i<8:
   side=add(node,mul(direction(yaw,32),reach*.5))
   fan(es,f'sidefan-{i}',side,yaw+(55 if i%2 else -55),15,length*.65,5)
  if stage>=7:
   # Bud mass increases, topology remains attached to each branch tip.
   r={7:.15,8:.30,9:.42}[stage];height={7:.40,8:.9,9:1.4}[stage]
   bud(es,f'flower-{i}',tip,r,height)
 if stage>=7:bud(es,'terminal-cola',at(.98),{7:.23,8:.44,9:.58}[stage],{7:.7,8:1.5,9:2.0}[stage])
 return shape(es)
def write(path,data):
 path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(data,indent=2)+'\n')
def main():
 for stage,name in enumerate(NAMES,1):
  d=model(stage);write(ASSETS/f'shapes/plant/cannabis_stage_{stage:02}_{name}.json',d);print(stage,len(d['elements']))
 es=[];rod(es,'bud-stem',(8,3,8),(8,5.8,8),.30)
 bud(es,'main-bud',(8,4,8),1.8,6.5)
 bud(es,'bud-lobe-a',(6.85,4.3,8.2),.85,3.6)
 bud(es,'bud-lobe-b',(9.0,5.5,7.7),.95,3.8)
 write(ASSETS/'shapes/item/cannabis-buds.json',shape(es))
 es=[]
 # Slender octagonal-ish parchment roll, visibly irregular, folded at one tip.
 for i in range(8):
  z=3+i*1.15;r=.29+i*.014
  for k,(sx,sy) in enumerate([(1,.6),(.6,1)]):box(es,f'paper-{i}-{k}',(8-r*sx,8-r*sy,z),(8+r*sx,8+r*sy,z+1.17),'paper')
 box(es,'exposed-end',(7.68,7.68,12.22),(8.32,8.32,12.27),'bud')
 box(es,'twisted-tip',(7.86,7.86,2.55),(8.14,8.14,3.05),'paper')
 write(ASSETS/'shapes/item/joint.json',shape(es))
 crop=json.loads((ASSETS/'blocktypes/coca-plant.json').read_text())
 crop['variantgroups'][0]['states']=['cannabis']
 crop['shapeByType']={f'*-{s}':{'base':f'plant/cannabis_stage_{s:02}_{n}'} for s,n in enumerate(NAMES,1)}
 crop['textures']={k:{'base':v} for k,v in TEXTURES.items()}
 crop['dropsByType']={'*-9':[{'type':'item','code':'vs-dope:cannabis-buds','quantity':{'avg':4}},{'type':'item','code':'vs-dope:seeds-cannabis','quantity':{'avg':1.5}}],'*':[{'type':'item','code':'vs-dope:seeds-cannabis','quantity':{'avg':.35}}]}
 write(ASSETS/'blocktypes/cannabis-plant.json',crop)
 # itemtypes/cannabis-seeds.json is hand-maintained (seedbag shape; label from tools/build_seed_icons.py).
 common={'creativeinventory':{'general':['*'],'items':['*']},'maxstacksize':64,'textures':{k:{'base':v} for k,v in TEXTURES.items()},'guiTransform':{'rotation':{'x':-20,'y':-35,'z':12},'scale':1.7},'groundTransform':{'scale':1.0},'tpHandTransform':{'translation':{'x':0,'y':0,'z':0},'rotation':{'x':0,'y':0,'z':0},'scale':.75},'fpHandTransform':{'translation':{'x':0,'y':0,'z':0},'rotation':{'x':0,'y':0,'z':0},'scale':.75}}
 write(ASSETS/'itemtypes/cannabis-buds.json',dict(common,code='cannabis-buds',shape={'base':'item/cannabis-buds'}))
 joint=dict(common,code='joint',**{'class':'vs-dope.joint'},shape={'base':'item/joint'},heldTpUseAnimation='vsdope-smoke',heldTpIdleAnimation='helditemready')
 joint['guiTransform']={'rotation':{'x':-35,'y':25,'z':-35},'scale':2.2}
 joint['tpHandTransform']={'translation':{'x':0,'y':-.25,'z':-.15},'rotation':{'x':0,'y':0,'z':0},'scale':.7}
 write(ASSETS/'itemtypes/joint.json',joint)
 write(ASSETS/'recipes/grid/joint.json',{'ingredientPattern':'BP','width':2,'height':1,'shapeless':True,'ingredients':{'B':{'type':'item','code':'vs-dope:cannabis-buds','quantity':1},'P':{'type':'item','code':'game:paper-parchment','quantity':1}},'output':{'type':'item','code':'vs-dope:joint','quantity':1}})
if __name__=='__main__':main()
