"""Original Okey101 art. Run with Blender --background --python this-file.
No downloaded meshes, fonts, textures, recordings or add-ons. Deterministic output.
"""
import bpy
import math
import numpy as np
import pathlib
import random
import struct
import wave
import zlib

ROOT = pathlib.Path(__file__).resolve().parents[1] / 'Assets/Okey101/Art'
for folder in ('Models', 'Textures', 'Audio', 'Sources', 'Materials'):
    (ROOT / folder).mkdir(parents=True, exist_ok=True)

def png(path, pixels):
    pixels = np.asarray(pixels, dtype=np.uint8)
    h, w, _ = pixels.shape
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data) & 0xffffffff)
    data = b''.join(b'\0' + row.tobytes() for row in pixels)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', w, h, 8, 6, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(data, 9)) + chunk(b'IEND', b''))

# Hand-authored rounded numeral paths; no font file is used or embedded.
PATHS = {
 '0': [[(12,1),(4,3),(1,10),(1,29),(4,36),(12,38),(20,36),(23,29),(23,10),(20,3),(12,1)]],
 '1': [[(5,9),(13,2),(13,37)],[(4,37),(22,37)]],
 '2': [[(2,8),(6,2),(16,1),(22,5),(23,12),(19,19),(2,37),(24,37)]],
 '3': [[(2,4),(10,1),(19,3),(23,8),(21,15),(13,19),(21,22),(24,29),(20,36),(11,38),(2,35)]],
 '4': [[(19,38),(19,1),(1,26),(25,26)]],
 '5': [[(23,2),(3,2),(2,18),(12,17),(21,21),(24,28),(21,35),(12,38),(2,35)]],
 '6': [[(22,4),(15,1),(7,5),(2,15),(1,28),(5,36),(13,38),(21,35),(24,28),(21,21),(13,18),(2,22)]],
 '7': [[(1,2),(24,2),(9,38)]],
 '8': [[(12,19),(4,15),(2,8),(6,2),(16,1),(23,6),(23,12),(18,17),(5,23),(1,29),(5,36),(13,38),(21,35),(24,29),(20,23),(12,19)]],
 '9': [[(2,34),(10,38),(18,34),(23,25),(24,12),(21,4),(13,1),(5,3),(1,10),(4,17),(12,20),(23,16)]]
}
COLORS = [(166,29,24),(158,105,14),(27,79,158),(27,37,39)]
atlas = np.zeros((1024,4096,4), dtype=np.uint8)
yy, xx = np.mgrid[0:256,0:256].astype(float)
def stroke(mask, points, radius):
    for a,b in zip(points, points[1:]):
        ax,ay = a; bx,by = b; dx,dy = bx-ax,by-ay
        t = np.clip(((xx-ax)*dx + (yy-ay)*dy) / max(1e-9,dx*dx+dy*dy),0,1)
        distance = np.sqrt((xx-ax-t*dx)**2 + (yy-ay-t*dy)**2)
        np.maximum(mask, np.clip(radius+.5-distance,0,1), out=mask)
def numerals(text, color):
    mask = np.zeros((256,256)); scale = 4.0 if len(text)<3 else 2.65
    total = (len(text)*31-7)*scale; left=(256-total)/2
    for index,char in enumerate(text):
        for path in PATHS[char]:
            stroke(mask, [(left+index*31*scale+x*scale, 35+y*scale) for x,y in path], 2.1*scale)
    return mask
for row,color in enumerate(COLORS):
    for col in range(16):
        mask = numerals(str(col+1) if col<13 else '101', color)
        if col < 13:
            if row==0: icon = np.sqrt((xx-128)**2+(yy-224)**2)<15
            elif row==1: icon = abs(xx-128)+abs(yy-224)<19
            elif row==2: icon = (yy>=207)&(yy<=239)&(abs(xx-128)<(yy-206)*.65)
            else: icon = (abs(xx-128)<15)&(abs(yy-224)<15)
            np.maximum(mask,icon.astype(float),out=mask)
        tile = np.zeros((256,256,4),dtype=np.uint8)
        tile[:,:,:3] = color if col<13 else (198,161,88)
        tile[:,:,3] = np.rint(mask*255).astype(np.uint8)
        atlas[row*256:(row+1)*256,col*256:(col+1)*256] = tile
png(ROOT/'Textures/TileFaces.png',atlas)
rng = np.random.default_rng(101)
noise = rng.normal(0,1,(512,512)); y,x=np.mgrid[0:512,0:512]
for name,base,texture in [('FeltWeave',(21,69,57), noise*1.7 + ((x%3==0)+(y%3==0))*1.1),
                         ('WoodGrain',(75,43,28), 4*np.sin(y*.12+np.sin(x*.012)*2)+noise*.7)]:
    rgba=np.empty((512,512,4),dtype=np.uint8)
    rgba[:,:,:3]=np.clip(np.array(base)+texture[:,:,None],0,255);rgba[:,:,3]=255
    png(ROOT/f'Textures/{name}.png',rgba)

# Short, low-level dry contact sounds, seeded noise plus damped resonances.
for name,duration,frequency,level in [('draw',.15,310,.23),('place',.11,690,.29),('discard',.14,490,.32)]:
    rate=44100; prng=random.Random(101+frequency); samples=[]; low=0
    for i in range(int(rate*duration)):
        t=i/rate; low=.72*low+.28*(prng.random()*2-1)
        envelope=(1-math.exp(-t*2100))*math.exp(-t*(32 if name!='draw' else 24))
        value=level*envelope*(.56*math.sin(2*math.pi*frequency*t)+.22*math.sin(2*math.pi*frequency*1.63*t)+.65*low)
        samples.append(int(max(-1,min(1,value))*32767))
    with wave.open(str(ROOT/f'Audio/{name}.wav'),'wb') as out:
        out.setparams((1,2,rate,0,'NONE','not compressed')); out.writeframes(struct.pack('<'+'h'*len(samples),*samples))

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
materials={}
for name,color,rough,metal in [('Wood',(.16,.075,.035,1),.42,0),('Felt',(.025,.16,.115,1),.9,0),
                              ('Brass',(.53,.32,.10,1),.3,.65),('Tile',(.96,.94,.85,1),.26,0)]:
    mat=bpy.data.materials.new(name);mat.diffuse_color=color
    bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=color
    bsdf.inputs['Roughness'].default_value=rough;bsdf.inputs['Metallic'].default_value=metal
    materials[name]=mat

def box(name,center,size,material,bevel):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center); obj=bpy.context.object;obj.name=name;obj.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=obj.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(materials[material]); return obj

models={}
models['Tile']=[box('TileBody',(0,0,0),(.72,.92,.16),'Tile',.055)]
models['Rack']=[box('RackBase',(0,0,0),(10.5,2.18,.30),'Wood',.09)]
for depth in [-1.05,0,1.05]:
    models['Rack'].append(box('RackRail',(0,depth,.21),(10.6,.095,.18),'Wood',.035))
models['Table']=[box('WalnutFrame',(0,0,-.40),(18,10.4,.45),'Wood',.18),
                 box('BrassInlay',(0,0,-.157),(17.62,10.02,.035),'Brass',.10),
                 box('FeltBed',(0,0,-.12),(17.45,9.85,.10),'Felt',.09)]
# Export meters, Y up. Each file contains only its own meshes, no animation/camera/light.
for name,objects in models.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(ROOT/f'Models/{name}.fbx'),use_selection=True,object_types={'MESH'},
                             axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,
                             bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Sources/okey-table.blend'))
print('OKEY_ART_READY',ROOT)
