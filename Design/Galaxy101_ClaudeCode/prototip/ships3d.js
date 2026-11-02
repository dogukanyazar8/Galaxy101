// Galaxy 101 — ana menüde geçen okey gemileri (three.js r184). Özgün tasarımlar; "kullanılmış gelecek" havası:
// yıpranmış panelli gövde, greeble detayları, iticiler, seyir lambaları. Her geminin cam salonunda okey masası + 4 oyuncu.
// Hareket: gezegenin kütle çekimiyle bükülen yörüngeler (önceden simüle edilir), burun hıza, gövde dönüşe yatar.
// Gezegen önündeki gemiler gezegenin üstünde, arkasındakiler gezegen (ve Satürn halkası) tarafından örtülür.
import * as THREE from './vendor/three/build/three.module.js';

const FOV = 28, CAMZ = 30, ASP = 16 / 9, TAN = Math.tan(FOV / 2 * Math.PI / 180), UPX = 2 * TAN * CAMZ / 720;
const GM = { 'Jüpiter': 17, 'Satürn': 14, 'Neptün': 11, 'Venüs': 9, 'Mars': 7 };
const FLAT = { 'Satürn': 1.1, 'Jüpiter': 1.07, 'Neptün': 1.02 };
const PLAYER = [0xD8443A, 0x2F6FE0, 0xE9A93A, 0x34343E], SKIN = [0xF1C7A2, 0xB97E55, 0xE7B48C, 0x8E5B3D];

function rng(seed) { let s = seed >>> 0 || 1; return () => (s = (s * 1664525 + 1013904223) >>> 0) / 4294967296; }
function canvasTex(size, draw, srgb = true) {
  const c = document.createElement('canvas'); c.width = c.height = size; draw(c.getContext('2d'), size);
  const t = new THREE.CanvasTexture(c); if (srgb) t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.anisotropy = 8; return t;
}
// Yıpranmış panel kaplama: renk + pürüzlülük haritası.
function hullTex(base, accent, seed, decal) {
  const R = rng(seed), panels = [];
  for (let i = 0; i < 70; i++) { const w = 40 + R() * 220, h = 30 + R() * 160; panels.push([Math.floor(R() * 1024 / 16) * 16, Math.floor(R() * 1024 / 16) * 16, w, h, R()]); }
  const map = canvasTex(1024, (x, S) => {
    x.fillStyle = base; x.fillRect(0, 0, S, S);
    panels.forEach(([px, py, w, h, v]) => { x.fillStyle = v < .5 ? `rgba(0,0,0,${.04 + v * .1})` : `rgba(255,255,255,${(v - .5) * .12})`; x.fillRect(px, py, w, h); });
    for (let i = 0; i < 9000; i++) { x.fillStyle = `rgba(${R() < .5 ? '0,0,0' : '255,255,255'},${R() * .05})`; x.fillRect(R() * S, R() * S, 2, 2); }
    x.strokeStyle = 'rgba(10,10,14,.55)'; x.lineWidth = 2.2;
    panels.forEach(([px, py, w, h]) => x.strokeRect(px + .5, py + .5, w, h));
    for (let i = 0; i < 16; i++) { const y = Math.floor(R() * S); x.beginPath(); x.moveTo(0, y); x.lineTo(S, y); x.stroke(); }
    x.fillStyle = 'rgba(255,255,255,.28)';
    panels.forEach(([px, py, w, h]) => { for (let k = 6; k < w; k += 14) { x.fillRect(px + k, py + 4, 2, 2); x.fillRect(px + k, py + h - 6, 2, 2); } });
    for (let i = 0; i < 60; i++) { const sx = R() * S, sy = R() * S, g = x.createLinearGradient(sx, sy, sx, sy + 60 + R() * 160); g.addColorStop(0, `rgba(30,22,16,${.1 + R() * .22})`); g.addColorStop(1, 'rgba(30,22,16,0)'); x.fillStyle = g; x.fillRect(sx, sy, 3 + R() * 9, 220); }
    for (let i = 0; i < 26; i++) { const sx = R() * S, sy = R() * S, r = 10 + R() * 50, g = x.createRadialGradient(sx, sy, 0, sx, sy, r); g.addColorStop(0, 'rgba(20,16,12,.35)'); g.addColorStop(1, 'rgba(20,16,12,0)'); x.fillStyle = g; x.fillRect(sx - r, sy - r, r * 2, r * 2); }
    x.fillStyle = accent; x.fillRect(0, 610, S, 34); x.fillRect(0, 652, S, 8);
    x.save(); x.beginPath(); x.rect(700, 300, 220, 36); x.clip(); x.fillStyle = '#E7C23A'; x.fillRect(700, 300, 220, 36); x.fillStyle = '#1a1a1a'; for (let k = -40; k < 260; k += 30) { x.beginPath(); x.moveTo(700 + k, 336); x.lineTo(715 + k, 300); x.lineTo(730 + k, 300); x.lineTo(715 + k, 336); x.fill(); } x.restore();
    x.fillStyle = 'rgba(20,20,24,.75)'; x.font = '900 64px Montserrat, Arial, sans-serif'; x.fillText(decal, 90, 560);
    x.font = '700 22px monospace'; x.fillText('GX-' + (100 + Math.floor(R() * 899)) + ' · OKEY', 90, 594);
  });
  const rough = canvasTex(1024, (x, S) => {
    x.fillStyle = '#8a8a8a'; x.fillRect(0, 0, S, S);
    panels.forEach(([px, py, w, h, v]) => { const c = 100 + Math.floor(v * 90); x.fillStyle = `rgb(${c},${c},${c})`; x.fillRect(px, py, w, h); });
    for (let i = 0; i < 6000; i++) { const c = 90 + R() * 140; x.fillStyle = `rgba(${c},${c},${c},.5)`; x.fillRect(R() * S, R() * S, 3, 3); }
    x.fillStyle = '#5a5a5a'; x.fillRect(0, 610, S, 34);
  }, false);
  return { map, rough };
}
function windowsTex(seed) {
  const R = rng(seed);
  return canvasTex(512, (x, S) => { x.fillStyle = '#000'; x.fillRect(0, 0, S, S); for (let y = 40; y < S; y += 64) for (let k = 10; k < S; k += 26) if (R() < .7) { x.fillStyle = R() < .8 ? '#FFD9A0' : '#9FE8FF'; x.globalAlpha = .55 + R() * .45; x.fillRect(k, y, 14, 9); } x.globalAlpha = 1; });
}
function glowTex() {
  return canvasTex(128, (x) => { const g = x.createRadialGradient(64, 64, 0, 64, 64, 64); g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(.18, 'rgba(255,255,255,.75)'); g.addColorStop(.5, 'rgba(255,255,255,.14)'); g.addColorStop(1, 'rgba(255,255,255,0)'); x.fillStyle = g; x.fillRect(0, 0, 128, 128); });
}
function plumeTex() {
  return canvasTex(128, (x) => { const g = x.createLinearGradient(0, 0, 0, 128); g.addColorStop(0, 'rgba(255,255,255,0)'); g.addColorStop(.55, 'rgba(255,255,255,.35)'); g.addColorStop(1, 'rgba(255,255,255,1)'); x.fillStyle = g; x.fillRect(0, 0, 128, 128); const h = x.createLinearGradient(0, 0, 128, 0); h.addColorStop(0, 'rgba(0,0,0,1)'); h.addColorStop(.5, 'rgba(0,0,0,0)'); h.addColorStop(1, 'rgba(0,0,0,1)'); x.globalCompositeOperation = 'destination-out'; x.fillStyle = h; x.fillRect(0, 0, 128, 128); });
}
function feltTex() {
  return canvasTex(256, (x) => { const g = x.createRadialGradient(128, 128, 10, 128, 128, 128); g.addColorStop(0, '#2FA864'); g.addColorStop(1, '#126A3A'); x.fillStyle = g; x.fillRect(0, 0, 256, 256); x.strokeStyle = 'rgba(255,225,150,.5)'; x.lineWidth = 3; x.beginPath(); x.arc(128, 128, 74, 0, 7); x.stroke(); });
}
function envScene() {
  const s = new THREE.Scene();
  s.add(new THREE.Mesh(new THREE.SphereGeometry(20, 32, 16), new THREE.MeshBasicMaterial({ color: 0x10152a, side: THREE.BackSide })));
  const pan = (c, w, h, x, y, z) => { const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshBasicMaterial({ color: c, side: THREE.DoubleSide })); m.position.set(x, y, z); m.lookAt(0, 0, 0); s.add(m); };
  pan(0xffffff, 12, 4, -8, 12, 6); pan(0xFFD9A0, 6, 6, 12, 4, 8); pan(0x6F90E8, 16, 3, 0, -12, -4); pan(0xffffff, 4, 10, -14, 0, -6); pan(0xC8B4FF, 8, 3, 6, 10, -12);
  return s;
}

