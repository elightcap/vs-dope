"""Orthographic textured previews from the production shape JSONs (Pillow, NumPy).
Not a replacement for Vintage Story's in-game renderer.
"""
import json,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
def render(path,yaw=35,elevation=12,size=600):
 d=json.loads(Path(path).read_text()); out=np.full((size,size,3),(232,235,222),dtype=np.uint8);depth=np.full((size,size),-1e9)
 textures={k:np.array(Image.open(ROOT/'assets/vs-dope/textures'/f'{v}.png').convert('RGBA')) for k,v in d['textures'].items()}
 a,b=map(math.radians,(yaw,elevation)); eye=np.array([math.sin(a)*math.cos(b),math.sin(b),math.cos(a)*math.cos(b)]);right=np.array([math.cos(a),0,-math.sin(a)]);up=np.cross(eye,right)
 camera=np.array([right,-up,eye]);zoom=size/20
 for e in d['elements']:
  x0,y0,z0=e['from'];x1,y1,z1=e['to'];origin=np.array(e.get('rotationOrigin',[0,0,0])); ay,az=map(math.radians,[e.get('rotationY',0),e.get('rotationZ',0)])
  cy,sy,cz,sz=math.cos(ay),math.sin(ay),math.cos(az),math.sin(az)
  rot=np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]])@np.array([[cz,-sz,0],[sz,cz,0],[0,0,1]])
  faces={'up':[(x0,y1,z0),(x1,y1,z0),(x0,y1,z1)],'down':[(x0,y0,z1),(x1,y0,z1),(x0,y0,z0)],'north':[(x1,y1,z0),(x0,y1,z0),(x1,y0,z0)],'south':[(x0,y1,z1),(x1,y1,z1),(x0,y0,z1)],'east':[(x1,y1,z1),(x1,y1,z0),(x1,y0,z1)],'west':[(x0,y1,z0),(x0,y1,z1),(x0,y0,z0)]}
  for face,fd in e['faces'].items():
   world=(np.array(faces[face])-origin)@rot.T+origin
   pts=(world-np.array([8,7.5,8]))@camera.T;pts[:,:2]=pts[:,:2]*zoom+size/2
   p,u,v=pts[0],pts[1]-pts[0],pts[2]-pts[0];mat=np.array([u[:2],v[:2]]).T
   if abs(np.linalg.det(mat))<1e-6:continue
   corners=np.array([p,p+u,p+v,p+u+v]);lo=np.maximum(np.floor(corners[:,:2].min(0)).astype(int),0);hi=np.minimum(np.ceil(corners[:,:2].max(0)).astype(int),size-1)
   if np.any(lo>hi):continue
   xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1));uv=np.stack([xx+.5-p[0],yy+.5-p[1]],axis=-1)@np.linalg.inv(mat).T
   valid=(uv[:,:,0]>=0)&(uv[:,:,0]<=1)&(uv[:,:,1]>=0)&(uv[:,:,1]<=1)
   tex=textures[fd['texture'][1:]];uvrect=fd['uv'];tu=(uvrect[0]+uv[:,:,0]*(uvrect[2]-uvrect[0]))/d['textureWidth'];tv=(uvrect[1]+uv[:,:,1]*(uvrect[3]-uvrect[1]))/d['textureHeight']
   rgba=tex[np.clip((tv*tex.shape[0]).astype(int),0,tex.shape[0]-1),np.clip((tu*tex.shape[1]).astype(int),0,tex.shape[1]-1)]
   zz=p[2]+u[2]*uv[:,:,0]+v[2]*uv[:,:,1];region=depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1];valid&=(rgba[:,:,3]>127)&(zz>region)
   normal=np.cross(world[1]-world[0],world[2]-world[0]);normal/=max(np.linalg.norm(normal),1e-8);light=.77+.23*abs(normal@np.array([.3,.85,.43]))
   out[lo[1]:hi[1]+1,lo[0]:hi[0]+1][valid]=(rgba[:,:,:3]*light).astype(np.uint8)[valid];region[valid]=zz[valid]
 return Image.fromarray(out)
if __name__=='__main__':
 dest=ROOT/'docs/previews';dest.mkdir(exist_ok=True)
 p=next((ROOT/'assets/vs-dope/shapes/plant').glob('coca_stage_09*.json'))
 sheet=Image.new('RGB',(1200,1250),(232,235,222));draw=ImageDraw.Draw(sheet)
 for i,(angle,el,label) in enumerate([(0,10,'Front'),(90,10,'Side'),(180,10,'Back'),(35,55,'Above')]):
  sheet.paste(render(p,angle,el),(i%2*600,i//2*625+25));draw.text((i%2*600+20,i//2*625+8),label,fill=(35,55,30))
 sheet.save(dest/'coca-mature.png')
 stages=Image.new('RGB',(1350,970),(232,235,222));draw=ImageDraw.Draw(stages)
 for i,p in enumerate(sorted((ROOT/'assets/vs-dope/shapes/plant').glob('coca_stage_*.json'))):
  stages.paste(render(p,size=320),(i%3*450+65,i//3*323));draw.text((i%3*450+15,i//3*323+15),f'Stage {i+1}',fill=(35,55,30))
 stages.save(dest/'coca-growth.png')
