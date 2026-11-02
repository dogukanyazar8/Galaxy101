// Galaxy 101 — tile skins (store: Galaxy / Gold / Neon / Marble + classic) and face art, incl. sahte okey.
import * as THREE from 'https://unpkg.com/three@0.184.0/build/three.module.js';

// Galaxy 101 tile numerals: monoline, round caps, 60×100 em (y down), stroke 14. Same paths are used in HTML previews.
export const GLYPHS = {
  0: { w: 60, d: 'M30 7C46 7 53 22 53 50C53 78 46 93 30 93C14 93 7 78 7 50C7 22 14 7 30 7Z' },
  1: { w: 42, d: 'M8 24L28 8V93' },
  2: { w: 60, d: 'M9 28C9 15 18 7 30 7C43 7 51 15 51 28C51 42 40 52 9 93H53' },
  3: { w: 60, d: 'M10 7H50L27 40C43 39 53 50 53 65C53 82 42 93 29 93C19 93 11 88 7 80' },
  4: { w: 62, d: 'M40 93V7L7 66H55' },
  5: { w: 60, d: 'M50 7H15L12 43C17 39 23 37 30 37C44 37 53 48 53 64C53 81 43 93 29 93C19 93 11 88 7 80' },
  6: { w: 60, d: 'M42 7L14 54M53 70A23 23 0 1 1 7 70A23 23 0 1 1 53 70Z' },
  7: { w: 60, d: 'M7 7H53L22 93' },
  8: { w: 60, d: 'M48 26A18.5 18.5 0 1 1 11 26A18.5 18.5 0 1 1 48 26ZM53 70A23 23 0 1 1 7 70A23 23 0 1 1 53 70Z' },
  9: { w: 60, d: 'M18 93L46 46M7 30A23 23 0 1 1 53 30A23 23 0 1 1 7 30Z' },
};
const P2D = {}; const path = (k) => P2D[k] || (P2D[k] = new Path2D(GLYPHS[k].d));
// draws number n centred at cx with top y0, glyph height h; returns nothing. mode: fn(ctx, path) per glyph
function drawNum(x, n, cx, y0, h, paint) {
  const ds = String(n).split(''), sc = h / 100, gap = -2, tot = ds.reduce((a, d) => a + GLYPHS[d].w, 0) + gap * (ds.length - 1);
  const fit = Math.min(1, 112 / (tot * sc)); let px = cx - (tot * sc * fit) / 2;
  ds.forEach((d) => { x.save(); x.translate(px, y0); x.scale(sc * fit, sc); paint(path(d), sc * fit); x.restore(); px += (GLYPHS[d].w + gap) * sc * fit; });
}
export const TILE_SKINS = ['classic', 'galaxy', 'gold', 'neon', 'marble'];
const TW = 0.05, TH = 0.07;
function cvs(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; }
function capTex(c, srgb = true) {
  const t = new THREE.CanvasTexture(c); if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  t.wrapS = t.wrapT = THREE.ClampToEdgeWrapping; t.repeat.set(1 / TW, 1 / TH); t.offset.set(0.5, 0.5); t.anisotropy = 8; return t;
}
const rnd0 = (seed) => { let s = seed; return () => ((s = (s * 16807) % 2147483647) / 2147483647); };
function rr(x, X, Y, w, h, r) { x.beginPath(); x.moveTo(X + r, Y); x.arcTo(X + w, Y, X + w, Y + h, r); x.arcTo(X + w, Y + h, X, Y + h, r); x.arcTo(X, Y + h, X, Y, r); x.arcTo(X, Y, X + w, Y, r); x.closePath(); }