// Ortak parçalar --------------------------------------------------------------------------------
function Kit(T) {
  const dark = new THREE.MeshStandardMaterial({ color: 0x24262d, metalness: .85, roughness: .42 });
  const steel = new THREE.MeshStandardMaterial({ color: 0x8a8f99, metalness: .9, roughness: .3 });
  const grb = new THREE.MeshStandardMaterial({ color: 0x5a5d66, metalness: .8, roughness: .45 });
  const glass = new THREE.MeshPhysicalMaterial({ color: 0xCFE8FF, metalness: 0, roughness: .03, transparent: true, opacity: .2, clearcoat: 1, envMapIntensity: 2.4, depthWrite: false, side: THREE.DoubleSide });
  const canopy = new THREE.MeshPhysicalMaterial({ color: 0x0b1626, metalness: .9, roughness: .07, clearcoat: 1, envMapIntensity: 1.8 });
  const wood = new THREE.MeshStandardMaterial({ color: 0x6B3F22, roughness: .55 });
  const tileM = new THREE.MeshStandardMaterial({ color: 0xF6F0E2, roughness: .35 });
  const felt = new THREE.MeshStandardMaterial({ map: T.felt, roughness: .9 });
  const add = (g, geo, mat, x, y, z, rx = 0, ry = 0, rz = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.rotation.set(rx, ry, rz); g.add(m); return m; };
  const sprite = (color, s, op = 1) => { const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true, opacity: op })); sp.scale.set(s, s, 1); return sp; };
  // Motor: gövde silindiri, emiş halkası, genişleyen meme, sıcak çekirdek, alev konisi, parıltı. Eksen = -x.
  function engine(g, x, y, z, r, len, color, nacelleMat) {
    const e = new THREE.Group(); e.position.set(x, y, z); g.add(e);
    if (nacelleMat) {
      add(e, new THREE.CylinderGeometry(r, r * 1.04, len, 40), nacelleMat, 0, 0, 0, 0, 0, Math.PI / 2);
      add(e, new THREE.TorusGeometry(r * .98, r * .1, 10, 40), steel, len / 2, 0, 0, 0, Math.PI / 2, 0);
      add(e, new THREE.CircleGeometry(r * .85, 40), dark, len / 2 + .001, 0, 0, 0, Math.PI / 2, 0);
      for (let k = 0; k < 3; k++) add(e, new THREE.TorusGeometry(r * 1.02, r * .035, 6, 40), dark, len * (.25 - k * .18), 0, 0, 0, Math.PI / 2, 0);
    }
    const noz = new THREE.LatheGeometry([[r * .78, 0], [r * .82, -.02], [r * .95, -r * .9], [r * .86, -r * .92], [r * .7, -.04]].map(([a, b]) => new THREE.Vector2(a, b)), 40);
    add(e, noz, dark, -len / 2, 0, 0, 0, 0, -Math.PI / 2);
    const core = add(e, new THREE.CircleGeometry(r * .72, 40), new THREE.MeshBasicMaterial({ color: 0xffffff }), -len / 2 - .02, 0, 0, 0, -Math.PI / 2, 0);
    const ring = add(e, new THREE.RingGeometry(r * .72, r * .88, 40), new THREE.MeshBasicMaterial({ color, side: THREE.DoubleSide }), -len / 2 - .025, 0, 0, 0, -Math.PI / 2, 0);
    const plume = add(e, new THREE.ConeGeometry(r * .78, r * 7, 32, 1, true), new THREE.MeshBasicMaterial({ map: T.plume, color, blending: THREE.AdditiveBlending, transparent: true, depthWrite: false, side: THREE.DoubleSide }), -len / 2 - r * 3.5, 0, 0, 0, 0, Math.PI / 2);
    const glow = sprite(color, r * 5.5); glow.position.set(-len / 2 - r * .5, 0, 0); e.add(glow);
    return { plume, glow, core, ring, r, x0: -len / 2, h: r * 3.5, emit: new THREE.Vector3(x - len / 2 - r * 1.5, y, z) };
  }
  // Okey salonu: zemin, masa, taşlar, ıstakalar, 4 oyuncu, sıcak lamba.
  function lounge(g, x, y, z, s) {
    const L = new THREE.Group(); L.position.set(x, y, z); L.scale.setScalar(s); g.add(L);
    add(L, new THREE.CylinderGeometry(.56, .56, .02, 48), new THREE.MeshStandardMaterial({ color: 0x2a2130, roughness: .7 }), 0, 0, 0);
    add(L, new THREE.CylinderGeometry(.05, .08, .12, 24), dark, 0, .07, 0);
    const tb = add(L, new THREE.CylinderGeometry(.24, .24, .045, 48), [wood, felt, wood], 0, .15, 0);
    for (let i = 0; i < 10; i++) { const a = i * 2.4, r = .06 + (i % 4) * .035; add(L, new THREE.BoxGeometry(.03, .009, .042), tileM, Math.cos(a) * r, .177, Math.sin(a) * r, 0, a, 0); }
    add(L, new THREE.BoxGeometry(.045, .04, .032), tileM, 0, .192, 0);
    const players = [];
    for (let i = 0; i < 4; i++) {
      const seat = new THREE.Group(); seat.rotation.y = i * Math.PI / 2 + Math.PI / 4; L.add(seat);
      add(seat, new THREE.BoxGeometry(.18, .035, .03), wood, 0, .19, .2);
      for (let k = 0; k < 6; k++) add(seat, new THREE.BoxGeometry(.02, .026, .005), tileM, -.065 + k * .026, .214, .211);
      add(seat, new THREE.BoxGeometry(.12, .1, .1), dark, 0, .06, .34);
      const body = add(seat, new THREE.CapsuleGeometry(.05, .07, 6, 16), new THREE.MeshStandardMaterial({ color: PLAYER[i], roughness: .55 }), 0, .19, .33);
      const head = add(seat, new THREE.SphereGeometry(.045, 24, 16), new THREE.MeshStandardMaterial({ color: SKIN[i], roughness: .6 }), 0, .3, .33);
      players.push({ head, body, ph: i * 1.7 + Math.random() });
    }
    const lamp = new THREE.PointLight(0xFFDDA8, 2.4, 1.6, 2); lamp.position.y = .55; L.add(lamp);
    const shade = add(L, new THREE.CylinderGeometry(.05, .1, .05, 24, 1, true), steel, 0, .5, 0); shade.material = steel;
    return { players, lamp };
  }
  // Greeble: yüzeye rastgele küçük kutular (tek InstancedMesh).
  function greeble(g, n, mat, R, place) {
    const im = new THREE.InstancedMesh(new THREE.BoxGeometry(1, 1, 1), mat, n), m = new THREE.Matrix4(), q = new THREE.Quaternion(), p = new THREE.Vector3(), sc = new THREE.Vector3();
    for (let i = 0; i < n; i++) { place(R, p, sc); m.compose(p, q, sc); im.setMatrixAt(i, m); }
    g.add(im); return im;
  }
  function navLight(g, color, x, y, z, s = .3) { const b = add(g, new THREE.SphereGeometry(.025, 10, 8), new THREE.MeshBasicMaterial({ color }), x, y, z); const sp = sprite(color, s); sp.position.set(x, y, z); g.add(sp); return sp; }
  return { dark, grb, steel, glass, canopy, add, sprite, engine, lounge, greeble, navLight };
}

