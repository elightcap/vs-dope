"""Rebuild the nine poppy shapes; preserve the existing atlas and crop behavior."""
import json,math
from pathlib import Path
from build_coca_models import add,mul,direction,segment
ROOT=Path(__file__).resolve().parents[1]
UV={'dirt':[0,0,8,8],'stem':[0,16,8,24],'leaf':[0,8,8,16],'petal':[8,0,16,8],'bud':[16,0,24,8],'capsule':[8,8,16,16],'crown':[16,8,24,16]}
def rod(es,name,a,b,w,texture='stem'):
 delta=tuple(y-x for x,y in zip(a,b));dx,dy,dz=delta
 return segment(es,name,a,math.sqrt(sum(v*v for v in delta)),w,math.degrees(math.atan2(dz,dx)),math.degrees(math.atan2(dy,math.hypot(dx,dz))),texture)
def box(es,name,lo,hi,texture):
 es.append({'name':name,'from':list(lo),'to':list(hi),'faces':{f:{'texture':'#'+texture,'uv':UV[texture]} for f in ('north','south','east','west','up','down')}})
def leaf(es,name,node,yaw,pitch,length,vertical=False):
 # Contiguous thin sections form a lobed silhouette without transparent atlas margins.
 widths=[.16,.42,.68,.49,.79,.59,.70,.48,.32,.12]
 axis=direction(yaw,pitch)
 for j,w in enumerate(widths):
  start=add(node,mul(axis,length*j/len(widths)))
  segment(es,f'{name}-lobe-{j}',start,length/len(widths)+.004,w*length*.60,yaw,pitch,'leaf',True)
  e=es[-1];x,y,z=start
  # Explicit orientation avoids the coca helper's name-based leaf orientation.
  e['from']=[x,y-.009,z-w*length*.30];e['to']=[x+length/len(widths)+.004,y+.009,z+w*length*.30]
  faces=('up','down')
  if vertical:
   e['from']=[x,y-w*length*.30,z-.009];e['to']=[x+length/len(widths)+.004,y+w*length*.30,z+.009];faces=('north','south')
  e['faces']={f:{'texture':'#leaf','uv':UV['leaf']} for f in faces}
 rod(es,name+'-midrib',node,add(node,mul(axis,length*.90)),.035)
def pod(es,name,base,radius,height,texture='capsule',crown=True):
 # Stepped rounded volume: each tier has clipped corners rather than one large cube.
 x,y,z=base
 for j,(lo,hi,r) in enumerate([(0,.18,.47),(.18,.42,.82),(.42,.73,1),(.73,.92,.83),(.92,1,.53)]):
  r*=radius
  for k,(wx,wz) in enumerate([(r,.60*r),(.60*r,r),(.83*r,.83*r)]):
   box(es,f'{name}-tier-{j}-{k}',(x-wx,y+height*lo,z-wz),(x+wx,y+height*hi,z+wz),texture)
 if crown:
  top=y+height
  box(es,name+'-disc',(x-radius*.65,top-.02,z-radius*.65),(x+radius*.65,top+.10,z+radius*.65),'crown')
  for k in range(8):segment(es,f'{name}-ray-{k}',(x,top+.11,z),radius*.83,.055,k*45,0,'crown')
def flower(es,name,base,scale):
 x,y,z=base
 pod(es,name+'-ovary',base,.28*scale,.38*scale,'bud',False)
 for k in range(4):
  yaw=k*90+18
  # Four broad petals, cupped at the base and opening into irregular rims.
  start=(x,y+.15*scale,z)
  for j,(length,width,pitch) in enumerate([(.45,.72,55),(.64,1.36,27),(.55,1.42,8),(.18,1.08,-4)]):
   end=segment(es,f'{name}-petal-{k}-{j}',start,length*scale,width*scale,yaw,pitch+(k%2)*4,'petal',True)
   start=end
 for k in range(12):
  a=math.radians(k*30);p=(x+.40*scale*math.cos(a),y+.29*scale,z+.40*scale*math.sin(a))
  rod(es,f'{name}-stamen-{k}',p,add(p,(0,.23*scale,0)),.05*scale,'crown')