function galaxyTex() {
  const [c, x] = cvs(256, 360), rnd = rnd0(41);
  const g = x.createLinearGradient(0, 0, 256, 360); g.addColorStop(0, '#2A1F6E'); g.addColorStop(0.55, '#140F3D'); g.addColorStop(1, '#080620'); x.fillStyle = g; x.fillRect(0, 0, 256, 360);
  x.globalCompositeOperation = 'lighter';
  [[70, 90, 120, 'rgba(160,90,255,.32)'], [190, 250, 140, 'rgba(60,140,255,.28)'], [150, 140, 70, 'rgba(255,110,200,.22)'], [60, 300, 90, 'rgba(90,220,255,.16)']].forEach(([cx, cy, r, col]) => { const rg = x.createRadialGradient(cx, cy, 0, cx, cy, r); rg.addColorStop(0, col); rg.addColorStop(1, 'rgba(0,0,0,0)'); x.fillStyle = rg; x.fillRect(0, 0, 256, 360); });
  x.save(); x.translate(128, 180); x.rotate(-0.5); x.scale(1, 0.35);
  for (let i = 0; i < 900; i++) { const t = rnd() * 10, r = 6 * Math.exp(0.28 * t), a = t + (i % 2) * Math.PI; if (r > 200) continue; x.fillStyle = `rgba(${200 + rnd() * 55 | 0},${190 + rnd() * 50 | 0},255,${0.08 + rnd() * 0.18})`; x.fillRect(Math.cos(a) * r + (rnd() - 0.5) * 14, Math.sin(a) * r + (rnd() - 0.5) * 14, 2, 2); }
  x.restore();
  for (let i = 0; i < 260; i++) { const r = rnd() < 0.06 ? 1.8 : 0.7; x.fillStyle = `rgba(255,255,255,${0.3 + rnd() * 0.7})`; x.beginPath(); x.arc(rnd() * 256, rnd() * 360, r, 0, 7); x.fill(); }
  x.globalCompositeOperation = 'source-over';
  x.strokeStyle = 'rgba(255,217,100,.55)'; x.lineWidth = 3; rr(x, 9, 9, 238, 342, 26); x.stroke();
  return c;
}
function marbleTex() {
  const [c, x] = cvs(256, 360), rnd = rnd0(77);
  const g = x.createLinearGradient(0, 0, 256, 360); g.addColorStop(0, '#FFFFFF'); g.addColorStop(1, '#ECEDF1'); x.fillStyle = g; x.fillRect(0, 0, 256, 360);
  for (let i = 0; i < 26; i++) { x.fillStyle = `rgba(200,204,214,${0.05 + rnd() * 0.08})`; x.beginPath(); x.ellipse(rnd() * 256, rnd() * 360, 20 + rnd() * 60, 8 + rnd() * 20, rnd() * 3, 0, 7); x.fill(); }
  const vein = (w, col, a) => {
    let px = rnd() * 256, py = -10, ang = 0.9 + rnd() * 0.8; x.strokeStyle = col; x.globalAlpha = a; x.lineWidth = w; x.beginPath(); x.moveTo(px, py);
    for (let k = 0; k < 60; k++) { ang += (rnd() - 0.5) * 0.5; px += Math.cos(ang) * 8; py += Math.sin(ang) * 8 + 2; x.lineTo(px, py); if (rnd() < 0.05) { x.stroke(); x.lineWidth = w * (0.4 + rnd() * 0.6); x.beginPath(); x.moveTo(px, py); } }
    x.stroke(); x.globalAlpha = 1;
  };
  x.filter = 'blur(1.4px)'; for (let i = 0; i < 6; i++) vein(3.2, '#7E8598', 0.5); x.filter = 'none';
  for (let i = 0; i < 8; i++) vein(1.1, '#5E6578', 0.6);
  for (let i = 0; i < 3; i++) vein(1.6, '#C9A24E', 0.75);
  return c;
}
function onyxTex() {
  const [c, x] = cvs(256, 360), rnd = rnd0(23);
  const g = x.createLinearGradient(0, 0, 256, 360); g.addColorStop(0, '#1d1d24'); g.addColorStop(0.5, '#0b0b0f'); g.addColorStop(1, '#050507'); x.fillStyle = g; x.fillRect(0, 0, 256, 360);
  for (let i = 0; i < 7; i++) { x.strokeStyle = 'rgba(120,120,140,.05)'; x.lineWidth = 8 + rnd() * 20; x.beginPath(); x.moveTo(-20, rnd() * 360); x.bezierCurveTo(80, rnd() * 360, 170, rnd() * 360, 280, rnd() * 360); x.stroke(); }
  const au = x.createLinearGradient(0, 0, 256, 360); au.addColorStop(0, '#FFF0B8'); au.addColorStop(0.3, '#D9B25A'); au.addColorStop(0.55, '#F7DE92'); au.addColorStop(0.8, '#B8892E'); au.addColorStop(1, '#E9C46A');
  x.fillStyle = au; x.fillRect(0, 0, 256, 14); x.fillRect(0, 346, 256, 14); x.fillRect(0, 0, 14, 360); x.fillRect(242, 0, 14, 360);
  x.strokeStyle = au; x.lineWidth = 1.6; rr(x, 22, 22, 212, 316, 16); x.stroke();
  [[22, 22], [234, 22], [22, 338], [234, 338]].forEach(([px, py]) => { x.fillStyle = au; x.beginPath(); x.moveTo(px, py - 5); x.lineTo(px + 5, py); x.lineTo(px, py + 5); x.lineTo(px - 5, py); x.closePath(); x.fill(); });
  return c;
}
function onyxMetal() {
  const [c, x] = cvs(256, 360); x.fillStyle = '#000'; x.fillRect(0, 0, 256, 360); x.fillStyle = '#fff';
  x.fillRect(0, 0, 256, 14); x.fillRect(0, 346, 256, 14); x.fillRect(0, 0, 14, 360); x.fillRect(242, 0, 14, 360);
  x.strokeStyle = '#fff'; x.lineWidth = 1.6; rr(x, 22, 22, 212, 316, 16); x.stroke();
  [[22, 22], [234, 22], [22, 338], [234, 338]].forEach(([px, py]) => { x.beginPath(); x.moveTo(px, py - 5); x.lineTo(px + 5, py); x.lineTo(px, py + 5); x.lineTo(px - 5, py); x.closePath(); x.fill(); });
  return c;
}
function neonEdge() {
  const [c, x] = cvs(256, 360); x.fillStyle = '#000'; x.fillRect(0, 0, 256, 360);
  x.fillStyle = '#ffffff'; x.fillRect(0, 0, 256, 3);
  x.shadowColor = '#fff'; x.shadowBlur = 14; x.strokeStyle = '#fff'; x.lineWidth = 5; rr(x, 10, 10, 236, 340, 26); x.stroke();
  x.shadowBlur = 0; x.strokeStyle = 'rgba(255,255,255,.35)'; x.lineWidth = 1.5; rr(x, 22, 22, 212, 316, 18); x.stroke();
  return c;
}
function neonBase() {
  const [c, x] = cvs(256, 360); const g = x.createLinearGradient(0, 0, 0, 360); g.addColorStop(0, '#16222f'); g.addColorStop(1, '#070b11'); x.fillStyle = g; x.fillRect(0, 0, 256, 360);
  x.strokeStyle = 'rgba(61,245,224,.08)'; x.lineWidth = 1; for (let i = 0; i < 360; i += 12) { x.beginPath(); x.moveTo(0, i + 0.5); x.lineTo(256, i + 0.5); x.stroke(); }
  x.fillStyle = '#3DF5E0'; x.fillRect(0, 0, 256, 3); return c;
}

