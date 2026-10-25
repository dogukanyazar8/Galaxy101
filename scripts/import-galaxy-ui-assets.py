"""Import exact supplied SVG paths, theme USS, static font weights and logo artwork.

Run with Python3 + Pillow/fontTools. Font inputs: Google Fonts official repository;
SIL OFL licenses retained beside the generated static font instances.
"""
from pathlib import Path
from urllib.request import Request, urlopen
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import hashlib,json,re,shutil

ROOT=Path(__file__).resolve().parents[1]
HANDOFF=ROOT/'Design/galaxy101_unity_handoff'
RES=ROOT/'Assets/Galaxy101/Resources/Galaxy101'
FONTS=RES/'Fonts';FONTS.mkdir(parents=True,exist_ok=True)
CACHE=ROOT/'artifacts/design-inspection/fonts';CACHE.mkdir(parents=True,exist_ok=True)
# The exact font sources the shipped static weights were cut from: google/fonts moves on, so a download is accepted only
# when its SHA-256 matches (a newer upstream font fails loudly instead of silently changing the build).
PINNED={'Montserrat-Variable.ttf':'0f7b311b2f3279e4eef9b2f968bcdbab6e28f4daeb1f049f4f278a902bcd82f7',
        'Montserrat-OFL.txt':'8b7141c03fa4f8d44e6345d5d4931709290f0f67875e452e95ac1fd3a027802e',
        'Baloo2-Variable.ttf':'d47a6852548059b1db49a1319d06d499d546c3fa2237cf9eee9c43c8abb025c2',
        'Baloo2-OFL.txt':'ad09b05dc8bc678c9daf7c4c5f7ef1f55e5726127f4330b2e98e40b9dffcb860'}
def fetch(path,url):
    data=path.read_bytes() if path.exists() else urlopen(Request(url,headers={'User-Agent':'Galaxy101-Asset-Import'})).read()
    digest=hashlib.sha256(data).hexdigest()
    if digest!=PINNED[path.name]:raise ValueError(f'{path.name}: SHA-256 {digest} is not the pinned source {PINNED[path.name]} ({url})')
    if not path.exists():path.write_bytes(data)
for family,name,weights in [('montserrat','Montserrat',[600,700,800,900]),('baloo2','Baloo2',[600,700,800])]:
    variable=CACHE/f'{name}-Variable.ttf'
    fetch(variable,f'https://raw.githubusercontent.com/google/fonts/main/ofl/{family}/{name}%5Bwght%5D.ttf')
    licensepath=CACHE/f'{name}-OFL.txt'
    fetch(licensepath,f'https://raw.githubusercontent.com/google/fonts/main/ofl/{family}/OFL.txt')
    shutil.copyfile(licensepath,FONTS/licensepath.name)
    for weight in weights:
        output=FONTS/f'{name}-{weight}.ttf'
        if not output.exists():
            font=TTFont(variable);instantiateVariableFont(font,{'wght':weight},inplace=True);font.save(output)
        font=TTFont(output);missing=[c for c in 'çğıİöşüÇĞÖŞÜ₺×·—…' if ord(c) not in font.getBestCmap()]
        if missing:raise ValueError(f'Missing font glyphs: {output.name}: {missing}')

html=(HANDOFF/'reference/Galaxy101_AnaMenu_1a.dc.html').read_text(encoding='utf-8').split('<!-- 1b -->')[0]
svgs=re.findall(r'<svg\b[\s\S]*?</svg>',html)
icons={}
actions={'settings':'openSettings','shop':'openShop','friends':'openFriends','leader':'openLeader','daily':'openDaily','rules':'openRules','share':'openShare','edit':'openProfile','add':'openAddFriend'}
for name,action in actions.items():
    marker=re.search(r'onClick="\{\{ '+action+r' \}\}"',html)
    if marker:
        hit=re.search(r'<svg\b[\s\S]*?</svg>',html[marker.end():])
        if hit:icons[name]=hit.group()
signatures={'settings':'M19.4 15','close':'M6 6l12 12','tick':'M5 12.5','share':'M8.2 10.8','edit':'M4 20h4','search':'M20 20l-4-4','copy':'M16 8V5','link':'M10 14a4','clock':'M12 9v4','lock':'M8 11V8','logout':'M14 4h4','delete':'M4 7h16','crown':'M3 20 1 5','profile':'M4 21a8'}
for name,signature in signatures.items():
    icons[name]=next(svg for svg in svgs if signature in svg)
iconpath=RES/'Icons';iconpath.mkdir(parents=True,exist_ok=True)
for name,svg in icons.items():(iconpath/(name+'.txt')).write_text(svg,encoding='utf-8')

themes=json.loads((HANDOFF/'spec/data/themes.json').read_text(encoding='utf-8'))
ids={'Satürn':'saturn','Mars':'mars','Neptün':'neptune','Jüpiter':'jupiter','Venüs':'venus'}
styles=ROOT/'Assets/Galaxy101/UI/Styles';styles.mkdir(parents=True,exist_ok=True)
for planet,data in themes.items():
    slug=ids[planet]
    text='.theme-'+slug+' {\n'+''.join(f'    --{key}: {value};\n' for key,value in data['cssVars'].items() if not isinstance(value,(int,float)))+'}\n'
    (styles/f'Theme.{slug.title()}.uss').write_text(text,encoding='utf-8')

def rgba(hexcolor):
    h=hexcolor.lstrip('#');return tuple(int(h[i:i+2],16) for i in (0,2,4))+(255,)

art=RES/'Artwork';art.mkdir(parents=True,exist_ok=True)
font=ImageFont.truetype(str(FONTS/'Montserrat-700.ttf'),128)
word='GALAXY';spacing=14
textwidth=round(sum(font.getlength(c) for c in word)+spacing*(len(word)-1))
for planet,data in themes.items():
    colors=data['cssVars'];size=(textwidth+160,308)
    mask=Image.new('L',size);draw=ImageDraw.Draw(mask);x=80
    for letter in word:
        draw.text((x,104),letter,font=font,fill=255,anchor='lt');x+=font.getlength(letter)+spacing
    result=Image.new('RGBA',size)
    def layer(color,blur=0,dy=0):
        m=mask.filter(ImageFilter.GaussianBlur(blur)) if blur else mask
        image=Image.new('RGBA',size,color);image.putalpha(m.point(lambda value:round(value*color[3]/255)))
        if dy:
            shifted=Image.new('RGBA',size);shifted.alpha_composite(image,(0,dy));image=shifted
        result.alpha_composite(image)
    layer(rgba(colors['a1'])[:3]+(115,),52)
    layer((0,0,0,128),36,20)
    layer(rgba(colors['lS']),0,10)
    gradient=Image.new('RGBA',size);gd=ImageDraw.Draw(gradient)
    stops=[rgba(colors[k]) for k in ['lg1','a1','a2']]
    for y in range(size[1]):
        t=max(0,min(1,(y-104)/96));a,b,u=(stops[0],stops[1],t/.45) if t<.45 else (stops[1],stops[2],(t-.45)/.55)
        gd.line((0,y,size[0],y),fill=tuple(round(a[i]+(b[i]-a[i])*u) for i in range(4)))
    gradient.putalpha(mask);result.alpha_composite(gradient);result.save(art/f'logo-{ids[planet]}.png')
print(f'Imported {len(icons)} original SVG icons, 7 static fonts, 5 theme styles/logos; Turkish glyphs verified.')