def model(stage):
 es=[];h=[0,.7,1.1,1.6,4.7,8.8,11.3,12.1,12.4,12.7][stage]
 # Every leaf origin lies on this continuous, subtly bent herbaceous stem.
 nodes=[(8,0,8),(8.10,h*.34,8.05),(7.91,h*.70,8.13),(8.13,h,8.12)]
 for i,(a,b) in enumerate(zip(nodes,nodes[1:])):rod(es,f'stem-{i}',a,b,.16-i*.025)
 def at(y):
  for a,b in zip(nodes,nodes[1:]):
   if a[1]<=y<=b[1]:return add(a,mul(tuple(b[j]-a[j] for j in range(3)),(y-a[1])/(b[1]-a[1])))
 # Small disturbed-earth clods keep the earlier visual cue without burying the seedling.
 for i,(x,z,w) in enumerate([(7.6,8.4,.45),(8.5,7.8,.35),(7.5,7.7,.25)]):box(es,f'soil-{i}',(x-w,0,z-w),(x+w,.14,z+w),'dirt')
 n=[0,2,4,7,9,10,10,10,10,10][stage]
 length=[0,.82,1.35,2.2,2.85,3.05,3.10,3.10,3.10,3.10][stage]
 for i in range(n):
  y=min(h*.68,.28+i*.085)
  leaf(es,f'rosette-{i}',at(y),25+i*137.508,12+i%3*7,length*(.82+.06*(i%4)),vertical=i%4==2)
 if stage>=4:
  for i in range(3 if stage==4 else 7):
   y=h*(.22+i*.082)
   leaf(es,f'cauline-{i}',at(y),70+i*137.508,23+i%3*11,(2.8-i*.19)*(min(stage,6)/6),vertical=i%3!=0)
 if stage>=6:
  tip=nodes[-1]
  if stage==6:
   hook=add(tip,(.65,.35,.10));end=add(hook,(.35,-.55,.03))
   rod(es,'bud-neck-up',tip,hook,.105);rod(es,'bud-neck-down',hook,end,.095)
   pod(es,'closed-bud',add(end,(0,-.80,0)),.48,.90,'bud',False)
  elif stage==7:pod(es,'swollen-bud',tip,.66,1.30,'bud',False)
  elif stage==8:flower(es,'flower',tip,1)
  else:pod(es,'capsule',tip,.93,1.95)
  # A restrained secondary flowering stem, arising from a real stem node.
  if stage>=7:
   base=at(h*.49);bend=add(base,(2.15,1.80,-.55));end=add(bend,(.20,2.5,.10))
   rod(es,'side-stalk-lower',base,bend,.11);rod(es,'side-stalk-upper',bend,end,.085)
   leaf(es,'side-leaf',bend,28,30,1.75,True)
   if stage==7:pod(es,'side-bud',end,.40,.8,'bud',False)
   elif stage==8:flower(es,'side-flower',end,.72)
   else:pod(es,'side-capsule',end,.68,1.45)
 for e in es:
  for f in e['faces'].values():f['uv']=UV[f['texture'][1:]]
 def clean(v):
  if isinstance(v,float):return round(v,5)
  if isinstance(v,list):return [clean(x) for x in v]
  if isinstance(v,dict):return {k:clean(x) for k,x in v.items()}
  return v
 return clean({'textureWidth':32,'textureHeight':32,'textures':{k:'block/poppy/papaver_atlas' for k in UV},'elements':es})
if __name__=='__main__':
 for stage in range(1,10):
  path=next((ROOT/'assets/vs-dope/shapes/plant').glob(f'papaver_stage_{stage:02}_*.json'))
  d=model(stage);path.write_text(json.dumps(d,indent=2)+'\n');print(stage,len(d['elements']))