// face palettes per skin
const FACE = {
  classic: { col: { R: '#D7263D', B: '#1F5FBF', K: '#1B1B1B', Y: '#E39B12' }, side: '#E3D7BC', joker: '#127A4A' },
  galaxy: { col: { R: '#FF3D5A', B: '#3F9BFF', K: '#E6E9F5', Y: '#FFC21A' }, glow: 3, emis: 0.18, hi: 'rgba(255,255,255,.35)', ink: 'rgba(6,4,24,.85)', side: '#120E30', sideEm: '#2A1F6E', joker: '#FFD964' },
  gold: { col: { R: '#FF3348', B: '#3D95FF', K: '#ECEEF4', Y: '#FFB000' }, rim: '#8A6420', emis: 0.1, side: '#D9B25A', metal: true, joker: '#E9C46A' },
  neon: { col: { R: '#FF2E63', B: '#2E9BFF', K: '#EEF4FF', Y: '#FFD21F' }, glow: 4, emis: 0.35, hi: 'rgba(255,255,255,.3)', ink: 'rgba(0,0,0,.85)', side: '#06090E', sideEm: '#3DF5E0', joker: '#3DF5E0' },
  marble: { col: { R: '#C21F36', B: '#1B4FA8', K: '#2B3A55', Y: '#D08A0C' }, side: '#C9CCD6', joker: '#2B3A55' },
};