// 1) KERVAN — ağır yük gemisi: kama burun, yan motor kapsülleri, sırtında cam okey salonu.
function buildKervan(K, T) {
  const g = new THREE.Group(), R = rng(7), ht = hullTex('#B9B3A6', '#B5452F', 11, 'KERVAN 101');
  ht.map.repeat.set(.32, .32); ht.rough.repeat.set(.32, .32);
  const hull = new THREE.MeshPhysicalMaterial({ map: ht.map, roughnessMap: ht.rough, metalness: .55, roughness: 1, clearcoat: .2, clearcoatRoughness: .6 });
  const hullB = hull.clone(); hullB.map = ht.map.clone(); hullB.map.repeat.set(1, 1); hullB.roughnessMap = ht.rough.clone(); hullB.roughnessMap.repeat.set(1, 1);
  const sh = new THREE.Shape([[2.1, -.05], [1.55, .34], [-1.55, .42], [-1.85, .18], [-1.85, -.3], [-1.2, -.42], [1.3, -.32]].map(([a, b]) => new THREE.Vector2(a, b)));
  const body = new THREE.ExtrudeGeometry(sh, { depth: 1.2, bevelEnabled: true, bevelThickness: .07, bevelSize: .06, bevelSegments: 3, curveSegments: 4 }); body.translate(0, 0, -.6);
  K.add(g, body, hull, 0, 0, 0);
  const spine = new THREE.ExtrudeGeometry(new THREE.Shape([[1.2, 0], [.9, .14], [-1.4, .16], [-1.6, 0]].map(([a, b]) => new THREE.Vector2(a, b))), { depth: .5, bevelEnabled: true, bevelThickness: .03, bevelSize: .03, bevelSegments: 2 }); spine.translate(0, .38, -.25);
  K.add(g, spine, hull, 0, 0, 0);
  K.add(g, new THREE.BoxGeometry(.55, .1, .5), K.canopy, 1.55, .33, 0, 0, 0, -.5);
  const win = new THREE.MeshStandardMaterial({ color: 0x111111, emissive: 0xffffff, emissiveMap: T.win, emissiveIntensity: 1.3, metalness: .5, roughness: .4 });
  [-.67, .67].forEach((z) => K.add(g, new THREE.BoxGeometry(2.2, .07, .01), win, -.1, .08, z));
  const engines = [];
  [-1.02, 1.02].forEach((z) => {
    K.add(g, new THREE.BoxGeometry(1.1, .14, .5), hullB, -.45, -.02, z * .72);
    engines.push(K.engine(g, -.3, -.02, z, .3, 2.1, 0x8FD8FF, hullB));
  });
  [-.36, 0, .36].forEach((z) => engines.push(K.engine(g, -1.85, .02, z, .14, .2, 0x8FD8FF)));
  const lg = K.lounge(g, -.25, .55, 0, .95);
  K.add(g, new THREE.SphereGeometry(.68, 64, 32, 0, Math.PI * 2, 0, Math.PI / 2), K.glass, -.25, .55, 0).scale.set(1.15, .62, 1).renderOrder = 5;
  for (let i = 0; i < 5; i++) { const rib = K.add(g, new THREE.TorusGeometry(.68, .012, 8, 48, Math.PI), K.steel, -.25, .55, 0, 0, i * Math.PI / 5, 0); rib.scale.set(1.15, .62, 1); }
  K.add(g, new THREE.TorusGeometry(.7, .03, 10, 64), K.steel, -.25, .55, 0, Math.PI / 2, 0, 0).scale.set(1.15, 1, 1);
  const dish = K.add(g, new THREE.LatheGeometry([[0, 0], [.18, .05], [.2, .07]].map(([a, b]) => new THREE.Vector2(a, b)), 32), K.steel, -1.35, .66, .38, -.5, 0, .3); dish.material = K.steel;
  K.add(g, new THREE.CylinderGeometry(.012, .012, .5, 8), K.steel, -1.1, .8, -.3);
  K.greeble(g, 160, K.grb, R, (R, p, s) => { const top = R() < .65, sd = R() < .5 ? -1 : 1; if (top) { p.set(-1.6 + R() * 3.0, 0, sd * (.33 + R() * .3)); p.y = .39 + (p.x + 1.55) / 3.1 * -.06 + .045; s.set(.03 + R() * .14, .02 + R() * .04, .03 + R() * .08); } else { p.set(-1.5 + R() * 2.6, -.2 + R() * .28, sd * .665); s.set(.03 + R() * .16, .02 + R() * .05, .02); } });
  const nav = [K.navLight(g, 0xFF3B30, .75, -.02, -1.32), K.navLight(g, 0x34E27A, .75, -.02, 1.32), K.navLight(g, 0xffffff, -1.1, 1.06, -.3, .45)];
  return { g, engines, lounge: lg, nav, len: 4.2 };
}

// 2) OK — hızlı kurye: ok başı gövde, kanat ucu motorları, dikey kanatçık, öne kayık cam salon.
function buildOk(K, T) {
  const g = new THREE.Group(), R = rng(21), ht = hullTex('#D9D6CE', '#2D63C8', 23, 'OK-7');
  ht.map.repeat.set(.4, .4); ht.rough.repeat.set(.4, .4);
  const hull = new THREE.MeshPhysicalMaterial({ map: ht.map, roughnessMap: ht.rough, metalness: .5, roughness: 1, clearcoat: .35, clearcoatRoughness: .4 });
  const sh = new THREE.Shape([[2.0, 0], [.55, .32], [-.55, 1.3], [-1.2, 1.36], [-.95, .5], [-1.35, .32], [-1.35, -.32], [-.95, -.5], [-1.2, -1.36], [-.55, -1.3], [.55, -.32]].map(([a, b]) => new THREE.Vector2(a, b)));
  const wing = new THREE.ExtrudeGeometry(sh, { depth: .16, bevelEnabled: true, bevelThickness: .05, bevelSize: .05, bevelSegments: 3 }); wing.rotateX(-Math.PI / 2); wing.translate(0, -.08, 0);
  K.add(g, wing, hull, 0, 0, 0);
  const hump = K.add(g, new THREE.CapsuleGeometry(.3, 2.1, 8, 32), hull, -.05, .1, 0, 0, 0, Math.PI / 2); hump.scale.set(1, 1, .9); hump.scale.set(.9, 1, 1);
  K.add(g, new THREE.CapsuleGeometry(.16, .5, 6, 24), K.canopy, 1.05, .24, 0, 0, 0, Math.PI / 2).scale.set(1, 1, 1.1);
  const fin = new THREE.ExtrudeGeometry(new THREE.Shape([[-.2, 0], [-1.05, .75], [-1.25, .75], [-1.15, 0]].map(([a, b]) => new THREE.Vector2(a, b))), { depth: .05, bevelEnabled: true, bevelThickness: .015, bevelSize: .015, bevelSegments: 2 }); fin.translate(0, .3, -.025);
  K.add(g, fin, hull, 0, 0, 0);
  const lg = K.lounge(g, .1, .32, 0, .78);
  K.add(g, new THREE.SphereGeometry(.56, 64, 32, 0, Math.PI * 2, 0, Math.PI / 2), K.glass, .1, .32, 0).scale.set(1.25, .6, 1).renderOrder = 5;
  for (let i = 0; i < 4; i++) K.add(g, new THREE.TorusGeometry(.56, .01, 8, 48, Math.PI), K.steel, .1, .32, 0, 0, i * Math.PI / 4, 0).scale.set(1.25, .6, 1);
  const engines = [];
  [-1.2, 1.2].forEach((z) => engines.push(K.engine(g, -.75, -.02, z, .16, 1.0, 0xFF8A5C, hull)));
  engines.push(K.engine(g, -1.3, .1, 0, .2, .4, 0xFF8A5C, hull));
  K.greeble(g, 70, K.grb, R, (R, p, s) => { const side = R() < .5 ? -1 : 1; p.set(-1.1 + R() * 1.6, .1, side * (.4 + R() * .6)); p.y = .1 + .01; s.set(.04 + R() * .16, .015 + R() * .03, .03 + R() * .1); });
  const nav = [K.navLight(g, 0xFF3B30, -.8, .0, -1.4), K.navLight(g, 0x34E27A, -.8, .0, 1.4), K.navLight(g, 0xffffff, -1.2, 1.08, 0, .45)];
  return { g, engines, lounge: lg, nav, len: 3.6 };
}

