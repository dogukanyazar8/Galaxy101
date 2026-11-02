// Galaxy 101 — ıstaka (tile rack) models. 6 skins, profile-extruded, sized to tile depth TD.
// Rack-local frame: x = length, y = up, z = toward player. Slot contract (table3d.js):
// bottom row y=0.045, back face z=-0.003 · top row y=0.128, back face z=-0.022 · tiles 50×70 mm.
import * as THREE from 'https://unpkg.com/three@0.184.0/build/three.module.js';

const LEN = 0.84;
// shape (u = -z, v = y), extruded along w → rack x
const XF = new THREE.Matrix4().set(0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 0, 1);
function cvs(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; }
function tex(c, rep = [1, 1], srgb = true) { const t = new THREE.CanvasTexture(c); if (srgb) t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(rep[0], rep[1]); t.anisotropy = 16; t.minFilter = THREE.LinearMipmapLinearFilter; t.generateMipmaps = true; return t; }
function shape(pts, rad, cham) {
  const s = new THREE.Shape(), n = pts.length;
  for (let i = 0; i < n; i++) {
    const p = pts[i], a = pts[(i - 1 + n) % n], b = pts[(i + 1) % n], r = Array.isArray(rad) ? rad[i] : rad;
    const da = Math.hypot(a[0] - p[0], a[1] - p[1]), db = Math.hypot(b[0] - p[0], b[1] - p[1]), k = Math.min(r, da * 0.45, db * 0.45);
    const p1 = [p[0] + (a[0] - p[0]) / da * k, p[1] + (a[1] - p[1]) / da * k], p2 = [p[0] + (b[0] - p[0]) / db * k, p[1] + (b[1] - p[1]) / db * k];
    if (i === 0) s.moveTo(p1[0], p1[1]); else s.lineTo(p1[0], p1[1]);
    if (k < 1e-6) continue;
    if (cham) s.lineTo(p2[0], p2[1]); else s.quadraticCurveTo(p[0], p[1], p2[0], p2[1]);
  }
  s.closePath(); return s;
}
function extX(sh, len, bevel = 0, seg = 6) {
  const g = new THREE.ExtrudeGeometry(sh, { depth: len, bevelEnabled: bevel > 0, bevelSize: bevel, bevelThickness: bevel, bevelSegments: 2, curveSegments: seg, steps: 1 });
  g.applyMatrix4(XF); g.translate(-len / 2, 0, 0); return g;
}
function mesh(name, geo, mat, x = 0, y = 0, z = 0, shadow = true) { const m = new THREE.Mesh(geo, mat); m.name = name; m.position.set(x, y, z); m.castShadow = shadow; m.receiveShadow = true; return m; }
// box lying on a profile segment (u1,v1)→(u2,v2), offset along (ou,ov), x-extent wx
function seg(name, mat, a, b, wx, thick, off = [0, 0], x = 0, frac = [0, 1]) {
  const du = b[0] - a[0], dv = b[1] - a[1], L = Math.hypot(du, dv) * (frac[1] - frac[0]);
  const um = a[0] + du * (frac[0] + frac[1]) / 2 + off[0], vm = a[1] + dv * (frac[0] + frac[1]) / 2 + off[1];
  const m = mesh(name, new THREE.BoxGeometry(wx, L, thick), mat, x, vm, -um); m.rotation.x = Math.atan2(-du, dv); return m;
}
const rod = (name, mat, r, len, u, v) => { const m = mesh(name, new THREE.CylinderGeometry(r, r, len, 12).rotateZ(Math.PI / 2), mat, 0, v, -u); return m; };

// Main profile: front lip → bottom shelf → riser → mid lip → mid shelf → back panel → crest → rear.
function prof(TD, o) {
  const le = Math.max(0.0045, 0.022 - TD - 0.0015), f = o.front;
  return [[f, -0.01], [f, o.lip], [f + o.lipW, o.lip], [f + o.lipW, 0.0105], [0.003, 0.0105], [0.003, 0.097], [le, 0.097], [le, 0.0935], [0.022, 0.0935], [0.022, o.top], [o.back, o.top - (o.slope || 0)], [o.back + o.flare, -0.01]];
}