export function createTileKit() {
  const cache = {}, faceCache = {};
  const tex = (k, f, srgb) => cache[k] || (cache[k] = capTex(f(), srgb));
  function applySide(m, skin) {
    const f = FACE[skin] || FACE.classic; m.color.set(f.side); m.metalness = f.metal ? 1 : 0.05; m.roughness = f.metal ? 0.2 : 0.34; m.clearcoat = 0.8; m.clearcoatRoughness = 0.15;
    m.emissive.set(f.sideEm || '#000000'); m.emissiveIntensity = skin === 'neon' ? 0.3 : 0.25; m.name = 'tas_kenar_' + skin; m.needsUpdate = true;
  }
  function applyBody(m, skin, th, side) {
    if (side) applySide(side, skin);
    m.map = null; m.emissiveMap = null; m.metalnessMap = null; m.emissive.set('#000000'); m.emissiveIntensity = 1; m.metalness = 0; m.roughness = 0.36; m.clearcoat = 0.7; m.clearcoatRoughness = 0.22; m.iridescence = 0; m.color.set('#ffffff');
    if (skin === 'galaxy') { m.map = tex('g', galaxyTex); m.emissiveMap = m.map; m.emissive.set('#ffffff'); m.emissiveIntensity = 0.22; m.roughness = 0.22; m.clearcoat = 1; m.clearcoatRoughness = 0.04; m.iridescence = 0.35; m.iridescenceIOR = 1.4; m.iridescenceThicknessRange = [200, 500]; }
    else if (skin === 'gold') { m.map = tex('ox', onyxTex); m.metalnessMap = tex('oxm', onyxMetal, false); m.emissiveMap = m.metalnessMap; m.emissive.set('#B8892E'); m.emissiveIntensity = 0.55; m.metalness = 0.9; m.roughness = 0.2; m.clearcoat = 1; m.clearcoatRoughness = 0.03; }
    else if (skin === 'neon') { m.map = tex('nb', neonBase); m.emissiveMap = tex('ne', neonEdge, false); m.emissive.set('#3DF5E0'); m.emissiveIntensity = 2.2; m.roughness = 0.12; m.metalness = 0.2; m.clearcoat = 1; m.clearcoatRoughness = 0.03; }
    else if (skin === 'marble') { m.map = tex('m', marbleTex); m.roughness = 0.16; m.clearcoat = 1; m.clearcoatRoughness = 0.05; }
    else m.color.set((th && th.tile) || '#FFF8EA');
    m.name = 'tas_' + skin; m.needsUpdate = true;
  }
  function drawJoker(x, f) {
    const col = f.joker; x.save(); x.translate(64, 84);
    if (f.glow) { x.shadowColor = col; x.shadowBlur = f.glow; }
    x.strokeStyle = col; x.lineWidth = 6; x.beginPath(); x.arc(0, 0, 42, 0, 7); x.stroke();
    x.lineWidth = 2; x.beginPath(); x.arc(0, 0, 33, 0, 7); x.stroke();
    x.fillStyle = col; x.beginPath(); for (let i = 0; i < 10; i++) { const a = -Math.PI / 2 + i * Math.PI / 5, r = i % 2 ? 11 : 26; i ? x.lineTo(Math.cos(a) * r, Math.sin(a) * r) : x.moveTo(Math.cos(a) * r, Math.sin(a) * r); } x.closePath(); x.fill();
    x.restore();
    x.fillStyle = col; x.font = '800 15px Montserrat, sans-serif'; x.textAlign = 'center'; if ('letterSpacing' in x) x.letterSpacing = '2px'; x.fillText('OKEY', 65, 158);
  }
  function drawClover(x, f) {
    const col = f.joker; x.save(); x.translate(64, 80);
    if (f.glow) { x.shadowColor = col; x.shadowBlur = f.glow; }
    x.fillStyle = col;
    [[0, -17], [17, 0], [0, 17], [-17, 0]].forEach(([dx, dy]) => { x.beginPath(); x.arc(dx, dy, 15, 0, 7); x.fill(); });
    x.beginPath(); x.arc(0, 0, 10, 0, 7); x.fill();
    x.lineWidth = 6; x.lineCap = 'round'; x.strokeStyle = col; x.beginPath(); x.moveTo(0, 18); x.quadraticCurveTo(4, 42, 16, 54); x.stroke();
    x.restore();
  }
  function drawOkey(x) {
    x.save(); x.translate(64, 82);
    const g = x.createLinearGradient(0, -48, 0, 48); g.addColorStop(0, '#FFE27A'); g.addColorStop(0.5, '#F5B82E'); g.addColorStop(1, '#C9851A');
    x.shadowColor = 'rgba(245,184,46,.9)'; x.shadowBlur = 14;
    x.strokeStyle = g; x.lineWidth = 5; x.beginPath(); x.arc(0, 0, 44, 0, 7); x.stroke(); x.shadowBlur = 0;
    x.fillStyle = g; x.beginPath(); for (let i = 0; i < 16; i++) { const a = -Math.PI / 2 + i * Math.PI / 8, r = i % 2 ? 14 : 34; i ? x.lineTo(Math.cos(a) * r, Math.sin(a) * r) : x.moveTo(Math.cos(a) * r, Math.sin(a) * r); } x.closePath(); x.fill();
    x.fillStyle = 'rgba(255,255,255,.85)'; x.beginPath(); x.arc(-5, -6, 5, 0, 7); x.fill();
    x.restore();
    x.fillStyle = '#E0A021'; x.font = '900 17px Montserrat, sans-serif'; x.textAlign = 'center'; if ('letterSpacing' in x) x.letterSpacing = '3px'; x.fillText('OKEY', 66, 160);
  }
  function faceMat(skin, c, n, font) {
    const k = skin + c + n + font; if (faceCache[k]) return faceCache[k];
    const f = FACE[skin] || FACE.classic, [cv, x] = cvs(128, 180); x.clearRect(0, 0, 128, 180);
    if (c === 'J') drawClover(x, f);
    else if (c === 'O') drawOkey(x);
    else {
      const col = f.col[c], W = 14;
      const stroke = (p, k, w, style, blur = 0, glowCol) => { x.lineCap = x.lineJoin = 'round'; x.lineWidth = w / k; x.strokeStyle = style; x.shadowBlur = blur; x.shadowColor = glowCol || 'transparent'; x.stroke(p); };
      const dot = (r, style, dy = 0, dx = 0, blur = 0) => { x.shadowBlur = blur; x.shadowColor = blur ? style : 'transparent'; x.fillStyle = style; x.beginPath(); x.arc(64 + dx, 145 + dy, r, 0, 7); x.fill(); };
      const num = (fn) => drawNum(x, n, 64, 16, 100, fn);
      if (f.rim) { num((p, k) => stroke(p, k, W + 5, f.rim)); dot(13.5, f.rim); }
      else if (f.ink) { num((p, k) => stroke(p, k, W + 6, f.ink)); dot(14, f.ink); }
      else if (!f.glow) { x.save(); x.translate(1.5, 2.5); num((p, k) => stroke(p, k, W, 'rgba(0,0,0,.16)')); dot(11, 'rgba(0,0,0,.16)'); x.restore(); }
      num((p, k) => stroke(p, k, W, col, f.glow || 0, col)); dot(11, col, 0, 0, f.glow || 0);
      if (f.hi) { num((p, k) => { x.save(); x.translate(0, -1.6 / 1); stroke(p, k, 3.2, f.hi); x.restore(); }); }
      x.shadowBlur = 0;
    }
    const t = new THREE.CanvasTexture(cv); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
    const m = new THREE.MeshStandardMaterial({ name: 'tas_yuz_' + skin + '_' + (c === 'J' ? 'sahte_okey' : c === 'O' ? 'okey' : c + n), map: t, transparent: true, roughness: f.engrave ? 0.5 : 0.4, depthWrite: false });
    if (f.emis) { m.emissive.set('#ffffff'); m.emissiveMap = t; m.emissiveIntensity = f.emis; }
    if (c === 'O') { m.emissive.set('#FFC94A'); m.emissiveMap = t; m.emissiveIntensity = 0.55; }
    return (faceCache[k] = m);
  }
  return { applyBody, faceMat, clearFaces() { Object.keys(faceCache).forEach((k) => { faceCache[k].map.dispose(); delete faceCache[k]; }); } };
}
