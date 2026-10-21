// Reproducible asset bootstrap. Executes the supplied generator unchanged; no browser needed.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const zlib = require('node:zlib');
const root = path.resolve(__dirname, '../..');
const source = fs.readFileSync(path.join(root, 'Design/galaxy101_unity_handoff/reference/planet-renderer.js'), 'utf8');
const context = { window: {} };
vm.createContext(context);
vm.runInContext(source.replace('window.GalaxyPlanet =', 'window.bake = buildTex; window.GalaxyPlanet ='), context);
function crc32(b) { let c = 0xffffffff; for (const n of b) { c ^= n; for (let i=0;i<8;i++) c = (c >>> 1) ^ ((c & 1) ? 0xedb88320 : 0); } return (c ^ 0xffffffff) >>> 0; }
function chunk(kind, data) { const k = Buffer.from(kind), size = Buffer.alloc(4), crc = Buffer.alloc(4); size.writeUInt32BE(data.length); crc.writeUInt32BE(crc32(Buffer.concat([k, data]))); return Buffer.concat([size,k,data,crc]); }
const destination = path.join(root, 'Assets/Galaxy101/Art/Planets/Resources/Galaxy101/Planets');
fs.mkdirSync(destination, {recursive:true});
for (const [name,file] of [['Satürn','saturn'],['Mars','mars'],['Neptün','neptune'],['Jüpiter','jupiter'],['Venüs','venus']]) {
 const t = context.window.bake(name,2048), raw = Buffer.alloc((t.W*3+1)*t.H);
 for(let y=0;y<t.H;y++) for(let x=0;x<t.W;x++) { const i=y*t.W+x, o=y*(t.W*3+1)+1+x*3; raw[o]=t.tr[i]; raw[o+1]=t.tg[i]; raw[o+2]=t.tb[i]; }
 const header=Buffer.alloc(13);header.writeUInt32BE(t.W,0);header.writeUInt32BE(t.H,4);header[8]=8;header[9]=2;
 const png=Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',header),chunk('IDAT',zlib.deflateSync(raw,{level:9})),chunk('IEND',Buffer.alloc(0))]);
 fs.writeFileSync(path.join(destination,file+'.png'),png);
 console.log(`${file}: ${t.W}x${t.H}, ${png.length} bytes`);
}