// ---------- textures ----------
// Ahşap: 1024² dikişsiz (y yönünde döşenir) damar dokusu. Damarlar dikey → ıstaka boyunca uzanır.
function woodTex(base, dark, light, seed) {
  const S = 1024, [c, x] = cvs(S, S); x.fillStyle = base; x.fillRect(0, 0, S, S);
  let s = seed; const rnd = () => ((s = (s * 16807) % 2147483647) / 2147483647), TAU = Math.PI * 2;
  for (let i = 0; i < 170; i++) {
    const x0 = (i / 170) * S + rnd() * 4, w = 0.8 + rnd() * 2.6, f1 = 1 + Math.floor(rnd() * 3), f2 = 2 + Math.floor(rnd() * 4), p1 = rnd() * TAU, p2 = rnd() * TAU;
    x.strokeStyle = rnd() < 0.72 ? dark : light; x.globalAlpha = 0.08 + rnd() * 0.22; x.lineWidth = w;
    for (const dx of [0, -S, S]) { x.beginPath(); for (let y = 0; y <= S; y += 6) { const a = y / S * TAU, px = dx + x0 + Math.sin(a * f1 + p1) * 7 + Math.sin(a * f2 + p2) * 3; y ? x.lineTo(px, y) : x.moveTo(px, y); } x.stroke(); }
  }
  x.globalAlpha = 0.16; x.fillStyle = dark; for (let i = 0; i < 2600; i++) x.fillRect(rnd() * S, rnd() * S, 1, 4 + rnd() * 10);
  x.globalAlpha = 1; return c;
}
function sedefTex() {
  const [c, x] = cvs(768, 128);
  x.drawImage(woodTex('#3a1810', 'rgba(12,4,2,.9)', 'rgba(140,70,40,.5)', 9), 0, 0, 768, 128);
  const pearl = (g) => { g.addColorStop(0, '#FFFFFF'); g.addColorStop(0.35, '#EDE6F5'); g.addColorStop(0.6, '#DDF0EC'); g.addColorStop(1, '#F6E9D8'); return g; };
  x.strokeStyle = '#D9B562'; x.lineWidth = 1.4;
  const star = (cx, cy, r) => {
    for (let k = 0; k < 2; k++) { x.beginPath(); for (let j = 0; j < 4; j++) { const a = k * Math.PI / 4 + j * Math.PI / 2; const px = cx + Math.cos(a) * r, py = cy + Math.sin(a) * r; j ? x.lineTo(px, py) : x.moveTo(px, py); } x.closePath(); x.fillStyle = pearl(x.createLinearGradient(cx - r, cy - r, cx + r, cy + r)); x.fill(); x.stroke(); }
    x.fillStyle = '#3a1810'; x.beginPath(); x.arc(cx, cy, r * 0.32, 0, 7); x.fill(); x.fillStyle = '#E6C878'; x.beginPath(); x.arc(cx, cy, r * 0.16, 0, 7); x.fill();
  };
  const rh = (cx, cy, w, h) => { x.beginPath(); x.moveTo(cx - w, cy); x.lineTo(cx, cy - h); x.lineTo(cx + w, cy); x.lineTo(cx, cy + h); x.closePath(); x.fillStyle = pearl(x.createLinearGradient(cx - w, cy, cx + w, cy)); x.fill(); x.stroke(); };
  for (let i = 0; i < 6; i++) { const cx = 64 + i * 128; star(cx, 64, 40); rh(cx + 64, 64, 14, 26); rh(cx + 64, 18, 8, 6); rh(cx + 64, 110, 8, 6); }
  x.fillStyle = pearl(x.createLinearGradient(0, 0, 768, 0)); x.fillRect(0, 2, 768, 4); x.fillRect(0, 122, 768, 4);
  x.fillStyle = '#D9B562'; x.fillRect(0, 7, 768, 1.5); x.fillRect(0, 119.5, 768, 1.5);
  return c;
}
function carbonTex() {
  const [c, x] = cvs(256, 256), n = 16, z = 256 / n;
  for (let i = 0; i < n; i++) for (let j = 0; j < n; j++) {
    const h = ((i + j * 2) % 4) < 2, g = h ? x.createLinearGradient(i * z, 0, i * z + z, 0) : x.createLinearGradient(0, j * z, 0, j * z + z);
    g.addColorStop(0, '#0c0d11'); g.addColorStop(0.5, h ? '#33363f' : '#23252c'); g.addColorStop(1, '#0c0d11'); x.fillStyle = g; x.fillRect(i * z, j * z, z, z);
  }
  return c;
}
function brushedTex() {
  const [c, x] = cvs(256, 256); x.fillStyle = '#d4d6dc'; x.fillRect(0, 0, 256, 256);
  let s = 5; const rnd = () => ((s = (s * 16807) % 2147483647) / 2147483647);
  for (let i = 0; i < 1400; i++) { x.fillStyle = rnd() < 0.5 ? 'rgba(255,255,255,.35)' : 'rgba(60,64,74,.22)'; x.fillRect(rnd() * 256, 0, 0.6 + rnd(), 256); }
  return c;
}

