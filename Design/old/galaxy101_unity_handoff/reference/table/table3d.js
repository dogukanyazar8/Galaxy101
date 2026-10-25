// Galaxy 101 — 3D game table (three.js r184). Scene, rack interaction, tile tweens, gifts.
import * as THREE from 'https://unpkg.com/three@0.184.0/build/three.module.js';
import { giftKit, GIFT_SCALE, glowTex } from './gifts3d.js';
import { createRackKit } from './racks3d.js';
import { createTileKit } from './tiles3d.js';

let TD = 0.010;
const TY = 0.76, TW = 0.05, TH = 0.07, SL = 0.053, RZ = 0.8, TILT = -0.36;
const HALF = 0.86, FELT = 0.76;
const SUIT = { R: '#D7263D', B: '#1F5FBF', K: '#1B1B1B', Y: '#E39B12' };
const YAW = [0, Math.PI / 2, Math.PI, -Math.PI / 2];
const UP = new THREE.Vector3(0, 1, 0);
const W = 1280, H = 720;
const ease = (t) => 1 - Math.pow(1 - t, 3);
const easeIO = (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);
const cl = (v, a, b) => Math.max(a, Math.min(b, v));
const hash = (s) => { let h = 7; for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) | 0; return ((h >>> 0) % 1000) / 1000; };

function rrPath(p, w, h, r) {
  const x = -w / 2, y = -h / 2;
  p.moveTo(x + r, y); p.lineTo(x + w - r, y); p.quadraticCurveTo(x + w, y, x + w, y + r);
  p.lineTo(x + w, y + h - r); p.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  p.lineTo(x + r, y + h); p.quadraticCurveTo(x, y + h, x, y + h - r);
  p.lineTo(x, y + r); p.quadraticCurveTo(x, y, x + r, y); return p;
}
const rrShape = (w, h, r) => rrPath(new THREE.Shape(), w, h, r);
function rrRing(wo, ro, wi, ri) { const s = rrShape(wo, wo, ro); s.holes.push(rrPath(new THREE.Path(), wi, wi, ri)); return s; }
function cvs(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; }
function mk(name, geo, mat, x = 0, y = 0, z = 0) { const m = new THREE.Mesh(geo, mat); m.name = name; m.position.set(x, y, z); m.castShadow = true; m.receiveShadow = true; return m; }
const lathe = (pts, seg = 40) => new THREE.LatheGeometry(pts.map(([a, b]) => new THREE.Vector2(a, b)), seg);
const flat = (shape, seg) => new THREE.ShapeGeometry(shape, seg || 12).rotateX(-Math.PI / 2);

function buildEnv(renderer, tint) {
  const pm = new THREE.PMREMGenerator(renderer), s = new THREE.Scene();
  s.add(new THREE.Mesh(new THREE.BoxGeometry(12, 12, 12), new THREE.MeshBasicMaterial({ color: '#10131f', side: THREE.BackSide })));
  const panel = (w, h, c, k, x, y, z, rx, ry) => { const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshBasicMaterial({ color: new THREE.Color(c).multiplyScalar(k), side: THREE.DoubleSide })); m.position.set(x, y, z); m.rotation.set(rx || 0, ry || 0, 0); s.add(m); };
  panel(5, 1.4, '#ffffff', 3.2, 0, 5.5, 0, Math.PI / 2);
  panel(3, 3, '#dfe6ff', 1.6, -5.5, 2, 1, 0, Math.PI / 2);
  panel(6, 2.5, tint || '#FFC94A', 1.4, 0, 1.5, -5.8);
  panel(2, 4, '#ffffff', 1.0, 5.5, 1.5, -1, 0, -Math.PI / 2);
  const t = pm.fromScene(s, 0.035).texture; pm.dispose(); return t;
}

