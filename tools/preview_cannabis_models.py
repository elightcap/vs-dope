"""Render actual production meshes, not concept illustrations. Requires Pillow/numpy."""
from PIL import Image,ImageDraw,ImageFont
from preview_coca_models import ROOT,render
DEST=ROOT/'docs/previews'
def sheet(paths,filename,size=360,cols=3):
 rows=(len(paths)+cols-1)//cols
 out=Image.new('RGB',(cols*(size+30),rows*(size+40)),(232,235,222));d=ImageDraw.Draw(out)
 for i,(p,label,yaw,el) in enumerate(paths):
  x=i%cols*(size+30);y=i//cols*(size+40)
  out.paste(render(p,yaw=yaw,elevation=el,size=size),(x+15,y+25));d.text((x+15,y+9),label,fill=(35,55,30))
 out.save(DEST/filename)
if __name__=='__main__':
 ps=sorted((ROOT/'assets/vs-dope/shapes/plant').glob('cannabis_stage_*.json'))
 sheet([(p,f'{i+1}. '+p.stem.split('_',3)[-1].replace('_',' ').title(),35,18) for i,p in enumerate(ps)],'cannabis-growth.png')
 sheet([(ps[-1],label,yaw,el) for label,yaw,el in [('Front',0,10),('Side',90,10),('Back',180,10),('Above',35,60)]],'cannabis-mature.png',size=560,cols=2)
 sheet([(ROOT/f'assets/vs-dope/shapes/item/{name}.json',label,35,22) for name,label in [('cannabis-buds','Cannabis buds'),('joint','Joint')]],'cannabis-items.png',size=560,cols=2)
