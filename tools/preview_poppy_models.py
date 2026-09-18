"""Render production poppy shapes with the same renderer used for coca."""
from PIL import Image,ImageDraw
from preview_coca_models import render,ROOT
if __name__=='__main__':
 paths=sorted((ROOT/'assets/vs-dope/shapes/plant').glob('papaver_stage_*.json'))
 dest=ROOT/'docs/previews';dest.mkdir(exist_ok=True)
 sheet=Image.new('RGB',(1350,1080),(232,235,222));draw=ImageDraw.Draw(sheet)
 for i,p in enumerate(paths):
  sheet.paste(render(p,35,18,350),(i%3*450+50,i//3*360));draw.text((i%3*450+15,i//3*360+12),f'Stage {i+1}',fill=(35,55,30))
 sheet.save(dest/'poppy-growth.png')
 sheet=Image.new('RGB',(1200,1250),(232,235,222));draw=ImageDraw.Draw(sheet)
 for i,(idx,yaw,el,label) in enumerate([(7,20,15,'Flowering / front'),(7,110,40,'Flowering / above'),(8,20,15,'Capsules / front'),(8,110,30,'Capsules / side')]):
  sheet.paste(render(paths[idx],yaw,el),(i%2*600,i//2*625+25));draw.text((i%2*600+20,i//2*625+8),label,fill=(35,55,30))
 sheet.save(dest/'poppy-mature.png')