// Kutu izdüşümlü UV (ıstaka yerel koordinatı, metre): baskın normale göre düzlem seçilir; boy yönü (x) dokunun v ekseni.
function boxUV(g) {
  const p = new THREE.Vector3(), n = new THREE.Vector3(), nm = new THREE.Matrix3();
  g.updateMatrixWorld(true);
  g.traverse((m) => {
    const k = m.isMesh && m.material && m.material.userData && m.material.userData.uvk; if (!k) return;
    const G = m.geometry, P = G.attributes.position, N = G.attributes.normal; if (!P || !N) return;
    m.updateMatrix(); nm.getNormalMatrix(m.matrix);
    const uv = new Float32Array(P.count * 2), a = 1 / k[0], b = 1 / k[1];
    for (let i = 0; i < P.count; i++) {
      p.fromBufferAttribute(P, i).applyMatrix4(m.matrix); n.fromBufferAttribute(N, i).applyMatrix3(nm);
      const ax = Math.abs(n.x), ay = Math.abs(n.y), az = Math.abs(n.z);
      if (ax > ay && ax > az) { uv[i * 2] = p.z / k[0]; uv[i * 2 + 1] = p.y / k[0]; }
      else if (ay > az) { uv[i * 2] = p.z * a; uv[i * 2 + 1] = p.x * b; }
      else { uv[i * 2] = p.y * a; uv[i * 2 + 1] = p.x * b; }
    }
    G.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
  });
}

export const RACK_SKINS = ['walnut', 'sedef', 'chrome', 'carbon', 'neon', 'andromeda'];