export function createTable(canvas, o = {}) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'high-performance', preserveDrawingBuffer: true });
  renderer.setClearColor(0x000000, 0);
  renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.05;
  renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFShadowMap;
  const setScale = (s) => { renderer.setPixelRatio(Math.min(2, (s || 1) * (window.devicePixelRatio || 1))); renderer.setSize(W, H, false); };
  setScale(o.scale);
  const scene = new THREE.Scene();
  scene.environment = buildEnv(renderer); scene.environmentIntensity = 0.75;
  const camera = new THREE.PerspectiveCamera(40, W / H, 0.05, 40);
  const VIEWS = { seat: { p: new THREE.Vector3(0, 1.55, 1.62), t: new THREE.Vector3(0, 0.62, -0.25) }, over: { p: new THREE.Vector3(0, 3.2, 2.95), t: new THREE.Vector3(0, 0.45, -0.3) }, top: { p: new THREE.Vector3(0, 2.6, 1.42), t: new THREE.Vector3(0, 0.58, 0.16) } };
  const cam = { p: VIEWS.over.p.clone(), t: VIEWS.over.t.clone(), a: null, b: null, t0: 0, dur: 1 };
  let font = o.font || 'Montserrat';
  let view = 'seat';
  const fl = { dbl: true, deck: 106, canDraw: false, canDiscard: false, sideId: null, dragRack: false, seats: [true, true, true, true] };

  // ---------- materials ----------
  const M = {
    steel: new THREE.MeshStandardMaterial({ name: 'masa_kenar', color: '#3b4260', metalness: 0.9, roughness: 0.28 }),
    dark: new THREE.MeshStandardMaterial({ name: 'koyu_metal', color: '#131829', metalness: 0.75, roughness: 0.42 }),
    led: new THREE.MeshStandardMaterial({ name: 'led', color: '#000000', emissive: '#FFC94A', emissiveIntensity: 2.4 }),
    ledLine: new THREE.MeshBasicMaterial({ name: 'led_cizgi', color: '#FFC94A', transparent: true, opacity: 0.55 }),
    felt: new THREE.MeshStandardMaterial({ name: 'cuha', roughness: 0.96, metalness: 0 }),
    tileSide: new THREE.MeshPhysicalMaterial({ name: 'tas_kenar', color: '#E3D7BC', roughness: 0.34, clearcoat: 0.8 }),
    tile: new THREE.MeshPhysicalMaterial({ name: 'tas', color: '#FFF8EA', roughness: 0.36, clearcoat: 0.7, clearcoatRoughness: 0.22 }),
    leather: new THREE.MeshStandardMaterial({ name: 'deri', color: '#1d2240', roughness: 0.6, metalness: 0.1 }),
    floor: new THREE.MeshStandardMaterial({ name: 'zemin', color: '#ffffff', metalness: 0.55, roughness: 0.5 }),
    wall: new THREE.MeshStandardMaterial({ name: 'duvar', color: '#ffffff', metalness: 0.4, roughness: 0.6 }),
    ok: new THREE.MeshBasicMaterial({ name: 'gecerli', color: '#3CCB6E' }),
    newBar: new THREE.MeshBasicMaterial({ name: 'yeni_tas', color: '#FFD964', transparent: true }),
    zone: new THREE.MeshBasicMaterial({ name: 'atma_alani', color: '#FFC94A', transparent: true, opacity: 0.4, depthWrite: false }),
    zoneFill: new THREE.MeshBasicMaterial({ name: 'atma_dolgu', color: '#FFC94A', transparent: true, opacity: 0.0, depthWrite: false, blending: THREE.AdditiveBlending }),
    halo: new THREE.MeshBasicMaterial({ name: 'hale', map: glowTex(), color: '#FFC94A', transparent: true, opacity: 0.25, depthWrite: false, blending: THREE.AdditiveBlending }),
    holo: new THREE.MeshBasicMaterial({ name: 'holo', color: '#FFC94A', transparent: true, opacity: 0.35, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide }),
  };
  const seatLed = [1, 2, 3].map(() => M.led.clone());
  const TM = { slab: M.dark, rim: M.steel, band: M.led, inner: M.ledLine };
  const RM = { base: M.dark.clone(), ledge: M.steel.clone(), back: M.dark.clone(), backOpp: M.steel.clone(), cap: M.steel.clone(), led: M.led.clone() };
  Object.entries(RM).forEach(([k, m]) => (m.name = 'istaka_' + k));
  let theme = o.theme || { a1: '#FFD964', a2: '#F5A623', f1: '#2C3790', f2: '#11153F', tile: '#FFFDF6' }, skins = { table: o.tableSkin || 'station', rack: o.rackSkin || 'chrome' };

  // ---------- textures ----------
  function feltTex(c1, c2, acc, style) {
    const [c, x] = cvs(1024, 1024), g = x.createRadialGradient(512, 470, 60, 512, 512, 760);
    g.addColorStop(0, c1); g.addColorStop(1, c2); x.fillStyle = g; x.fillRect(0, 0, 1024, 1024);
    for (let i = 0; i < 9000; i++) { x.fillStyle = `rgba(${i % 2 ? 255 : 0},${i % 2 ? 255 : 0},${i % 2 ? 255 : 0},${0.02 + (i % 5) * 0.004})`; x.fillRect((i * 97.13) % 1024, (i * 57.71) % 1024, 1.5, 1.5); }
    x.strokeStyle = 'rgba(255,255,255,.07)'; x.lineWidth = 2; x.beginPath(); x.arc(512, 512, 252, 0, 7); x.stroke();
    x.beginPath(); x.arc(512, 512, 262, 0, 7); x.stroke();
    x.strokeStyle = acc; x.globalAlpha = 0.18; x.setLineDash([10, 14]); x.lineWidth = 3; x.beginPath(); x.arc(512, 512, 300, 0, 7); x.stroke(); x.setLineDash([]); x.globalAlpha = 1;
    if (style === 'holo') { x.fillStyle = 'rgba(2,4,14,.55)'; x.fillRect(0, 0, 1024, 1024); x.strokeStyle = acc; x.globalAlpha = 0.16; x.lineWidth = 1.5; for (let i = 0; i <= 1024; i += 48) { x.beginPath(); x.moveTo(i, 0); x.lineTo(i, 1024); x.moveTo(0, i); x.lineTo(1024, i); x.stroke(); } x.globalAlpha = 0.45; x.lineWidth = 3; x.beginPath(); x.arc(512, 512, 252, 0, 7); x.stroke(); x.globalAlpha = 1; }
    if (style === 'wood') { const v = x.createRadialGradient(512, 512, 300, 512, 512, 760); v.addColorStop(0, 'rgba(0,0,0,0)'); v.addColorStop(1, 'rgba(0,0,0,.35)'); x.fillStyle = v; x.fillRect(0, 0, 1024, 1024); x.strokeStyle = '#D9B15A'; x.globalAlpha = 0.35; x.lineWidth = 2; x.strokeRect(40, 40, 944, 944); x.globalAlpha = 1; }
    x.fillStyle = 'rgba(255,255,255,.09)'; x.font = `700 30px "${font}"`; x.textAlign = 'center'; if ('letterSpacing' in x) x.letterSpacing = '14px'; x.fillText('GALAXY 101', 519, 512 + 232);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
    t.repeat.set(1 / (FELT * 2), 1 / (FELT * 2)); t.offset.set(0.5, 0.5); return t;
  }
  function floorTex() {
    const [c, x] = cvs(512, 512); x.fillStyle = '#0b0e19'; x.fillRect(0, 0, 512, 512);
    x.strokeStyle = 'rgba(255,255,255,.035)'; x.lineWidth = 1; for (let i = 0; i <= 512; i += 64) { x.beginPath(); x.moveTo(i, 0); x.lineTo(i, 512); x.moveTo(0, i); x.lineTo(512, i); x.stroke(); }
    x.strokeStyle = 'rgba(255,255,255,.07)'; x.lineWidth = 3; x.strokeRect(1.5, 1.5, 509, 509);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(7, 6); t.anisotropy = 8; return t;
  }
  function wallTex() {
    const [c, x] = cvs(512, 256); const g = x.createLinearGradient(0, 0, 0, 256); g.addColorStop(0, '#0d1120'); g.addColorStop(1, '#161b2e'); x.fillStyle = g; x.fillRect(0, 0, 512, 256);
    x.strokeStyle = 'rgba(255,255,255,.05)'; for (let i = 0; i < 512; i += 128) { x.beginPath(); x.moveTo(i + 0.5, 0); x.lineTo(i + 0.5, 256); x.stroke(); }
    x.strokeStyle = 'rgba(255,255,255,.04)'; x.beginPath(); x.moveTo(0, 170.5); x.lineTo(512, 170.5); x.stroke();
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = THREE.RepeatWrapping; t.repeat.set(3, 1); return t;
  }
  function wallEmis() {
    const [c, x] = cvs(512, 256); x.fillStyle = '#000'; x.fillRect(0, 0, 512, 256);
    x.fillStyle = '#fff'; for (let i = 64; i < 512; i += 128) x.fillRect(i - 1.5, 30, 3, 140); x.fillRect(0, 184, 512, 2);
    const t = new THREE.CanvasTexture(c); t.wrapS = THREE.RepeatWrapping; t.repeat.set(3, 1); return t;
  }
  const woodCache = {};
  function woodTex(c1, c2, rep) {
    const k = c1 + c2 + rep; if (woodCache[k]) return woodCache[k];
    const [c, x] = cvs(512, 512); x.fillStyle = c1; x.fillRect(0, 0, 512, 512);
    for (let i = 0; i < 140; i++) { const y = i * 3.7 + Math.sin(i) * 2; x.strokeStyle = i % 3 ? c2 : 'rgba(255,220,170,.12)'; x.globalAlpha = 0.18 + (i % 5) * 0.06; x.lineWidth = 1 + (i % 4) * 0.7; x.beginPath(); x.moveTo(0, y); for (let k2 = 0; k2 <= 512; k2 += 16) x.lineTo(k2, y + Math.sin(k2 * 0.02 + i * 0.7) * 4 + Math.sin(k2 * 0.005 + i) * 6); x.stroke(); }
    x.globalAlpha = 1; const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(rep, rep); t.anisotropy = 8; return (woodCache[k] = t);
  }
  let _andro = {};
  function andromedaTex(w, h, band) {
    const key = w + 'x' + h + band; if (_andro[key]) return _andro[key];
    const [c, x] = cvs(w, h);
    const bg = x.createRadialGradient(w / 2, h / 2, 10, w / 2, h / 2, Math.max(w, h) * 0.75); bg.addColorStop(0, '#141a46'); bg.addColorStop(0.5, '#0a0d2a'); bg.addColorStop(1, '#04050f');
    x.fillStyle = bg; x.fillRect(0, 0, w, h);
    const [sc, sx] = cvs(32, 32); const sg = sx.createRadialGradient(16, 16, 0, 16, 16, 16); sg.addColorStop(0, 'rgba(255,255,255,1)'); sg.addColorStop(0.3, 'rgba(255,255,255,.5)'); sg.addColorStop(1, 'rgba(255,255,255,0)'); sx.fillStyle = sg; sx.fillRect(0, 0, 32, 32);
    let seed = 7; const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
    const spr = {}; const tint = (col) => { if (spr[col]) return spr[col]; const [tc, tx] = cvs(32, 32); const g = tx.createRadialGradient(16, 16, 0, 16, 16, 16); g.addColorStop(0, col); g.addColorStop(0.35, col + '88'); g.addColorStop(1, col + '00'); tx.fillStyle = g; tx.fillRect(0, 0, 32, 32); return (spr[col] = tc); };
    x.globalCompositeOperation = 'lighter';
    for (let i = 0; i < (band ? 500 : 1400); i++) { const r = rnd() < 0.08 ? 3 + rnd() * 3 : 1 + rnd() * 1.4; x.globalAlpha = 0.25 + rnd() * 0.6; x.drawImage(sc, rnd() * w - r, rnd() * h - r, r * 2, r * 2); }
    const S = band ? h * 0.9 : w * 0.42;
    x.save(); x.translate(w / 2, h / 2); x.rotate(band ? -0.12 : -0.55); x.scale(1, band ? 0.32 : 0.42);
    const core = x.createRadialGradient(0, 0, 0, 0, 0, S * 0.42); core.addColorStop(0, 'rgba(255,248,230,1)'); core.addColorStop(0.18, 'rgba(255,214,150,.85)'); core.addColorStop(0.5, 'rgba(230,150,120,.25)'); core.addColorStop(1, 'rgba(120,80,160,0)');
    x.globalAlpha = 1; x.fillStyle = core; x.beginPath(); x.arc(0, 0, S * 0.42, 0, 7); x.fill();
    const halo = x.createRadialGradient(0, 0, S * 0.2, 0, 0, S * 1.15); halo.addColorStop(0, 'rgba(140,170,255,.32)'); halo.addColorStop(0.7, 'rgba(90,110,220,.12)'); halo.addColorStop(1, 'rgba(60,60,160,0)');
    x.fillStyle = halo; x.beginPath(); x.arc(0, 0, S * 1.15, 0, 7); x.fill();
    for (let arm = 0; arm < 2; arm++) for (let i = 0; i < (band ? 700 : 1600); i++) {
      const tt = rnd() * 4.2 * Math.PI, rr = S * 0.1 * Math.exp(0.2 * tt), a = tt + arm * Math.PI + (rnd() - 0.5) * 0.5;
      if (rr > S * 1.12) continue;
      const px = Math.cos(a) * rr + (rnd() - 0.5) * S * 0.06, py = Math.sin(a) * rr + (rnd() - 0.5) * S * 0.06, f = rr / S;
      const pink = rnd() < 0.05, sz = (pink ? 3 : 1.4) + rnd() * 3 * (1 - f * 0.5);
      x.globalAlpha = (pink ? 0.6 : 0.22 + rnd() * 0.35) * (1 - f * 0.55);
      x.drawImage(tint(pink ? '#FF7AD0' : f < 0.35 ? '#FFE2B0' : f < 0.65 ? '#CFE0FF' : '#8FB0FF'), px - sz, py - sz, sz * 2, sz * 2);
    }
    x.globalCompositeOperation = 'source-over';
    for (let arm = 0; arm < 2; arm++) { x.strokeStyle = 'rgba(8,4,18,.55)'; x.lineWidth = S * 0.035; x.beginPath(); for (let tt = 0.9; tt < 3.4 * Math.PI; tt += 0.05) { const rr = S * 0.1 * Math.exp(0.2 * tt) * 0.92, a = tt + arm * Math.PI + 0.35; const px = Math.cos(a) * rr, py = Math.sin(a) * rr; tt === 0.9 ? x.moveTo(px, py) : x.lineTo(px, py); } x.globalAlpha = 0.6; x.stroke(); }
    x.restore();
    x.globalCompositeOperation = 'lighter'; x.globalAlpha = 0.7;
    const m32 = (cx, cy, r, col) => { const g = x.createRadialGradient(cx, cy, 0, cx, cy, r); g.addColorStop(0, col); g.addColorStop(1, 'rgba(0,0,0,0)'); x.fillStyle = g; x.beginPath(); x.arc(cx, cy, r, 0, 7); x.fill(); };
    if (!band) { m32(w * 0.62, h * 0.38, w * 0.035, 'rgba(255,236,200,.9)'); m32(w * 0.3, h * 0.66, w * 0.06, 'rgba(220,210,255,.35)'); }
    x.globalAlpha = 1; x.globalCompositeOperation = 'source-over';
    if (!band) { x.fillStyle = 'rgba(255,255,255,.08)'; x.font = `700 30px "${font}"`; x.textAlign = 'center'; if ('letterSpacing' in x) x.letterSpacing = '14px'; x.fillText('ANDROMEDA · 101', w / 2 + 7, h / 2 + 300); }
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
    if (!band) { t.repeat.set(1 / (FELT * 2), 1 / (FELT * 2)); t.offset.set(0.5, 0.5); }
    return (_andro[key] = t);
  }
  let _const = null;
  function constellationTex() {
    if (_const) return _const;
    const [c, x] = cvs(1024, 256), w = 1024, h = 256;
    const g = x.createLinearGradient(0, 0, w, 0); g.addColorStop(0, '#07081a'); g.addColorStop(0.5, '#120f33'); g.addColorStop(1, '#07081a'); x.fillStyle = g; x.fillRect(0, 0, w, h);
    const v = x.createLinearGradient(0, 0, 0, h); v.addColorStop(0, 'rgba(183,156,255,.14)'); v.addColorStop(0.45, 'rgba(0,0,0,0)'); v.addColorStop(1, 'rgba(90,140,255,.12)'); x.fillStyle = v; x.fillRect(0, 0, w, h);
    let seed = 11; const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
    for (let i = 0; i < 260; i++) { x.fillStyle = `rgba(220,228,255,${0.15 + rnd() * 0.45})`; const r = rnd() < 0.1 ? 1.6 : 0.8; x.beginPath(); x.arc(rnd() * w, rnd() * h, r, 0, 7); x.fill(); }
    const groups = [[[60, 70], [120, 40], [170, 92], [230, 60], [270, 120]], [[420, 180], [470, 130], [540, 150], [600, 96], [640, 160], [700, 130]], [[820, 60], [870, 110], [930, 70], [980, 150]]];
    groups.forEach((pts) => { x.strokeStyle = 'rgba(183,156,255,.38)'; x.lineWidth = 1.3; x.beginPath(); pts.forEach(([px, py], i) => (i ? x.lineTo(px, py) : x.moveTo(px, py))); x.stroke(); pts.forEach(([px, py]) => { const sg = x.createRadialGradient(px, py, 0, px, py, 9); sg.addColorStop(0, 'rgba(255,255,255,1)'); sg.addColorStop(0.3, 'rgba(207,224,255,.7)'); sg.addColorStop(1, 'rgba(207,224,255,0)'); x.fillStyle = sg; x.beginPath(); x.arc(px, py, 9, 0, 7); x.fill(); }); });
    x.fillStyle = 'rgba(183,156,255,.55)'; x.fillRect(0, h - 5, w, 2); x.fillStyle = 'rgba(127,227,255,.35)'; x.fillRect(0, 3, w, 1.5);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8; return (_const = t);
  }
  const faceCache = {};
  const tileKit = createTileKit(); let tileSkin = o.tileSkin || 'classic';
  function faceMat(c, n) { return tileKit.faceMat(tileSkin, c, n, font); }


  // ---------- room ----------
  const floor = mk('zemin', new THREE.PlaneGeometry(7, 5.4).rotateX(-Math.PI / 2), M.floor, 0, 0, 0.1); M.floor.map = floorTex(); floor.castShadow = false; scene.add(floor);
  const sill = mk('pencere_esik', new THREE.BoxGeometry(7, 0.14, 0.12), M.dark, 0, 0.07, -2.62); scene.add(sill);
  const sillLed = mk('esik_led', new THREE.BoxGeometry(7, 0.008, 0.01), M.led, 0, 0.142, -2.57); scene.add(sillLed);
  [-2.35, -1.2, 1.2, 2.35].forEach((x, i) => { scene.add(mk('dikme' + i, new THREE.BoxGeometry(0.07, 3.2, 0.09), M.dark, x, 1.6, -2.64)); scene.add(mk('dikme_led' + i, new THREE.BoxGeometry(0.008, 3.0, 0.01), M.led, x, 1.65, -2.59)); });
  M.wall.map = wallTex(); M.wall.emissiveMap = wallEmis(); M.wall.emissive = new THREE.Color('#FFC94A'); M.wall.emissiveIntensity = 0.5;
  [-1, 1].forEach((sx) => { const w = mk('duvar', new THREE.PlaneGeometry(5.2, 3), M.wall, sx * 2.6, 1.5, -0.1); w.rotation.y = -sx * Math.PI / 2; scene.add(w); scene.add(mk('kose', new THREE.BoxGeometry(0.14, 3.2, 0.14), M.dark, sx * 2.56, 1.6, -2.6)); });
  const halo = mk('masa_hale', new THREE.PlaneGeometry(3.6, 3.6).rotateX(-Math.PI / 2), M.halo, 0, 0.004, 0); halo.castShadow = halo.receiveShadow = false; scene.add(halo);

  // ---------- table ----------
  const slab = mk('masa_govde', new THREE.ExtrudeGeometry(rrShape(HALF * 2, HALF * 2, 0.2), { depth: 0.05, bevelEnabled: true, bevelThickness: 0.01, bevelSize: 0.01, bevelSegments: 3, curveSegments: 18 }).rotateX(-Math.PI / 2), TM.slab, 0, TY - 0.062, 0); scene.add(slab);
  const felt = mk('cuha', flat(rrShape(FELT * 2, FELT * 2, 0.13), 18), M.felt, 0, TY, 0); felt.castShadow = false; scene.add(felt);
  const rimM = mk('masa_kenar', new THREE.ExtrudeGeometry(rrRing(HALF * 2, 0.2, FELT * 2, 0.13), { depth: 0.026, bevelEnabled: true, bevelThickness: 0.007, bevelSize: 0.007, bevelSegments: 3, curveSegments: 18 }).rotateX(-Math.PI / 2), TM.rim, 0, TY - 0.006, 0); scene.add(rimM);
  const band = mk('masa_led', new THREE.ExtrudeGeometry(rrRing(HALF * 2 + 0.03, 0.215, HALF * 2 + 0.004, 0.2), { depth: 0.008, bevelEnabled: false, curveSegments: 18 }).rotateX(-Math.PI / 2), TM.band, 0, TY - 0.05, 0); band.castShadow = false; scene.add(band);
  const inner = mk('cuha_led', flat(rrRing(FELT * 2 - 0.012, 0.124, FELT * 2 - 0.022, 0.119), 18), TM.inner, 0, TY + 0.0008, 0); inner.castShadow = false; scene.add(inner);
  scene.add(mk('ayak', new THREE.CylinderGeometry(0.16, 0.22, TY - 0.08, 40), M.dark, 0, (TY - 0.08) / 2, 0));
  scene.add(mk('taban', new THREE.CylinderGeometry(0.55, 0.6, 0.04, 64), M.steel, 0, 0.02, 0));
  const footLed = mk('taban_led', new THREE.TorusGeometry(0.585, 0.006, 8, 96), M.led, 0, 0.036, 0); footLed.rotation.x = Math.PI / 2; scene.add(footLed);
  const [hc, hx] = cvs(512, 512); hx.strokeStyle = '#fff'; hx.lineWidth = 6; for (let i = 0; i < 48; i++) { if (i % 6 === 5) continue; hx.beginPath(); hx.arc(256, 256, 240, (i / 48) * 6.283, ((i + 0.6) / 48) * 6.283); hx.stroke(); }
  hx.lineWidth = 2; hx.globalAlpha = 0.6; hx.beginPath(); hx.arc(256, 256, 222, 0, 7); hx.stroke();
  const holoTex = new THREE.CanvasTexture(hc); M.holo.map = holoTex;
  const holo = mk('holo_halka', new THREE.PlaneGeometry(0.56, 0.56).rotateX(-Math.PI / 2), M.holo, 0, TY + 0.0015, 0); holo.castShadow = holo.receiveShadow = false; scene.add(holo);

  // ---------- seats, racks, chairs ----------
  const seatF = YAW.map((y) => { const f = new THREE.Object3D(); f.rotation.y = y; scene.add(f); return f; });
  const rackF = seatF.map((sf, k) => {
    const r = new THREE.Group(); r.name = 'istaka' + k; r.position.set(0, TY + 0.033, RZ); r.rotation.x = TILT; sf.add(r);
    return r;
  });
  const chairs = [1, 2, 3].map((k, i) => {
    const c = new THREE.Group(); c.name = 'koltuk' + k; c.position.set(0, 0, 1.32); seatF[k].add(c);
    c.add(mk('koltuk_ayak', new THREE.CylinderGeometry(0.035, 0.035, 0.4, 16), M.steel, 0, 0.22, 0));
    c.add(mk('koltuk_taban', new THREE.CylinderGeometry(0.26, 0.28, 0.025, 40), M.dark, 0, 0.0125, 0));
    c.add(mk('oturak', new THREE.ExtrudeGeometry(rrShape(0.5, 0.46, 0.12), { depth: 0.06, bevelEnabled: true, bevelThickness: 0.015, bevelSize: 0.015, bevelSegments: 3 }).rotateX(-Math.PI / 2), M.leather, 0, 0.43, 0));
    const back = new THREE.Group(); back.position.set(0, 0.5, 0.24); back.rotation.x = 0.14; c.add(back);
    back.add(mk('sirtlik', new THREE.ExtrudeGeometry(rrShape(0.5, 0.46, 0.14), { depth: 0.06, bevelEnabled: true, bevelThickness: 0.015, bevelSize: 0.015, bevelSegments: 3 }), M.leather, 0, 0.34, -0.03));
    back.add(mk('sirt_led', new THREE.BoxGeometry(0.008, 0.28, 0.01), seatLed[i], 0, 0.27, -0.04));
    return c;
  });

  // ---------- tiles ----------
  if (o.tileDepth) TD = o.tileDepth;
  const buildTileGeo = () => { const bv = Math.min(0.0028, TD * 0.2); const g = new THREE.ExtrudeGeometry(rrShape(TW - 2 * bv, TH - 2 * bv, 0.006), { depth: Math.max(0.002, TD - 2 * bv), bevelEnabled: true, bevelThickness: bv, bevelSize: bv, bevelSegments: 2, curveSegments: 5 }); g.center(); return g; };
  let tileGeo = buildTileGeo();
  const faceGeo = new THREE.PlaneGeometry(TW * 0.92, TH * 0.92);
  const barGeo = new THREE.BoxGeometry(TW * 0.78, 0.0035, 0.004);
  const Q_FLAT = new THREE.Quaternion().setFromEuler(new THREE.Euler(-Math.PI / 2, 0, 0));
  const Q_DOWN = new THREE.Quaternion().setFromEuler(new THREE.Euler(Math.PI / 2, 0, 0));
  const tiles = new Map();
  function makeTile(id) {
    const g = new THREE.Group(); g.name = 'tas_' + id;
    const body = new THREE.Mesh(tileGeo, [M.tile, M.tileSide]); body.castShadow = body.receiveShadow = true; body.userData.id = id; g.add(body);
    const face = new THREE.Mesh(faceGeo, M.ok); face.position.z = TD / 2 + 0.0006; face.visible = false; g.add(face);
    const bar = new THREE.Mesh(barGeo, M.ok); bar.position.set(0, -TH / 2 - 0.003, TD / 2 - 0.002); bar.visible = false; g.add(bar);
    scene.add(g); return { id, g, body, face, bar, c: null, n: null, tgt: null, tw: null, lift: 0, liftT: 0, base: null, glow: false, newUntil: 0, pop: 0 };
  }
  function setFace(t, c, n) { if (t.c === c && t.n === n) return; t.c = c; t.n = n; if (c) { t.face.material = faceMat(c, n); t.face.visible = true; } else t.face.visible = false; }
  function poseIn(frame, x, y, z, ql) {
    frame.updateWorldMatrix(true, false);
    const p = new THREE.Vector3(x, y, z).applyMatrix4(frame.matrixWorld), q = frame.getWorldQuaternion(new THREE.Quaternion());
    if (ql) q.multiply(ql); return { p, q };
  }
  const slotLocal = (r, i) => [(i - 7) * SL, r ? 0.045 : 0.128, r ? -0.003 + TD / 2 : -0.022 + TD / 2];
  const jitter = (id, a) => new THREE.Quaternion().setFromAxisAngle(UP, (hash(id) - 0.5) * a);
  function poseFor(d) {
    const [k, s, a, b] = d.at;
    if (k === 'r') { const [x, y, z] = slotLocal(a, b); return poseIn(rackF[s], x, y, z); }
    if (k === 'd') return poseIn(seatF[s], 0.6, TY + TD / 2 + Math.min(a, 4) * TD, 0.6, jitter(d.id, 0.5).multiply(Q_FLAT));
    if (k === 'm') return poseIn(seatF[s], -0.245 + a * 0.049, TY + TD / 2, 0.37 - b * 0.08, Q_FLAT);
    if (k === 'f') return { p: deckTop.clone().add(new THREE.Vector3(0, 0.004, 0)), q: new THREE.Quaternion().setFromAxisAngle(UP, 0.45).multiply(Q_DOWN) };
    if (k === 'i') return poseIn(seatF[0], 0, TY + TH / 2 * Math.cos(0.42) + 0.002, 0.07, new THREE.Quaternion().setFromEuler(new THREE.Euler(-0.42, 0, 0)));
    return poseIn(seatF[0], 0, TY, 0);
  }

  // deck (instanced)
  const deckIM = new THREE.InstancedMesh(tileGeo, [M.tile, M.tileSide], 110); deckIM.name = 'deste'; deckIM.castShadow = deckIM.receiveShadow = true; deckIM.count = 0; scene.add(deckIM);
  const deckHit = new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshBasicMaterial({ visible: false })); scene.add(deckHit);
  const deckGlow = mk('deste_parilti', new THREE.PlaneGeometry(0.5, 0.5).rotateX(-Math.PI / 2), M.halo.clone(), 0, TY + 0.002, 0); deckGlow.castShadow = deckGlow.receiveShadow = false; deckGlow.material.opacity = 0; scene.add(deckGlow);
  let deckShown = -1, deckTop = new THREE.Vector3(0, TY + 0.1, 0), deckAnim = null;
  function layoutDeck(n) {
    n = Math.max(0, Math.min(110, Math.round(n))); if (n === deckShown) return; deckShown = n; deckIM.count = n;
    const stacks = Math.ceil(n / 5), rows = n > 60 ? 4 : n > 25 ? 2 : 1, cols = Math.max(1, Math.ceil(stacks / rows)), m = new THREE.Matrix4(), p = new THREE.Vector3(), one = new THREE.Vector3(1, 1, 1);
    for (let j = 0; j < n; j++) {
      const s = Math.floor(j / 5), lv = j % 5, col = Math.floor(s / rows), row = s % rows;
      p.set((col - (cols - 1) / 2) * (TW + 0.006), TY + TD / 2 + lv * TD, (row - (rows - 1) / 2) * (TH + 0.006));
      m.compose(p, new THREE.Quaternion().setFromAxisAngle(UP, ((j * 0.37) % 1 - 0.5) * 0.06).multiply(Q_DOWN), one); deckIM.setMatrixAt(j, m);
      if (j === n - 1) deckTop.copy(p).y += TD;
    }
    if (!n) deckTop.set(0, TY + TD, 0);
    deckIM.instanceMatrix.needsUpdate = true; deckIM.computeBoundingSphere();
    deckHit.scale.set(cols * (TW + 0.006) + 0.04, 0.12, rows * (TH + 0.006) + 0.04); deckHit.position.set(0, TY + 0.06, 0);
    deckGlow.scale.setScalar(Math.max(0.6, cols * 0.18));
  }
  layoutDeck(106);

  // discard zone + side ring
  const zone = mk('atma_halka', new THREE.RingGeometry(0.058, 0.066, 48).rotateX(-Math.PI / 2), M.zone, 0, 0, 0); zone.castShadow = false;
  const zoneFill = mk('atma_dolgu', new THREE.CircleGeometry(0.058, 40).rotateX(-Math.PI / 2), M.zoneFill, 0, 0, 0); zoneFill.castShadow = false;
  const zp = poseIn(seatF[0], 0.6, TY + 0.0012, 0.6).p; zone.position.copy(zp); zoneFill.position.copy(zp); scene.add(zone, zoneFill);
  const sideRing = zone.clone(); sideRing.material = M.zone.clone(); sideRing.material.opacity = 0; sideRing.position.copy(poseIn(seatF[3], 0.6, TY + 0.0012, 0.6).p); scene.add(sideRing);

  // ---------- lights ----------
  const hemi = new THREE.HemisphereLight('#c9d6ff', '#0a0a14', 0.45); scene.add(hemi);
  const key = new THREE.DirectionalLight('#fff4e2', 1.7); key.position.set(1.1, 4.2, 2.0); key.target.position.set(0, TY, 0);
  key.castShadow = true; key.shadow.mapSize.set(2048, 2048); Object.assign(key.shadow.camera, { left: -1.5, right: 1.5, top: 1.5, bottom: -1.5, near: 1, far: 8 }); key.shadow.bias = -0.0004; key.shadow.normalBias = 0.015;
  scene.add(key, key.target);
  const spot = new THREE.SpotLight('#ffffff', 9, 7, 0.62, 0.7, 1.6); spot.position.set(0, 3.2, 0.3); spot.target.position.set(0, TY, 0); scene.add(spot, spot.target);
  const back = new THREE.PointLight('#FFC94A', 5, 7, 1.4); back.position.set(0, 1.5, -2.3); scene.add(back);
  const fill = new THREE.PointLight('#9fb6ff', 1.6, 6, 1.5); fill.position.set(0, 1.6, 2.2); scene.add(fill);

  // ---------- gifts + fx ----------
  const kit = giftKit();
  const gifts = [[], [], [], []], fx = [];
  const GSLOT = [[-0.43, 0.57], [-0.3, 0.61], [-0.46, 0.44]], GSLOT_ME = [[-0.58, 0.42], [-0.6, 0.27], [-0.47, 0.5]];
  function burst(pos, color, n = 28, spd = 0.55) {
    const geo = new THREE.BufferGeometry(), arr = new Float32Array(n * 3), vel = [];
    for (let i = 0; i < n; i++) { arr.set([pos.x, pos.y, pos.z], i * 3); const a = Math.random() * 6.283, e = Math.random() * 1.2 + 0.2; vel.push(new THREE.Vector3(Math.cos(a) * Math.cos(e), Math.sin(e) * 1.3, Math.sin(a) * Math.cos(e)).multiplyScalar(spd * (0.5 + Math.random()))); }
    geo.setAttribute('position', new THREE.BufferAttribute(arr, 3));
    const pts = new THREE.Points(geo, new THREE.PointsMaterial({ size: 0.022, map: glowTex(), color, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending }));
    scene.add(pts); fx.push({ kind: 'burst', o: pts, vel, t0: now, life: 0.9 });
  }
  let shakeUntil = 0;
  function smoke(pos) {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: '#C9D0E6', transparent: true, opacity: 0.32, depthWrite: false }));
    s.position.copy(pos).add(new THREE.Vector3((Math.random() - 0.5) * 0.02, (Math.random() - 0.5) * 0.02, (Math.random() - 0.5) * 0.02)); s.scale.setScalar(0.02); scene.add(s); fx.push({ kind: 'smoke', o: s, t0: now, life: 1.3 });
  }
  function wave(pos, color) {
    const m = new THREE.Mesh(new THREE.RingGeometry(0.85, 1, 56).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending }));
    m.position.copy(pos).y += 0.002; scene.add(m); fx.push({ kind: 'wave', o: m, t0: now, life: 0.7 });
  }
  function spark(pos, color) {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending }));
    s.position.copy(pos); s.scale.setScalar(0.03); scene.add(s); fx.push({ kind: 'spark', o: s, t0: now, life: 0.5 });
  }
  const originOf = (k) => (k === 0 ? poseIn(seatF[0], -0.6, 1.1, 1.25).p : poseIn(seatF[k], 0, 1.15, 1.1).p);
  function sendGift(kind, from, to) {
    const build = kit.B[kind]; if (!build) return;
    let slot = [0, 1, 2].find((s) => !gifts[to].some((g) => g.slot === s && g.state !== 'out'));
    if (slot == null) { const old = gifts[to].find((g) => g.state !== 'out'); old.state = 'out'; old.t1 = now; slot = old.slot; }
    const g = build(); g.traverse((m) => { if (m.isMesh && m.material && m.material.blending === THREE.AdditiveBlending) m.castShadow = false; });
    const GS = to === 0 ? GSLOT_ME : GSLOT, end = poseIn(seatF[to], GS[slot][0], TY, GS[slot][1]).p, start = originOf(from);
    let curve = null;
    if (kind === 'rocket') {
      const C = (nx, ny, dist) => { const v = new THREE.Vector3(nx, ny, 0.5).unproject(camera).sub(camera.position).normalize(); return camera.position.clone().addScaledVector(v, dist); };
      curve = new THREE.CatmullRomCurve3([start.clone(), start.clone().add(new THREE.Vector3(0, 0.32, 0)), C(-0.78, 0.62, 1.7), C(-0.05, 0.86, 1.15), C(0.82, 0.5, 1.0), C(0.62, -0.5, 0.78), C(-0.15, -0.62, 0.7), C(-0.72, -0.1, 0.95), C(-0.25, 0.42, 1.6), end.clone().add(new THREE.Vector3(0, 0.42, 0)), end.clone().add(new THREE.Vector3(0, 0.07, 0)), end.clone()], false, 'centripetal');
    }
    const yaw = YAW[to] + (hash(kind + slot) - 0.5) * 0.8 + (kind === 'coffee' || kind === 'tea' ? -0.6 : 0);
    g.position.copy(start); g.scale.setScalar(0.001); scene.add(g);
    if (g.userData.flame && g.userData.rocket) g.userData.flame.visible = true;
    gifts[to].push({ kind, g, slot, start, end, yaw, curve, s: GIFT_SCALE[kind] || 1.4, t0: now, dur: kind === 'rocket' ? 5.6 : 1.35, state: 'fly', seed: Math.random() * 6 });
    return gifts[to].length;
  }
  let deckSpin = -9, holoBoost = 0, winSpotT = -9;
  const winSpot = new THREE.SpotLight('#FFE7A3', 0, 5, 0.32, 0.6, 1.4); scene.add(winSpot, winSpot.target);
  function dealFx() {
    const c = new THREE.Vector3(0, TY, 0);
    wave(c, '#FFD964'); setTimeout(() => wave(c, theme.a1), 170); setTimeout(() => wave(c, '#ffffff'), 340);
    burst(new THREE.Vector3(0, TY + 0.1, 0), '#FFE08A', 50, 0.7); burst(new THREE.Vector3(0, TY + 0.1, 0), theme.a1, 30, 0.5);
    deckSpin = now; holoBoost = now + 2.6;
  }
  function celebrate(k) {
    const tp = k === 0 ? poseIn(seatF[0], 0, TY, 0.5).p : poseIn(seatF[k], 0, TY, 0.6).p, hp = k === 0 ? poseIn(seatF[0], 0, TY + 0.25, 0.75).p : anchorWorld['a' + k]();
    winSpot.position.copy(tp).add(new THREE.Vector3(0, 1.8, 0)); winSpot.target.position.copy(tp); winSpotT = now;
    const cols = ['#FFD964', '#FF6A9A', '#7FE3FF', '#8EF0B0', '#B79CFF'];
    for (let i = 0; i < 8; i++) setTimeout(() => { burst(hp.clone().add(new THREE.Vector3((Math.random() - 0.5) * 0.3, 0.05, (Math.random() - 0.5) * 0.3)), cols[i % 5], 36, 0.9); if (i % 2 === 0) wave(tp, cols[i % 5]); }, i * 240);
    if (k !== 0) { const v = VIEWS[view] || VIEWS.seat; VIEWS.win = { p: v.p.clone().lerp(tp, 0.16).add(new THREE.Vector3(0, -0.04, 0)), t: v.t.clone().lerp(tp, 0.5) }; setCamera('win', 1.3); }
  }
  function clearGifts() { gifts.forEach((arr) => arr.forEach((g) => { if (g.state !== 'out') { g.state = 'out'; g.t1 = now; } })); }
  const tmpV = new THREE.Vector3(), tmpV2 = new THREE.Vector3();
  function giftPos(g, u, out) {
    const e = easeIO(cl(u, 0, 1));
    out.lerpVectors(g.start, g.end, e);
    if (g.curve) { const uu = cl(u, 0, 1), k = uu * uu * (3 - 2 * uu) * 0.35 + uu * 0.65; return g.curve.getPointAt(cl(k, 0, 1), out); }
    out.y += Math.sin(Math.PI * e) * 0.38;
    return out;
  }
  function tickGifts(dt, t) {
    for (let k = 0; k < 4; k++) {
      const arr = gifts[k];
      for (let i = arr.length - 1; i >= 0; i--) {
        const g = arr[i], ud = g.g.userData;
        if (g.state === 'fly') {
          const u = (now - g.t0) / g.dur;
          giftPos(g, u, g.g.position);
          if (g.kind === 'rocket') {
            giftPos(g, Math.min(1, u + 0.008), tmpV); tmpV.sub(g.g.position);
            const up = Math.min(1, Math.max(0, (u - 0.9) / 0.1));
            if (tmpV.lengthSq() > 1e-10) { const q = new THREE.Quaternion().setFromUnitVectors(UP, tmpV.normalize()).multiply(new THREE.Quaternion().setFromAxisAngle(UP, u * 16)); const qe = new THREE.Quaternion().setFromAxisAngle(UP, g.yaw); g.g.quaternion.copy(q).slerp(qe, ease(up)); }
            spark(g.g.position, Math.random() < 0.5 ? '#FFB347' : '#FF6A3D'); if (Math.random() < 0.5) spark(g.g.position, '#FFF1C0');
            if (Math.random() < 0.55) smoke(g.g.position);
            g.g.scale.setScalar(g.s * Math.min(1, 0.4 + u * 6));
          } else {
            g.g.rotation.set(0, g.yaw + (1 - easeIO(cl(u, 0, 1))) * Math.PI * 3, Math.sin(u * Math.PI) * 0.25);
            g.g.scale.setScalar(g.s * (0.35 + 0.65 * Math.min(1, u * 2.2)));
            if (Math.random() < 0.35) spark(g.g.position, '#FFE08A');
          }
          if (u >= 1) { g.state = 'land'; g.tl = now; g.g.position.copy(g.end); g.g.rotation.set(0, g.yaw, 0); burst(g.end.clone().setY(TY + 0.04), g.kind === 'rocket' ? '#FFB347' : '#FFE08A'); wave(g.end, '#FFD964'); if (ud.rocket) { ud.flame.visible = false; burst(g.end.clone().setY(TY + 0.06), '#7FE3FF', 40, 0.9); burst(g.end.clone().setY(TY + 0.06), '#FF7AD0', 30, 0.8); setTimeout(() => wave(g.end, '#7FE3FF'), 140); shakeUntil = now + 0.4; } }
        } else if (g.state === 'land') {
          const v = (now - g.tl) / 0.5, sq = Math.sin(Math.PI * Math.min(1, v)) * (1 - Math.min(1, v));
          g.g.scale.set(g.s * (1 + 0.18 * sq), g.s * (1 - 0.3 * sq), g.s * (1 + 0.18 * sq));
          if (v >= 1) { g.state = 'idle'; g.g.scale.setScalar(g.s); }
        } else if (g.state === 'out') {
          const v = (now - g.t1) / 0.35; g.g.scale.setScalar(g.s * Math.max(0.001, 1 - v) * (1 + Math.sin(v * Math.PI) * 0.2));
          if (v >= 1) { burst(g.g.position.clone().setY(TY + 0.03), '#ffffff', 14, 0.3); scene.remove(g.g); arr.splice(i, 1); continue; }
        }
        if (g.state === 'idle' || g.state === 'land') {
          if (ud.steam) ud.steam.forEach((s, j) => { const ph = (t * 0.32 + j / ud.steam.length + g.seed) % 1; s.position.set(Math.sin(t * 1.7 + j * 2) * 0.006 * ph, s.userData.y + ph * 0.075, Math.cos(t * 1.3 + j) * 0.004 * ph); s.material.opacity = 0.2 * Math.sin(Math.PI * ph); s.scale.setScalar(0.014 + ph * 0.03); });
          if (ud.sway) ud.sway.rotation.set(Math.sin(t * 1.1 + g.seed) * 0.04, 0, Math.sin(t * 1.4 + g.seed) * 0.06);
          if (ud.flame && !ud.rocket) { const f = 1 + Math.sin(t * 23 + g.seed) * 0.12 + Math.sin(t * 37) * 0.08; ud.flame.scale.set(1, f, 1); }
          if (ud.spin) ud.spin.rotation.y += dt * 0.7;
          if (ud.bubbles) ud.bubbles.forEach((b, j) => { const ph = (t * 0.25 + j * 0.1 + g.seed) % 1; b.position.set(Math.cos(b.userData.a + t) * b.userData.r, 0.018 + ph * 0.078, Math.sin(b.userData.a + t) * b.userData.r); b.scale.setScalar(0.6 + ph * 0.8); });
          if (ud.rocket) g.g.position.y = g.end.y + 0.004 + Math.sin(t * 2 + g.seed) * 0.003;
        }
        if (ud.rocket && ud.flame.visible) { const f = 1 + Math.sin(t * 40) * 0.2; ud.flame.scale.set(f, 1 + Math.sin(t * 31) * 0.25, f); }
      }
    }
    for (let i = fx.length - 1; i >= 0; i--) {
      const f = fx[i], u = (now - f.t0) / f.life;
      if (u >= 1) { scene.remove(f.o); f.o.geometry && f.o.geometry.dispose(); f.o.material.dispose(); fx.splice(i, 1); continue; }
      if (f.kind === 'burst') { const a = f.o.geometry.attributes.position; for (let j = 0; j < f.vel.length; j++) { f.vel[j].y -= 1.4 * dt; a.array[j * 3] += f.vel[j].x * dt; a.array[j * 3 + 1] += f.vel[j].y * dt; a.array[j * 3 + 2] += f.vel[j].z * dt; } a.needsUpdate = true; f.o.material.opacity = 1 - u; }
      else if (f.kind === 'wave') { f.o.scale.setScalar(0.03 + ease(u) * 0.16); f.o.material.opacity = 0.9 * (1 - u); }
      else if (f.kind === 'smoke') { f.o.material.opacity = 0.32 * (1 - u); f.o.scale.setScalar(0.02 + ease(u) * 0.11); f.o.position.y += dt * 0.03; }
      else { f.o.material.opacity = 0.8 * (1 - u); f.o.scale.setScalar(0.03 * (1 - u * 0.6)); }
    }
  }

  // ---------- anchors ----------
  const anchors = {};
  const anchorWorld = { a1: () => (view === 'top' ? poseIn(seatF[1], 0, 0.8, 1.22) : poseIn(seatF[1], 0, 0.86, 0.98)).p, a2: () => (view === 'top' ? poseIn(seatF[2], 0.56, 0.86, 0.98) : poseIn(seatF[2], 0, 1.16, 1.14)).p, a3: () => (view === 'top' ? poseIn(seatF[3], 0, 0.8, 1.22) : poseIn(seatF[3], 0, 0.86, 0.98)).p, disc: () => poseIn(seatF[0], 0.6, TY + 0.02, 0.6).p, ind: () => poseIn(seatF[0], 0, TY, 0.11).p, deck: () => new THREE.Vector3(0, TY + 0.13, 0) };
  function bindAnchor(k, el) { if (!el) { delete anchors[k]; return; } anchors[k] = { el, w: anchorWorld[k] ? anchorWorld[k]() : new THREE.Vector3() }; }
  function project(w) { tmpV2.copy(w).project(camera); return { x: (tmpV2.x + 1) / 2 * W, y: (1 - tmpV2.y) / 2 * H, z: tmpV2.z }; }
  const clampA = (k, s) => { if (k[0] === 'a') { s.x = cl(s.x, 84, W - 84); s.y = cl(s.y, 150, H - 140); } return s; };
  function anchorXY(k) { if (k === 0 || k === 'me') return { x: 55, y: 650 }; const key = typeof k === 'number' ? 'a' + k : k, a = anchorWorld[key]; return a ? clampA(key, project(a())) : { x: 640, y: 360 }; }

  // ---------- sync ----------
  let pendingSpawn = null, ghost = null, clearOvrOnSync = false;
  const desc = new Map();
  function startTween(t, tgt, delay, durOverride, mode, fxk) {
    const p0 = t.g.position.clone(), q0 = t.g.quaternion.clone(), dist = p0.distanceTo(tgt.p), slide = mode === 'slide';
    t.tw = { p0, q0, p1: tgt.p.clone(), q1: tgt.q.clone(), t0: now + (delay || 0) / 1000, slide, fx: fxk || null,
      dur: durOverride || (slide ? cl(0.18 + dist * 0.6, 0.18, 0.34) : cl(0.42 + dist * 0.38, 0.42, 0.85)), arc: slide ? 0 : cl(dist * 0.42, 0.05, 0.24) };
    if (mode === 'deal') {
      const a = hash(t.id) * 6.283, rr = 0.16 + hash(t.id + 'r') * 0.08;
      t.tw.c1 = new THREE.Vector3(Math.cos(a) * rr, TY + 0.3 + hash(t.id + 'y') * 0.12, Math.sin(a) * rr);
      t.tw.c2 = tgt.p.clone().lerp(new THREE.Vector3(0, tgt.p.y, 0), 0.3); t.tw.c2.y += 0.22;
      t.tw.dur = 0.78; t.tw.arc = 0; t.tw.spin = (hash(t.id + 's') > 0.5 ? 1 : -1) * Math.PI * 2;
    }
  }
  function sync(list, flags, opts = {}) {
    lastList = list; Object.assign(fl, flags || {});
    if (clearOvrOnSync) { clearOvrOnSync = false; tiles.forEach((t) => { t.ovr = null; }); }
    const seen = new Set();
    for (const d of list) {
      const prev = desc.get(d.id);
      seen.add(d.id); desc.set(d.id, d);
      let t = tiles.get(d.id), fresh = false;
      if (!t) {
        t = makeTile(d.id); tiles.set(d.id, t); fresh = true;
        const sp = pendingSpawn || { p: deckTop.clone(), q: Q_DOWN.clone() };
        t.g.position.copy(sp.p); t.g.quaternion.copy(sp.q); if (d.dl) t.g.visible = false;
      }
      setFace(t, d.c || null, d.n || null);
      t.glow = !!d.glow;
      const tgt = poseFor(d);
      const changed = !t.tgt || t.tgt.p.distanceToSquared(tgt.p) > 1e-8 || 1 - Math.abs(t.tgt.q.dot(tgt.q)) > 1e-6;
      if (changed) {
        t.tgt = tgt;
        if (drag && (drag.t === t || (drag.groupSet && drag.groupSet.has(t)))) continue;
        if (opts.instant) { t.g.position.copy(tgt.p); t.g.quaternion.copy(tgt.q); t.tw = null; t.g.visible = true; }
        else {
          const slide = prev && prev.at[0] === 'r' && d.at[0] === 'r' && prev.at[1] === d.at[1];
          const fxk = d.dl || slide ? null : d.at[0] === 'd' ? 'disc' : d.at[0] === 'r' && d.at[1] === 0 && !slide ? 'mine' : d.at[0] === 'm' ? 'meld' : 'travel';
          const dealing = fresh && d.dl;
          startTween(t, tgt, d.dl || 0, 0, dealing ? 'deal' : slide ? 'slide' : 'travel', d.at[0] === 'i' ? 'reveal' : d.at[0] === 'f' ? 'finish' : dealing ? null : fxk);
        }
      }
    }
    for (const [id, t] of tiles) if (!seen.has(id)) { scene.remove(t.g); tiles.delete(id); desc.delete(id); }
    pendingSpawn = null; if (ghost) { scene.remove(ghost.g); ghost = null; }
    if (flags && flags.deckAnim) deckAnim = { a: flags.deckAnim[0], b: flags.deckAnim[1], t0: now + (flags.deckAnim[3] || 0) / 1000, dur: flags.deckAnim[2] / 1000 };
    else if (!deckAnim) layoutDeck(fl.deck);
    seatLed.forEach((m, i) => { m.emissiveIntensity = fl.seats[i + 1] ? 2.4 : 0.25; });
    chairs.forEach((c, i) => { c.visible = true; });
  }

  // ---------- pointer ----------
  const ray = new THREE.Raycaster(), ndc = new THREE.Vector2(), tablePlane = new THREE.Plane(UP.clone(), -TY), rackPlane = new THREE.Plane();
  let drag = null, hoverId = null, selId = null, lastClick = { id: null, t: 0 };
  function setRay(e) { const r = canvas.getBoundingClientRect(); ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1); ray.setFromCamera(ndc, camera); }
  function rackHit() {
    const q = rackF[0].getWorldQuaternion(new THREE.Quaternion()), n = new THREE.Vector3(0, 0, 1).applyQuaternion(q), p = rackF[0].getWorldPosition(new THREE.Vector3());
    rackPlane.setFromNormalAndCoplanarPoint(n, p); const hit = ray.ray.intersectPlane(rackPlane, new THREE.Vector3()); return hit ? rackF[0].worldToLocal(hit) : null;
  }
  function slotAt(l) { if (!l || Math.abs(l.x) > 0.47 || l.y < -0.05 || l.y > 0.24) return null; return { r: l.y > 0.087 ? 0 : 1, i: cl(Math.round(l.x / SL + 7), 0, 14) }; }
  const mineIds = () => [...desc.values()].filter((d) => d.at[0] === 'r' && d.at[1] === 0).map((d) => d.id);
  function pick() {
    const objs = [];
    if (fl.dragRack) mineIds().forEach((id) => tiles.get(id) && objs.push(tiles.get(id).body));
    if (fl.canDraw) { objs.push(deckHit); if (fl.sideId && tiles.get(fl.sideId)) objs.push(tiles.get(fl.sideId).body); }
    const h = ray.intersectObjects(objs, false)[0]; if (!h) return null;
    if (h.object === deckHit) return { kind: 'deck' };
    const id = h.object.userData.id; if (id === fl.sideId) return { kind: 'side', id, t: tiles.get(id) };
    const d = desc.get(id); return { kind: 'rack', id, t: tiles.get(id), from: { r: d.at[2], i: d.at[3] }, seg: d.seg };
  }
  function inZone() { const p = ray.ray.intersectPlane(tablePlane, new THREE.Vector3()); return p && p.distanceTo(zone.position) < 0.1; }
  function onDown(e) {
    if (e.button !== 0) return; setRay(e);
    const p = pick();
    if (!p) { if (selId && fl.canDiscard && inZone()) { const d = desc.get(selId); selId = null; o.onDiscard && o.onDiscard({ r: d.at[2], i: d.at[3] }); } return; }
    drag = { ...p, sx: e.clientX, sy: e.clientY, moved: false, pose: null };
    if (p.kind === 'rack' && p.seg) { if (e.shiftKey) startGroup(); else drag.holdT = setTimeout(startGroup, 360); }
    try { canvas.setPointerCapture(e.pointerId); } catch (err) {}
  }
  function followPose(di = 0) {
    const l = rackHit();
    if (l && l.y > -0.07 && l.y < 0.3 && Math.abs(l.x) < 0.56) return poseIn(rackF[0], cl(l.x, -0.43, 0.43) + di * SL, cl(l.y, 0.04, 0.135), 0.05);
    const p = ray.ray.intersectPlane(tablePlane, new THREE.Vector3()); if (!p) return null;
    const q = seatF[0].getWorldQuaternion(new THREE.Quaternion());
    p.y += 0.05; p.add(new THREE.Vector3(di * SL, 0, 0).applyQuaternion(q)); return { p, q: q.multiply(Q_FLAT) };
  }
  function planRow(row, st, n, N) {
    const tries = [[1, 1], [1, 0], [0, 1], [0, 0]];
    let best = null;
    for (const [bl, br] of tries) {
      const en = st + n - 1, L = st > 0 ? st - 1 - bl : -1, Rr = en < N - 1 ? en + 1 + br : N;
      for (let k = 0; k <= row.length; k++) {
        const place = {}, over = []; let cost = 0, lim = L;
        for (let j = k - 1; j >= 0; j--) { const p = Math.min(row[j].i, lim); if (p < 0) { over.push(row[j]); continue; } place[row[j].id] = p; cost += row[j].i - p; lim = p - 1; }
        lim = Rr;
        for (let j = k; j < row.length; j++) { const p = Math.max(row[j].i, lim); if (p > N - 1) { over.push(row[j]); continue; } place[row[j].id] = p; cost += p - row[j].i; lim = p + 1; }
        const split = k > 0 && k < row.length && row[k - 1].i + 1 === row[k].i ? 60 : 0;
        const score = over.length * 1000 + (2 - bl - br) * 100 + split + cost;
        if (!best || score < best.score) best = { score, place, over };
      }
      if (best && best.over.length === 0 && best.score < 100) break;
    }
    return best;
  }
  function groupPlan(slot) {
    const N = 15, grid = [Array(N).fill(null), Array(N).fill(null)];
    desc.forEach((d, id) => { if (d.at[0] === 'r' && d.at[1] === 0) grid[d.at[2]][d.at[3]] = id; });
    const mem = drag.members, n = mem.length, gi = mem.findIndex((m) => m.di === 0);
    mem.forEach((m) => { grid[m.from.r][m.from.i] = null; });
    const r = slot.r, o2 = 1 - r, place = {}, st = Math.max(0, Math.min(N - n, slot.i - gi));
    const row = grid[r].map((id, i) => (id ? { id, i } : null)).filter(Boolean);
    const res = planRow(row, st, n, N);
    Object.entries(res.place).forEach(([id, i]) => { place[id] = { r, i }; });
    mem.forEach((m, k) => { place[m.t.id] = { r, i: st + k }; });
    const other = grid[o2].slice(); other.forEach((id, i) => { if (id) place[id] = { r: o2, i }; });
    res.over.sort((a, b) => a.i - b.i).forEach((x) => { let b = -1, bd = 99; for (let i = 0; i < N; i++) if (!other[i] && Math.abs(i - x.i) < bd) { bd = Math.abs(i - x.i); b = i; } if (b >= 0) { other[b] = x.id; place[x.id] = { r: o2, i: b }; } });
    return place;
  }
  function singlePlan(slot) {
    const N = 15, grid = [Array(N).fill(null), Array(N).fill(null)];
    desc.forEach((d, id) => { if (d.at[0] === 'r' && d.at[1] === 0) grid[d.at[2]][d.at[3]] = id; });
    grid[drag.from.r][drag.from.i] = null;
    const r = slot.r, row = grid[r], i = slot.i;
    if (row[i]) {
      let lE = -1, rE = -1; for (let k = i - 1; k >= 0; k--) if (!row[k]) { lE = k; break; } for (let k = i + 1; k < N; k++) if (!row[k]) { rE = k; break; }
      const goR = rE >= 0 && (lE < 0 || rE - i <= i - lE);
      if (goR) { for (let k = rE; k > i; k--) row[k] = row[k - 1]; }
      else if (lE >= 0) { for (let k = lE; k < i; k++) row[k] = row[k + 1]; }
      else { const o2 = grid[1 - r]; let b = -1, bd = 99; for (let k = 0; k < N; k++) if (!o2[k] && Math.abs(k - i) < bd) { bd = Math.abs(k - i); b = k; } if (b >= 0) o2[b] = row[i]; }
    }
    row[i] = drag.id;
    const place = {}; grid.forEach((rw, rr) => rw.forEach((id, k) => { if (id) place[id] = { r: rr, i: k }; }));
    return place;
  }
  function previewGroup(slot) {
    const memSet = drag.groupSet || new Set([drag.t]);
    const plan = slot ? (drag.groupActive ? groupPlan(slot) : singlePlan(slot)) : null;
    desc.forEach((d, id) => {
      if (d.at[0] !== 'r' || d.at[1] !== 0) return; const t = tiles.get(id); if (!t || memSet.has(t)) return;
      const p = plan && plan[id], same = !p || (p.r === d.at[2] && p.i === d.at[3]);
      if (same) { if (t.ovr) { t.ovr = null; if (t.tgt) startTween(t, t.tgt, 0, 0.2, 'slide'); } return; }
      const [x, y, z] = slotLocal(p.r, p.i), pose = poseIn(rackF[0], x, y, z);
      if (t.ovr && t.ovr.p.distanceToSquared(pose.p) < 1e-8) return;
      t.ovr = pose; startTween(t, pose, 0, 0.2, 'slide');
    });
    drag.plan = plan;
  }
  function startGroup() {
    if (!drag || drag.kind !== 'rack' || drag.groupActive) return;
    const d0 = desc.get(drag.id); if (!d0 || !d0.seg) return;
    const mem = [...desc.values()].filter((d) => d.seg === d0.seg && d.at[0] === 'r' && d.at[1] === 0).sort((a, b) => a.at[3] - b.at[3]);
    if (mem.length < 2) return;
    drag.groupActive = true; selId = null;
    drag.members = mem.map((d) => ({ t: tiles.get(d.id), di: d.at[3] - d0.at[3], from: { r: d.at[2], i: d.at[3] } })).filter((m) => m.t);
    drag.groupSet = new Set(drag.members.map((m) => m.t));
    drag.moved = true; drag.poses = drag.members.map((m) => followPose(m.di));
    drag.members.forEach((m) => { m.t.tw = null; });
    canvas.style.cursor = 'grabbing'; o.onGroupHint && o.onGroupHint(true);
  }
  function onMove(e) {
    setRay(e);
    if (drag) {
      if (!drag.moved && Math.hypot(e.clientX - drag.sx, e.clientY - drag.sy) > 5) {
        drag.moved = true; selId = null;
        if (drag.kind === 'deck') { ghost = makeTile('_ghost'); ghost.g.position.copy(deckTop); ghost.g.quaternion.copy(Q_DOWN); drag.t = ghost; }
      }
      if (drag.moved && !drag.groupActive && drag.holdT) { clearTimeout(drag.holdT); drag.holdT = null; }
      if (drag.groupActive) { drag.poses = drag.members.map((m) => followPose(m.di)); const sl = slotAt(rackHit()), key = sl ? sl.r + ':' + sl.i : 'none'; if (key !== drag.prevKey) { drag.prevKey = key; previewGroup(sl); } }
      else if (drag.moved) { drag.pose = followPose(); if (drag.kind === 'rack') { const sl = slotAt(rackHit()), sk = sl && !(sl.r === drag.from.r && sl.i === drag.from.i) ? sl.r + ':' + sl.i : 'none'; if (sk !== drag.prevKey) { drag.prevKey = sk; previewGroup(sk === 'none' ? null : sl); } } }
      canvas.style.cursor = 'grabbing'; return;
    }
    const p = pick(); hoverId = p && p.id ? p.id : p && p.kind === 'deck' ? '_deck' : null;
    canvas.style.cursor = p ? 'pointer' : selId && fl.canDiscard && inZone() ? 'pointer' : 'default';
  }
  function onUp(e) {
    if (!drag) return; setRay(e); const d = drag; drag = null; canvas.style.cursor = 'default'; if (d.holdT) clearTimeout(d.holdT);
    if (d.groupActive) {
      o.onGroupHint && o.onGroupHint(false);
      const slot = slotAt(rackHit()), commit = slot && (slot.r !== d.from.r || slot.i !== d.from.i);
      if (commit) {
        const plan = (drag = d, groupPlan(slot)); drag = null;
        d.members.forEach((m) => { const p = plan[m.t.id], [x, y, z] = slotLocal(p.r, p.i), pose = poseIn(rackF[0], x, y, z); m.t.ovr = pose; m.t.g.scale.setScalar(1); startTween(m.t, pose, 0, 0.2, 'slide'); });
        clearOvrOnSync = true;
        o.onGroupMove && o.onGroupMove(d.members.map((m) => m.from), d.from, slot, plan);
      } else {
        tiles.forEach((t) => { if (t.ovr) { t.ovr = null; if (t.tgt) startTween(t, t.tgt, 0, 0.22, 'slide'); } });
        d.members.forEach((m) => { m.t.g.scale.setScalar(1); if (m.t.tgt) startTween(m.t, m.t.tgt, 0, 0.24, 'slide'); });
      }
      return;
    }
    if (!d.moved) {
      if (d.kind === 'deck' || d.kind === 'side') { o.onDraw && o.onDraw(d.kind, null); return; }
      const t = performance.now();
      if (fl.dbl && lastClick.id === d.id && t - lastClick.t < 380 && fl.canDiscard) { selId = null; lastClick = { id: null, t: 0 }; o.onDiscard && o.onDiscard(d.from); return; }
      lastClick = { id: d.id, t }; selId = selId === d.id ? null : d.id; return;
    }
    const slot = slotAt(rackHit());
    if (d.kind === 'rack') {
      const tp = ray.ray.intersectPlane(tablePlane, new THREE.Vector3());
      let meldHit = null; if (!slot && tp && fl.canMeld) { let best = 0.075; for (const [id, dd] of desc) if (dd.at[0] === 'm') { const tt = tiles.get(id); if (!tt) continue; const dist = tt.g.position.distanceTo(tp); if (dist < best) { best = dist; meldHit = id; } } }
      if (fl.canDiscard && inZone()) { o.onDiscard && o.onDiscard(d.from); }
      else if (meldHit) { o.onMeldDrop && o.onMeldDrop(d.from, meldHit); }
      else if (slot && (slot.r !== d.from.r || slot.i !== d.from.i)) {
        drag = d; const plan = singlePlan(slot); drag = null;
        Object.entries(plan).forEach(([id, p]) => { const t = tiles.get(id); if (!t) return; const [x, y, z] = slotLocal(p.r, p.i), pose = poseIn(rackF[0], x, y, z); t.ovr = pose; startTween(t, pose, 0, 0.2, 'slide'); });
        clearOvrOnSync = true;
        if (o.onGroupMove) o.onGroupMove(null, d.from, slot, plan); else o.onMove && o.onMove(d.from, slot);
        return;
      }
      tiles.forEach((t) => { if (t.ovr) { t.ovr = null; if (t.tgt) startTween(t, t.tgt, 0, 0.22, 'slide'); } });
      if (d.t.tgt) startTween(d.t, d.t.tgt, 0, 0.22, 'slide');
      return;
    }
    if (d.kind === 'side' && d.t.tgt) startTween(d.t, d.t.tgt, 0, 0.2);
    if (d.kind === 'deck' && ghost) pendingSpawn = { p: ghost.g.position.clone(), q: ghost.g.quaternion.clone() };
    o.onDraw && o.onDraw(d.kind, slot);
    setTimeout(() => { if (ghost && !drag) { scene.remove(ghost.g); ghost = null; pendingSpawn = null; } }, 600);
  }
  canvas.addEventListener('pointerdown', onDown); canvas.addEventListener('pointermove', onMove); canvas.addEventListener('pointerup', onUp);
  canvas.addEventListener('pointercancel', () => { drag = null; });
  canvas.style.touchAction = 'none';

  // ---------- theme ----------
  const swap = (mesh, mat) => { if (mesh.material !== mat) mesh.material = mat; };
  const P = (o2) => new THREE.MeshPhysicalMaterial(o2), S = (o2) => new THREE.MeshStandardMaterial(o2);
  const SK = {
    wood: { slab: P({ name: 'masa_ceviz_govde', roughness: 0.5, clearcoat: 0.5 }), rim: P({ name: 'masa_ceviz_kenar', roughness: 0.32, clearcoat: 1, clearcoatRoughness: 0.15 }), band: S({ name: 'masa_pirinc', color: '#D9A94A', metalness: 1, roughness: 0.25 }), inner: new THREE.MeshBasicMaterial({ name: 'masa_pirinc_cizgi', color: '#D9B15A', transparent: true, opacity: 0.7 }) },
    holo: { slab: P({ name: 'masa_cam_govde', color: '#070a16', metalness: 0.3, roughness: 0.1, clearcoat: 1 }), rim: P({ name: 'masa_cam_kenar', color: '#1a2448', metalness: 0.15, roughness: 0.03, clearcoat: 1, transparent: true, opacity: 0.42, emissiveIntensity: 0.06, envMapIntensity: 2 }), band: S({ name: 'masa_holo_led', color: '#000', emissiveIntensity: 3.4 }), inner: new THREE.MeshBasicMaterial({ name: 'masa_holo_cizgi', transparent: true, opacity: 0.95 }) },
    andro: { slab: P({ name: 'masa_andromeda_govde', color: '#080a1c', metalness: 0.5, roughness: 0.3, clearcoat: 1 }), rim: P({ name: 'masa_andromeda_kenar', color: '#2a2470', metalness: 0.9, roughness: 0.14, clearcoat: 1, iridescence: 1, iridescenceIOR: 1.35, iridescenceThicknessRange: [300, 520], emissive: '#3a2a90', emissiveIntensity: 0.25 }), band: S({ name: 'masa_andromeda_led', color: '#000', emissive: '#8FB4FF', emissiveIntensity: 2.8 }), inner: new THREE.MeshBasicMaterial({ name: 'masa_andromeda_cizgi', color: '#B79CFF', transparent: true, opacity: 0.85 }) },
    randro: { base: P({ name: 'istaka_andromeda_taban', color: '#0b0d26', metalness: 0.8, roughness: 0.18, clearcoat: 1, iridescence: 1, iridescenceIOR: 1.6, iridescenceThicknessRange: [180, 820] }), ledge: P({ name: 'istaka_andromeda_kenar', color: '#2a2f6a', metalness: 1, roughness: 0.18, iridescence: 1, iridescenceIOR: 1.7, iridescenceThicknessRange: [250, 900] }), back: P({ name: 'istaka_andromeda_arka', color: '#ffffff', roughness: 0.25, metalness: 0.15, clearcoat: 1, clearcoatRoughness: 0.1, emissive: '#ffffff', emissiveIntensity: 0.4 }), cap: P({ name: 'istaka_andromeda_uc', color: '#141038', metalness: 0.8, roughness: 0.18, clearcoat: 1, iridescence: 1, iridescenceIOR: 1.35, iridescenceThicknessRange: [300, 520], emissive: '#6B4FE0', emissiveIntensity: 0.18 }), led: S({ name: 'istaka_andromeda_led', color: '#000', emissive: '#B79CFF', emissiveIntensity: 3 }) },
    rwood: { all: P({ name: 'istaka_ceviz', roughness: 0.34, clearcoat: 1, clearcoatRoughness: 0.18 }), back: P({ name: 'istaka_ceviz_arka', roughness: 0.4, clearcoat: 0.8 }), led: S({ name: 'istaka_pirinc', color: '#E0B04E', metalness: 1, roughness: 0.22 }) },
    rneon: { base: P({ name: 'istaka_neon_taban', color: '#05060c', roughness: 0.12, clearcoat: 1 }), ledge: S({ name: 'istaka_neon_kenar', color: '#05050a', roughness: 0.2, emissiveIntensity: 0.42 }), back: P({ name: 'istaka_neon_arka', color: '#07080f', roughness: 0.1, clearcoat: 1, metalness: 0.3 }), cap: P({ name: 'istaka_neon_uc', color: '#0b0c16', roughness: 0.1, clearcoat: 1, emissiveIntensity: 0.55 }), led: S({ name: 'istaka_neon_led', color: '#000', emissive: '#ffffff', emissiveIntensity: 3 }) },
  };
  const rackMeshes = (role) => { const out = []; rackF.forEach((r, k) => r.children.forEach((m) => { const n = m.name.replace('istaka_', ''); const rl = n === 'taban' ? 'base' : n === 'alt' || n === 'orta' ? 'ledge' : n === 'arka' ? (k ? 'backOpp' : 'back') : n === 'uc' ? 'cap' : n === 'led' ? 'led' : null; if (rl === role) out.push(m); })); return out; };
  function applyLook() {
    const th = theme, acc = th.a1, acc2 = th.a2, ts = skins.table, rs = skins.rack;
    M.led.emissive.set(acc); seatLed.forEach((m) => m.emissive.set(acc)); M.ledLine.color.set(acc); M.zone.color.set(acc); M.zoneFill.color.set(acc); sideRing.material.color.set(acc);
    M.halo.color.set(acc2); deckGlow.material.color.set(acc); M.holo.color.set(acc); M.wall.emissive.set(acc);
    tileKit.applyBody(M.tile, tileSkin, th, M.tileSide); M.steel.color.set(th.rim || '#3b4260'); M.leather.color.set(th.leather || '#1d2240');
    back.color.set(th.glow || acc); hemi.color.set(th.sky || '#c9d6ff');
    if (M.felt.map) M.felt.map.dispose();
    M.felt.map = ts === 'andromeda' ? andromedaTex(1024, 1024, false) : ts === 'holo' ? feltTex(th.f2, '#03050d', acc, 'holo') : ts === 'wood' ? feltTex(th.f1, th.f2, acc, 'wood') : feltTex(th.f1, th.f2, acc); M.felt.needsUpdate = true;
    M.felt.emissive = new THREE.Color(ts === 'holo' ? acc : ts === 'andromeda' ? '#ffffff' : '#000'); M.felt.emissiveMap = ts === 'holo' || ts === 'andromeda' ? M.felt.map : null; M.felt.emissiveIntensity = ts === 'holo' ? 0.35 : ts === 'andromeda' ? 0.5 : 0;
    if (ts === 'wood') { const W1 = woodTex('#5A3220', 'rgba(30,14,6,.8)', 2), W2 = woodTex('#3A2014', 'rgba(15,6,2,.8)', 2); SK.wood.rim.map = W1; SK.wood.slab.map = W2; SK.wood.rim.needsUpdate = SK.wood.slab.needsUpdate = true; swap(slab, SK.wood.slab); swap(rimM, SK.wood.rim); swap(band, SK.wood.band); swap(inner, SK.wood.inner); }
    else if (ts === 'andromeda') { swap(slab, SK.andro.slab); swap(rimM, SK.andro.rim); swap(band, SK.andro.band); swap(inner, SK.andro.inner); }
    else if (ts === 'holo') { SK.holo.rim.emissive.set(acc); SK.holo.band.emissive.set(acc); SK.holo.inner.color.set(acc); swap(slab, SK.holo.slab); swap(rimM, SK.holo.rim); swap(band, SK.holo.band); swap(inner, SK.holo.inner); }
    else { swap(slab, M.dark); swap(rimM, M.steel); swap(band, M.led); swap(inner, M.ledLine); }
    M.holo.opacity = ts === 'holo' ? 0.5 : ts === 'andromeda' ? 0.12 : 0.3; halo.material.opacity = ts === 'wood' ? 0.12 : ts === 'holo' ? 0.4 : 0.25; if (ts === 'andromeda') { M.halo.color.set('#7F9CFF'); M.holo.color.set('#B79CFF'); }
    buildRacks(); rackKit.theme(th);
  }
  const rackKit = createRackKit({ constellationTex });
  let rackKey = '';
  function buildRacks() {
    const key = skins.rack + ':' + TD; if (key === rackKey) return; rackKey = key;
    rackF.forEach((r) => { const old = r.getObjectByName('istaka_govde_grubu'); if (old) { r.remove(old); old.traverse((m) => m.geometry && m.geometry.dispose()); } r.add(rackKit.build(skins.rack, TD)); });
  }
  function setTheme(th) { theme = th; applyLook(); }
  function setTileSkin(k) { if (!k || k === tileSkin) return; tileSkin = k; tileKit.applyBody(M.tile, k, theme, M.tileSide); tiles.forEach((t) => { const c = t.c, n = t.n; t.c = null; setFace(t, c, n); }); }
  function setSkins(sk) { skins = { ...skins, ...sk }; applyLook(); }
  function setOptions(op) { Object.assign(fl, op); }
  let lastList = [];
  function setTileDepth(d) {
    if (!d || Math.abs(d - TD) < 1e-5) return; TD = d; const old = tileGeo; tileGeo = buildTileGeo();
    tiles.forEach((t) => { t.body.geometry = tileGeo; t.face.position.z = TD / 2 + 0.0006; t.bar.position.z = TD / 2 - 0.002; t.tgt = null; });
    deckIM.geometry = tileGeo; deckShown = -1; layoutDeck(fl.deck); old.dispose(); buildRacks();
    const keep = desc; sync(lastList, null, { instant: true });
  }
  function setFont(f) { if (!f || f === font) return; font = f; Object.keys(faceCache).forEach((k) => { faceCache[k].map.dispose(); delete faceCache[k]; }); tiles.forEach((t) => { const c = t.c, n = t.n; t.c = null; setFace(t, c, n); }); applyLook(); }
  applyLook();
  function setCamera(name, dur = 1.6) { if (name === 'player') name = view; if (name === 'seat' || name === 'top') view = name; const v = VIEWS[name]; if (!v) return; if (!dur) { cam.p.copy(v.p); cam.t.copy(v.t); cam.a = null; return; } cam.a = { p: cam.p.clone(), t: cam.t.clone() }; cam.b = v; cam.t0 = now; cam.dur = dur; }

  // ---------- loop ----------
  let now = performance.now() / 1000, last = now, raf = 0, alive = true, visible = true;
  const io = window.IntersectionObserver ? new IntersectionObserver((e) => { visible = e[0].isIntersecting; }) : null; if (io) io.observe(canvas);
  const liftDir = new THREE.Vector3(), qSpin = new THREE.Quaternion();
  function tick() {
    if (!alive) return; raf = requestAnimationFrame(tick);
    if (!visible || document.hidden) { last = performance.now() / 1000; return; }
    frame();
  }
  function frame() {
    now = performance.now() / 1000; const dt = Math.min(0.05, now - last); last = now;
    const t = now;
    if (cam.a) { const u = Math.min(1, (now - cam.t0) / cam.dur), e = easeIO(u); cam.p.lerpVectors(cam.a.p, cam.b.p, e); cam.t.lerpVectors(cam.a.t, cam.b.t, e); if (u >= 1) cam.a = null; }
    camera.position.copy(cam.p); camera.position.x += Math.sin(t * 0.21) * 0.006; camera.position.y += Math.sin(t * 0.33) * 0.004;
    if (now < shakeUntil) { const k = (shakeUntil - now) * 0.03; camera.position.x += (Math.random() - 0.5) * k; camera.position.y += (Math.random() - 0.5) * k; }
    camera.lookAt(cam.t);
    if (deckAnim) { const u = Math.max(0, Math.min(1, (now - deckAnim.t0) / deckAnim.dur)); layoutDeck(deckAnim.a + (deckAnim.b - deckAnim.a) * u); if (u >= 1) { deckAnim = null; layoutDeck(fl.deck); } }
    for (const tl of tiles.values()) {
      if (drag && (drag.t === tl || (drag.groupSet && drag.groupSet.has(tl)))) continue;
      if (tl.tw) {
        const u = (now - tl.tw.t0) / tl.tw.dur;
        if (u >= 0) {
          tl.g.visible = true; const e = ease(Math.min(1, u));
          const e2 = tl.tw.slide ? e : easeIO(Math.min(1, u));
          if (tl.tw.c1) {
            const b = easeIO(Math.min(1, u)), ib = 1 - b, w = tl.tw;
            tl.g.position.set(0, 0, 0).addScaledVector(w.p0, ib * ib * ib).addScaledVector(w.c1, 3 * ib * ib * b).addScaledVector(w.c2, 3 * ib * b * b).addScaledVector(w.p1, b * b * b);
            tl.g.quaternion.slerpQuaternions(w.q0, w.q1, easeIO(Math.min(1, u * 1.1))).premultiply(qSpin.setFromAxisAngle(UP, (1 - b) * w.spin));
            if (u < 0.85 && Math.random() < 0.07) spark(tl.g.position, Math.random() < 0.5 ? '#FFE08A' : theme.a1);
          } else {
          tl.g.position.lerpVectors(tl.tw.p0, tl.tw.p1, e2); tl.g.position.y += Math.sin(Math.PI * Math.min(1, u)) * tl.tw.arc;
          tl.g.quaternion.slerpQuaternions(tl.tw.q0, tl.tw.q1, tl.tw.slide ? e : easeIO(Math.min(1, u * 1.15)));
          }
          if (tl.tw.fx && u < 0.92 && Math.random() < 0.6) spark(tl.g.position, '#FFE08A');
          if (u >= 1) {
            const f = tl.tw.fx, wasDeal = !!tl.tw.c1; tl.tw = null; if (wasDeal) { tl.pop = now; if (Math.random() < 0.35) burst(tl.g.position.clone(), '#FFE08A', 6, 0.14); }
            if (f === 'reveal') { burst(tl.g.position.clone(), '#FFE08A', 40, 0.5); wave(tl.g.position.clone().setY(TY), theme.a1); spark(tl.g.position, '#ffffff'); tl.pop = now; }
            if (f === 'finish') { burst(tl.g.position.clone(), '#FFD964', 60, 0.8); wave(new THREE.Vector3(0, TY, 0), '#FFD964'); setTimeout(() => wave(new THREE.Vector3(0, TY, 0), theme.a1), 180); tl.pop = now; }
            if (f === 'disc' || f === 'meld') { wave(tl.g.position.clone().setY(TY), f === 'meld' ? '#3CCB6E' : '#FFD964'); burst(tl.g.position.clone(), '#FFE08A', 12, 0.25); tl.pop = now; }
            else if (f === 'mine') { tl.newUntil = now + 1.6; tl.pop = now; burst(tl.g.position.clone(), '#FFE08A', 10, 0.18); }
            else if (f === 'travel') tl.pop = now;
          }
        }
      } else if (tl.ovr || tl.tgt) { const b = tl.ovr || tl.tgt; tl.g.position.copy(b.p); tl.g.quaternion.copy(b.q); }
      const isNew = tl.newUntil > now; tl.bar.visible = tl.glow || isNew; tl.bar.material = isNew && !tl.glow ? M.newBar : M.ok; if (isNew) M.newBar.opacity = 0.55 + 0.45 * Math.sin(now * 9);
      const pv = (now - tl.pop) / 0.28; tl.g.scale.setScalar(pv >= 0 && pv < 1 ? 1 + Math.sin(Math.PI * pv) * 0.07 : 1);
      const want = tl.id === selId ? 0.022 : tl.id === hoverId && fl.dragRack ? 0.008 : 0;
      tl.lift += (want - tl.lift) * Math.min(1, dt * 14);
      if (tl.lift > 0.0002 && !tl.tw) { liftDir.set(0, 1, 0).applyQuaternion(tl.g.quaternion); tl.g.position.addScaledVector(liftDir, tl.lift); }
    }
    if (drag && drag.groupActive && drag.poses) drag.members.forEach((m, j) => { const ps = drag.poses[j]; if (!ps) return; m.t.g.visible = true; m.t.g.position.lerp(ps.p, Math.min(1, dt * 20)); m.t.g.quaternion.slerp(ps.q, Math.min(1, dt * 14)); m.t.g.scale.setScalar(1.05); m.t.bar.visible = true; m.t.bar.material = M.ok; });
    if (drag && drag.t && drag.pose && !drag.groupActive) { drag.t.g.visible = true; drag.t.g.position.lerp(drag.pose.p, Math.min(1, dt * 22)); drag.t.g.quaternion.slerp(drag.pose.q, Math.min(1, dt * 14)); }
    const pulse = 0.5 + 0.5 * Math.sin(t * 5);
    const zoneHot = fl.canDiscard; M.zone.opacity = zoneHot ? 0.55 + pulse * 0.45 : 0.16; M.zoneFill.opacity = zoneHot ? 0.12 + pulse * 0.12 : 0; zone.scale.setScalar(zoneHot ? 1 + pulse * 0.08 : 1);
    deckGlow.material.opacity = fl.canDraw ? 0.25 + pulse * 0.35 : 0.0;
    sideRing.material.opacity = fl.canDraw && fl.sideId ? 0.5 + pulse * 0.5 : 0;
    const hb = Math.max(0, holoBoost - now); holo.rotation.y += dt * (0.12 + hb * 1.6); M.holo.opacity = 0.22 + Math.sin(t * 1.3) * 0.06 + Math.min(0.5, hb * 0.25);
    const ds = (now - deckSpin) / 0.9; if (ds >= 0 && ds <= 1) { deckIM.rotation.y = easeIO(ds) * Math.PI * 2; deckIM.position.y = Math.sin(Math.PI * ds) * 0.05; } else if (deckIM.rotation.y) { deckIM.rotation.y = 0; deckIM.position.y = 0; }
    const ws = (now - winSpotT) / 3.4; winSpot.intensity = ws >= 0 && ws <= 1 ? 40 * Math.sin(Math.PI * Math.min(1, ws * 1.6)) * (ws < 0.6 ? 1 : (1 - ws) / 0.4) : 0;
    M.led.emissiveIntensity = 2.2 + Math.sin(t * 1.2) * 0.25;
    tickGifts(dt, t);
    for (const k in anchors) { const a = anchors[k]; if (anchorWorld[k]) a.w = anchorWorld[k](); const s = clampA(k, project(a.w)); a.el.style.transform = `translate3d(${s.x.toFixed(1)}px,${s.y.toFixed(1)}px,0)`; a.el.style.visibility = s.z < 1 ? 'visible' : 'hidden'; }
    renderer.render(scene, camera);
  }
  raf = requestAnimationFrame(tick);

  // ---------- thumbnails ----------
  async function thumbs(kinds, size = 220) {
    const cv = document.createElement('canvas'), r = new THREE.WebGLRenderer({ canvas: cv, antialias: true, alpha: true, preserveDrawingBuffer: true });
    r.setPixelRatio(1); r.setSize(size, size, false); r.toneMapping = THREE.ACESFilmicToneMapping; r.setClearColor(0, 0);
    const s = new THREE.Scene(); s.environment = buildEnv(r); s.environmentIntensity = 0.9;
    s.add(new THREE.HemisphereLight('#ffffff', '#30344a', 1.1)); const dl = new THREE.DirectionalLight('#fff4e2', 2.4); dl.position.set(1, 2, 1.6); s.add(dl);
    const c = new THREE.PerspectiveCamera(30, 1, 0.01, 10), out = {};
    for (const k of kinds) {
      const g = kit.B[k] && kit.B[k](); if (!g) continue; if (g.userData.steam) g.userData.steam.forEach((sp) => g.remove(sp)); s.add(g);
      const box = new THREE.Box3().setFromObject(g), ctr = box.getCenter(new THREE.Vector3()), sz = box.getSize(new THREE.Vector3()).length();
      c.position.set(ctr.x + sz * 0.7, ctr.y + sz * 0.95, ctr.z + sz * 1.5); c.lookAt(ctr);
      r.render(s, c); out[k] = await new Promise((res) => cv.toBlob((b) => res(b ? URL.createObjectURL(b) : ''), 'image/png')); s.remove(g);
    }
    r.dispose(); r.forceContextLoss(); return out;
  }

  // ---------- asset export (GLB for Blender/Unity) ----------
  const TABLE_PARTS = ['masa_govde', 'cuha', 'masa_kenar', 'masa_led', 'cuha_led', 'ayak', 'taban', 'taban_led', 'holo_halka'];
  const ROOM_PARTS = ['zemin', 'pencere_esik', 'esik_led', 'duvar', 'kose', 'dikme0', 'dikme1', 'dikme2', 'dikme3', 'dikme_led0', 'dikme_led1', 'dikme_led2', 'dikme_led3'];
  const cloneParts = (names, gname) => { const g = new THREE.Group(); g.name = gname; scene.children.forEach((m) => { if (m.isMesh && names.includes(m.name)) g.add(m.clone()); }); return g; };
  const stripSprites = (g) => { const rm = []; g.traverse((m) => { if (m.isSprite) rm.push(m); }); rm.forEach((m) => m.parent.remove(m)); if (g.userData.flame) g.userData.flame.visible = true; return g; };
  function makeTileAsset(c, n) {
    const g = new THREE.Group(); g.name = c === 'J' ? 'tas_sahte_okey' : c ? 'tas_' + c + n : 'tas_arka';
    const b = new THREE.Mesh(tileGeo, [M.tile, M.tileSide]); b.name = 'tas_govde'; g.add(b);
    if (c) { const f = new THREE.Mesh(faceGeo, faceMat(c, n)); f.name = 'tas_yuz'; f.position.z = TD / 2 + 0.0006; g.add(f); }
    return g;
  }
  function exportAsset(kind, variant) {
    if (kind === 'table') { setSkins({ table: variant || skins.table }); const g = cloneParts(TABLE_PARTS, 'masa_' + (variant || skins.table)); return g; }
    if (kind === 'rack') { setSkins({ rack: variant || skins.rack }); const g = rackF[0].clone(true); g.name = 'istaka_' + (variant || skins.rack); g.position.set(0, 0, 0); g.rotation.set(TILT, 0, 0); return g; }
    if (kind === 'chair') { const g = chairs[0].clone(true); g.name = 'koltuk'; g.position.set(0, 0, 0); return g; }
    if (kind === 'room') return cloneParts(ROOM_PARTS, 'oda_istasyon');
    if (kind === 'tile') return makeTileAsset(variant ? variant[0] : 'R', variant ? +variant.slice(1) : 7);
    if (kind === 'tiles') {
      if (variant) setTileSkin(variant);
      const g = new THREE.Group(); g.name = 'tas_seti_' + tileSkin;
      ['R', 'B', 'Y', 'K'].forEach((c, ci) => { for (let n = 1; n <= 13; n++) { const t = makeTileAsset(c, n); t.position.set((n - 7) * (TW + 0.01), TH / 2 + ci * (TH + 0.012), 0); g.add(t); } });
      const back = makeTileAsset(null); back.position.set(0, TH / 2 - (TH + 0.012), 0); back.position.x = -(TW + 0.01) / 2; back.rotation.y = Math.PI; g.add(back);
      const jk = makeTileAsset('J', 0); jk.name = 'tas_sahte_okey'; jk.position.set((TW + 0.01) / 2, TH / 2 - (TH + 0.012), 0); g.add(jk);
      return g;
    }
    if (kind === 'gift') { const g = kit.B[variant](); g.name = 'hediye_' + variant; return stripSprites(g); }
    return null;
  }

  const api = {
    renderNow: () => { if (alive) frame(); },
    exportAsset,
    sync, sendGift, clearGifts, dealFx, celebrate, bindAnchor, anchorXY, setTheme, setSkins, setTileSkin, setOptions, setTileDepth, setFont, setScale, setCamera, thumbs,
    giftCount: (k) => gifts[k].filter((g) => g.state !== 'out').length,
    destroy() { alive = false; cancelAnimationFrame(raf); if (io) io.disconnect(); canvas.removeEventListener('pointerdown', onDown); canvas.removeEventListener('pointermove', onMove); canvas.removeEventListener('pointerup', onUp); renderer.dispose(); },
  };
  window.__galaxyTable = api;
  return api;
}
