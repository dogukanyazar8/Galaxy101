// Galaxy 101 — 3D game table (three.js r184). Scene, rack interaction, tile tweens, gifts.
import * as THREE from 'https://unpkg.com/three@0.184.0/build/three.module.js';
import { giftKit, GIFT_SCALE, glowTex } from './gifts3d.js';
import { createRackKit } from './racks3d.js';
import { createTileKit } from './tiles3d.js';

let TD = 0.010;
const TY = 0.76, TW = 0.05, TH = 0.07, SL = 0.053, RZ = 0.8, TILT = -0.36;
const HALF = 0.86, FELT = 0.76, SG = 0.057, PX0 = 0.036, DIE = 0.03;
const BX0 = -0.49, BDX = 0.0515, BZ0 = -0.3, BDZ = 0.0857;   // ortak tahta: 20 sütun × 8 sıra (sütun 0–13 seri/grup, 15–19 çift)
const SUIT = { R: '#D7263D', B: '#1F5FBF', K: '#1B1B1B', Y: '#E39B12' };
const YAW = [0, Math.PI / 2, Math.PI, -Math.PI / 2];
const UP = new THREE.Vector3(0, 1, 0);
const W = 1280, H = 720;
const ease = (t) => 1 - Math.pow(1 - t, 3);
const easeIO = (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);
const easeS = (t) => 0.5 - 0.5 * Math.cos(Math.PI * Math.max(0, Math.min(1, t)));   // sinüs: en düşük tepe hızı, en akıcı geçiş
const ss = (t) => { t = Math.max(0, Math.min(1, t)); return t * t * t * (t * (t * 6 - 15) + 10); };   // smootherstep: yumuşak hızlanma/yavaşlama
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
  const pcam = new THREE.PerspectiveCamera(40, W / H, 0.05, 40), OZ = 0.2, OH = 1.17;
  const ocam = new THREE.OrthographicCamera(-OH * W / H, OH * W / H, OH, -OH, 0.1, 20); ocam.up.set(0, 0, -1); ocam.position.set(0, 6, OZ); ocam.lookAt(0, TY, OZ);   // 2D: tepeden düz görünüm
  let camera = pcam, flat2d = false;
  const VIEWS = { deal: { p: new THREE.Vector3(0, 2.05, 1.62), t: new THREE.Vector3(0, 0.66, -0.12) }, seat: { p: new THREE.Vector3(0, 1.55, 1.62), t: new THREE.Vector3(0, 0.62, -0.25) }, over: { p: new THREE.Vector3(0, 3.2, 2.95), t: new THREE.Vector3(0, 0.45, -0.3) }, top: { p: new THREE.Vector3(0, 2.6, 1.42), t: new THREE.Vector3(0, 0.58, 0.16) }, mid: { p: new THREE.Vector3(0, 2.08, 1.62), t: new THREE.Vector3(0, 0.6, -0.06) } };
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
    { const rg = x.createRadialGradient(512, 512, 200, 512, 512, 330); rg.addColorStop(0, 'rgba(255,255,255,0)'); rg.addColorStop(0.78, 'rgba(255,255,255,.035)'); rg.addColorStop(1, 'rgba(255,255,255,0)'); x.fillStyle = rg; x.beginPath(); x.arc(512, 512, 330, 0, 7); x.fill(); }
    x.save(); x.strokeStyle = 'rgba(255,255,255,.16)'; x.shadowColor = 'rgba(255,255,255,.35)'; x.shadowBlur = 6; x.lineWidth = 1.6; x.beginPath(); x.arc(512, 512, 256, 0, 7); x.stroke(); x.restore();
    x.save(); x.strokeStyle = acc; x.globalAlpha = 0.5; x.shadowColor = acc; x.shadowBlur = 10; x.lineWidth = 2.4; x.beginPath(); x.arc(512, 512, 302, 0, 7); x.stroke(); x.restore();
    x.save(); x.fillStyle = acc; x.globalAlpha = 0.55; x.shadowColor = acc; x.shadowBlur = 6; for (let i = 0; i < 72; i++) { const an = (i / 72) * 6.283, r = i % 6 ? 1.7 : 3.2; x.beginPath(); x.arc(512 + Math.cos(an) * 318, 512 + Math.sin(an) * 318, r, 0, 7); x.fill(); } x.restore();
    if (style === 'holo') { x.fillStyle = 'rgba(2,4,14,.55)'; x.fillRect(0, 0, 1024, 1024); x.strokeStyle = acc; x.globalAlpha = 0.16; x.lineWidth = 1.5; for (let i = 0; i <= 1024; i += 48) { x.beginPath(); x.moveTo(i, 0); x.lineTo(i, 1024); x.moveTo(0, i); x.lineTo(1024, i); x.stroke(); } x.globalAlpha = 0.45; x.lineWidth = 3; x.beginPath(); x.arc(512, 512, 252, 0, 7); x.stroke(); x.globalAlpha = 1; }
    if (style === 'wood') { const v = x.createRadialGradient(512, 512, 300, 512, 512, 760); v.addColorStop(0, 'rgba(0,0,0,0)'); v.addColorStop(1, 'rgba(0,0,0,.35)'); x.fillStyle = v; x.fillRect(0, 0, 1024, 1024); x.strokeStyle = '#D9B15A'; x.globalAlpha = 0.35; x.lineWidth = 2; x.strokeRect(40, 40, 944, 944); x.globalAlpha = 1; }
    arcText(x, 'GALAXY 101', 512, 512, 279, 30, 13, 'rgba(255,255,255,.14)');   // yazı ortadaki dairenin yayını izler
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
    t.repeat.set(1 / (FELT * 2), 1 / (FELT * 2)); t.offset.set(0.5, 0.5); return t;
  }
  function arcText(x, txt, cx, cy, r, px, sp, col) {
    x.save(); x.fillStyle = col; x.font = `700 ${px}px "${font}"`; x.textAlign = 'center'; x.textBaseline = 'middle'; if ('letterSpacing' in x) x.letterSpacing = '0px';
    const ch = [...txt], ws = ch.map((c) => x.measureText(c).width + sp), tot = ws.reduce((a, b) => a + b, 0) - sp; let th = Math.PI / 2 + tot / r / 2;
    ch.forEach((c, i) => { const a = th - (ws[i] - sp) / 2 / r; x.save(); x.translate(cx + Math.cos(a) * r, cy + Math.sin(a) * r); x.rotate(a - Math.PI / 2); x.fillText(c, 0, 0); x.restore(); th -= ws[i] / r; });
    x.restore();
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
    if (!band) { x.fillStyle = 'rgba(255,255,255,.08)'; x.font = `700 30px "${font}"`; x.textAlign = 'center'; if ('letterSpacing' in x) x.letterSpacing = '14px'; arcText(x, 'ANDROMEDA · 101', w / 2, h / 2, w * 0.2725, 30, 11, 'rgba(255,255,255,.12)'); }
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
  const [hc, hx] = cvs(1024, 1024); hx.translate(512, 512); hx.lineCap = 'round';
  { const g = hx.createRadialGradient(0, 0, 380, 0, 0, 500); g.addColorStop(0, 'rgba(255,255,255,0)'); g.addColorStop(0.7, 'rgba(255,255,255,.12)'); g.addColorStop(1, 'rgba(255,255,255,0)'); hx.fillStyle = g; hx.beginPath(); hx.arc(0, 0, 500, 0, 7); hx.fill(); }
  hx.save(); hx.strokeStyle = '#fff'; hx.shadowColor = '#fff'; hx.shadowBlur = 16; hx.lineWidth = 9; for (let i = 0; i < 40; i++) { if (i % 5 === 4) continue; const a0 = (i / 40) * 6.283 + 0.02, a1 = ((i + 0.62) / 40) * 6.283; hx.beginPath(); hx.arc(0, 0, 470, a0, a1); hx.stroke(); } hx.restore();
  hx.save(); hx.strokeStyle = 'rgba(255,255,255,.55)'; hx.shadowColor = '#fff'; hx.shadowBlur = 8; hx.lineWidth = 2.5; hx.beginPath(); hx.arc(0, 0, 438, 0, 7); hx.stroke(); hx.restore();
  hx.save(); hx.fillStyle = 'rgba(255,255,255,.8)'; hx.shadowColor = '#fff'; hx.shadowBlur = 8; for (let i = 0; i < 8; i++) { const an = (i / 8) * 6.283 + 0.39; hx.beginPath(); hx.arc(Math.cos(an) * 500, Math.sin(an) * 500, 5.5, 0, 7); hx.fill(); } hx.restore();
  hx.setTransform(1, 0, 0, 1, 0, 0);
  if (false) { hx.strokeStyle = '#fff'; hx.lineWidth = 6; for (let i = 0; i < 48; i++) { if (i % 6 === 5) continue; hx.beginPath(); hx.arc(256, 256, 240, (i / 48) * 6.283, ((i + 0.6) / 48) * 6.283); hx.stroke(); }
  }
  const holoTex = new THREE.CanvasTexture(hc); holoTex.anisotropy = 8; M.holo.map = holoTex;
  const holo = mk('holo_halka', new THREE.PlaneGeometry(0.5, 0.5).rotateX(-Math.PI / 2), M.holo, 0, TY + 0.0015, 0); holo.castShadow = holo.receiveShadow = false; scene.add(holo);

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
  // Istaka taşı belirteci (yerden / işlek)
  const TAG_COL = { side: ['#FFB547', '#2A1600'], islek: ['#43D6FF', '#04222E'] }, tagMats = {}, tagGeo = new THREE.PlaneGeometry(TW * 0.5, TW * 0.5);
  // Simge: yerden = sola bakan ok (önceki oyuncudan), işlek = artı (tahtaya eklenebilir). Yuvarlak rozet, taşın sağ üst köşesinde.
  function tagMat(kind) {
    if (tagMats[kind]) return tagMats[kind];
    const S = 128, cv = document.createElement('canvas'); cv.width = cv.height = S; const x = cv.getContext('2d'), [bg, fg] = TAG_COL[kind] || TAG_COL.islek;
    x.fillStyle = 'rgba(0,0,0,.28)'; x.beginPath(); x.arc(S / 2, S / 2 + 4, 56, 0, Math.PI * 2); x.fill();
    x.fillStyle = bg; x.beginPath(); x.arc(S / 2, S / 2, 56, 0, Math.PI * 2); x.fill();
    x.strokeStyle = 'rgba(255,255,255,.85)'; x.lineWidth = 6; x.stroke();
    x.strokeStyle = fg; x.fillStyle = fg; x.lineWidth = 14; x.lineCap = 'round'; x.lineJoin = 'round';
    if (kind === 'side') { x.beginPath(); x.moveTo(90, 64); x.lineTo(42, 64); x.stroke(); x.beginPath(); x.moveTo(62, 40); x.lineTo(38, 64); x.lineTo(62, 88); x.stroke(); }
    else { x.beginPath(); x.moveTo(64, 36); x.lineTo(64, 92); x.moveTo(36, 64); x.lineTo(92, 64); x.stroke(); }
    const tx = new THREE.CanvasTexture(cv); tx.anisotropy = 4; if ('colorSpace' in tx) tx.colorSpace = THREE.SRGBColorSpace;
    return (tagMats[kind] = new THREE.MeshBasicMaterial({ name: 'simge_' + kind, map: tx, transparent: true, depthWrite: false }));
  }
  function setTag(t, kind, text) {
    const k = kind || null; if (t.tagK === k) return; t.tagK = k;
    if (!kind) { if (t.tag) t.tag.visible = false; return; }
    if (!t.tag) { t.tag = new THREE.Mesh(tagGeo, tagMat(kind)); t.tag.position.set(TW * 0.36, TH / 2 + TW * 0.06, TD / 2 + 0.0012); t.tag.renderOrder = 3; t.g.add(t.tag); }
    t.tag.material = tagMat(kind); t.tag.visible = true;
  }
  const flatCache = {};
  function faceMatFlat(c, n) {
    const base = faceMat(c, n), k = base.uuid; if (flatCache[k]) return flatCache[k];
    const m = new THREE.MeshBasicMaterial({ name: base.name + '_duz', map: base.map, transparent: true, depthWrite: false, toneMapped: false });
    return (flatCache[k] = m);
  }
  function setFace(t, c, n, flat) {
    flat = !!flat; if (t.c === c && t.n === n && t.flat === flat) return; t.c = c; t.n = n; t.flat = flat;
    if (c && c !== 'O') { t.face.material = flat ? faceMatFlat(c, n) : faceMat(c, n); t.face.visible = true; } else t.face.visible = false;
  }
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
    if (k === 'm') { const g = flat2d ? M2D : 1; return poseIn(seatF[0], (BX0 + a * BDX) * g, TY + TD / 2, (BZ0 + b * BDZ) * g, Q_FLAT); }
    if (k === 's') return poseIn(seatF[s], (a - 7) * SG + (a >= 15 ? 0.014 : 0), TY + TD / 2 + b * TD, 0.36, d.up ? Q_FLAT : jitter(d.id, 0.05).multiply(Q_DOWN));
    if (k === 'h') return poseIn(seatF[s], (a - 1) * (TW + 0.016), TY + TD / 2 + b * TD, 0.53, jitter(d.id, 0.05).multiply(Q_DOWN));
    if (k === 'c') return { p: pileP(s, new THREE.Vector3()), q: pileQ(s) };
    if (k === 'tag') { const g = flat2d ? M2D : 1; return poseIn(seatF[0], BX0 * g - 0.046, TY + 0.0015, (BZ0 + s * BDZ) * g); }
    if (k === 'f' && d.nuke) return { p: new THREE.Vector3(0, TY + TD / 2 + 0.002, 0.02), q: new THREE.Quaternion().setFromAxisAngle(UP, 0.3).multiply(Q_FLAT) };   // elden bitiş: masanın tam ortası (deste nerede olursa olsun)
    if (k === 'f') return { p: deckTop.clone().add(new THREE.Vector3(0, 0.004, 0)), q: new THREE.Quaternion().setFromAxisAngle(UP, 0.45).multiply(Q_DOWN) };
    if (k === 'i' && s == null && (fl.deck || 0) <= 6) return { p: new THREE.Vector3(pileX() + PX0 + (fl.pileLeft ? 0 : 0.085), TY + TD / 2, fl.pileLeft ? 0.115 : 0.02), q: new THREE.Quaternion().setFromAxisAngle(UP, -0.22).multiply(Q_FLAT) };
    if (k === 'i') { const lv = s != null ? s : Math.min(6, fl.deck || 0); return { p: new THREE.Vector3(PX0 + (s != null ? 0 : pileX()), TY + TD / 2 + lv * TD, 0), q: new THREE.Quaternion().setFromAxisAngle(UP, 0.04).multiply(Q_FLAT) }; }
    return poseIn(seatF[0], 0, TY, 0);
  }

  // deck (instanced)
  const deckIM = new THREE.InstancedMesh(tileGeo, [M.tile, M.tileSide], 110); deckIM.name = 'deste'; deckIM.castShadow = deckIM.receiveShadow = true; deckIM.count = 0; scene.add(deckIM);
  const deckHit = new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshBasicMaterial({ visible: false })); scene.add(deckHit);
  const deckGlow = mk('deste_parilti', new THREE.PlaneGeometry(0.5, 0.5).rotateX(-Math.PI / 2), M.halo.clone(), 0, TY + 0.002, 0); deckGlow.castShadow = deckGlow.receiveShadow = false; deckGlow.material.opacity = 0; scene.add(deckGlow);
  let deckShown = -1, deckTop = new THREE.Vector3(0, TY + 0.1, 0), deckAnim = null, deckHeap = false;
  const PILE_LEFT = -0.62, M2D = 1.2; const pileX = () => (fl.pileLeft ? (flat2d ? -0.69 : PILE_LEFT) : 0); let pileK = 0;   // M2D: 2D'de tahta taşları ve aralıkları büyür
  function pileP(j, v) { const ind = j < 6; return v.set(ind ? PX0 : -PX0, TY + TD / 2 + (ind ? j : j - 6) * TD, 0); }
  function pileQ(j) { return new THREE.Quaternion().setFromAxisAngle(UP, ((j * 0.37) % 1 - 0.5) * 0.05).multiply(Q_DOWN); }
  function layoutDeck(n) {
    n = Math.max(0, Math.min(110, Math.round(n))); const piles = n <= 21 && !deckHeap, key = n + (piles ? 'p' : 'h'); if (key === deckShown) return; deckShown = key; deckIM.count = n;
    if (piles) {
      const m = new THREE.Matrix4(), one = new THREE.Vector3(1, 1, 1), p = new THREE.Vector3();
      for (let j = 0; j < n; j++) { pileP(j, p); m.compose(p, pileQ(j), one); deckIM.setMatrixAt(j, m); }
      if (n) deckTop.copy(pileP(n - 1, new THREE.Vector3())).y += TD; else deckTop.set(PX0, TY + TD, 0);
      deckTop.x += pileX();
      deckIM.instanceMatrix.needsUpdate = true; deckIM.computeBoundingSphere();
      deckHit.scale.set(0.2, 0.26, 0.15); deckHit.position.set(pileX(), TY + 0.1, 0); deckGlow.scale.setScalar(0.7); return;
    }
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
  const sideHit = new THREE.Mesh(new THREE.CylinderGeometry(0.13, 0.13, 0.1, 24), new THREE.MeshBasicMaterial({ visible: false })); sideHit.position.copy(sideRing.position); scene.add(sideHit);
  function roundedBox(s, r, seg) {
    const g = new THREE.BoxGeometry(s, s, s, seg, seg, seg), P = g.attributes.position, N = g.attributes.normal, h = s / 2 - r, v = new THREE.Vector3(), c = new THREE.Vector3(), n = new THREE.Vector3();
    for (let i = 0; i < P.count; i++) { v.fromBufferAttribute(P, i); c.set(cl(v.x, -h, h), cl(v.y, -h, h), cl(v.z, -h, h)); n.subVectors(v, c); if (n.lengthSq() < 1e-14) continue; n.normalize(); v.copy(c).addScaledVector(n, r); P.setXYZ(i, v.x, v.y, v.z); N.setXYZ(i, n.x, n.y, n.z); }
    return g;
  }
  const PIPS = { 1: [[1, 1]], 2: [[0, 0], [2, 2]], 3: [[0, 0], [1, 1], [2, 2]], 4: [[0, 0], [2, 0], [0, 2], [2, 2]], 5: [[0, 0], [2, 0], [1, 1], [0, 2], [2, 2]], 6: [[0, 0], [2, 0], [0, 1], [2, 1], [0, 2], [2, 2]] };
  function dieTex(v) {
    const [c, x] = cvs(256, 256), g = x.createRadialGradient(128, 110, 20, 128, 128, 190); g.addColorStop(0, '#FFFDF7'); g.addColorStop(0.75, '#F3ECDC'); g.addColorStop(1, '#D9CDB2'); x.fillStyle = g; x.fillRect(0, 0, 256, 256);
    PIPS[v].forEach(([i, j]) => {
      const px = 128 + (i - 1) * 70, py = 128 + (j - 1) * 70, r = v === 1 ? 34 : 23, col = v === 1 ? '#C81E33' : '#141733';
      x.fillStyle = 'rgba(255,255,255,.9)'; x.beginPath(); x.arc(px + 1.5, py + 2.5, r + 2, 0, 7); x.fill();
      const pg = x.createRadialGradient(px - r * 0.3, py - r * 0.35, r * 0.1, px, py, r); pg.addColorStop(0, v === 1 ? '#7A0E1C' : '#05060F'); pg.addColorStop(1, col);
      x.fillStyle = pg; x.beginPath(); x.arc(px, py, r, 0, 7); x.fill();
    });
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8; return t;
  }
  const DIE_V = [2, 5, 1, 6, 3, 4], DIE_N = { 2: new THREE.Vector3(1, 0, 0), 5: new THREE.Vector3(-1, 0, 0), 1: new THREE.Vector3(0, 1, 0), 6: new THREE.Vector3(0, -1, 0), 3: new THREE.Vector3(0, 0, 1), 4: new THREE.Vector3(0, 0, -1) };
  const die = new THREE.Mesh(roundedBox(DIE, DIE * 0.17, 10), DIE_V.map((v) => new THREE.MeshPhysicalMaterial({ name: 'zar_' + v, map: dieTex(v), roughness: 0.28, clearcoat: 1, clearcoatRoughness: 0.08 })));
  die.name = 'zar'; die.castShadow = die.receiveShadow = true; die.visible = false; scene.add(die);
  let dieS = null, dieOut = 0; const qDie = new THREE.Quaternion();
  // Zar: elden fırlatma → 3 sekme (her sekmede enerji kaybı, eksen sapması) → keçede yuvarlanma → hafif sallanıp yüzüne oturma.
  // İlk uçuş balyaların üstünden geçip masa ortasına yakın iner (f≈.64), sonra sekmeler ortada kalır.
  const DSEG = [[0, 0.32, 0, 0.64], [0.32, 0.51, 0.05, 0.82], [0.51, 0.64, 0.019, 0.91], [0.64, 0.73, 0.007, 0.955]], DROLL = [0.73, 0.955], DIMP = [0.3, 0.5, 0.63];
  const qDie2 = new THREE.Quaternion();
  function rollDie(seat, v) {
    const sf = seatF[seat], start = poseIn(sf, 0.1 + (Math.random() - 0.5) * 0.08, TY + 0.42, 0.6).p, land = poseIn(sf, (Math.random() - 0.5) * 0.14, TY + DIE / 2, -0.04 + Math.random() * 0.08).p;
    const qF = new THREE.Quaternion().setFromUnitVectors(DIE_N[v], UP).premultiply(new THREE.Quaternion().setFromAxisAngle(UP, Math.random() * 6.283));
    const dir = land.clone().sub(start).setY(0).normalize(), axis = new THREE.Vector3().crossVectors(UP, dir).normalize();
    const ax2 = new THREE.Vector3(Math.random() - 0.5, (Math.random() - 0.5) * 0.4, Math.random() - 0.5).normalize(), A = 11 + Math.random() * 4, B = 2.2 + Math.random() * 1.6, wob = (Math.random() < 0.5 ? 1 : -1) * (0.22 + Math.random() * 0.1);
    const qStart = qF.clone().premultiply(new THREE.Quaternion().setFromAxisAngle(ax2, B)).premultiply(new THREE.Quaternion().setFromAxisAngle(axis, A)), rest = die.visible && !dieOut;
    dieOut = 0; die.scale.setScalar(1);
    if (!rest) { die.position.copy(start); die.quaternion.copy(qStart); }
    dieS = { t0: now, pre: rest ? 0.45 : 0, rest: die.position.clone(), q0: die.quaternion.clone(), start, land, qF, qStart, axis, ax2, A, B, wob, dur: 1.75, hit: [] };
    die.visible = true; return Math.round((dieS.pre + dieS.dur) * 1000);
  }
  function clearDice() { if (die.visible && !dieOut) dieOut = now; dieS = null; markStack(null); }
  function dust(p, n, spd) { for (let i = 0; i < n; i++) { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: '#E9E4D6', transparent: true, opacity: 0.22, depthWrite: false })); s.position.copy(p).add(new THREE.Vector3((Math.random() - 0.5) * 0.03, 0.004, (Math.random() - 0.5) * 0.03)); s.scale.setScalar(0.012); scene.add(s); fx.push({ kind: 'dust', o: s, t0: now, life: 0.7 + Math.random() * 0.4, vel: new THREE.Vector3((Math.random() - 0.5) * spd, 0.02 + Math.random() * 0.03, (Math.random() - 0.5) * spd) }); } }
  function tickDie() {
    if (dieOut) { const u = (now - dieOut) / 0.45; if (u >= 1) { die.visible = false; dieOut = 0; die.scale.setScalar(1); } else { die.scale.setScalar(1 - ss(u)); die.position.y += 0.0012; } }
    if (!dieS) return;
    const S = dieS, el = now - S.t0; if (el < 0) return;
    if (el < S.pre) { const u = ss(el / S.pre); die.position.lerpVectors(S.rest, S.start, u); die.position.y += Math.sin(Math.PI * u) * 0.06; die.quaternion.slerpQuaternions(S.q0, S.qStart, u); return; }
    const u = Math.min(1, (el - S.pre) / S.dur), H0 = S.start.y - S.land.y;
    let f = 1, y = 0;
    for (const [a, b, h, fb] of DSEG) if (u >= a && u < b) { const v = (u - a) / (b - a), fa = a === 0 ? 0 : DSEG.find((q) => q[1] === a)[3]; f = fa + (fb - fa) * v; y = a === 0 ? H0 + 0.5 * H0 * v - 1.5 * H0 * v * v : h * 4 * v * (1 - v); }
    if (u >= DROLL[0] && u < DROLL[1]) { const v = (u - DROLL[0]) / (DROLL[1] - DROLL[0]); f = 0.955 + 0.045 * (1 - Math.pow(1 - v, 2.4)); }
    die.position.lerpVectors(S.start, S.land, f);
    const g = 1 - Math.pow(1 - Math.min(1, u / DROLL[1]), 2.6), g2 = 1 - Math.pow(1 - Math.min(1, u / 0.63), 2);
    let r = S.A * (1 - g); const r2 = S.B * (1 - g2);
    if (u >= DROLL[1]) { const w = (u - DROLL[1]) / (1 - DROLL[1]); r = S.wob * Math.exp(-4.5 * w) * Math.cos(w * 9.5) * (1 - w); }
    die.quaternion.copy(S.qF).premultiply(qDie2.setFromAxisAngle(S.ax2, r2)).premultiply(qDie.setFromAxisAngle(S.axis, r));
    die.position.y = S.land.y + y + (DIE / 2) * 0.82 * (Math.abs(Math.cos(r)) + Math.abs(Math.sin(r)) - 1);
    DIMP.forEach((a, i) => { if (!S.hit[i] && u >= a) { S.hit[i] = 1; const p = die.position.clone().setY(TY + 0.004); dust(p, i ? 4 : 9, i ? 0.05 : 0.09); if (!i) shock(p.clone().setY(TY + 0.001), '#FFF1C0', 0.5, 0.06); } });
    if (u >= 1) { die.position.copy(S.land); die.quaternion.copy(S.qF); dieS = null; shock(S.land.clone().setY(TY + 0.001), theme.a1, 0.9, 0.1); burst(S.land.clone().setY(TY + 0.03), '#FFE08A', 10, 0.08); }
  }
  const stackMark = mk('balya_isaret', new THREE.PlaneGeometry(0.11, 0.16).rotateX(-Math.PI / 2), M.halo.clone(), 0, 0, 0); stackMark.castShadow = stackMark.receiveShadow = false; stackMark.material.opacity = 0; scene.add(stackMark);
  let markOn = false;
  function markStack(seat, si) { if (seat == null) { markOn = false; return; } const p = poseIn(seatF[seat], (si - 7) * SG, TY + 0.0016, 0.36); stackMark.position.copy(p.p); stackMark.quaternion.copy(p.q); markOn = true; }
  function markerTex(okey) {
    const W2 = 200, H2 = 264, [c, x] = cvs(W2, H2), col = okey ? [92, 242, 154] : [255, 201, 74], rgba = (al) => `rgba(${col[0]},${col[1]},${col[2]},${al})`;
    const rr = (px, py, w, h, r) => { x.beginPath(); x.moveTo(px + r, py); x.arcTo(px + w, py, px + w, py + h, r); x.arcTo(px + w, py + h, px, py + h, r); x.arcTo(px, py + h, px, py, r); x.arcTo(px, py, px + w, py, r); x.closePath(); };
    const g = x.createRadialGradient(W2 / 2, H2 / 2, 10, W2 / 2, H2 / 2, H2 * 0.62); g.addColorStop(0, rgba(0.34)); g.addColorStop(1, rgba(0.06));
    rr(14, 14, W2 - 28, H2 - 28, 26); x.fillStyle = g; x.fill();
    x.save(); x.shadowColor = rgba(0.95); x.shadowBlur = 14; x.setLineDash([22, 13]); x.lineDashOffset = 6; x.lineWidth = 7; x.lineCap = 'round'; x.strokeStyle = rgba(1); rr(14, 14, W2 - 28, H2 - 28, 26); x.stroke(); x.restore();
    const tx = new THREE.CanvasTexture(c); tx.colorSpace = THREE.SRGBColorSpace; tx.anisotropy = 8; return tx;
  }
  function badgeTex(okey) {
    const [c, x] = cvs(160, 160), cx = 80, cy = 80;
    x.save(); x.shadowColor = 'rgba(0,0,0,.45)'; x.shadowBlur = 14; x.shadowOffsetY = 5;
    const g = x.createLinearGradient(0, 22, 0, 138); okey ? (g.addColorStop(0, '#9BFFC4'), g.addColorStop(1, '#22B565')) : (g.addColorStop(0, '#FFE58A'), g.addColorStop(1, '#F2A019'));
    x.fillStyle = g; x.beginPath(); x.arc(cx, cy, 56, 0, 7); x.fill(); x.restore();
    x.strokeStyle = 'rgba(255,255,255,.85)'; x.lineWidth = 4; x.beginPath(); x.arc(cx, cy, 54, 0, 7); x.stroke();
    x.fillStyle = 'rgba(255,255,255,.35)'; x.beginPath(); x.ellipse(cx - 14, cy - 26, 26, 12, -0.4, 0, 7); x.fill();
    x.strokeStyle = okey ? '#063B1E' : '#3A2300'; x.lineWidth = 13; x.lineCap = 'round'; x.lineJoin = 'round';
    if (!okey) { x.beginPath(); x.moveTo(cx - 24, cy); x.lineTo(cx + 24, cy); x.moveTo(cx, cy - 24); x.lineTo(cx, cy + 24); x.stroke(); }
    else {
      x.lineWidth = 9; x.beginPath(); x.arc(cx, cy, 25, Math.PI * 1.1, Math.PI * 1.85); x.stroke(); x.beginPath(); x.arc(cx, cy, 25, Math.PI * 0.1, Math.PI * 0.85); x.stroke();
      const head = (ang, dir) => { const px = cx + Math.cos(ang) * 25, py = cy + Math.sin(ang) * 25; x.beginPath(); x.moveTo(px + dir * 2, py - 12); x.lineTo(px + dir * 12, py + 1); x.lineTo(px - dir * 4, py + 8); x.closePath(); x.fillStyle = '#063B1E'; x.fill(); };
      head(Math.PI * 1.85, 1); head(Math.PI * 0.85, -1);
    }
    const tx = new THREE.CanvasTexture(c); tx.colorSpace = THREE.SRGBColorSpace; tx.anisotropy = 8; return tx;
  }
  const MKT = { frame: [markerTex(false), markerTex(true)], badge: [badgeTex(false), badgeTex(true)] };
  const MK_FRAME = new THREE.PlaneGeometry(TW + 0.014, (TW + 0.014) * 264 / 200).rotateX(-Math.PI / 2), MK_BADGE = new THREE.PlaneGeometry(0.03, 0.03).rotateX(-Math.PI / 2), MK_HALO = new THREE.PlaneGeometry(0.15, 0.17).rotateX(-Math.PI / 2);
  const markers = Array.from({ length: 48 }, () => {
    const g = new THREE.Group(), bm = (map, add) => new THREE.MeshBasicMaterial({ map, transparent: true, opacity: 0, depthWrite: false, toneMapped: false, blending: add ? THREE.AdditiveBlending : THREE.NormalBlending });
    const halo = new THREE.Mesh(MK_HALO, bm(glowTex(), true)), frame = new THREE.Mesh(MK_FRAME, bm(MKT.frame[0])), badge = new THREE.Mesh(MK_BADGE, bm(MKT.badge[0]));
    halo.position.y = -0.0004; badge.position.y = 0.004; frame.renderOrder = 2; badge.renderOrder = 3;
    g.add(halo, frame, badge); g.visible = false; scene.add(g); return { g, halo, frame, badge, a: 0, key: null, h: 0, ph: Math.random() * 6 };
  });
  const TAG_GEO = new THREE.CircleGeometry(0.011, 28).rotateX(-Math.PI / 2), TAG_RING = new THREE.RingGeometry(0.014, 0.017, 32).rotateX(-Math.PI / 2);
  const tags = Array.from({ length: 8 }, () => { const g = new THREE.Group(), dot = new THREE.Mesh(TAG_GEO, new THREE.MeshBasicMaterial({ transparent: true, opacity: 0, depthWrite: false, toneMapped: false })), ring = new THREE.Mesh(TAG_RING, new THREE.MeshBasicMaterial({ color: '#ffffff', transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending })); g.add(dot, ring); scene.add(g); return { g, dot, ring, a: 0 }; });
  function tickTags(dt) {
    const rows = fl.boardRows || [];
    tags.forEach((tg, i) => { const r = rows[i]; if (r) { tg.dot.material.color.set(r.color); tg.ring.material.color.set(r.color); tg.g.position.copy(poseFor({ id: '', at: ['tag', i] }).p); } tg.a += ((r ? 1 : 0) - tg.a) * Math.min(1, dt * 6); tg.dot.material.opacity = tg.a; tg.ring.material.opacity = tg.a * (0.35 + (r && r.active ? 0.45 + 0.2 * Math.sin(now * 5) : 0)); tg.g.visible = tg.a > 0.01; });
  }
  function nearTarget(id, px) {
    const list = (fl.targets && fl.targets[id]) || []; let best = null, bd = px || 80;
    const mx = (ndc.x + 1) / 2 * W, my = (1 - ndc.y) / 2 * H;
    list.forEach((tg) => { if (!tg.p) return; const s = project(tg.p), d = Math.hypot(s.x - mx, s.y - my); if (d < bd) { bd = d; best = tg; } });
    return best;
  }
  function tickMarkers(dt) {
    const dragId = drag && drag.moved && drag.kind === 'rack' && !drag.groupActive ? drag.id : null, id = dragId || selId, list = ((fl.canMeld || dragId) && id && fl.targets && fl.targets[id]) || [], hot = drag && drag.target;
    markers.forEach((m, i) => {
      const tg = list[i];
      if (tg && tg.p) { const key = tg.kind + tg.id; if (m.key !== key) { m.key = key; const ok = tg.kind === 'okey' ? 1 : 0; m.frame.material.map = MKT.frame[ok]; m.badge.material.map = MKT.badge[ok]; m.halo.material.color.set(ok ? '#5CF29A' : '#FFC94A'); m.frame.visible = !ok; m.okey = !!ok; m.frame.material.needsUpdate = m.badge.material.needsUpdate = true; m.g.position.copy(tg.p); m.g.position.y = ok ? TY + TD + 0.0012 : TY + 0.0014; m.a = Math.min(m.a, 0.01); } }
      const on = !!(tg && tg.p), isHot = on && hot === tg;
      m.a += ((on ? 1 : 0) - m.a) * Math.min(1, dt * 10); m.h += ((isHot ? 1 : 0) - m.h) * Math.min(1, dt * 14); m.g.visible = m.a > 0.01;
      if (!m.g.visible) { if (!on) m.key = null; return; }
      const e = m.a * m.a * (3 - 2 * m.a), br = 0.5 + 0.5 * Math.sin(now * 3.2 + m.ph);
      m.frame.material.opacity = e * (0.78 + 0.22 * br + m.h * 0.2); m.halo.material.opacity = e * (0.16 + 0.12 * br + m.h * 0.45); m.badge.material.opacity = e;
      m.badge.position.y = 0.004 + (m.okey ? 0.006 : 0) + 0.0025 * br + m.h * 0.006; m.badge.scale.setScalar((0.6 + 0.4 * e) * (1 + m.h * 0.25)); m.badge.rotation.y = m.okey ? now * 1.4 : 0;
      m.g.scale.setScalar(0.92 + 0.08 * e + m.h * 0.1);
    });
  }
  const nukeLight = new THREE.PointLight('#FFB25A', 0, 4.5, 1.4); scene.add(nukeLight); let nukeT = -9;
  // Çift alanı ayracı: masada çift açıldığında seri/grup alanı ile çift alanı arasında ince çizgi + etiket
  const pzLine = mk('cift_ayrac', new THREE.PlaneGeometry(0.003, 0.7).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ name: 'cift_ayrac', color: '#FFC94A', transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending }), BX0 + 14 * BDX, TY + 0.0012, 0);
  const pzLabel = mk('cift_etiket', new THREE.PlaneGeometry(0.22, 0.033).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ name: 'cift_etiket', transparent: true, opacity: 0, depthWrite: false }), BX0 + 17 * BDX, TY + 0.0012, BZ0 - 0.058);
  pzLine.castShadow = pzLine.receiveShadow = pzLabel.castShadow = pzLabel.receiveShadow = false; scene.add(pzLine, pzLabel); let pzText = null, pzA = 0;
  function pzTex(txt) { const [c, x] = cvs(512, 76); x.fillStyle = '#ffffff'; x.font = `800 40px "${font}"`; x.textAlign = 'center'; x.textBaseline = 'middle'; if ('letterSpacing' in x) x.letterSpacing = '12px'; x.fillText(txt, 262, 40); const tx = new THREE.CanvasTexture(c); tx.colorSpace = THREE.SRGBColorSpace; tx.anisotropy = 8; return tx; }
  // Işık konisinde süzülen ince toz: sahneye derinlik katar (çok hafif)
  const MOTES = 90, moteGeo = new THREE.BufferGeometry(), moteArr = new Float32Array(MOTES * 3), moteSeed = [];
  for (let i = 0; i < MOTES; i++) { const a = Math.random() * 6.283, r = Math.sqrt(Math.random()) * 0.95; moteSeed.push({ a, r, y: Math.random(), s: 0.15 + Math.random() * 0.35, ph: Math.random() * 6.283 }); }
  moteGeo.setAttribute('position', new THREE.BufferAttribute(moteArr, 3));
  const motes = new THREE.Points(moteGeo, new THREE.PointsMaterial({ name: 'toz', size: 0.006, map: glowTex(), color: '#FFF1D6', transparent: true, opacity: 0.35, depthWrite: false, blending: THREE.AdditiveBlending })); motes.frustumCulled = false; scene.add(motes);
  function tickMotes(t) { for (let i = 0; i < MOTES; i++) { const m = moteSeed[i], yy = (m.y + t * 0.012 * m.s) % 1; moteArr[i * 3] = Math.cos(m.a + t * 0.03 * m.s) * m.r + Math.sin(t * 0.4 + m.ph) * 0.01; moteArr[i * 3 + 1] = TY + 0.08 + yy * 1.4; moteArr[i * 3 + 2] = Math.sin(m.a + t * 0.03 * m.s) * m.r * 0.8; } moteGeo.attributes.position.needsUpdate = true; }

  // ---------- lights ----------
  const hemi = new THREE.HemisphereLight('#c9d6ff', '#0a0a14', 0.45); scene.add(hemi);
  const key = new THREE.DirectionalLight('#fff4e2', 1.7); key.position.set(1.1, 4.2, 2.0); key.target.position.set(0, TY, 0);
  key.castShadow = true; key.shadow.mapSize.set(4096, 4096); Object.assign(key.shadow.camera, { left: -1.25, right: 1.25, top: 1.25, bottom: -1.25, near: 2, far: 7 }); key.shadow.bias = -0.00025; key.shadow.normalBias = 0.006; key.shadow.radius = 3; key.shadow.blurSamples = 12;   // yumuşak gölge kenarı
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
  const trailMats = [];
  function trail(pos, color, size, op) {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color, transparent: true, depthWrite: false, opacity: op, blending: THREE.AdditiveBlending }));
    s.position.copy(pos); s.scale.setScalar(size); scene.add(s); fx.push({ kind: 'trail', o: s, t0: now, life: 0.38, s0: size, op });
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
  function shock(pos, color, life, max) {
    const m = new THREE.Mesh(new THREE.RingGeometry(0.9, 1, 72).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide }));
    m.position.copy(pos).y += 0.003; m.scale.setScalar(0.02); scene.add(m); fx.push({ kind: 'shock', o: m, t0: now, life: life || 0.9, max: max || 1.2 });
  }
  function glowSprite(pos, color, life, max, op, additive) {
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color, transparent: true, depthWrite: false, opacity: op, blending: additive === false ? THREE.NormalBlending : THREE.AdditiveBlending }));
    s.position.copy(pos); s.scale.setScalar(0.04); scene.add(s); fx.push({ kind: 'fire', o: s, t0: now, life, max, op }); return s;
  }
  const PLUME_A = new THREE.Color('#FFB066'), PLUME_B = new THREE.Color('#4a4d5e');
  function nukeFx(p) {
    const c = p.clone().setY(TY);
    nukeLight.position.copy(c).y += 0.3; nukeT = now; shakeUntil = now + 1.2;
    glowSprite(c.clone().setY(TY + 0.05), '#FFFFFF', 0.55, 0.9, 1);
    glowSprite(c.clone().setY(TY + 0.06), '#FFD58A', 1.2, 1.4, 1);
    glowSprite(c.clone().setY(TY + 0.08), '#FF6A2E', 1.8, 1.1, 0.85);
    shock(c, '#FFFFFF', 0.7, 1.5); setTimeout(() => shock(c, '#FFD964', 1.0, 1.25), 110); setTimeout(() => shock(c, theme.a1, 1.3, 1.0), 240);
    burst(c.clone().setY(TY + 0.04), '#FFE08A', 140, 1.4); burst(c.clone().setY(TY + 0.04), '#FF7A3D', 90, 1.0); burst(c.clone().setY(TY + 0.05), '#ffffff', 50, 1.8);
    for (let i = 0; i < 34; i++) {
      const cap = i >= 14, a = Math.random() * 6.283, r = cap ? 0.03 + Math.random() * 0.05 : Math.random() * 0.025;
      const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: PLUME_A.clone(), transparent: true, opacity: 0, depthWrite: false }));
      s.position.set(c.x + Math.cos(a) * r, TY + 0.02 + Math.random() * 0.02, c.z + Math.sin(a) * r); scene.add(s);
      const vy = cap ? 0.34 + Math.random() * 0.12 : 0.12 + Math.random() * 0.28;
      fx.push({ kind: 'plume', o: s, t0: now + (cap ? 0.08 : 0) + Math.random() * 0.15, life: 2.6 + Math.random() * 0.9, vel: new THREE.Vector3(Math.cos(a) * (cap ? 0.1 : 0.02), vy, Math.sin(a) * (cap ? 0.1 : 0.02)), s0: 0.05, s1: cap ? 0.24 + Math.random() * 0.1 : 0.12 + Math.random() * 0.06, cap });
    }
    tiles.forEach((tl) => { if (tl.tw) return; const dd = tl.g.position.distanceTo(c); tl.hop = now + dd * 0.35; tl.hopA = 0.012 + 0.05 * Math.max(0, 1 - dd / 1.1); tl.hopD = 0.55; });
    deckSpin = now; holoBoost = now + 3.2;
  }
  // Elden bitiş efektleri (oyuncu seçer): nuke · blackhole · supernova · meteor
  function hopAll(c, delay) { tiles.forEach((tl) => { if (tl.tw) return; const dd = tl.g.position.distanceTo(c); tl.hop = now + (delay || 0) + dd * 0.35; tl.hopA = 0.012 + 0.05 * Math.max(0, 1 - dd / 1.1); tl.hopD = 0.6; }); }
  function blackholeFx(p) {
    const c = p.clone().setY(TY); nukeLight.color.set('#9B7BFF'); nukeLight.position.copy(c).y += 0.25; shakeUntil = now + 0.6;
    const core = new THREE.Mesh(new THREE.CircleGeometry(1, 56).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: '#04020a', transparent: true, opacity: 0, depthWrite: false }));
    core.position.copy(c).y += 0.0016; core.renderOrder = -1; scene.add(core);   // keçenin hemen üstünde: taşlar üstünde kalır, rengi bozulmaz fx.push({ kind: 'bhcore', o: core, t0: now, life: 1.6 });
    const ring = new THREE.Mesh(new THREE.RingGeometry(0.72, 1, 72).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ map: glowTex(), color: '#B79CFF', transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide }));
    ring.position.copy(c).y += 0.0018; scene.add(ring); fx.push({ kind: 'bhring', o: ring, t0: now, life: 1.6 });
    for (let i = 0; i < 80; i++) { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: ['#B79CFF', '#7FE3FF', '#FFFFFF'][i % 3], transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending })); s.scale.setScalar(0.018); scene.add(s); fx.push({ kind: 'spiral', o: s, t0: now + Math.random() * 0.5, life: 0.9 + Math.random() * 0.4, a: Math.random() * 6.283, r: 0.22 + Math.random() * 0.5, c, h: 0.01 + Math.random() * 0.07 }); }
    setTimeout(() => { if (!alive) return; nukeT = now; shakeUntil = now + 0.9; glowSprite(c.clone().setY(TY + 0.05), '#EDE6FF', 0.55, 1.0, 1); glowSprite(c.clone().setY(TY + 0.06), '#7F6BFF', 1.3, 1.3, 0.8); shock(c, '#FFFFFF', 0.6, 1.4); shock(c, '#B79CFF', 1.0, 1.15); setTimeout(() => shock(c, '#7FE3FF', 1.2, 0.95), 120); burst(c.clone().setY(TY + 0.04), '#B79CFF', 110, 1.2); burst(c.clone().setY(TY + 0.04), '#7FE3FF', 60, 0.9); hopAll(c, 0); holoBoost = now + 3; }, 1150);
    deckSpin = now;
  }
  // ---- Elden bitiş: ortak yüksek kaliteli parçalar ----
  const _fxT = {};
  function fxCanvas(key, w, h, draw) { if (_fxT[key]) return _fxT[key]; const c = document.createElement('canvas'); c.width = w; c.height = h; draw(c.getContext('2d'), w, h); const tx = new THREE.CanvasTexture(c); tx.colorSpace = THREE.SRGBColorSpace; return (_fxT[key] = tx); }
  const ringTex = () => fxCanvas('ring', 256, 256, (x) => { const g = x.createRadialGradient(128, 128, 0, 128, 128, 128); g.addColorStop(0, 'rgba(255,255,255,0)'); g.addColorStop(0.6, 'rgba(255,255,255,0)'); g.addColorStop(0.8, 'rgba(255,255,255,1)'); g.addColorStop(0.88, 'rgba(255,255,255,.4)'); g.addColorStop(1, 'rgba(255,255,255,0)'); x.fillStyle = g; x.fillRect(0, 0, 256, 256); });
  const streakTex = () => fxCanvas('streak', 512, 64, (x, w, h) => { const g = x.createLinearGradient(0, 0, w, 0); g.addColorStop(0, 'rgba(255,255,255,0)'); g.addColorStop(0.5, 'rgba(255,255,255,1)'); g.addColorStop(1, 'rgba(255,255,255,0)'); x.fillStyle = g; x.fillRect(0, 0, w, h); const v = x.createLinearGradient(0, 0, 0, h); v.addColorStop(0, 'rgba(0,0,0,0)'); v.addColorStop(0.5, 'rgba(0,0,0,1)'); v.addColorStop(1, 'rgba(0,0,0,0)'); x.globalCompositeOperation = 'destination-in'; x.fillStyle = v; x.fillRect(0, 0, w, h); });
  const beamTex = () => fxCanvas('beam', 8, 256, (x, w, h) => { const g = x.createLinearGradient(0, h, 0, 0); g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(0.35, 'rgba(255,255,255,.5)'); g.addColorStop(1, 'rgba(255,255,255,0)'); x.fillStyle = g; x.fillRect(0, 0, w, h); });
  const addM = (o) => new THREE.MeshBasicMaterial({ transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, toneMapped: false, ...o });
  function ring2(c, color, life, max, op = 1, dl = 0, y = 0.004) { const m = new THREE.Mesh(new THREE.PlaneGeometry(2, 2).rotateX(-Math.PI / 2), addM({ map: ringTex(), color, opacity: 0 })); m.position.copy(c).y = TY + y; m.scale.setScalar(0.01); scene.add(m); fx.push({ kind: 'ring2', o: m, t0: now + dl, life, max, op }); return m; }
  function flare(p, color, life, w, h, op = 1) { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: streakTex(), color, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false })); s.position.copy(p); s.scale.set(0.01, h, 1); scene.add(s); fx.push({ kind: 'flare', o: s, t0: now, life, w, h, op }); }
  function shell(c, color, life, max, op = 0.55) { const m = new THREE.Mesh(new THREE.SphereGeometry(1, 48, 24, 0, Math.PI * 2, 0, Math.PI / 2), addM({ color, opacity: 0, side: THREE.BackSide })); m.position.copy(c).y = TY; m.scale.setScalar(0.01); scene.add(m); fx.push({ kind: 'shell', o: m, t0: now, life, max, op }); }
  function beam(c, color, life, hgt) { const m = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 32, 1, true).translate(0, 0.5, 0), addM({ map: beamTex(), color, opacity: 0 })); m.position.copy(c).y = TY; m.scale.set(0.03, 0.01, 0.03); scene.add(m); fx.push({ kind: 'beam', o: m, t0: now, life, hgt }); }
  // Kıvılcım: tek Points; parçacık başına renk, sürtünme, yerçekimi, masada sekme, soğuyarak kararma.
  function embers(c, cols, n, spd, life, size = 0.016, up = 1) {
    const pos = new Float32Array(n * 3), col = new Float32Array(n * 3), base = [], vel = [];
    for (let i = 0; i < n; i++) { pos.set([c.x, TY + 0.02, c.z], i * 3); const a = Math.random() * 6.283, e = Math.random() * 1.25 + 0.1, k = spd * (0.25 + Math.random() * 0.9); vel.push(new THREE.Vector3(Math.cos(a) * Math.cos(e) * k, Math.sin(e) * k * up, Math.sin(a) * Math.cos(e) * k)); const cc = new THREE.Color(cols[i % cols.length]); base.push(cc); col.set([cc.r, cc.g, cc.b], i * 3); }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(pos, 3)); g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    const o = new THREE.Points(g, new THREE.PointsMaterial({ size, map: glowTex(), vertexColors: true, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false }));
    o.frustumCulled = false; scene.add(o); fx.push({ kind: 'emb', o, t0: now, life, vel, base, lf: Array.from({ length: n }, () => 0.45 + Math.random() * 0.55) });
  }
  const ROCK_HOT = new THREE.Color('#FF7A1E'), ROCK_COLD = new THREE.Color('#000000');
  function rocks(c, n, spd = 1) {
    for (let i = 0; i < n; i++) {
      const r = 0.006 + Math.random() * 0.012, m = new THREE.Mesh(new THREE.IcosahedronGeometry(r, 0), new THREE.MeshStandardMaterial({ color: '#2b1d16', roughness: 0.9, flatShading: true, emissive: ROCK_HOT.clone(), emissiveIntensity: 2.4 }));
      m.castShadow = false; const a = Math.random() * 6.283, k = (0.35 + Math.random() * 0.7) * spd; m.position.set(c.x + Math.cos(a) * 0.02, TY + 0.02, c.z + Math.sin(a) * 0.02); scene.add(m);
      fx.push({ kind: 'rock', o: m, t0: now, life: 2.2 + Math.random() * 0.8, r, vel: new THREE.Vector3(Math.cos(a) * k, 0.5 + Math.random() * 1.0 * spd, Math.sin(a) * k), spin: new THREE.Vector3(Math.random() * 14 - 7, Math.random() * 14 - 7, Math.random() * 14 - 7) });
    }
  }
  function dustRoll(c, n, color = '#B9A493') {
    for (let i = 0; i < n; i++) { const a = (i / n) * 6.283 + Math.random() * 0.3, s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color, transparent: true, opacity: 0, depthWrite: false })); s.position.set(c.x + Math.cos(a) * 0.04, TY + 0.015, c.z + Math.sin(a) * 0.04); s.scale.setScalar(0.04); scene.add(s); fx.push({ kind: 'roll', o: s, t0: now + Math.random() * 0.08, life: 1.6 + Math.random() * 0.7, vel: new THREE.Vector3(Math.cos(a), 0.04, Math.sin(a)).multiplyScalar(0.55 + Math.random() * 0.35), s1: 0.12 + Math.random() * 0.08 }); }
  }
  function implode(c, n, cols, life) {
    for (let i = 0; i < n; i++) { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: cols[i % cols.length], transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false })); s.scale.setScalar(0.018); scene.add(s); const a = Math.random() * 6.283, e = Math.random() * 1.2; fx.push({ kind: 'imp', o: s, t0: now + Math.random() * life * 0.35, life: life * (0.55 + Math.random() * 0.3), c: c.clone().setY(TY + 0.07), d: new THREE.Vector3(Math.cos(a) * Math.cos(e), Math.sin(e) * 0.8, Math.sin(a) * Math.cos(e)), r: 0.3 + Math.random() * 0.45 }); }
  }
  function nebula(c, cols, n) {
    for (let i = 0; i < n; i++) { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: cols[i % cols.length], transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending })); const a = Math.random() * 6.283, r = Math.random() * 0.12; s.position.set(c.x + Math.cos(a) * r, TY + 0.06 + Math.random() * 0.08, c.z + Math.sin(a) * r); s.scale.setScalar(0.1); scene.add(s); fx.push({ kind: 'neb', o: s, t0: now + 0.1 + Math.random() * 0.25, life: 2.4 + Math.random() * 1.0, s0: 0.15, s1: 0.5 + Math.random() * 0.4, op: 0.16 + Math.random() * 0.1, vel: new THREE.Vector3(Math.cos(a) * 0.05, 0.02, Math.sin(a) * 0.05) }); }
  }
  function coreGlow(c, color, life) { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color, transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false })); s.position.copy(c).y = TY + 0.07; s.scale.setScalar(0.02); scene.add(s); fx.push({ kind: 'core', o: s, t0: now, life }); }

  // Süpernova: önce çöküş (ışık merkeze çekilir, çekirdek titreyerek parlar), sonra patlama —
  // beyaz çakma + yatay mercek parlaması, yarım küre şok kabuğu, yumuşak halkalar, dönen ışınlar, ışık sütunu,
  // renkli kıvılcımlar ve yavaşça genişleyen mavi-mor bulutsu kalıntı.
  function supernovaFx(p) {
    const c = p.clone().setY(TY); nukeLight.color.set('#CFEBFF'); nukeLight.position.copy(c).y += 0.35; shakeUntil = now + 0.5;
    coreGlow(c, '#DDF2FF', 0.62); implode(c, 90, ['#FFFFFF', '#BFE6FF', '#8FB8FF'], 0.6);
    setTimeout(() => {
      if (!alive) return; nukeT = now; shakeUntil = now + 1.2;
      glowSprite(c.clone().setY(TY + 0.08), '#FFFFFF', 0.55, 2.4, 1); glowSprite(c.clone().setY(TY + 0.08), '#9FD8FF', 1.5, 3.0, 0.75); glowSprite(c.clone().setY(TY + 0.1), '#FFD27A', 1.1, 1.5, 0.45);
      flare(c.clone().setY(TY + 0.09), '#CFEBFF', 1.1, 3.0, 0.14, 1); flare(c.clone().setY(TY + 0.09), '#FFFFFF', 0.6, 1.8, 0.045, 1);
      shell(c, '#9FD8FF', 1.0, 0.95, 0.5); shell(c, '#FFFFFF', 0.55, 0.55, 0.7);
      ring2(c, '#FFFFFF', 0.85, 2.0, 1); ring2(c, '#7FD0FF', 1.25, 1.55, 0.95, 0.07); ring2(c, '#FFD27A', 1.6, 1.15, 0.8, 0.16); ring2(c, '#B79CFF', 3.2, 0.85, 0.42, 0.3);
      for (let i = 0; i < 28; i++) { const a = (i / 28) * 6.283 + Math.random() * 0.15, m = new THREE.Mesh(new THREE.PlaneGeometry(1, i % 3 ? 0.035 : 0.07).translate(0.5, 0, 0).rotateX(-Math.PI / 2), addM({ map: streakTex(), color: i % 2 ? '#9FD8FF' : '#FFFFFF', opacity: 0 })); m.position.copy(c).y += 0.008; m.rotation.y = -a; scene.add(m); fx.push({ kind: 'ray', o: m, t0: now + i * 0.008, life: 1.3, len: 0.7 + Math.random() * 0.7, rot: (Math.random() - 0.5) * 0.25 }); }
      beam(c, '#BFE6FF', 1.5, 1.7);
      embers(c, ['#FFFFFF', '#BFE6FF', '#7FB8FF', '#FFD27A'], 280, 1.6, 1.7, 0.017, 0.9);
      nebula(c, ['#5B7BFF', '#B06BFF', '#4FD8FF'], 10);
      hopAll(c, 0); deckSpin = now; holoBoost = now + 3.4;
    }, 560);
  }
  // Meteor: taş alev topu gibi düşer (tween), çarpmada: çakma, mercek parlaması, yumuşak şok halkaları,
  // akkor kaya parçaları (sekip soğur), kıvılcım yağmuru, yere yayılan toz halkası, kızgın kenarlı krater ve 7 küçük meteor.
  function meteorFx(p) {
    const c = p.clone().setY(TY); nukeLight.color.set('#FF8A3A'); nukeLight.position.copy(c).y += 0.25; nukeT = now; shakeUntil = now + 1.35;
    glowSprite(c.clone().setY(TY + 0.05), '#FFFFFF', 0.35, 1.0, 1); glowSprite(c.clone().setY(TY + 0.06), '#FFB347', 1.0, 1.6, 0.9); glowSprite(c.clone().setY(TY + 0.07), '#FF5A1F', 1.8, 1.15, 0.6);
    flare(c.clone().setY(TY + 0.05), '#FFC27A', 0.8, 2.0, 0.11, 0.95);
    ring2(c, '#FFF1C0', 0.6, 1.35, 1); ring2(c, '#FF8A3D', 1.1, 1.05, 0.9, 0.06); ring2(c, '#FF4A1A', 1.6, 0.8, 0.7, 0.14);
    rocks(c, 26); embers(c, ['#FFF1C0', '#FFB347', '#FF6A2E', '#FF3B1F'], 260, 1.25, 2.2, 0.018); dustRoll(c, 28, '#D8C2AE');
    const fire = glowSprite(c.clone().setY(TY + 0.03), '#FF7A2E', 2.6, 0.45, 0.7); fire.userData.hold = 1;
    const scorch = new THREE.Mesh(new THREE.CircleGeometry(1, 48).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ map: glowTex(), color: '#120604', transparent: true, opacity: 0, depthWrite: false })); scorch.position.copy(c).y += 0.0015; scorch.scale.setScalar(0.24); scene.add(scorch); fx.push({ kind: 'scorch', o: scorch, t0: now, life: 3.4, op: 0.85 });
    const rim = new THREE.Mesh(new THREE.PlaneGeometry(2, 2).rotateX(-Math.PI / 2), addM({ map: ringTex(), color: '#FF6A1A', opacity: 0 })); rim.position.copy(c).y += 0.0025; rim.scale.setScalar(0.12); scene.add(rim); fx.push({ kind: 'rim', o: rim, t0: now, life: 2.6 });
    for (let i = 0; i < 7; i++) { const a = Math.random() * 6.283, r = 0.22 + Math.random() * 0.42, e = c.clone().add(new THREE.Vector3(Math.cos(a) * r, 0.004, Math.sin(a) * r)), s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: '#FFD27A', transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false })); s.scale.setScalar(0.07); scene.add(s); fx.push({ kind: 'met', o: s, t0: now + 0.2 + i * 0.13, life: 0.5, s0: e.clone().add(new THREE.Vector3(-0.4, 1.0, -0.3)), e }); }
    hopAll(c, 0); deckSpin = now; holoBoost = now + 2.8;
  }
  const FIN_FX = { nuke: (p) => { nukeLight.color.set('#FFB25A'); nukeFx(p); }, blackhole: blackholeFx, supernova: supernovaFx, meteor: meteorFx };
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
      else if (f.kind === 'trail') { const k = 1 - u; f.o.scale.setScalar(f.s0 * (0.35 + 0.65 * k)); f.o.material.opacity = f.op * k * k; }
      else if (f.kind === 'shock') { const e = 1 - Math.pow(1 - u, 3); f.o.scale.setScalar(0.02 + e * f.max); f.o.material.opacity = 0.95 * Math.pow(1 - u, 1.4); }
      else if (f.kind === 'fire') { const e = 1 - Math.pow(1 - u, 4); f.o.scale.setScalar(0.04 + e * f.max); f.o.material.opacity = f.op * Math.pow(1 - u, 1.6); }
      else if (f.kind === 'plume') { if (u < 0) continue; f.o.position.addScaledVector(f.vel, dt); f.vel.multiplyScalar(f.cap ? 0.982 : 0.975); if (f.cap) { f.vel.x *= 0.99; f.vel.z *= 0.99; } f.o.scale.setScalar(f.s0 + (1 - Math.pow(1 - u, 2)) * f.s1); f.o.material.color.lerpColors(PLUME_A, PLUME_B, Math.min(1, u * 2.2)); f.o.material.opacity = 0.62 * Math.pow(Math.sin(Math.PI * Math.min(1, u * 1.05)), 0.7); }
      else if (f.kind === 'bhcore') { const g = u < 0.3 ? ss(u / 0.3) : u < 0.68 ? 1 : 1 - ss((u - 0.68) / 0.12); f.o.scale.setScalar(Math.max(0.001, 0.13 * g)); f.o.material.opacity = 0.92 * Math.min(1, g * 1.4); }
      else if (f.kind === 'bhring') { const g = u < 0.3 ? ss(u / 0.3) : u < 0.68 ? 1 : 1 - ss((u - 0.68) / 0.12); f.o.scale.setScalar(Math.max(0.001, 0.26 * g * (1 + 0.05 * Math.sin(now * 9)))); f.o.rotation.y -= dt * 6; f.o.material.opacity = 0.85 * g; }
      else if (f.kind === 'spiral') { if (u < 0) continue; const k = ease(u), rr = f.r * (1 - k), an = f.a + k * 7; f.o.position.set(f.c.x + Math.cos(an) * rr, TY + 0.012 + f.h * (1 - k), f.c.z + Math.sin(an) * rr); f.o.material.opacity = Math.sin(Math.PI * u) * 0.9; f.o.scale.setScalar(0.022 * (1 - k * 0.6)); }
      else if (f.kind === 'ray') { if (u < 0) continue; f.o.scale.set(f.len * ease(Math.min(1, u * 1.6)), 1, 1 + u * 1.5); f.o.material.opacity = 0.9 * Math.pow(1 - u, 1.5); if (f.rot) f.o.rotation.y += f.rot * dt; }
      else if (f.kind === 'ring2') { if (u < 0) { f.o.material.opacity = 0; continue; } const e = 1 - Math.pow(1 - u, 3); f.o.scale.setScalar(0.01 + e * f.max); f.o.material.opacity = f.op * Math.pow(1 - u, 1.3); }
      else if (f.kind === 'flare') { const e = 1 - Math.pow(1 - u, 3); f.o.scale.set(0.05 + f.w * e, f.h * (1 - u * 0.5), 1); f.o.material.opacity = f.op * Math.pow(1 - u, 2); }
      else if (f.kind === 'shell') { const e = 1 - Math.pow(1 - u, 2.4); f.o.scale.set(f.max * e, f.max * e * 0.55, f.max * e); f.o.material.opacity = f.op * Math.pow(1 - u, 1.8); }
      else if (f.kind === 'beam') { const e = ease(Math.min(1, u * 2.5)); f.o.scale.set(0.02 + 0.05 * Math.sin(Math.PI * Math.min(1, u * 1.2)), 0.01 + f.hgt * e, 0.02 + 0.05 * Math.sin(Math.PI * Math.min(1, u * 1.2))); f.o.material.opacity = 0.85 * Math.pow(1 - u, 1.4); }
      else if (f.kind === 'core') { const e = Math.pow(u, 1.6); f.o.scale.setScalar(0.03 + e * 0.16 + Math.sin(now * 60) * 0.008 * u); f.o.material.opacity = Math.min(1, u * 1.6); }
      else if (f.kind === 'imp') { if (u < 0) continue; const e = Math.pow(u, 2.2); f.o.position.copy(f.c).addScaledVector(f.d, f.r * (1 - e)); f.o.material.opacity = Math.sin(Math.PI * u) * 0.95; f.o.scale.setScalar(0.012 + 0.012 * e); }
      else if (f.kind === 'neb') { if (u < 0) continue; f.o.position.addScaledVector(f.vel, dt); f.o.scale.setScalar(f.s0 + ease(u) * f.s1); f.o.material.opacity = f.op * Math.pow(Math.sin(Math.PI * u), 0.8); }
      else if (f.kind === 'roll') { if (u < 0) continue; f.o.position.addScaledVector(f.vel, dt); f.vel.multiplyScalar(Math.pow(0.12, dt)); f.o.scale.setScalar(0.04 + ease(u) * f.s1); f.o.material.opacity = 0.55 * Math.sin(Math.PI * Math.min(1, u * 1.3 + 0.08)) * (1 - u); }
      else if (f.kind === 'rim') { f.o.scale.setScalar(0.12 + 0.05 * ease(Math.min(1, u * 3))); f.o.material.opacity = 1.2 * Math.pow(1 - u, 1.2) * (0.85 + 0.15 * Math.sin(now * 23)); }
      else if (f.kind === 'rock') {
        f.vel.y -= 3.2 * dt; f.o.position.addScaledVector(f.vel, dt); f.o.rotation.x += f.spin.x * dt; f.o.rotation.y += f.spin.y * dt; f.o.rotation.z += f.spin.z * dt;
        if (f.o.position.y < TY + f.r) { f.o.position.y = TY + f.r; f.vel.y = Math.abs(f.vel.y) * 0.32; f.vel.x *= 0.62; f.vel.z *= 0.62; f.spin.multiplyScalar(0.6); }
        f.o.material.emissive.lerpColors(ROCK_HOT, ROCK_COLD, Math.min(1, u * 0.9)); f.o.material.emissiveIntensity = 3 * (1 - Math.min(1, u * 0.95)); f.o.scale.setScalar(u > 0.8 ? 1 - (u - 0.8) / 0.2 : 1);
      }
      else if (f.kind === 'emb') {
        const P = f.o.geometry.attributes.position, C = f.o.geometry.attributes.color, a = P.array, dr = Math.pow(0.35, dt);
        for (let j = 0; j < f.vel.length; j++) { const v = f.vel[j]; v.multiplyScalar(dr); v.y -= 0.9 * dt; a[j * 3] += v.x * dt; a[j * 3 + 1] += v.y * dt; a[j * 3 + 2] += v.z * dt; if (a[j * 3 + 1] < TY + 0.003) { a[j * 3 + 1] = TY + 0.003; v.y = Math.abs(v.y) * 0.25; } const k = Math.max(0, 1 - u / f.lf[j]), b = f.base[j]; C.array[j * 3] = b.r * k; C.array[j * 3 + 1] = b.g * k * k; C.array[j * 3 + 2] = b.b * k * k * k; }
        P.needsUpdate = true; C.needsUpdate = true;
      }
      else if (f.kind === 'scorch') { f.o.material.opacity = (f.op || 0.55) * Math.min(1, u * 8) * (1 - ss(Math.max(0, (u - 0.5) / 0.5))); }
      else if (f.kind === 'met') { if (u < 0) { f.o.material.opacity = 0; continue; } const e = u * u; f.o.position.lerpVectors(f.s0, f.e, e); f.o.material.opacity = 1; trail(f.o.position, '#FF8A3D', 0.09, 0.55); trail(f.o.position, '#FFF1C0', 0.035, 0.85); if (u > 0.92 && !f.done) { f.done = 1; embers(f.e, ['#FFF1C0', '#FFB347', '#FF6A2E'], 40, 0.55, 0.9, 0.012); ring2(f.e, '#FF8A3D', 0.6, 0.3, 0.9); rocks(f.e, 3, 0.5); dust(f.e, 5, 0.1); } }
      else if (f.kind === 'dust') { f.o.position.addScaledVector(f.vel, dt); f.vel.multiplyScalar(0.94); f.o.scale.setScalar(0.012 + ease(u) * 0.05); f.o.material.opacity = 0.22 * (1 - u); }
      else if (f.kind === 'smoke') { f.o.material.opacity = 0.32 * (1 - u); f.o.scale.setScalar(0.02 + ease(u) * 0.11); f.o.position.y += dt * 0.03; }
      else { f.o.material.opacity = 0.8 * (1 - u); f.o.scale.setScalar(0.03 * (1 - u * 0.6)); }
    }
  }

  // ---------- anchors ----------
  const anchors = {};
  const A2D = { a1: new THREE.Vector3(1.0, TY, 0.05), a2: new THREE.Vector3(-0.98, TY, -0.62), a3: new THREE.Vector3(-1.0, TY, 0.05) };
  const anchorWorld0 = { a1: () => (flat2d ? { p: A2D.a1.clone() } : view === 'top' ? poseIn(seatF[1], 0, 0.8, 1.22) : poseIn(seatF[1], 0, 0.86, 0.98)).p, a2: () => (flat2d ? { p: A2D.a2.clone() } : view === 'top' ? poseIn(seatF[2], 0.56, 0.86, 0.98) : poseIn(seatF[2], 0, 1.16, 1.14)).p, a3: () => (flat2d ? { p: A2D.a3.clone() } : view === 'top' ? poseIn(seatF[3], 0, 0.8, 1.22) : poseIn(seatF[3], 0, 0.86, 0.98)).p, disc: () => poseIn(seatF[0], 0.6, TY + 0.02, 0.6).p, ind: () => poseIn(seatF[0], 0, TY, 0.11).p, deck: () => new THREE.Vector3(0, TY + 0.13, 0) };
  const anchorWorld = { ...anchorWorld0, ind: () => new THREE.Vector3(PX0 + deckIM.position.x, TY + 0.002, 0.07) };
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
      dur: durOverride || (slide ? cl(0.18 + dist * 0.6, 0.18, 0.34) : mode === 'lay' ? cl(0.3 + dist * 0.3, 0.3, 0.55) : cl(0.42 + dist * 0.38, 0.42, 0.85)), arc: slide || mode === 'lay' ? 0 : cl(dist * 0.42, 0.05, 0.24) };
    if (mode === 'deal') {
      const sx = hash(t.id + 'x') - 0.5;
      t.tw.c1 = p0.clone().add(new THREE.Vector3(sx * 0.05, 0.12 + hash(t.id + 'y') * 0.04, 0));
      t.tw.c2 = tgt.p.clone().lerp(p0, 0.18); t.tw.c2.y = Math.max(t.tw.c2.y, tgt.p.y) + 0.1;
      t.tw.dur = 0.56 + cl(dist * 0.2, 0, 0.24); t.tw.arc = 0; t.tw.spin = (hash(t.id + 's') > 0.5 ? 1 : -1) * Math.PI * 0.5;
    } else if (mode === 'nuke') {
      t.tw.nuke = true; t.tw.apex = p0.clone().lerp(new THREE.Vector3(0, TY, 0), 0.4); t.tw.apex.y = TY + 0.58; t.tw.dur = 1.75; t.tw.arc = 0; t.tw.fx = 'nuke';
    } else if (mode === 'block') {
      // Balya taşıma: blok halinde kalkar, gidiş yönüne hafif eğilir, yumuşakça iner.
      t.tw.arc = cl(0.04 + dist * 0.06, 0.04, 0.09); t.tw.dur = durOverride || cl(0.46 + dist * 0.22, 0.46, 0.72); t.tw.smooth = true;
      const hd = tgt.p.clone().sub(p0).setY(0); if (hd.lengthSq() > 1e-6) { t.tw.axis = new THREE.Vector3().crossVectors(UP, hd.normalize()); t.tw.roll = cl(dist * 0.18, 0.03, 0.11); }
    } else if (mode === 'drop') {
      t.tw.slide = true; t.tw.dur = durOverride || 0.3;
    } else if (mode === 'flip') {
      const out = new THREE.Vector3(-p0.x, 0, -p0.z).normalize();
      t.tw.c1 = p0.clone().addScaledVector(out, 0.1).add(new THREE.Vector3(0, 0.03, 0)); t.tw.c2 = tgt.p.clone().addScaledVector(out, 0.05).add(new THREE.Vector3(0, 0.2, 0));
      t.tw.dur = durOverride || 1.1; t.tw.arc = 0; t.tw.spin = 0;
    } else if (!slide && mode !== 'lay') {
      const dir = tgt.p.clone().sub(p0).setY(0); if (dir.lengthSq() > 1e-6) { t.tw.axis = dir.normalize(); t.tw.roll = (hash(t.id + 'roll') - 0.5) * 0.7; }
    }
  }
  function sync(list, flags, opts = {}) {
    const wasLeft = !!fl.pileLeft; lastList = list; Object.assign(fl, flags || {}); deckHeap = !!fl.heap;
    if (wasLeft !== !!fl.pileLeft) { deckShown = -1; if (opts.instant) deckIM.position.x = pileX(); }
    if (flags && flags.targets) for (const id in fl.targets) fl.targets[id].forEach((tg) => { tg.p = poseFor({ id: '', at: tg.at }).p; });
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
      setFace(t, d.c || null, d.n || null, flat2d || ['i', 'm', 'd', 'f', 's', 'c'].includes(d.at[0]));
      t.glow = !!d.glow; setTag(t, d.at[0] === 'r' && d.at[1] === 0 ? d.tag : null, d.tagL || '');
      const tgt = poseFor(d);
      const changed = !t.tgt || t.tgt.p.distanceToSquared(tgt.p) > 1e-8 || 1 - Math.abs(t.tgt.q.dot(tgt.q)) > 1e-6;
      if (changed) {
        t.tgt = tgt;
        if (drag && (drag.t === t || (drag.groupSet && drag.groupSet.has(t)))) continue;
        if (opts.instant) { t.g.position.copy(tgt.p); t.g.quaternion.copy(tgt.q); t.tw = null; t.g.visible = true; }
        else {
          const slide = prev && prev.at[0] === 'r' && d.at[0] === 'r' && prev.at[1] === d.at[1];
          const fxk = d.dl || slide ? null : d.at[0] === 'd' ? 'disc' : d.at[0] === 'r' && d.at[1] === 0 && !slide ? 'mine' : d.at[0] === 'm' ? 'meld' : 'travel';
          const dealing = fresh && d.dl, lay = prev && prev.at[0] === 'r' && d.at[0] === 'm';
          startTween(t, tgt, d.dl || 0, d.dur || 0, d.mv ? d.mv : d.nuke ? 'nuke' : dealing ? 'deal' : slide ? 'slide' : lay ? 'lay' : 'travel', d.mv ? d.fx || null : lay ? null : d.at[0] === 'i' ? 'reveal' : d.at[0] === 'f' ? (d.nuke ? 'nuke' : 'finish') : dealing ? null : fxk);
          if (d.nuke && t.tw && t.tw.nuke) { t.tw.style = d.fxs || 'nuke'; if (t.tw.style === 'meteor') { t.tw.apex = t.tw.p0.clone().add(new THREE.Vector3(0, 1.7, 0)); t.tw.fall = t.tw.p1.clone().add(new THREE.Vector3(-0.75, 1.5, -0.55)); t.tw.dur = 1.8; } }
        }
      }
    }
    for (const [id, t] of tiles) if (!seen.has(id)) { scene.remove(t.g); tiles.delete(id); desc.delete(id); }
    pendingSpawn = null; if (ghost) { scene.remove(ghost.g); ghost = null; }
    if (flags && flags.deckAnim) deckAnim = { a: flags.deckAnim[0], b: flags.deckAnim[1], t0: now + (flags.deckAnim[3] || 0) / 1000, dur: flags.deckAnim[2] / 1000 };
    else if (!deckAnim) layoutDeck(fl.deck);
    seatLed.forEach((m, i) => { m.emissiveIntensity = fl.seats[i + 1] ? 2.4 : 0.25; });
    chairs.forEach((c, i) => { c.visible = !flat2d; });
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
    const sideOk = fl.canDraw && fl.sideId && tiles.get(fl.sideId);
    if (fl.canDraw) { objs.push(deckHit); if (sideOk) objs.push(tiles.get(fl.sideId).body, sideHit); }
    const h = ray.intersectObjects(objs, false)[0];
    if (!h) { if (sideOk && screenNear(sideRing.position, 135)) return { kind: 'side', id: fl.sideId, t: tiles.get(fl.sideId) }; return null; }
    if (h.object === deckHit) return { kind: 'deck' };
    if (h.object === sideHit) return { kind: 'side', id: fl.sideId, t: tiles.get(fl.sideId) };
    const id = h.object.userData.id; if (id === fl.sideId) return { kind: 'side', id, t: tiles.get(id) };
    const d = desc.get(id); return { kind: 'rack', id, t: tiles.get(id), from: { r: d.at[2], i: d.at[3] }, seg: d.seg };
  }
  function screenNear(w, px) { const s = project(w), mx = (ndc.x + 1) / 2 * W, my = (1 - ndc.y) / 2 * H; return Math.hypot(s.x - mx, s.y - my) < px; }
  function inZoneTight() { const p = ray.ray.intersectPlane(tablePlane, new THREE.Vector3()); return !!p && p.distanceTo(zone.position) < 0.14; }
  function inZone() { return inZoneTight() || screenNear(zone.position, 100); }
  function zoneDrop() { return inZoneTight() || (!slotAt(rackHit()) && inZone()); }
  // Geri bırakma alanı (soldan alınan taş): geniş yakalama + mıknatıs, halka parlar
  function returnNear() { if (slotAt(rackHit())) return false; const p = ray.ray.intersectPlane(tablePlane, new THREE.Vector3()); return (!!p && p.distanceTo(sideRing.position) < 0.22) || screenNear(sideRing.position, 160); }
  function ringGlow(v) { sideRing.material.opacity = v; sideRing.visible = v > 0; }
  function onDown(e) {
    if (e.button !== 0) return; setRay(e);
    const p = pick();
    if (!p && selId && fl.canMeld) { const tg = nearTarget(selId, 60), d0 = tg && desc.get(selId); if (tg && d0) { selId = null; o.onMeldDrop && o.onMeldDrop({ r: d0.at[2], i: d0.at[3] }, tg.id, tg.kind); return; } }
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
      else if (drag.moved) { drag.pose = followPose(); drag.inZone = drag.kind === 'rack' && fl.canDiscard && zoneDrop(); if (drag.inZone) drag.pose = { p: zone.position.clone().add(new THREE.Vector3(0, 0.03 + Math.sin(now * 8) * 0.004, 0)), q: seatF[0].getWorldQuaternion(new THREE.Quaternion()).multiply(Q_FLAT) };
        const back = drag.kind === 'rack' && fl.returnId && drag.id === fl.returnId;
        drag.inReturn = back && returnNear(); if (drag.inReturn) { drag.inZone = false; drag.pose = { p: sideRing.position.clone().add(new THREE.Vector3(0, 0.035 + Math.sin(now * 8) * 0.004, 0)), q: seatF[3].getWorldQuaternion(new THREE.Quaternion()).multiply(Q_FLAT) }; }
        ringGlow(drag.kind === 'side' ? 0.32 : back ? (drag.inReturn ? 0.7 : 0.3) : 0);
        drag.target = drag.kind === 'rack' && !drag.inZone && !drag.inReturn && fl.canMeld && !slotAt(rackHit()) ? nearTarget(drag.id) : null; if (drag.target) drag.pose = { p: drag.target.p.clone().add(new THREE.Vector3(0, drag.target.kind === 'okey' ? 0.035 : 0.022, 0)), q: Q_FLAT.clone() }; if (drag.kind === 'rack') { const sl = slotAt(rackHit()), sk = sl && !(sl.r === drag.from.r && sl.i === drag.from.i) ? sl.r + ':' + sl.i : 'none'; if (sk !== drag.prevKey) { drag.prevKey = sk; previewGroup(sk === 'none' ? null : sl); } } }
      canvas.style.cursor = drag.inZone || drag.target || drag.inReturn ? 'copy' : 'grabbing'; return;
    }
    const p = pick(); hoverId = p && p.id ? p.id : p && p.kind === 'deck' ? '_deck' : null;
    ringGlow(p && p.kind === 'side' ? 0.28 : 0);   // soldan alınabilir: halka yanar
    canvas.style.cursor = p ? (p.kind === 'deck' ? 'pointer' : 'grab') : selId && fl.canDiscard && inZone() ? 'pointer' : 'default';
  }
  function onUp(e) {
    if (!drag) return; setRay(e); const d = drag; drag = null; canvas.style.cursor = 'default'; if (d.holdT) clearTimeout(d.holdT); ringGlow(0);
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
      if (fl.returnId && d.t && d.t.id === fl.returnId && (d.inReturn || returnNear())) { o.onReturn && o.onReturn(); }
      else if (fl.canDiscard && (d.inZone || zoneDrop())) { o.onDiscard && o.onDiscard(d.from); }
      else if (d.target) { o.onMeldDrop && o.onMeldDrop(d.from, d.target.id, d.target.kind); }
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
    M.led.emissive.set(acc); seatLed.forEach((m) => m.emissive.set(acc)); M.ledLine.color.set(acc); M.zone.color.set(acc); M.zoneFill.color.set(acc); sideRing.material.color.set(acc); pzLine.material.color.set(acc);
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
  function setTileSkin(k) { if (!k || k === tileSkin) return; tileSkin = k; tileKit.applyBody(M.tile, k, theme, M.tileSide); tiles.forEach((t) => { const c = t.c, n = t.n, fl0 = t.flat; t.c = null; setFace(t, c, n, fl0); }); }
  function setSkins(sk) { skins = { ...skins, ...sk }; applyLook(); }
  function setOptions(op) { Object.assign(fl, op); }
  let lastList = [];
  function setTileDepth(d) {
    if (!d || Math.abs(d - TD) < 1e-5) return; TD = d; const old = tileGeo; tileGeo = buildTileGeo();
    tiles.forEach((t) => { t.body.geometry = tileGeo; t.face.position.z = TD / 2 + 0.0006; t.bar.position.z = TD / 2 - 0.002; t.tgt = null; });
    deckIM.geometry = tileGeo; deckShown = -1; layoutDeck(fl.deck); old.dispose(); buildRacks();
    const keep = desc; sync(lastList, null, { instant: true });
  }
  function setFont(f) { if (!f || f === font) return; font = f; Object.keys(faceCache).forEach((k) => { faceCache[k].map.dispose(); delete faceCache[k]; }); tiles.forEach((t) => { const c = t.c, n = t.n, fl0 = t.flat; t.c = null; setFace(t, c, n, fl0); }); applyLook(); }
  applyLook();
  // 2D masa: tepeden ortografik kamera, ıstakalar düz yatar, oyuncunun ıstakası masanın önünde büyük; oda gizlenir. Etkileşimler aynen çalışır.
  const R2D = 2.4;   // 2D'de oyuncu ıstakası ve taşları bu oranda büyür
  function setView2D(on) {
    on = !!on; if (on === flat2d) return; flat2d = on; camera = on ? ocam : pcam;
    rackF.forEach((r, k) => { if (on) { r.rotation.x = -Math.PI / 2; r.position.set(0, TY + 0.014, k === 0 ? 1.27 : RZ + 0.03); r.scale.setScalar(k === 0 ? R2D : 1); } else { r.rotation.x = TILT; r.position.set(0, TY + 0.033, RZ); r.scale.setScalar(1); } r.updateMatrixWorld(true); });
    scene.children.forEach((m) => { if (['zemin', 'pencere_esik', 'esik_led', 'duvar', 'kose'].includes(m.name) || /^dikme/.test(m.name)) m.visible = !on; });
    chairs.forEach((c) => { c.visible = !on; });
    tiles.forEach((tl) => { const c = tl.c, n = tl.n; tl.c = null; setFace(tl, c, n, on || tl.flat); tl.tgt = null; tl.tw = null; });
    renderer.toneMappingExposure = on ? 1.12 : 1.05; const g = on ? M2D : 1; pzLine.position.x = (BX0 + 14 * BDX) * g; pzLine.scale.set(1, 1, g); pzLabel.position.set((BX0 + 17 * BDX) * g, TY + 0.0012, BZ0 * g - 0.062); deckShown = -1;
    if (lastList.length) sync(lastList, null, { instant: true });
  }
  function setCamera(name, dur = 1.6) { if (name === 'player') name = view; if (name === 'seat' || name === 'top' || name === 'mid') view = name; const v = VIEWS[name]; if (!v) return; if (!dur) { cam.p.copy(v.p); cam.t.copy(v.t); cam.a = null; return; } cam.a = { p: cam.p.clone(), t: cam.t.clone() }; cam.b = v; cam.t0 = now; cam.dur = dur; }

  // ---------- loop ----------
  let now = performance.now() / 1000, last = now, raf = 0, alive = true, visible = true, susp = false;
  const io = window.IntersectionObserver ? new IntersectionObserver((e) => { visible = e[0].isIntersecting; }) : null; if (io) io.observe(canvas);
  const liftDir = new THREE.Vector3(), qSpin = new THREE.Quaternion(), tmpTrail = new THREE.Vector3();
  function tick() {
    if (!alive) return; raf = requestAnimationFrame(tick);
    if (!visible || document.hidden || susp) { last = performance.now() / 1000; return; }
    frame();
  }
  function frame() {
    now = performance.now() / 1000; const dt = Math.min(0.05, now - last); last = now;
    const t = now;
    if (cam.a) { const u = Math.min(1, (now - cam.t0) / cam.dur), e = ss(u); cam.p.lerpVectors(cam.a.p, cam.b.p, e); cam.t.lerpVectors(cam.a.t, cam.b.t, e); if (u >= 1) cam.a = null; }
    if (flat2d) { ocam.position.set(0, 6, OZ); if (now < shakeUntil) { const k = (shakeUntil - now) * 0.02; ocam.position.x += (Math.random() - 0.5) * k; ocam.position.z += (Math.random() - 0.5) * k; } ocam.lookAt(ocam.position.x, TY, ocam.position.z); }
    else {
    camera.position.copy(cam.p); camera.position.x += Math.sin(t * 0.21) * 0.006; camera.position.y += Math.sin(t * 0.33) * 0.004;
    if (now < shakeUntil) { const k = (shakeUntil - now) * 0.03, sh = Math.sin(now * 61) * 0.6 + Math.sin(now * 37) * 0.4; camera.position.x += sh * k * 0.5; camera.position.y += Math.cos(now * 53) * k * 0.4; }
    camera.lookAt(cam.t);
    }
    tickMotes(t); motes.visible = !flat2d;
    { const pzOn = !!fl.pairZone && !deckHeap; if (pzOn && pzText !== fl.pairZone) { pzText = fl.pairZone; if (pzLabel.material.map) pzLabel.material.map.dispose(); pzLabel.material.map = pzTex(pzText); pzLabel.material.needsUpdate = true; } pzA += ((pzOn ? 1 : 0) - pzA) * Math.min(1, dt * 4); pzLine.visible = pzLabel.visible = pzA > 0.01; pzLine.material.opacity = 0.32 * pzA; pzLabel.material.opacity = 0.5 * pzA; }
    tickDie(); tickMarkers(dt);
    { const tx = deckHeap ? 0 : pileX(); deckIM.position.x += (tx - deckIM.position.x) * Math.min(1, dt * 3.2); deckGlow.position.x = deckIM.position.x; pileK += ((fl.pileLeft ? 1 : 0) - pileK) * Math.min(1, dt * 3); tickTags(dt); } stackMark.material.opacity += ((markOn ? 0.6 + 0.3 * Math.sin(now * 6) : 0) - stackMark.material.opacity) * Math.min(1, dt * 8);
    if (deckAnim) { const u = Math.max(0, Math.min(1, (now - deckAnim.t0) / deckAnim.dur)); layoutDeck(deckAnim.a + (deckAnim.b - deckAnim.a) * u); if (u >= 1) { deckAnim = null; layoutDeck(fl.deck); } }
    for (const tl of tiles.values()) {
      if (drag && (drag.t === tl || (drag.groupSet && drag.groupSet.has(tl)))) continue;
      if (tl.tw) {
        const u = (now - tl.tw.t0) / tl.tw.dur;
        if (u >= 0) {
          tl.g.visible = true; const e = ease(Math.min(1, u));
          const e2 = tl.tw.slide ? e : tl.tw.smooth ? easeS(u) : ss(u);
          if (tl.tw.nuke && tl.tw.style === 'meteor') {
            const w = tl.tw, U = Math.min(1, u);
            if (U < 0.32) { const e = Math.pow(U / 0.32, 2.2); tl.g.position.lerpVectors(w.p0, w.apex, e); w.sc = 1; if (Math.random() < 0.6) spark(tl.g.position, '#FFE08A'); }
            else if (U < 0.5) { tl.g.position.copy(w.apex); w.sc = 0.001; }
            else { const v = (U - 0.5) / 0.5, e = v * v; tl.g.position.lerpVectors(w.fall, w.p1, e); w.sc = 1.35 - e * 0.35; for (let q = 0; q < 2; q++) spark(tl.g.position.clone().add(new THREE.Vector3((Math.random() - 0.5) * 0.02, (Math.random() - 0.5) * 0.02, (Math.random() - 0.5) * 0.02)), Math.random() < 0.5 ? '#FF7A3D' : '#FFD27A'); trail(tl.g.position, '#FF9A4A', 0.16, 0.55); trail(tl.g.position, '#FFF1C0', 0.06, 0.9); if (Math.random() < 0.9) smoke(tl.g.position); }
            tl.g.quaternion.slerpQuaternions(w.q0, w.q1, easeIO(Math.min(1, U * 1.2))).premultiply(qSpin.setFromAxisAngle(UP, (1 - easeIO(U)) * Math.PI * 6));
          } else if (tl.tw.nuke) {
            const w = tl.tw, U = Math.min(1, u), SC = { blackhole: ['#B79CFF', '#7FE3FF'], supernova: ['#FFFFFF', '#9FD8FF'] }[w.style] || ['#FFE08A', '#FFB347'];
            if (U < 0.46) { const e = 1 - Math.pow(1 - U / 0.46, 3); tl.g.position.lerpVectors(w.p0, w.apex, e); w.sc = 1 + e * 0.15; if (Math.random() < 0.5) spark(tl.g.position, SC[0]); }
            else if (U < 0.62) { const v = (U - 0.46) / 0.16; tl.g.position.copy(w.apex); tl.g.position.x += (Math.random() - 0.5) * 0.004 * v; tl.g.position.y += Math.sin(v * Math.PI) * 0.015 + (Math.random() - 0.5) * 0.004 * v; w.sc = 1.15 + v * 0.25; spark(tl.g.position.clone().add(new THREE.Vector3((Math.random() - 0.5) * 0.08, (Math.random() - 0.5) * 0.08, (Math.random() - 0.5) * 0.08)), Math.random() < 0.5 ? '#FFF1C0' : '#FF8A3D'); }
            else { const v = (U - 0.62) / 0.38, e = v * v * v; tl.g.position.lerpVectors(w.apex, w.p1, e); w.sc = 1.4 - e * 0.4; spark(tl.g.position, SC[1]); spark(tl.g.position, '#FFF1C0'); if (w.style === 'nuke' && Math.random() < 0.8) smoke(tl.g.position); }
            tl.g.quaternion.slerpQuaternions(w.q0, w.q1, easeIO(Math.min(1, U * 1.3))).premultiply(qSpin.setFromAxisAngle(UP, (1 - easeIO(U)) * Math.PI * 5));
          } else if (tl.tw.c1) {
            const b = easeS(u), ib = 1 - b, w = tl.tw;
            tl.g.position.set(0, 0, 0).addScaledVector(w.p0, ib * ib * ib).addScaledVector(w.c1, 3 * ib * ib * b).addScaledVector(w.c2, 3 * ib * b * b).addScaledVector(w.p1, b * b * b);
            tl.g.quaternion.slerpQuaternions(w.q0, w.q1, ss(u * 1.08)).premultiply(qSpin.setFromAxisAngle(UP, (1 - ss(u)) * w.spin));

          } else {
          tl.g.position.lerpVectors(tl.tw.p0, tl.tw.p1, e2); tl.g.position.y += Math.sin(Math.PI * e2) * tl.tw.arc;
          tl.g.quaternion.slerpQuaternions(tl.tw.q0, tl.tw.q1, tl.tw.slide ? e : tl.tw.smooth ? ss(u) : easeIO(Math.min(1, u * 1.15)));
          if (tl.tw.axis) tl.g.quaternion.premultiply(qSpin.setFromAxisAngle(tl.tw.axis, Math.sin(Math.PI * Math.min(1, u)) * tl.tw.roll));
          }
          if (tl.tw.fx && tl.tw.fx !== 'nuke' && tl.tw.fx !== 'reveal' && u < 0.94) {
            const lp = tl.lastP || tl.g.position, col = tl.tw.fx === 'meld' ? '#8EF0B0' : '#FFD98A';
            for (let q = 1; q <= 3; q++) trail(tmpTrail.lerpVectors(lp, tl.g.position, q / 3), col, 0.05, 0.14 * Math.sin(Math.PI * Math.min(1, u * 1.15)));
          }
          tl.lastP = (tl.lastP || new THREE.Vector3()).copy(tl.g.position);
          if (u >= 1) {
            const f = tl.tw.fx, wasDeal = !!tl.tw.c1; tl.tw = null; tl.lastP = null; tl.g.position.copy(tl.tgt ? (tl.ovr || tl.tgt).p : tl.g.position);
            if (wasDeal) { tl.hop = now; tl.hopA = 0.0035; tl.hopD = 0.26; }
            if (f === 'nuke') { const st = (desc.get(tl.id) || {}).fxs || 'nuke'; (FIN_FX[st] || nukeFx)(new THREE.Vector3(0, TY, 0)); tl.pop = now; }
            if (f === 'disc' || f === 'travel') { tl.hop = now; tl.hopA = 0.007; tl.hopD = 0.24; }
            if (f === 'reveal') { burst(tl.g.position.clone(), '#FFE08A', 40, 0.5); wave(tl.g.position.clone().setY(TY), theme.a1); spark(tl.g.position, '#ffffff'); tl.pop = now; }
            if (f === 'finish') { burst(tl.g.position.clone(), '#FFD964', 60, 0.8); wave(new THREE.Vector3(0, TY, 0), '#FFD964'); setTimeout(() => wave(new THREE.Vector3(0, TY, 0), theme.a1), 180); tl.pop = now; }
            if (f === 'disc' || f === 'meld') { wave(tl.g.position.clone().setY(TY), f === 'meld' ? '#3CCB6E' : '#FFD964'); burst(tl.g.position.clone(), '#FFE08A', 12, 0.25); tl.pop = now; }
            else if (f === 'mine') { tl.newUntil = now + 1.6; tl.pop = now; burst(tl.g.position.clone(), '#FFE08A', 10, 0.18); }
            else if (f === 'travel') tl.pop = now;
          }
        }
      } else if (tl.ovr || tl.tgt) { const b = tl.ovr || tl.tgt; tl.g.position.copy(b.p); tl.g.quaternion.copy(b.q); }
      if (tl.hop && !tl.tw) { const v = (now - tl.hop) / (tl.hopD || 0.5); if (v >= 1) tl.hop = 0; else if (v > 0) tl.g.position.y += Math.abs(Math.sin(Math.PI * 2 * v)) * Math.pow(1 - v, 2.2) * tl.hopA; }   // iki küçük sekme, sönümlü
      const isNew = tl.newUntil > now; tl.bar.visible = tl.glow || isNew; tl.bar.material = isNew && !tl.glow ? M.newBar : M.ok; if (isNew) M.newBar.opacity = 0.55 + 0.45 * Math.sin(now * 9);
      const pv = (now - tl.pop) / 0.34, dd0 = flat2d && desc.get(tl.id), s2 = !dd0 ? 1 : dd0.at[0] === 'r' && dd0.at[1] === 0 ? R2D : dd0.at[0] === 'm' ? M2D : 1; tl.g.scale.setScalar(s2 * (pv >= 0 && pv < 1 ? 1 + Math.sin(Math.PI * pv) * (1 - pv) * 0.05 : 1) * (tl.tw && tl.tw.nuke ? tl.tw.sc || 1 : 1));
      const want = tl.id === selId ? 0.022 : tl.id === hoverId && fl.dragRack ? 0.008 : 0;
      tl.lift += (want - tl.lift) * Math.min(1, dt * 14);
      if (tl.lift > 0.0002 && !tl.tw) { liftDir.set(0, 1, 0).applyQuaternion(tl.g.quaternion); tl.g.position.addScaledVector(liftDir, tl.lift); }
    }
    if (drag && drag.groupActive && drag.poses) drag.members.forEach((m, j) => { const ps = drag.poses[j]; if (!ps) return; m.t.g.visible = true; m.t.g.position.lerp(ps.p, Math.min(1, dt * 20)); m.t.g.quaternion.slerp(ps.q, Math.min(1, dt * 14)); m.t.g.scale.setScalar(1.05); m.t.bar.visible = true; m.t.bar.material = M.ok; });
    if (drag && drag.t && drag.pose && !drag.groupActive) { drag.t.g.visible = true; drag.t.g.position.lerp(drag.pose.p, Math.min(1, dt * 22)); drag.t.g.quaternion.slerp(drag.pose.q, Math.min(1, dt * 14)); }
    const pulse = 0.5 + 0.5 * Math.sin(t * 5);
    const zoneHot = fl.canDiscard, zIn = !!(drag && drag.inZone), zDrag = !!(drag && drag.moved && drag.kind === 'rack' && zoneHot);
    zone.userData.s = (zone.userData.s || 1) + ((zIn ? 1.45 : zDrag ? 1.2 : zoneHot ? 1 + pulse * 0.08 : 1) - (zone.userData.s || 1)) * Math.min(1, dt * 12);
    M.zone.opacity = zIn ? 1 : zoneHot ? 0.55 + pulse * 0.45 : 0.16; M.zoneFill.opacity = zIn ? 0.42 : zDrag ? 0.22 : zoneHot ? 0.12 + pulse * 0.12 : 0; zone.scale.setScalar(zone.userData.s); zoneFill.scale.setScalar(zone.userData.s);
    deckGlow.material.opacity = fl.canDraw ? 0.25 + pulse * 0.35 : 0.0;
    const sRet = !!(fl.returnId && drag && drag.moved && drag.t && drag.t.id === fl.returnId), sHot = (fl.canDraw && fl.sideId) || sRet, sHov = (sHot && hoverId === fl.sideId) || (sRet && screenNear(sideRing.position, 95));
    sideRing.userData.s = (sideRing.userData.s || 1) + ((sHov ? 1.4 : sHot ? 1.12 + pulse * 0.06 : 1) - (sideRing.userData.s || 1)) * Math.min(1, dt * 12);
    sideRing.material.opacity = sHov ? 1 : sHot ? 0.5 + pulse * 0.5 : 0; sideRing.scale.setScalar(sideRing.userData.s);
    const nk = now - nukeT; nukeLight.intensity = nk >= 0 && nk < 2 ? 90 * Math.exp(-nk * 3) : 0;
    const hb = Math.max(0, holoBoost - now); holo.rotation.y += dt * (0.12 + hb * 1.6); M.holo.opacity = (0.34 + Math.sin(t * 0.9) * 0.05) * (1 - pileK * 0.55) + Math.min(0.5, hb * 0.25);
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
    if (kind === 'die') { const g = new THREE.Group(); g.name = 'zar'; const m = die.clone(); m.visible = true; m.position.set(0, DIE / 2, 0); m.quaternion.identity(); m.scale.setScalar(1); g.add(m); return g; }
    if (kind === 'markers') {   // oyun içi işaretler: hale, deste parıltısı, atma halkası/dolgusu, soldan alma halkası, balya işareti, işleme hedefleri, taş simgeleri
      const skip = new Set([...TABLE_PARTS, ...ROOM_PARTS, 'zar', 'deste']), g = new THREE.Group(); g.name = 'isaretler';
      const vis = (m) => { const c = m.clone(); c.visible = true; c.material = Array.isArray(m.material) ? m.material.map((x) => x.clone()) : m.material.clone(); (Array.isArray(c.material) ? c.material : [c.material]).forEach((x) => { if (x.opacity < 0.3) x.opacity = 0.6; }); return c; };
      scene.children.forEach((m) => { if (!m.isMesh || m === deckHit || !m.name || skip.has(m.name) || m.name.startsWith('tas_')) return; const c = vis(m); if (m === sideRing) c.name = 'soldan_halka'; g.add(c); });
      const grp = (src, name, x) => { const c = src.clone(true); c.name = name; c.visible = true; c.traverse((o) => { if (o.isMesh) { o.material = o.material.clone(); if (o.material.opacity < 0.3) o.material.opacity = 0.8; } }); c.position.set(x, TY + 0.003, 0.95); return c; };
      g.add(grp(markers[0].g, 'isleme_hedefi', -0.12), grp(tags[0].g, 'tas_belirteci', 0.12));
      ['side', 'islek'].forEach((k, i) => { const p = new THREE.Mesh(tagGeo, tagMat(k)); p.name = 'simge_' + (k === 'side' ? 'yerden' : 'islek'); p.position.set(0.2 + i * 0.04, TY + 0.02, 0.95); g.add(p); });
      return g;
    }
    if (kind === 'scene') {   // tam sahne: oda + masa + ıstakalar + koltuklar + işaretler + ışıklar (oyundaki yerleşimle)
      const g = new THREE.Group(); g.name = 'sahne_tam';
      scene.children.forEach((c) => { if (c === deckHit || c.isSprite || c.isPoints) return; const k = c.clone(true); k.traverse((x) => { if (x.isSprite || x.isPoints) x.visible = false; }); g.add(k); });
      return g;
    }
    return null;
  }

  const api = {
    renderNow: () => { if (alive) frame(); },
    exportAsset,
    // Ekrandaki ıstaka sınırı (1280×720 px): HUD'ın ıstakayı örtmemesi için.
    rackRect: () => { if (flat2d) return null; const v = new THREE.Vector3(); let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9; rackF[0].updateMatrixWorld(true); for (const x of [-0.46, 0.46]) for (const y of [-0.01, 0.2]) for (const z of [-0.04, 0.04]) { v.set(x, y, z); rackF[0].localToWorld(v); const s = project(v); x0 = Math.min(x0, s.x); x1 = Math.max(x1, s.x); y0 = Math.min(y0, s.y); y1 = Math.max(y1, s.y); } return { x0, x1, y0, y1 }; },
    finFx: (k) => (FIN_FX[k] || FIN_FX.nuke)(new THREE.Vector3(0, TY, 0)),
    sync, sendGift, clearGifts, dealFx, rollDie, clearDice, markStack, debugSelect: (id) => { selId = id; }, celebrate, bindAnchor, anchorXY, setTheme, setSkins, setTileSkin, setOptions, setTileDepth,
    // Durum görüntüleri için (g101-state.js): taşı sürükleniyor gibi tut. to: 'ring' (soldan halkasına), 'target' (ilk uygun işleme hedefine), 'zone' (atma halkasına); k: 0..1 yol oranı.
    debugDrag: (id, to, k) => {
      if (!id) { drag = null; return; } const t = tiles.get(id), d = desc.get(id); if (!t || !d) return;
      const tg = to === 'ring' ? { p: sideRing.position.clone(), q: Q_DOWN.clone() } : to === 'zone' ? { p: zone.position.clone(), q: t.g.quaternion.clone() } : (((fl.targets && fl.targets[id]) || [])[0] || null);
      const a = t.g.position.clone(), b = tg ? tg.p.clone() : a.clone().add(new THREE.Vector3(0, 0.08, -0.15)), f = k == null ? 0.7 : k;
      const p = a.lerp(b, f); p.y += 0.06; drag = { kind: 'rack', id, t, from: { r: d.at[2], i: d.at[3] }, moved: true, sx: 0, sy: 0, pose: { p, q: t.g.quaternion.clone() }, inReturn: to === 'ring' && f > 0.85, inZone: to === 'zone' && f > 0.85, target: to === 'target' && f > 0.85 && tg ? { ...tg, id: tg.id } : null };
      if (to === 'ring') ringGlow(f > 0.85 ? 0.7 : 0.3);
    }, setFont, setScale, setCamera, setView2D, is2D: () => flat2d, thumbs,
    giftCount: (k) => gifts[k].filter((g) => g.state !== 'out').length, shake: (d) => { shakeUntil = now + (d || 0.5); }, suspend: (v) => { susp = !!v; },
    destroy() { alive = false; cancelAnimationFrame(raf); if (io) io.disconnect(); canvas.removeEventListener('pointerdown', onDown); canvas.removeEventListener('pointermove', onMove); canvas.removeEventListener('pointerup', onUp); renderer.dispose(); },
  };
  window.__galaxyTable = api;
  return api;
}