export function createRackKit(o = {}) {
  const P = (p) => new THREE.MeshPhysicalMaterial(p), S = (p) => new THREE.MeshStandardMaterial(p);
  const wood = woodTex('#6B3A20', 'rgba(28,12,4,.9)', 'rgba(255,214,160,.35)', 3), woodD = woodTex('#4A2614', 'rgba(14,5,1,.9)', 'rgba(230,170,120,.25)', 17), rose = woodTex('#4A1E12', 'rgba(10,3,1,.95)', 'rgba(170,80,50,.35)', 29);
  const M = {
    walnut: P({ name: 'istaka_ceviz', map: tex(wood), roughness: 0.42, clearcoat: 0.9, clearcoatRoughness: 0.22 }),
    walnutEnd: P({ name: 'istaka_ceviz_uc', map: tex(woodD), roughness: 0.4, clearcoat: 0.9, clearcoatRoughness: 0.2 }),
    maple: P({ name: 'istaka_akcaagac_kakma', color: '#EBD5A8', roughness: 0.45, clearcoat: 0.8 }),
    brass: S({ name: 'istaka_pirinc', color: '#D6A649', metalness: 1, roughness: 0.24 }),
    rose: P({ name: 'istaka_gulagaci', map: tex(rose), roughness: 0.3, clearcoat: 1, clearcoatRoughness: 0.12 }),
    roseEnd: P({ name: 'istaka_gulagaci_uc', map: tex(rose), roughness: 0.3, clearcoat: 1, clearcoatRoughness: 0.12 }),
    sedef: P({ name: 'istaka_sedef_panel', map: tex(sedefTex(), [2, 1]), roughness: 0.22, clearcoat: 1, clearcoatRoughness: 0.06, iridescence: 0.55, iridescenceIOR: 1.45, iridescenceThicknessRange: [260, 620] }),
    pearl: P({ name: 'istaka_sedef', color: '#F6F0E6', roughness: 0.18, clearcoat: 1, iridescence: 1, iridescenceIOR: 1.5, iridescenceThicknessRange: [240, 640] }),
    gold: S({ name: 'istaka_yaldiz', color: '#E4BE62', metalness: 1, roughness: 0.2 }),
    alu: P({ name: 'istaka_aluminyum', color: '#c3c8d2', map: tex(brushedTex(), [1, 1]), metalness: 0.8, roughness: 0.34, clearcoat: 0.25 }),
    aluDark: P({ name: 'istaka_eloksal', color: '#2b3044', map: tex(brushedTex(), [1, 1]), metalness: 0.95, roughness: 0.34, clearcoat: 0.4 }),
    smoke: P({ name: 'istaka_fume_cam', color: '#0a0d18', roughness: 0.04, metalness: 0.25, clearcoat: 1, clearcoatRoughness: 0.02 }),
    led: S({ name: 'istaka_led', color: '#000', emissive: '#FFD964', emissiveIntensity: 2.6 }),
    carbon: P({ name: 'istaka_karbon', map: tex(carbonTex()), roughness: 0.32, metalness: 0.35, clearcoat: 1, clearcoatRoughness: 0.04 }),
    carbonEdge: S({ name: 'istaka_karbon_cerceve', color: '#14161d', metalness: 0.9, roughness: 0.28 }),
    seam: S({ name: 'istaka_karbon_dikis', color: '#000', emissive: '#FFD964', emissiveIntensity: 2.2 }),
    nGlass: P({ name: 'istaka_neon_cam', color: '#141a36', roughness: 0.05, metalness: 0.1, clearcoat: 1, transparent: true, opacity: 0.62 }),
    nCore: P({ name: 'istaka_neon_govde', color: '#07080e', roughness: 0.14, metalness: 0.6, clearcoat: 1 }),
    tube1: S({ name: 'istaka_neon_tup1', color: '#000', emissive: '#FFD964', emissiveIntensity: 3.2 }),
    tube2: S({ name: 'istaka_neon_tup2', color: '#000', emissive: '#F5A623', emissiveIntensity: 3.2 }),
    iris: P({ name: 'istaka_andromeda_govde', color: '#1b1f52', metalness: 0.85, roughness: 0.16, clearcoat: 1, iridescence: 1, iridescenceIOR: 1.65, iridescenceThicknessRange: [220, 860] }),
    irisEnd: P({ name: 'istaka_andromeda_uc', color: '#141038', metalness: 0.85, roughness: 0.16, clearcoat: 1, iridescence: 1, iridescenceIOR: 1.35, iridescenceThicknessRange: [300, 520], emissive: '#6B4FE0', emissiveIntensity: 0.18 }),
    galaxy: P({ name: 'istaka_andromeda_panel', color: '#ffffff', roughness: 0.2, clearcoat: 1, emissive: '#ffffff', emissiveIntensity: 0.45 }),
    aled: S({ name: 'istaka_andromeda_led', color: '#000', emissive: '#B79CFF', emissiveIntensity: 3 }),
  };
  // Doku ölçeği (m): [kesit yönü, boy yönü]. Bu malzemeler kutu izdüşümlü UV alır → çizgiler esnemez/kaymaz.
  const UVK = { walnut: [0.11, 0.55], walnutEnd: [0.09, 0.09], rose: [0.11, 0.55], roseEnd: [0.09, 0.09], alu: [0.06, 0.3], aluDark: [0.06, 0.3], carbon: [0.05, 0.05] };
  Object.entries(UVK).forEach(([k, v]) => { M[k].userData.uvk = v; });
  if (o.constellationTex) { const t = o.constellationTex(); M.galaxy.map = t; M.galaxy.emissiveMap = t; }

  // end piece outline (u,v) and builder
  const endShape = (pts, rad, cham) => shape(pts, rad, cham);
  function ends(g, sh, mat, thick, bevel, inset = 0) {
    [-1, 1].forEach((sx) => { const m = mesh('istaka_uc_' + (sx < 0 ? 'sol' : 'sag'), extX(sh, thick, bevel, 10), mat, sx * (LEN / 2 + thick / 2 - inset)); g.add(m); });
  }

  const B = {
    walnut(g, TD) {
      const o2 = { front: -0.025, lip: 0.017, lipW: 0.0075, top: 0.176, back: 0.036, flare: 0.008 }, pts = prof(TD, o2);
      g.add(mesh('istaka_govde', extX(shape(pts, [0.002, 0.004, 0.002, 0.0012, 0.001, 0.003, 0.002, 0.001, 0.001, 0.004, 0.011, 0.002]), LEN), M.walnut));
      const K = [o2.back, o2.top - 0.008], Lp = [o2.back + o2.flare, -0.01];
      [0.16, 0.84].forEach((f, i) => g.add(seg('istaka_kakma' + i, M.maple, K, Lp, LEN - 0.03, 0.0012, [0.0004, 0], 0, [f - 0.012, f + 0.012])));
      g.add(seg('istaka_isim_plakasi', M.brass, K, Lp, 0.11, 0.0016, [0.0008, 0], 0, [0.4, 0.6]));
      g.add(rod('istaka_pirinc_cubuk', M.brass, 0.0016, LEN - 0.01, -0.0205, 0.0175));
      ends(g, endShape([[-0.029, -0.012], [-0.029, 0.03], [0.012, 0.186], [0.046, 0.186], [0.05, -0.012]], [0.003, 0.012, 0.012, 0.012, 0.003]), M.walnutEnd, 0.018, 0.0016);
      [-1, 1].forEach((sx) => g.add(mesh('istaka_pirinc_topuz', new THREE.SphereGeometry(0.0045, 16, 12), M.brass, sx * (LEN / 2 + 0.0195), 0.15, -0.03)));
    },
    sedef(g, TD) {
      const o2 = { front: -0.025, lip: 0.016, lipW: 0.0075, top: 0.178, back: 0.038, flare: 0.006 }, pts = prof(TD, o2);
      g.add(mesh('istaka_govde', extX(shape(pts, [0.002, 0.003, 0.002, 0.0012, 0.001, 0.003, 0.002, 0.001, 0.001, 0.003, 0.008, 0.002]), LEN), M.rose));
      const K = [o2.back, o2.top - 0.006], Lp = [o2.back + o2.flare, -0.01];
      g.add(seg('istaka_sedef_panel', M.sedef, K, Lp, LEN - 0.02, 0.0012, [0.0006, 0], 0, [0.1, 0.9]));
      g.add(rod('istaka_yaldiz_tepe', M.gold, 0.0018, LEN, 0.03, o2.top - 0.0005));
      g.add(rod('istaka_yaldiz_on', M.gold, 0.0014, LEN - 0.01, -0.0212, 0.0165));
      ends(g, endShape([[-0.029, -0.012], [-0.029, 0.026], [-0.004, 0.13], [0.014, 0.19], [0.048, 0.19], [0.05, -0.012]], [0.003, 0.01, 0.03, 0.01, 0.012, 0.003]), M.roseEnd, 0.02, 0.0016);
      [-1, 1].forEach((sx) => {
        const d = mesh('istaka_sedef_madalyon', new THREE.CylinderGeometry(0.011, 0.011, 0.0016, 32).rotateZ(Math.PI / 2), M.pearl, sx * (LEN / 2 + 0.0215), 0.1, -0.022); g.add(d);
        const r = mesh('istaka_yaldiz_halka', new THREE.TorusGeometry(0.0118, 0.0011, 8, 40).rotateY(Math.PI / 2), M.gold, sx * (LEN / 2 + 0.0218), 0.1, -0.022); g.add(r);
      });
    },
    chrome(g, TD) {
      const o2 = { front: -0.021, lip: 0.0145, lipW: 0.0052, top: 0.168, back: 0.03, flare: 0.004 }, pts = prof(TD, o2);
      g.add(mesh('istaka_govde', extX(shape(pts, [0.0015, 0.0022, 0.0012, 0.001, 0.0008, 0.0018, 0.0012, 0.0008, 0.0008, 0.0022, 0.006, 0.0015]), LEN), M.alu));
      const K = [o2.back, o2.top - 0.004], Lp = [o2.back + o2.flare, -0.01];
      g.add(seg('istaka_cam_panel', M.smoke, K, Lp, LEN - 0.05, 0.0018, [0.0008, 0], 0, [0.12, 0.88]));
      g.add(seg('istaka_led_arka', M.led, K, Lp, LEN - 0.05, 0.0006, [0.0019, 0], 0, [0.1, 0.112]));
      g.add(mesh('istaka_led_orta', new THREE.BoxGeometry(LEN - 0.02, 0.0024, 0.0008), M.led, 0, 0.0905, -0.0026));
      g.add(mesh('istaka_led_tepe', new THREE.BoxGeometry(LEN - 0.02, 0.0018, 0.0008), M.led, 0, o2.top - 0.0035, -0.0216));
      ends(g, endShape([[-0.023, -0.011], [-0.023, 0.022], [0.02, 0.171], [0.034, 0.171], [0.035, -0.011]], [0.004, 0.008, 0.005, 0.005, 0.004]), M.aluDark, 0.012, 0.0012);
      [-1, 1].forEach((sx) => g.add(seg('istaka_led_uc', M.led, [-0.02, 0.03], [0.016, 0.158], 0.0008, 0.0016, [0.002, -0.0005], sx * (LEN / 2 + 0.0132))));
    },
    carbon(g, TD) {
      const o2 = { front: -0.023, lip: 0.016, lipW: 0.006, top: 0.176, back: 0.036, flare: 0.006, slope: 0.012 }, pts = prof(TD, o2);
      g.add(mesh('istaka_govde', extX(shape(pts, [0.002, 0.003, 0.0015, 0.001, 0.0008, 0.003, 0.0015, 0.0008, 0.0008, 0.003, 0.004, 0.002], true), LEN), M.carbon));
      g.add(mesh('istaka_dikis_on', new THREE.BoxGeometry(LEN - 0.01, 0.0007, 0.0026), M.seam, 0, o2.lip + 0.0002, -(o2.front + o2.lipW / 2)));
      g.add(mesh('istaka_dikis_orta', new THREE.BoxGeometry(LEN - 0.01, 0.0007, 0.0024), M.seam, 0, 0.0972, -0.0062));
      const K = [o2.back, o2.top - o2.slope], Lp = [o2.back + o2.flare, -0.01];
      g.add(seg('istaka_dikis_arka', M.seam, K, Lp, LEN - 0.12, 0.0006, [0.0004, 0], 0, [0.3, 0.315]));
      g.add(seg('istaka_dikis_tepe', M.seam, [0.022, o2.top], K, LEN - 0.03, 0.0006, [0, 0.0004], 0, [0.25, 0.75]));
      const blade = [[-0.028, -0.012], [-0.028, 0.012], [-0.012, 0.05], [0.014, 0.188], [0.03, 0.188], [0.05, 0.15], [0.052, -0.012]];
      ends(g, endShape(blade, [0.003, 0.003, 0.004, 0.003, 0.003, 0.004, 0.003], true), M.carbonEdge, 0.014, 0.001);
      [-1, 1].forEach((sx) => g.add(seg('istaka_dikis_uc', M.seam, [-0.012, 0.05], [0.014, 0.188], 0.0009, 0.0015, [-0.0012, 0.0002], sx * (LEN / 2 + 0.0152))));
    },
    neon(g, TD) {
      const le = Math.max(0.0045, 0.022 - TD - 0.0015);
      const glass = [[-0.021, 0.0065], [0.0075, 0.0065], [0.0075, 0.0895], [0.0265, 0.0895], [0.0265, 0.172], [0.022, 0.172], [0.022, 0.0935], [0.003, 0.0935], [0.003, 0.0105], [-0.021, 0.0105]];
      const sh = mesh('istaka_cam_kabuk', extX(shape(glass, 0.0012), LEN - 0.01), M.nGlass, 0, 0, 0, false); g.add(sh);
      g.add(mesh('istaka_omurga', extX(shape([[0.0075, -0.004], [0.03, -0.004], [0.03, 0.0895], [0.0075, 0.0895]], 0.002), LEN - 0.03), M.nCore));
      g.add(rod('istaka_tup_on', M.tube1, 0.0019, LEN - 0.012, -0.0195, 0.0126));
      g.add(rod('istaka_tup_orta', M.tube1, 0.0016, LEN - 0.012, Math.min(0.0052, le - 0.0018), 0.0953));
      g.add(rod('istaka_tup_tepe', M.tube2, 0.0019, LEN - 0.012, 0.0243, 0.1742));
      g.add(mesh('istaka_alt_isik', new THREE.BoxGeometry(LEN - 0.04, 0.0006, 0.014), M.tube2, 0, 0.006, 0.006, false));
      [-1, 1].forEach((sx) => {
        const x = sx * (LEN / 2 + 0.008);
        g.add(mesh('istaka_kule', new THREE.CapsuleGeometry(0.0105, 0.16, 8, 20), M.nCore, x, 0.088, -0.006));
        [0.028, 0.088, 0.148].forEach((v, i) => g.add(mesh('istaka_kule_halka' + i, new THREE.TorusGeometry(0.0108, 0.0012, 8, 32).rotateX(Math.PI / 2), i === 1 ? M.tube2 : M.tube1, x, v, -0.006, false)));
      });
    },
    andromeda(g, TD) {
      const o2 = { front: -0.022, lip: 0.015, lipW: 0.006, top: 0.172, back: 0.033, flare: 0.005 }, pts = prof(TD, o2);
      g.add(mesh('istaka_govde', extX(shape(pts, [0.002, 0.004, 0.002, 0.001, 0.001, 0.003, 0.002, 0.001, 0.001, 0.004, 0.012, 0.002]), LEN), M.iris));
      const K = [o2.back, o2.top - 0.008], Lp = [o2.back + o2.flare, -0.01];
      g.add(seg('istaka_galaksi_panel', M.galaxy, K, Lp, LEN - 0.03, 0.0012, [0.0006, 0], 0, [0.08, 0.92]));
      g.add(mesh('istaka_led_orta', new THREE.BoxGeometry(LEN - 0.02, 0.0022, 0.0008), M.aled, 0, 0.0905, -0.0026));
      g.add(rod('istaka_led_tepe', M.aled, 0.0014, LEN - 0.01, 0.027, o2.top + 0.0004));
      ends(g, endShape([[-0.026, -0.012], [-0.026, 0.024], [0.016, 0.18], [0.04, 0.18], [0.042, -0.012]], [0.004, 0.014, 0.012, 0.012, 0.004]), M.irisEnd, 0.014, 0.0015);
      [-1, 1].forEach((sx) => g.add(mesh('istaka_led_halka', new THREE.TorusGeometry(0.009, 0.0012, 8, 36).rotateY(Math.PI / 2), M.aled, sx * (LEN / 2 + 0.0155), 0.1, -0.012, false)));
    },
  };

  return {
    skins: RACK_SKINS,
    materials: M,
    build(skin, TD) {
      const g = new THREE.Group(); g.name = 'istaka_govde_grubu';
      (B[skin] || B.chrome)(g, TD);
      boxUV(g);
      return g;
    },
    theme(th) {
      const a1 = th.a1 || '#FFD964', a2 = th.a2 || '#F5A623';
      M.led.emissive.set(a1); M.seam.emissive.set(a1); M.tube1.emissive.set(a1); M.tube2.emissive.set(a2);
      M.aluDark.color.set(th.rim || '#2b3044').multiplyScalar(0.7);
    },
  };
}
