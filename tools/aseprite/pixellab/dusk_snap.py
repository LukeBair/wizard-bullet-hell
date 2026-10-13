import sys,glob,os,colorsys
from PIL import Image
H=lambda h:tuple(int(h[i:i+2],16) for i in (0,2,4))
OUT,D1,D2,G1,G2,SK,CO,RD=map(H,["141120","1D1A2B","2B2F4A","3E5A4E","6F9B7A","F0D2BC","E5555F","8C3446"])
def m(c):
    r,g,b=c; l=.3*r+.59*g+.11*b
    h,s,v=colorsys.rgb_to_hsv(r/255,g/255,b/255); h*=360
    if l<22: return OUT
    if s<0.18 or v<0.12:
        return SK if l>150 else (D2 if l>45 else D1)
    if 80<=h<200: return G2 if l>100 else G1
    if 200<=h<320: return D1 if l<38 else D2
    if l>140: return CO if g/max(r,1)<0.55 else SK
    if l>=90: return CO
    if l>=42: return RD
    return D2
def snap(im):
    im=im.convert('RGBA'); px=im.load()
    for y in range(im.height):
        for x in range(im.width):
            r,g,b,a=px[x,y]; px[x,y]=(0,0,0,0) if a<128 else m((r,g,b))+(255,)
    return im
src,dst=sys.argv[1],sys.argv[2]
for f in glob.glob(src+'/**/*.png',recursive=True):
    o=os.path.join(dst,os.path.relpath(f,src)); os.makedirs(os.path.dirname(o),exist_ok=True); snap(Image.open(f)).save(o)