// 3) HALKA — salon kruvazörü: uzun omurga, dönen pencereli yaşam halkası, burunda cam okey küresi.
function buildHalka(K, T) {
  const g = new THREE.Group(), R = rng(33), ht = hullTex('#5E6066', '#E07A2E', 37, 'HALKA');
  ht.map.repeat.set(1, 1);
  const hull = new THREE.MeshPhysicalMaterial({ map: ht.map, roughnessMap: ht.rough, metalness: .7, roughness: 1, clearcoat: .2, clearcoatRoughness: .5 });
  const spine = new THREE.LatheGeometry([[0, 1.15], [.2, 1.05], [.3, .7], [.34, 0], [.3, -1.1], [.4, -1.35], [.42, -1.75], [0, -1.75]].map(([a, b]) => new THREE.Vector2(a, b)), 48);
  K.add(g, spine, hull, 0, 0, 0, 0, 0, -Math.PI / 2);
  const ring = new THREE.Group(); ring.position.x = -.15; g.add(ring);
  const win = new THREE.MeshStandardMaterial({ map: ht.map, emissive: 0xffffff, emissiveMap: T.win, emissiveIntensity: 1.2, metalness: .6, roughness: .5 });
  K.add(ring, new THREE.TorusGeometry(1.15, .16, 24, 96), win, 0, 0, 0, 0, Math.PI / 2, 0).scale.set(1, 1, .8);
  for (let i = 0; i < 4; i++) K.add(ring, new THREE.CylinderGeometry(.05, .05, 1.0, 12), K.steel, 0, Math.cos(i * Math.PI / 2) * .6, Math.sin(i * Math.PI / 2) * .6, i * Math.PI / 2, 0, 0);
  K.add(g, new THREE.CylinderGeometry(.42, .42, .3, 32), K.dark, -.15, 0, 0, 0, 0, Math.PI / 2);
  const lg = K.lounge(g, 1.5, -.18, 0, .82);
  K.add(g, new THREE.SphereGeometry(.56, 64, 32), K.glass, 1.5, 0, 0).renderOrder = 5;
  K.add(g, new THREE.TorusGeometry(.56, .025, 10, 64), K.steel, 1.5, 0, 0, 0, Math.PI / 2, 0);
  K.add(g, new THREE.TorusGeometry(.4, .02, 10, 48), K.steel, 1.5, -.18, 0, Math.PI / 2, 0, 0);
  const rad = new THREE.MeshStandardMaterial({ map: ht.map, color: 0x9a9ca4, metalness: .75, roughness: .5 });
  [-1, 1].forEach((s) => { K.add(g, new THREE.BoxGeometry(.9, .015, .42), rad, -1.2, 0, s * .62); K.add(g, new THREE.BoxGeometry(.06, .03, .44), K.steel, -.78, 0, s * .62); });
  [-1, 1].forEach((s) => { K.add(g, new THREE.BoxGeometry(.9, .42, .015), rad, -1.2, s * .62, 0); K.add(g, new THREE.BoxGeometry(.06, .44, .03), K.steel, -.78, s * .62, 0); });
  const engines = [];
  for (let i = 0; i < 4; i++) { const a = i * Math.PI / 2 + Math.PI / 4; engines.push(K.engine(g, -1.85, Math.cos(a) * .2, Math.sin(a) * .2, .13, .2, 0xB9A0FF)); }
  K.greeble(g, 90, K.grb, R, (R, p, s) => { const a = R() * Math.PI * 2, x = -1.6 + R() * 2.4; if (x > -.5 && x < .2) return p.set(0, 0, 0), s.set(.001, .001, .001); p.set(x, Math.cos(a) * .33, Math.sin(a) * .33); s.set(.05 + R() * .2, .04 + R() * .06, .04 + R() * .06); });
  const nav = [K.navLight(g, 0xFF3B30, -1.2, 0, -1.08), K.navLight(g, 0x34E27A, -1.2, 0, 1.08), K.navLight(g, 0xffffff, -1.65, .45, 0, .45)];
  return { g, engines, lounge: lg, nav, ring, len: 4.1 };
}

// 4) Kovalamaca avcıları — küçük tek kişilik avcı: basık gövde, ok kanat, kanat ucu topları, çift itici, kuyruk kanatçığı.
function buildFighter(K, T, o) {
  const g = new THREE.Group(), R = rng(o.seed), ht = hullTex(o.base, o.accent, o.seed, o.decal);
  ht.map.repeat.set(.6, .6); ht.rough.repeat.set(.6, .6);
  const hull = new THREE.MeshPhysicalMaterial({ map: ht.map, roughnessMap: ht.rough, metalness: .6, roughness: 1, clearcoat: .5, clearcoatRoughness: .3 });
  const acc = new THREE.MeshStandardMaterial({ color: o.accentHex, metalness: .5, roughness: .35 });
  const fus = new THREE.LatheGeometry([[0, -.62], [.13, -.6], [.16, -.4], [.155, .15], [.12, .55], [.06, .88], [0, 1.0]].map(([a, b]) => new THREE.Vector2(a, b)), 40); fus.rotateZ(-Math.PI / 2);
  K.add(g, fus, hull, 0, 0, 0).scale.set(1, .78, 1);
  K.add(g, new THREE.SphereGeometry(.11, 32, 16), K.canopy, .36, .085, 0).scale.set(2.3, .85, 1);
  const ws = new THREE.Shape([[.22, .13], [-.38, 1.0], [-.58, 1.0], [-.5, .13], [-.5, -.13], [-.58, -1.0], [-.38, -1.0], [.22, -.13]].map(([a, b]) => new THREE.Vector2(a, b)));
  const wing = new THREE.ExtrudeGeometry(ws, { depth: .035, bevelEnabled: true, bevelThickness: .012, bevelSize: .012, bevelSegments: 2 }); wing.rotateX(Math.PI / 2);
  K.add(g, wing, hull, 0, .005, 0);
  [-1, 1].forEach((s) => {
    K.add(g, new THREE.BoxGeometry(.2, .05, .06), acc, -.33, -.012, s * .62, 0, s * .52, 0);
    K.add(g, new THREE.CylinderGeometry(.024, .024, .72, 14), K.dark, -.12, -.012, s * 1.0, 0, 0, Math.PI / 2);
    K.add(g, new THREE.CylinderGeometry(.012, .012, .24, 10), K.steel, .34, -.012, s * 1.0, 0, 0, Math.PI / 2);
  });
  const fin = new THREE.ExtrudeGeometry(new THREE.Shape([[-.12, 0], [-.52, .3], [-.64, .3], [-.6, 0]].map(([a, b]) => new THREE.Vector2(a, b))), { depth: .025, bevelEnabled: true, bevelThickness: .008, bevelSize: .008, bevelSegments: 1 }); fin.translate(0, .08, -.0125);
  K.add(g, fin, hull, 0, 0, 0);
  K.greeble(g, 28, K.grb, R, (R, p, s) => { p.set(-.45 + R() * .75, .115, (R() - .5) * .16); s.set(.03 + R() * .08, .014, .02 + R() * .04); });
  const engines = [-.13, .13].map((z) => K.engine(g, -.62, -.005, z, .075, .3, o.engine, hull));
  const nav = [K.navLight(g, 0xFF3B30, -.5, -.012, -1.02, .22), K.navLight(g, 0x34E27A, -.5, -.012, 1.02, .22), K.navLight(g, 0xffffff, -.62, .38, 0, .26)];
  return { g, engines, nav, guns: [new THREE.Vector3(.48, -.012, -1.0), new THREE.Vector3(.48, -.012, 1.0)], len: 1.8 };
}

class Ships {
  // o: { x, y, s } — gezegenin ekrandaki merkezi (px, 1280×720) ve ölçeği. Menü: 640,300,1 · Masa: 300,118,.82
  constructor(canvas, o = {}) {
    (window.__galaxyFx = window.__galaxyFx || []).push(this);
    this.band = o.band || null; this.px = o.x ?? 640; this.py = o.y ?? 300; this.ps = o.s ?? 1;
    this.PC = new THREE.Vector3((this.px - 640) * UPX, (360 - this.py) * UPX, 0); this.PR = 149 * this.ps * UPX;
    this.cv = canvas; this.scale = 1; this.paused = false; this.visible = true; this.planet = 'Satürn';
    const r = this.r = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'high-performance' });
    r.setClearColor(0, 0); r.outputColorSpace = THREE.SRGBColorSpace; r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 1.1;
    this.scene = new THREE.Scene();
    const pm = new THREE.PMREMGenerator(r); this.scene.environment = pm.fromScene(envScene(), .03).texture; pm.dispose();
    this.cam = new THREE.PerspectiveCamera(FOV, ASP, .5, 140); this.cam.position.set(0, 0, CAMZ);
    const key = new THREE.DirectionalLight(0xFFF0D8, 3.0); key.position.set(-9, 8, 10); this.scene.add(key);
    const rim = new THREE.DirectionalLight(0x86AEFF, 1.6); rim.position.set(10, -3, -8); this.scene.add(rim);
    this.scene.add(new THREE.HemisphereLight(0x9DB8FF, 0x1A1430, .45));
    // Gezegen örtücüsü (görünmez; yalnız derinlik yazar).
    const occM = new THREE.MeshBasicMaterial({ colorWrite: false });
    this.occ = new THREE.Group(); this.occ.visible = false; this.occ.position.copy(this.PC); this.occ.rotation.z = -16 * Math.PI / 180; this.scene.add(this.occ);
    this.occBody = new THREE.Mesh(new THREE.SphereGeometry(this.PR, 48, 32), occM); this.occBody.renderOrder = -1; this.occ.add(this.occBody);
    this.occRing = new THREE.Mesh(new THREE.RingGeometry(this.PR * 1.53, this.PR * 2.27, 96), new THREE.MeshBasicMaterial({ colorWrite: false, side: THREE.DoubleSide }));
    this.occRing.rotation.x = -Math.PI / 2 + 22 * Math.PI / 180; this.occRing.renderOrder = -1; this.occ.add(this.occRing);
    const T = { glow: glowTex(), plume: plumeTex(), felt: feltTex(), win: windowsTex(5) }, K = Kit(T);
    this.T = T;
    this.ships = [buildKervan(K, T), buildOk(K, T), buildHalka(K, T)].map((s) => {
      s.root = new THREE.Group(); s.root.add(s.g); s.root.visible = false; this.scene.add(s.root);
      this.addTrail(s, 140, .22);
      s.up = new THREE.Vector3(0, 1, 0); s.ph = Math.random() * 6;
      return s;
    });
    this.ships.forEach((s, i) => this.launch(s, i === 0 ? .25 : -(2 + i * 5)));
    // Kovalamaca: biri kaçar, öbürü kovalayıp ateş eder (lazer atışları, namlu çakması, kalkana isabet).
    const mk = (o) => { const s = buildFighter(K, T, o); s.root = new THREE.Group(); s.root.add(s.g); s.root.visible = false; this.scene.add(s.root); this.addTrail(s, 110, .12); s.up = new THREE.Vector3(0, 1, 0); s.ph = Math.random() * 6; return s; };
    const fA = mk({ base: '#DAD8D0', accent: '#2D63C8', accentHex: 0x2D63C8, engine: 0x8FD8FF, seed: 61, decal: 'KAÇAK' });
    const fB = mk({ base: '#3A3C44', accent: '#C8352D', accentHex: 0xC8352D, engine: 0xFF7A4A, seed: 73, decal: 'AVCI' });
    const sh = new THREE.Mesh(new THREE.SphereGeometry(1, 32, 20), new THREE.MeshBasicMaterial({ color: 0x8FE3FF, transparent: true, opacity: 0, blending: THREE.AdditiveBlending, depthWrite: false })); sh.scale.set(1.05, .45, 1.15); fA.root.add(sh); fA.shield = sh; fA.shT = -9;
    this.ch = { a: fA, b: fB, on: false, wait: 4 + Math.random() * 5, bolts: [], fx: [], gi: 0,
      boltG: [new THREE.CylinderGeometry(.045, .045, .7, 10).rotateZ(Math.PI / 2), new THREE.CylinderGeometry(.014, .014, .62, 8).rotateZ(Math.PI / 2)],
      boltM: [new THREE.MeshBasicMaterial({ color: 0xFF3A2E, transparent: true, opacity: .75, blending: THREE.AdditiveBlending, depthWrite: false }), new THREE.MeshBasicMaterial({ color: 0xFFF1E8, blending: THREE.AdditiveBlending, transparent: true, depthWrite: false })] };
    this.size(); this.t = 0;
    this.tick = this.tick.bind(this); this.raf = requestAnimationFrame(this.tick);
    if (window.IntersectionObserver) { this.io = new IntersectionObserver((e) => { this.visible = e[0].isIntersecting; }); this.io.observe(canvas); }
  }
  addTrail(s, TN, size) {
    const pos = new Float32Array(TN * 3), col = new Float32Array(TN * 3), geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3)); geo.setAttribute('color', new THREE.BufferAttribute(col, 3));
    s.trail = new THREE.Points(geo, new THREE.PointsMaterial({ size, map: this.T.glow, vertexColors: true, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true }));
    s.trail.frustumCulled = false; this.scene.add(s.trail);
    s.tr = { pos, col, age: new Float32Array(TN).fill(9), i: 0, acc: 0, c: s.engines.map((e) => e.ring.material.color.clone()) };
  }
  // Kovalamaca rotası: ekranı bir uçtan öbürüne geçen, iki harmonikli kaçış dalgası; kovalayan aynı izi ~0,55 sn geriden izler.
  planChase() {
    const q = new THREE.Vector3(), p = new THREE.Vector3(), c = new THREE.Vector3(), LS = .55, LAG = Math.round(LS * 60);
    const far = Math.random() < .4;
    for (let tries = 0; tries < 160; tries++) {
      const z0 = this.band ? (far ? -18 + Math.random() * 12 : 4 + Math.random() * 5) : (far ? -34 + Math.random() * 22 : Math.random() * 9), hh = TAN * (CAMZ - z0), hw = hh * ASP, sd = Math.random() < .5 ? -1 : 1, D = 5.2 + Math.random() * 2, X0 = hw + 4.5, vx = 2 * X0 / D;
      const wy = (py) => hh * (1 - py / 360), y0 = this.band ? wy(this.band[0] + Math.random() * (this.band[1] - this.band[0])) : (Math.random() * 1.2 - .6) * hh, y1 = this.band ? wy(this.band[0] + Math.random() * (this.band[1] - this.band[0])) : (Math.random() * 1.2 - .6) * hh;
      const Ay = this.band ? .35 + Math.random() * .25 : .8 + Math.random() * .9, w1 = 1.5 + Math.random() * 1.2, f1 = Math.random() * 6, Az = this.band ? .8 : 1.2 + Math.random() * 1.6, w2 = .9 + Math.random() * .8, f2 = Math.random() * 6;
      const n = Math.round((D + LS) * 60) + 1, P = new Float32Array(n * 3);
      for (let i = 0; i < n; i++) { const t = i / 60; P[i * 3] = -sd * X0 + sd * vx * t; P[i * 3 + 1] = y0 + (y1 - y0) * Math.min(1, t / D) + Ay * Math.sin(w1 * t + f1) + .35 * Ay * Math.sin(2.7 * w1 * t + f1 * 1.7); P[i * 3 + 2] = z0 + Az * Math.sin(w2 * t + f2); }
      let ok = true, inView = 0, inBand = 0;
      for (let i = 0; i < n && ok; i += 4) {
        p.set(P[i * 3], P[i * 3 + 1], P[i * 3 + 2]); if (p.z > 11) ok = false;
        q.copy(p).project(this.cam); const sx = (q.x + 1) * 640, sy = (1 - q.y) * 360;
        if (sx > -60 && sx < 1340 && sy > -60 && sy < 780) { inView++; if (this.band && sy > this.band[0] - 10 && sy < this.band[1] + 10) inBand++; if (!this.band && this.hitsPlanet(p, 2.4, c)) ok = false; }
      }
      if (!ok || inView < 30 || (this.band && inBand < inView * .85)) continue;
      const V = new Float32Array(n * 3); for (let i = 0; i < n; i++) { const a = Math.max(0, i - 1), b = Math.min(n - 1, i + 1); for (let k = 0; k < 3; k++) V[i * 3 + k] = (P[b * 3 + k] - P[a * 3 + k]) * 60 / (b - a); }
      return { P, V, n, lag: LAG, k: 0, fireT: 1.1 + Math.random() * .6, burst: 0, rollT: LS + 1.4 + Math.random() * (D - 3.2), rollD: Math.random() < .5 ? -1 : 1, off: new THREE.Vector3(0, .18 * (Math.random() < .5 ? -1 : 1), .35) };
    }
    return null;
  }
  sampleAt(c, k, p, v, a) {
    const i = Math.min(c.n - 2, Math.floor(k)), u = k - i, A = c.P, B = c.V;
    p.set(A[i * 3] + (A[i * 3 + 3] - A[i * 3]) * u, A[i * 3 + 1] + (A[i * 3 + 4] - A[i * 3 + 1]) * u, A[i * 3 + 2] + (A[i * 3 + 5] - A[i * 3 + 2]) * u);
    v.set(B[i * 3] + (B[i * 3 + 3] - B[i * 3]) * u, B[i * 3 + 1] + (B[i * 3 + 4] - B[i * 3 + 1]) * u, B[i * 3 + 2] + (B[i * 3 + 5] - B[i * 3 + 2]) * u);
    a.set(B[i * 3 + 3] - B[i * 3], B[i * 3 + 4] - B[i * 3 + 1], B[i * 3 + 5] - B[i * 3 + 2]).multiplyScalar(60);
  }
  orientF(s, p, v, a, dt, roll) {
    const f = this._f || (this._f = new THREE.Vector3()), up = this._u || (this._u = new THREE.Vector3()), cd = this._cd || (this._cd = new THREE.Vector3()), z = this._z || (this._z = new THREE.Vector3()), m = this._m || (this._m = new THREE.Matrix4()), lat = this._l || (this._l = new THREE.Vector3());
    f.copy(v).normalize(); lat.copy(a).addScaledVector(f, -a.dot(f));
    cd.subVectors(this.cam.position, p).normalize(); cd.addScaledVector(f, -cd.dot(f));
    up.set(0, 1, 0).addScaledVector(f, -f.y).normalize().addScaledVector(cd, .35).addScaledVector(lat, .2).normalize();
    s.up.lerp(up, 1 - Math.exp(-dt * 7)).normalize();
    z.crossVectors(f, s.up).normalize(); up.crossVectors(z, f);
    m.makeBasis(f, up, z); s.root.quaternion.setFromRotationMatrix(m);
    if (roll) s.root.quaternion.multiply(this._rq || (this._rq = new THREE.Quaternion()).setFromAxisAngle(this._ax || (this._ax = new THREE.Vector3(1, 0, 0)), roll));
    s.root.position.copy(p); s.root.visible = true;
  }
  liveF(s, dt, T) {
    s.engines.forEach((e, k) => { const fl = .85 + .12 * Math.sin(T * 37 + k * 2.1) + .07 * Math.sin(T * 61 + k); e.plume.scale.set(1, fl, 1); e.plume.position.x = e.x0 - e.h * fl; e.glow.material.opacity = .8 + .2 * fl; });
    s.nav.forEach((n, k) => { n.material.opacity = k === 2 ? Math.pow(Math.max(0, Math.sin(T * 3.4 + s.ph)), 16) : .6 + .4 * Math.pow(.5 + .5 * Math.sin(T * 2.2 + k * 3), 4); });
    s.root.updateMatrixWorld(true);
    const tr = s.tr, wp = this._wp || (this._wp = new THREE.Vector3()); tr.acc += dt;
    while (tr.acc > 1 / 60) { tr.acc -= 1 / 60; s.engines.forEach((e, k) => { wp.copy(e.emit); s.g.localToWorld(wp); const j = tr.i; tr.pos[j * 3] = wp.x; tr.pos[j * 3 + 1] = wp.y; tr.pos[j * 3 + 2] = wp.z; tr.age[j] = 0; tr.ci = tr.ci || []; tr.ci[j] = k; tr.i = (tr.i + 1) % tr.age.length; }); }
  }
  fire(c, T) {
    const A = c.a, B = c.b, gun = B.guns[c.gi++ % 2].clone(); B.g.localToWorld(gun);
    const dist = gun.distanceTo(A.root.position), BS = 46, tgt = A.root.position.clone().addScaledVector(c.va, dist / BS), hit = Math.random() < .38;
    if (!hit) { const o = new THREE.Vector3(Math.random() - .5, Math.random() - .5, Math.random() - .5).normalize().multiplyScalar(.6 + Math.random() * .7); tgt.add(o); }
    const dir = tgt.sub(gun).normalize(), g = new THREE.Group();
    g.add(new THREE.Mesh(c.boltG[0], c.boltM[0]), new THREE.Mesh(c.boltG[1], c.boltM[1]));
    g.position.copy(gun).addScaledVector(dir, .35); g.quaternion.setFromUnitVectors(this._ax || (this._ax = new THREE.Vector3(1, 0, 0)), dir); this.scene.add(g);
    c.bolts.push({ g, vel: dir.multiplyScalar(BS).add(c.vb), age: 0, hit });
    const mf = this.T.glow, fl = new THREE.Sprite(new THREE.SpriteMaterial({ map: mf, color: 0xFF7A5A, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true }));
    fl.position.copy(B.guns[(c.gi + 1) % 2]).x += .12; fl.scale.setScalar(.55); B.g.add(fl); c.fx.push({ o: fl, par: B.g, age: 0, life: .08, s0: .55, s1: .2 });
  }
  hitFx(c, at, T) {
    c.a.shT = T;
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.T.glow, color: 0xBFF1FF, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true }));
    sp.position.copy(at); sp.scale.setScalar(.5); this.scene.add(sp); c.fx.push({ o: sp, par: this.scene, age: 0, life: .28, s0: .5, s1: 1.4 });
    const n = 18, pos = new Float32Array(n * 3), vel = []; for (let i = 0; i < n; i++) { pos.set([at.x, at.y, at.z], i * 3); vel.push(new THREE.Vector3(Math.random() - .5, Math.random() - .5, Math.random() - .5).normalize().multiplyScalar(4 + Math.random() * 6)); }
    const geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const pts = new THREE.Points(geo, new THREE.PointsMaterial({ size: .09, map: this.T.glow, color: 0xFFD2A0, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true })); pts.frustumCulled = false; this.scene.add(pts);
    c.fx.push({ o: pts, par: this.scene, age: 0, life: .5, vel });
  }
  tickChase(dt, T) {
    const c = this.ch, A = c.a, B = c.b, p = this._cp || (this._cp = new THREE.Vector3()), v = this._cv || (this._cv = new THREE.Vector3()), a = this._ca || (this._ca = new THREE.Vector3()), q = this._cq || (this._cq = new THREE.Vector3());
    c.va = c.va || new THREE.Vector3(); c.vb = c.vb || new THREE.Vector3();
    if (!c.on) {
      A.root.visible = B.root.visible = false; c.wait -= dt;
      if (c.wait <= 0) { const pl = this.planChase(); if (pl) { Object.assign(c, pl); c.on = true; } else c.wait = 3; }
    } else {
      c.k += dt * 60;
      if (c.k >= c.n - c.lag - 2) { c.on = false; c.wait = 9 + Math.random() * 12; A.root.visible = B.root.visible = false; }
      else {
        const tl = (c.k + c.lag) / 60, ru = (tl - c.rollT) / .8, roll = ru > 0 && ru < 1 ? c.rollD * Math.PI * 2 * (ru * ru * (3 - 2 * ru)) : 0, jolt = Math.exp(-(T - A.shT) * 9) * .25 * Math.sin((T - A.shT) * 40);
        this.sampleAt(c, c.k + c.lag, p, v, a); c.va.copy(v); this.orientF(A, p, v, a, dt, roll + jolt); this.liveF(A, dt, T);
        this.sampleAt(c, c.k, p, v, a); p.add(c.off); c.vb.copy(v); this.orientF(B, p, v, a, dt, 0); this.liveF(B, dt, T);
        q.copy(A.root.position).project(this.cam); const inA = Math.abs(q.x) < 1 && Math.abs(q.y) < 1; q.copy(B.root.position).project(this.cam); const inB = Math.abs(q.x) < 1.02 && Math.abs(q.y) < 1.02;
        c.fireT -= dt;
        if (c.fireT <= 0 && inA && inB) { if (c.burst <= 0) c.burst = 2 + Math.floor(Math.random() * 3); this.fire(c, T); c.burst--; c.fireT = c.burst > 0 ? .085 : .6 + Math.random() * .7; }
      }
    }
    A.shield.material.opacity = .42 * Math.exp(-(T - A.shT) * 6);
    for (let i = c.bolts.length - 1; i >= 0; i--) {
      const b = c.bolts[i]; b.age += dt; b.g.position.addScaledVector(b.vel, dt);
      if (b.hit && A.root.visible && b.g.position.distanceTo(A.root.position) < .7) { this.hitFx(c, b.g.position, T); this.scene.remove(b.g); c.bolts.splice(i, 1); continue; }
      if (b.age > .9) { this.scene.remove(b.g); c.bolts.splice(i, 1); }
    }
    for (let i = c.fx.length - 1; i >= 0; i--) {
      const f = c.fx[i]; f.age += dt; const u = f.age / f.life;
      if (u >= 1) { f.par.remove(f.o); f.o.material.dispose(); if (f.vel) f.o.geometry.dispose(); c.fx.splice(i, 1); continue; }
      if (f.vel) { const P = f.o.geometry.attributes.position; for (let j = 0; j < f.vel.length; j++) { f.vel[j].multiplyScalar(Math.pow(.05, dt)); P.array[j * 3] += f.vel[j].x * dt; P.array[j * 3 + 1] += f.vel[j].y * dt; P.array[j * 3 + 2] += f.vel[j].z * dt; } P.needsUpdate = true; f.o.material.opacity = 1 - u; }
      else { f.o.scale.setScalar(f.s0 + (f.s1 - f.s0) * u); f.o.material.opacity = (1 - u) * (1 - u); }
    }
    this.fade(A, dt); this.fade(B, dt);
  }
  size() { const pr = Math.min((window.devicePixelRatio || 1) * this.scale, 2.5); if (pr === this.pr) return; this.pr = pr; this.r.setPixelRatio(pr); this.r.setSize(1280, 720, false); }
  set(scale, planet) {
    if (scale && scale !== this.scale) { this.scale = scale; this.size(); }
    if (planet && planet !== this.planet) { this.planet = planet; this.ships.forEach((s) => { if (s.wait > 0) this.plan(s); }); }
    this.occBody.scale.set(1, 1 / (FLAT[this.planet] || 1), 1); this.occRing.visible = this.planet === 'Satürn';
  }
  acc(p, out) { out.subVectors(this.PC, p); const d2 = out.lengthSq(), d = Math.sqrt(d2); return out.multiplyScalar((GM[this.planet] || 10) * this.ps / ((d2 + 1.5) * d)); }
  // Yörünge planı: kenardan gir, gezegenin yakınından çekimle bükülerek geç, ekrandan çık. Çarpma/yakalanma olursa yeniden dene.
  // Ekrandaki gezegen silüeti (gövde + Satürn halkası), px; gemi bu alana girip örtülmesin/kaybolmasın.
  hitsPlanet(p, len, q) {
    q.copy(p).project(this.cam);
    const dx = (q.x + 1) * 640 - this.px, dy = (1 - q.y) * 360 - this.py, S = this.ps, c = Math.cos(16 * Math.PI / 180), sn = Math.sin(16 * Math.PI / 180);
    const lx = dx * c - dy * sn, ly = dx * sn + dy * c, fl = FLAT[this.planet] || 1, m = len * .5 * 720 / (2 * TAN * (CAMZ - p.z)) + 14;
    if (lx * lx + (ly * fl) * (ly * fl) < (149 * S + m) * (149 * S + m)) return true;
    return this.planet === 'Satürn' && (lx / (352 * S + m)) ** 2 + (ly / (118 * S + m)) ** 2 < 1;
  }
  plan(s) {
    const p = new THREE.Vector3(), v = new THREE.Vector3(), a = new THREE.Vector3(), q = new THREE.Vector3(), dt = 1 / 60, minD = this.PR * (this.planet === 'Satürn' ? 2.5 : 1) + s.len * .6 + .8;
    const far = Math.random() < .4;   // bir kısmı çok daha uzaktan geçer (yakın sınır aynı); karar rota başına, denemeler arasında değişmez
    for (let tries = 0; tries < (this.band ? 400 : 140); tries++) {
      const z = this.band ? (far ? -22 + Math.random() * 14 : 4 + Math.random() * 6) : (far ? -52 + Math.random() * 30 : -16 + Math.random() * 22), hh = TAN * (CAMZ - z), hw = hh * ASP, horiz = Math.random() < .75, sd = Math.random() < .5 ? -1 : 1;
      if (horiz) p.set(sd * (hw + 3), this.band ? hh * (1 - (this.band[0] + Math.random() * (this.band[1] - this.band[0])) / 360) : (Math.random() * 1.4 - .7) * hh, z); else p.set((Math.random() * 1.2 - .6) * hw, sd * (hh + 3), z);
      const spread = this.planet === 'Satürn' ? 1.8 : 1.1, aim = new THREE.Vector3(this.PC.x + (Math.random() - .5) * hw * spread, this.PC.y + (Math.random() - .5) * hh * spread, z + (Math.random() - .5) * 8);
      v.subVectors(aim, p).normalize().multiplyScalar(2.3 + Math.random() * 1.1);
      const P = [], V = []; let md = 1e9, inside = 0, out = false, inBand = 0;
      for (let k = 0; k < 60 * 48; k++) {
        this.acc(p, a); v.addScaledVector(a, dt); p.addScaledVector(v, dt);
        P.push(p.x, p.y, p.z); V.push(v.x, v.y, v.z);
        md = Math.min(md, p.distanceTo(this.PC));
        const h2 = TAN * (CAMZ - p.z), w2 = h2 * ASP, inView = Math.abs(p.x) < w2 + s.len * .5 && Math.abs(p.y) < h2 + s.len * .5;
        if (inView) { inside++; if (this.band && k % 3 === 0) { q.copy(p).project(this.cam); const sy = (1 - q.y) * 360; if (sy > this.band[0] && sy < this.band[1]) inBand += 3; } } else if (inside > 0 && (Math.abs(p.x) > w2 + 4 || Math.abs(p.y) > h2 + 4)) { out = true; break; }
        if (p.z > 12 || p.z < -75 || md < minD || (!this.band && k % 4 === 0 && this.hitsPlanet(p, s.len, q))) { md = -1; break; }
      }
      if (out && md >= 0 && inside > 60 * 6 && (!this.band || inBand > inside * 0.8)) { s.P = new Float32Array(P); s.V = new Float32Array(V); s.n = P.length / 3; return true; }
    }
    s.P = null; return false;
  }
  launch(s, start) {
    this.plan(s); s.k = 0; s.wait = 0;
    if (start > 0 && start < 1 && s.P) s.k = Math.floor(s.n * start); else if (start <= 0) s.wait = -start;
  }
  tick(now) {
    this.raf = requestAnimationFrame(this.tick);
    if (!this.visible || document.hidden || this.paused) { this.last = now; return; }
    const dt = Math.min(.05, (now - (this.last || now)) / 1000); this.last = now; this.t += dt;
    const T = this.t, p = new THREE.Vector3(), v = new THREE.Vector3(), a = new THREE.Vector3(), f = new THREE.Vector3(), up = new THREE.Vector3(), cd = new THREE.Vector3(), z = new THREE.Vector3(), m = new THREE.Matrix4(), wp = new THREE.Vector3();
    for (const s of this.ships) {
      if (s.wait > 0 || !s.P) { s.wait -= dt; s.root.visible = false; this.fade(s, dt); if (s.wait <= 0 && !s.P) this.launch(s, -3); continue; }
      s.k += dt * 60;
      if (s.k >= s.n - 2) { s.root.visible = false; this.launch(s, -(3 + Math.random() * 7)); continue; }
      const i = Math.floor(s.k), u = s.k - i, A = s.P, B = s.V;
      p.set(A[i * 3] + (A[i * 3 + 3] - A[i * 3]) * u, A[i * 3 + 1] + (A[i * 3 + 4] - A[i * 3 + 1]) * u, A[i * 3 + 2] + (A[i * 3 + 5] - A[i * 3 + 2]) * u);
      v.set(B[i * 3], B[i * 3 + 1], B[i * 3 + 2]);
      f.copy(v).normalize(); this.acc(p, a);
      const lat = a.clone().addScaledVector(f, -a.dot(f));
      cd.subVectors(this.cam.position, p).normalize(); cd.addScaledVector(f, -cd.dot(f));
      up.set(0, 1, 0).addScaledVector(f, -f.y).normalize().addScaledVector(cd, .5).addScaledVector(lat, 1.4).normalize();
      s.up.lerp(up, 1 - Math.exp(-dt * 2.5)).normalize();
      z.crossVectors(f, s.up).normalize(); up.crossVectors(z, f);
      m.makeBasis(f, up, z); s.root.quaternion.setFromRotationMatrix(m);
      s.root.position.copy(p); s.root.visible = true;
      // Canlı ayrıntılar
      s.engines.forEach((e, k) => { const fl = .85 + .1 * Math.sin(T * 31 + k * 2.1) + .06 * Math.sin(T * 53 + k); e.plume.scale.set(1, fl, 1); e.plume.position.x = e.x0 - e.h * fl; e.glow.material.opacity = .75 + .25 * fl; });
      s.nav.forEach((n, k) => { n.material.opacity = k === 2 ? Math.pow(Math.max(0, Math.sin(T * 2.6 + s.ph)), 18) : .55 + .45 * Math.pow(.5 + .5 * Math.sin(T * 1.6 + k * 3), 4); });
      if (s.ring) s.ring.rotation.x += dt * .35;
      s.lounge.players.forEach((q) => { q.head.position.y = .3 + Math.max(0, Math.sin(T * 2 + q.ph)) * .01; q.head.rotation.y = Math.sin(T * .7 + q.ph) * .55; q.body.rotation.x = Math.sin(T * 1.3 + q.ph) * .05; });
      s.lounge.lamp.intensity = 2.2 + Math.sin(T * 2.2 + s.ph) * .2;
      s.root.updateMatrixWorld(true);
      const tr = s.tr; tr.acc += dt;
      while (tr.acc > 1 / 40) { tr.acc -= 1 / 40; s.engines.forEach((e, k) => { if (k > 3) return; wp.copy(e.emit); s.g.localToWorld(wp); const j = tr.i; tr.pos[j * 3] = wp.x; tr.pos[j * 3 + 1] = wp.y; tr.pos[j * 3 + 2] = wp.z; tr.age[j] = 0; tr.ci = tr.ci || []; tr.ci[j] = k; tr.i = (tr.i + 1) % tr.age.length; }); }
      this.fade(s, dt);
    }
    this.tickChase(dt, T);
    this.r.render(this.scene, this.cam);
  }
  fade(s, dt) {
    const tr = s.tr, n = tr.age.length;
    for (let k = 0; k < n; k++) { tr.age[k] += dt; const q = Math.max(0, 1 - tr.age[k] / 1.4), g = q * q * .6, c = tr.c[(tr.ci && tr.ci[k]) || 0]; tr.col[k * 3] = c.r * g; tr.col[k * 3 + 1] = c.g * g; tr.col[k * 3 + 2] = c.b * g; }
    s.trail.geometry.attributes.position.needsUpdate = true; s.trail.geometry.attributes.color.needsUpdate = true;
  }
  destroy() { cancelAnimationFrame(this.raf); if (this.io) this.io.disconnect(); this.r.dispose(); }
}
// Dışa aktarma (GLB): parıltı sprite'ları, ışıklar ve alev konileri çıkarılır; motor çekirdekleri kalır.
const FIGHTERS = { kacak: { base: '#DAD8D0', accent: '#2D63C8', accentHex: 0x2D63C8, engine: 0x8FD8FF, seed: 61, decal: 'KAÇAK' }, avci: { base: '#3A3C44', accent: '#C8352D', accentHex: 0xC8352D, engine: 0xFF7A4A, seed: 73, decal: 'AVCI' } };
function exportShip(kind, opt = {}) {
  const T = { glow: glowTex(), plume: plumeTex(), felt: feltTex(), win: windowsTex(5) }, K = Kit(T);
  const s = kind === 'kervan' ? buildKervan(K, T) : kind === 'ok' ? buildOk(K, T) : kind === 'halka' ? buildHalka(K, T) : FIGHTERS[kind] ? buildFighter(K, T, FIGHTERS[kind]) : null;
  if (!s) return null;
  const g = s.g, rm = [];
  g.name = 'gemi_' + kind;
  g.traverse((o) => { if (o.isSprite || o.isLight || (!opt.plumes && s.engines.some((e) => e.plume === o))) rm.push(o); });
  rm.forEach((o) => o.parent && o.parent.remove(o));
  let i = 0; g.traverse((o) => { if ((o.isMesh || o.isInstancedMesh) && !o.name) o.name = 'parca_' + (i++); });
  return g;
}
window.GalaxyShips = { create: (canvas, o) => new Ships(canvas, o), exportShip };
