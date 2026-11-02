// Galaxy 101 — 3D treat & gift models (meters, y-up, base at y=0). Named meshes/materials for GLB/FBX export.
import * as THREE from 'https://unpkg.com/three@0.184.0/build/three.module.js';

function cvs(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; }
function tex(w, h, draw, rep) {
  const [c, x] = cvs(w, h); draw(x, w, h);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
  if (rep) { t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(rep[0], rep[1]); }
  return t;
}
let _glow;
export function glowTex() {
  if (_glow) return _glow;
  _glow = tex(128, 128, (x) => { const g = x.createRadialGradient(64, 64, 0, 64, 64, 64); g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(0.25, 'rgba(255,255,255,.55)'); g.addColorStop(1, 'rgba(255,255,255,0)'); x.fillStyle = g; x.fillRect(0, 0, 128, 128); });
  return _glow;
}
function mk(name, geo, mat, x = 0, y = 0, z = 0) { const m = new THREE.Mesh(geo, mat); m.name = name; m.position.set(x, y, z); m.castShadow = true; m.receiveShadow = true; return m; }
const lathe = (pts, seg = 48) => new THREE.LatheGeometry(pts.map(([a, b]) => new THREE.Vector2(a, b)), seg);
const ring = (name, r, t, mat, y) => { const m = mk(name, new THREE.TorusGeometry(r, t, 10, 72), mat, 0, y, 0); m.rotation.x = Math.PI / 2; return m; };

export const GIFT_SCALE = { tea: 1.05, coffee: 1.0, cola: 0.98, simit: 0.85, baklava: 0.82, seeds: 0.9, flower: 0.95, cake: 0.82, trophy: 1.1, rocket: 1.15 };

export function giftKit() {
  const P = (o) => new THREE.MeshPhysicalMaterial(o), S = (o) => new THREE.MeshStandardMaterial(o);
  const iznik = tex(512, 128, (x, w, h) => {
    x.fillStyle = '#FFFFFF'; x.fillRect(0, 0, w, h);
    x.fillStyle = '#1E4FA6'; x.fillRect(0, 46, w, 44);
    x.fillStyle = '#D9A441'; x.fillRect(0, 42, w, 3); x.fillRect(0, 91, w, 3);
    for (let i = 0; i < 16; i++) { const cx = i * 32 + 16; x.fillStyle = '#FFFFFF'; x.beginPath(); x.ellipse(cx, 68, 7, 13, 0, 0, 7); x.fill(); x.fillStyle = '#C8302A'; x.beginPath(); x.arc(cx, 68, 3.2, 0, 7); x.fill(); x.fillStyle = '#7FC0E8'; x.beginPath(); x.arc(cx + 16, 56, 2.4, 0, 7); x.arc(cx + 16, 80, 2.4, 0, 7); x.fill(); }
  });
  const foam = tex(256, 256, (x) => { const g = x.createRadialGradient(128, 128, 10, 128, 128, 128); g.addColorStop(0, '#A87346'); g.addColorStop(0.7, '#8A5A33'); g.addColorStop(1, '#5E3A1E'); x.fillStyle = g; x.fillRect(0, 0, 256, 256); for (let i = 0; i < 900; i++) { x.fillStyle = `rgba(${i % 3 ? 255 : 60},${i % 3 ? 230 : 30},${i % 3 ? 200 : 10},${0.06 + (i % 4) * 0.03})`; x.beginPath(); x.arc((i * 73.1) % 256, (i * 41.7) % 256, 1 + (i % 3), 0, 7); x.fill(); } });
  const straw = tex(64, 64, (x) => { x.fillStyle = '#FFFFFF'; x.fillRect(0, 0, 64, 64); x.fillStyle = '#E8343A'; for (let i = -64; i < 128; i += 22) { x.beginPath(); x.moveTo(i, 0); x.lineTo(i + 11, 0); x.lineTo(i + 75, 64); x.lineTo(i + 64, 64); x.fill(); } }, [2, 6]);
  const lemonFace = tex(128, 128, (x) => { x.fillStyle = '#F7E9A0'; x.beginPath(); x.arc(64, 64, 62, 0, 7); x.fill(); x.fillStyle = '#F6CF2A'; x.beginPath(); x.arc(64, 64, 56, 0, 7); x.fill(); x.strokeStyle = '#FFF6C8'; x.lineWidth = 4; for (let i = 0; i < 9; i++) { const a = (i / 9) * 6.283; x.beginPath(); x.moveTo(64, 64); x.lineTo(64 + Math.cos(a) * 54, 64 + Math.sin(a) * 54); x.stroke(); } x.fillStyle = '#FFF6C8'; x.beginPath(); x.arc(64, 64, 6, 0, 7); x.fill(); });
  const wood = tex(256, 256, (x) => { x.fillStyle = '#9A6534'; x.fillRect(0, 0, 256, 256); for (let i = 0; i < 60; i++) { x.strokeStyle = `rgba(${i % 2 ? 70 : 190},${i % 2 ? 38 : 130},${i % 2 ? 16 : 80},.35)`; x.lineWidth = 1 + (i % 3); x.beginPath(); const y = i * 4.3; x.moveTo(0, y); for (let k = 0; k <= 256; k += 16) x.lineTo(k, y + Math.sin(k * 0.04 + i) * 3); x.stroke(); } });
  const paper = tex(256, 256, (x) => { x.fillStyle = '#EFE6D2'; x.fillRect(0, 0, 256, 256); x.fillStyle = 'rgba(40,40,50,.45)'; for (let r = 0; r < 26; r++) for (let c = 0; c < 5; c++) { if ((r + c) % 7 === 0) continue; x.fillRect(8 + c * 50, 10 + r * 9.4, 38 - ((r * 7 + c * 3) % 12), 3); } x.fillStyle = 'rgba(30,30,40,.8)'; x.fillRect(8, 8, 120, 10); x.fillStyle = '#C8302A'; x.fillRect(150, 8, 90, 6); });
  const layers = tex(16, 128, (x) => { for (let i = 0; i < 16; i++) { x.fillStyle = i % 2 ? '#E7B45A' : '#B97A26'; x.fillRect(0, i * 8, 16, i % 2 ? 6 : 2); } }, [1, 1]);
  const candleTex = tex(32, 64, (x) => { x.fillStyle = '#FFFFFF'; x.fillRect(0, 0, 32, 64); x.fillStyle = '#5AA9FF'; for (let i = -32; i < 96; i += 16) { x.beginPath(); x.moveTo(0, i); x.lineTo(32, i + 16); x.lineTo(32, i + 24); x.lineTo(0, i + 8); x.fill(); } }, [1, 2]);
  const plaque = tex(128, 48, (x) => { x.fillStyle = '#1A1410'; x.fillRect(0, 0, 128, 48); x.fillStyle = '#F0C25A'; x.font = '800 30px Montserrat, sans-serif'; x.textAlign = 'center'; x.fillText('101', 64, 35); });

  const GM = {
    porcelain: P({ name: 'porselen', color: '#FFFFFF', roughness: 0.16, clearcoat: 1, clearcoatRoughness: 0.05 }),
    iznik: P({ name: 'iznik_desen', color: '#FFFFFF', map: iznik, roughness: 0.18, clearcoat: 1, clearcoatRoughness: 0.05 }),
    gold: S({ name: 'altin', color: '#F2C45C', metalness: 1, roughness: 0.16 }),
    copper: S({ name: 'bakir', color: '#D07A42', metalness: 1, roughness: 0.28 }),
    glass: P({ name: 'cam', color: '#F2F8FF', roughness: 0.02, transparent: true, opacity: 0.3, clearcoat: 1, envMapIntensity: 1.8, side: THREE.DoubleSide, depthWrite: false }),
    glassThick: P({ name: 'cam_dip', color: '#E4F0FF', roughness: 0.04, transparent: true, opacity: 0.55, clearcoat: 1, envMapIntensity: 1.6 }),
    frost: P({ name: 'buzlu_cam', color: '#E8F4FF', roughness: 0.18, transparent: true, opacity: 0.38, clearcoat: 1, envMapIntensity: 1.5, side: THREE.DoubleSide, depthWrite: false }),
    tea: P({ name: 'cay', color: '#C03A0C', roughness: 0.04, emissive: '#A32C04', emissiveIntensity: 0.62, clearcoat: 1 }),
    teaTop: S({ name: 'cay_yuzey', color: '#DE6420', emissive: '#C04A0E', emissiveIntensity: 0.55, roughness: 0.04 }),
    foam: S({ name: 'kopuk', map: foam, roughness: 0.7 }),
    water: P({ name: 'su', color: '#CFEFFF', roughness: 0.02, transparent: true, opacity: 0.35, clearcoat: 1, depthWrite: false }),
    cola: P({ name: 'kola', color: '#86300F', roughness: 0.04, emissive: '#5A1A05', emissiveIntensity: 0.75, clearcoat: 1 }),
    colaFoam: S({ name: 'kola_kopuk', color: '#EBD2AE', roughness: 0.8 }),
    ice: P({ name: 'buz', color: '#EAF7FF', roughness: 0.06, transparent: true, opacity: 0.6, clearcoat: 1, envMapIntensity: 1.6 }),
    bubble: new THREE.MeshBasicMaterial({ name: 'kabarcik', color: '#FFE0C2', transparent: true, opacity: 0.8 }),
    straw: S({ name: 'pipet', map: straw, roughness: 0.4 }),
    lemonRind: S({ name: 'limon_kabuk', color: '#F2C81E', roughness: 0.5 }),
    lemonFace: S({ name: 'limon_dilim', map: lemonFace, roughness: 0.4 }),
    mint: S({ name: 'nane', color: '#3FA64A', roughness: 0.55 }),
    cork: S({ name: 'mantar', color: '#B98A5A', roughness: 0.9 }),
    wood: S({ name: 'ahsap', map: wood, roughness: 0.6 }),
    simit: P({ name: 'simit', color: '#A9561C', roughness: 0.42, clearcoat: 0.5, clearcoatRoughness: 0.4 }),
    sesame: S({ name: 'susam', color: '#F4DFAE', roughness: 0.6 }),
    pastry: S({ name: 'hamur_kat', map: layers, color: '#FFFFFF', roughness: 0.45 }),
    syrup: P({ name: 'serbet', color: '#C88A2A', roughness: 0.08, clearcoat: 1, emissive: '#4a2a00', emissiveIntensity: 0.25 }),
    pistachio: S({ name: 'fistik', color: '#7DBA3A', roughness: 0.7 }),
    seed: P({ name: 'cekirdek', color: '#26262B', roughness: 0.35, clearcoat: 0.4 }),
    seedStripe: S({ name: 'cekirdek_cizgili', color: '#6A6A72', roughness: 0.45 }),
    shell: S({ name: 'kabuk', color: '#D9CFBF', roughness: 0.75 }),
    paper: S({ name: 'kese_kagidi', map: paper, roughness: 0.9, side: THREE.DoubleSide }),
    ceramic: P({ name: 'seramik', color: '#F6F3EE', roughness: 0.55, clearcoat: 0.2 }),
    stem: S({ name: 'sap', color: '#3E8E41', roughness: 0.55 }),
    leaf: S({ name: 'yaprak', color: '#2F7D3A', roughness: 0.5, side: THREE.DoubleSide }),
    petals: ['#E0263A', '#F5B82E', '#F27AA8', '#8E5BD8', '#FF6A3D'].map((c, i) => P({ name: 'lale_' + i, color: c, roughness: 0.32, clearcoat: 0.6, sheen: 0.6, sheenColor: '#ffffff' })),
    cakePink: S({ name: 'pasta_pembe', color: '#F7A3BE', roughness: 0.55 }),
    icing: P({ name: 'krema', color: '#FFF6EE', roughness: 0.35, clearcoat: 0.5 }),
    berry: P({ name: 'cilek', color: '#D7192F', roughness: 0.2, clearcoat: 1 }),
    berryTop: S({ name: 'cilek_yaprak', color: '#3E9A3E', roughness: 0.6 }),
    candle: S({ name: 'mum', map: candleTex, roughness: 0.5 }),
    flame: new THREE.MeshBasicMaterial({ name: 'alev', color: '#FFB547', transparent: true, blending: THREE.AdditiveBlending, depthWrite: false }),
    flameCore: new THREE.MeshBasicMaterial({ name: 'alev_ic', color: '#FFFBE8' }),
    lokum: S({ name: 'lokum', color: '#F6B4C6', roughness: 0.95 }),
    sugar: S({ name: 'seker', color: '#FFFFFF', roughness: 0.9 }),
    steel: S({ name: 'celik', color: '#A6AEC2', metalness: 0.9, roughness: 0.28 }),
    marble: P({ name: 'kaide', color: '#151A2C', metalness: 0.2, roughness: 0.22, clearcoat: 1 }),
    plaque: S({ name: 'plaka', map: plaque, metalness: 0.4, roughness: 0.4 }),
    white: P({ name: 'beyaz', color: '#F7F8FC', roughness: 0.22, clearcoat: 1 }),
    red: P({ name: 'kirmizi', color: '#E8352E', roughness: 0.25, clearcoat: 1 }),
    navy: P({ name: 'lacivert', color: '#1C2A5A', roughness: 0.3, clearcoat: 1 }),
    win: S({ name: 'pencere', color: '#0c2a40', emissive: '#7FE3FF', emissiveIntensity: 1.5 }),
  };
  const sprite = (c, o) => new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: c, transparent: true, opacity: o, depthWrite: false, blending: THREE.AdditiveBlending }));
  const steam = (g, y) => { g.userData.steam = []; for (let i = 0; i < 4; i++) { const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTex(), color: '#ffffff', transparent: true, opacity: 0, depthWrite: false })); s.userData.y = y; s.name = 'buhar' + i; g.add(s); g.userData.steam.push(s); } };
  const saucer = (r) => {
    const g = new THREE.Group(); g.name = 'tabak';
    g.add(mk('tabak', lathe([[0, 0.0025], [r * 0.36, 0.0025], [r * 0.4, 0.0012], [r * 0.46, 0.003], [r * 0.8, 0.006], [r * 0.97, 0.0095], [r, 0.0105], [r * 0.98, 0.011], [r * 0.8, 0.0072], [r * 0.46, 0.0042], [0, 0.0042]], 64), GM.porcelain));
    g.add(ring('tabak_yaldiz', r * 0.995, 0.0011, GM.gold, 0.0108));
    g.add(ring('tabak_ic_yaldiz', r * 0.44, 0.0007, GM.gold, 0.0044));
    return g;
  };
  const flame = (y, s = 1) => {
    const fl = new THREE.Group(); fl.name = 'alev'; fl.position.y = y;
    const f1 = mk('alev_dis', new THREE.SphereGeometry(0.0046 * s, 12, 10), GM.flame); f1.scale.set(1, 2, 1); f1.castShadow = false; fl.add(f1);
    const f2 = mk('alev_ic', new THREE.SphereGeometry(0.002 * s, 10, 8), GM.flameCore, 0, -0.002 * s, 0); f2.castShadow = false; fl.add(f2);
    const gs = sprite('#FFB347', 0.75); gs.scale.setScalar(0.05 * s); fl.add(gs); return fl;
  };
  class Twist extends THREE.Curve {
    constructor(R, a, k, ph) { super(); this.R = R; this.a = a; this.k = k; this.ph = ph; }
    getPoint(t, out = new THREE.Vector3()) { const u = t * Math.PI * 2, p = u * this.k + this.ph, rr = this.R + this.a * Math.cos(p); return out.set(rr * Math.cos(u), this.a * Math.sin(p), rr * Math.sin(u)); }
  }

  const B = {
    tea() {
      const g = new THREE.Group(); g.name = 'cay'; g.add(saucer(0.05));
      const prof = [[0, 0.0045], [0.0172, 0.0045], [0.0194, 0.0065], [0.0198, 0.014], [0.0174, 0.03], [0.0139, 0.045], [0.0132, 0.052], [0.0151, 0.066], [0.0196, 0.086], [0.022, 0.098], [0.0224, 0.101]];
      g.add(mk('bardak', lathe(prof, 64), GM.glass));
      g.add(mk('bardak_dip', new THREE.CylinderGeometry(0.0186, 0.019, 0.008, 48), GM.glassThick, 0, 0.0085, 0));
      g.add(mk('cay', lathe([[0, 0.0125], [0.0176, 0.0125], [0.0184, 0.0145], [0.0163, 0.03], [0.0129, 0.045], [0.0123, 0.052], [0.0142, 0.066], [0.0185, 0.0835], [0, 0.0835]], 56), GM.tea));
      g.add(mk('cay_yuzey', new THREE.CircleGeometry(0.0185, 48).rotateX(-Math.PI / 2), GM.teaTop, 0, 0.0837, 0));
      g.add(ring('bardak_yaldiz', 0.0224, 0.0009, GM.gold, 0.101));
      g.add(ring('bardak_bel', 0.0134, 0.0007, GM.gold, 0.0485));
      const sp = new THREE.Group(); sp.name = 'kasik'; sp.position.set(0.026, 0.0068, 0.012); sp.rotation.set(0, -0.55, 0.06); g.add(sp);
      const bowl = mk('kasik_cukur', new THREE.SphereGeometry(0.0055, 16, 10), GM.gold); bowl.scale.set(1.5, 0.28, 1); sp.add(bowl);
      sp.add(mk('kasik_sap', new THREE.BoxGeometry(0.038, 0.0011, 0.0022), GM.gold, 0.024, 0.0005, 0));
      g.add(mk('seker1', new THREE.BoxGeometry(0.0085, 0.0085, 0.0085), GM.sugar, -0.031, 0.0105, 0.012));
      const s2 = mk('seker2', new THREE.BoxGeometry(0.0085, 0.0085, 0.0085), GM.sugar, -0.028, 0.0105, -0.005); s2.rotation.y = 0.5; g.add(s2);
      steam(g, 0.104); return g;
    },
    coffee() {
      const g = new THREE.Group(); g.name = 'kahve'; g.add(saucer(0.052));
      g.add(mk('fincan', lathe([[0, 0.0075], [0.014, 0.0075], [0.0165, 0.0095], [0.019, 0.016], [0.025, 0.028], [0.0285, 0.042], [0.0294, 0.048]], 64), GM.iznik));
      g.add(mk('fincan_ic', lathe([[0.0294, 0.048], [0.0278, 0.0425], [0.0238, 0.03], [0.017, 0.017], [0, 0.0145]], 64), GM.porcelain));
      g.add(mk('kopuk', new THREE.CircleGeometry(0.0266, 48).rotateX(-Math.PI / 2), GM.foam, 0, 0.0436, 0));
      g.add(ring('fincan_yaldiz', 0.0294, 0.0011, GM.gold, 0.0482));
      const h = mk('kulp', new THREE.TorusGeometry(0.0098, 0.0026, 12, 28, Math.PI * 1.15), GM.porcelain, 0.0285, 0.03, 0); h.rotation.z = -Math.PI / 2 - 0.25; g.add(h);
      const wg = new THREE.Group(); wg.name = 'su_bardagi'; wg.position.set(-0.066, 0, 0.012); g.add(wg);
      wg.add(mk('su_bardak', lathe([[0, 0.002], [0.0125, 0.002], [0.0135, 0.004], [0.0158, 0.058]], 48), GM.glass));
      wg.add(mk('su_bardak_dip', new THREE.CylinderGeometry(0.0125, 0.0128, 0.005, 40), GM.glassThick, 0, 0.0045, 0));
      wg.add(mk('su', lathe([[0, 0.007], [0.0126, 0.007], [0.0148, 0.044], [0, 0.044]], 40), GM.water));
      g.add(mk('lokum1', new THREE.BoxGeometry(0.012, 0.011, 0.012), GM.lokum, 0.033, 0.0122, 0.024));
      const l2 = mk('lokum2', new THREE.BoxGeometry(0.012, 0.011, 0.012), GM.lokum, 0.037, 0.0122, 0.008); l2.rotation.y = 0.6; g.add(l2);
      steam(g, 0.052); return g;
    },
    cola() {
      const g = new THREE.Group(); g.name = 'soguk_icecek';
      g.add(mk('altlik', new THREE.CylinderGeometry(0.036, 0.036, 0.004, 48), GM.cork, 0, 0.002, 0));
      g.add(mk('bardak', lathe([[0, 0.004], [0.0218, 0.004], [0.0234, 0.007], [0.0272, 0.122]], 64), GM.frost));
      g.add(mk('bardak_dip', new THREE.CylinderGeometry(0.022, 0.0222, 0.01, 48), GM.glassThick, 0, 0.0095, 0));
      g.add(mk('icecek', lathe([[0, 0.0145], [0.0222, 0.0145], [0.0258, 0.098], [0, 0.098]], 56), GM.cola));
      g.add(ring('kopuk_halka', 0.0244, 0.0028, GM.colaFoam, 0.0985));
      g.add(mk('kopuk', new THREE.CircleGeometry(0.0246, 40).rotateX(-Math.PI / 2), GM.colaFoam, 0, 0.0992, 0));
      [[0.007, 0.101, 0.004, 0.3], [-0.009, 0.1, -0.004, 1.1], [0.002, 0.108, -0.009, 2]].forEach(([x, y, z, r], i) => { const c = mk('buz' + i, new THREE.BoxGeometry(0.015, 0.015, 0.015), GM.ice, x, y, z); c.rotation.set(r, r * 0.7, r * 0.3); g.add(c); });
      const bub = []; for (let i = 0; i < 10; i++) { const b = mk('kabarcik' + i, new THREE.SphereGeometry(0.0011, 6, 4), GM.bubble, Math.cos(i * 2.4) * 0.018, 0.02 + i * 0.007, Math.sin(i * 2.4) * 0.018); b.castShadow = false; b.userData.r = 0.012 + (i % 3) * 0.004; b.userData.a = i * 2.4; g.add(b); bub.push(b); }
      g.userData.bubbles = bub;
      const st = mk('pipet', new THREE.CylinderGeometry(0.0026, 0.0026, 0.16, 12), GM.straw, 0.008, 0.1, 0); st.rotation.z = -0.22; g.add(st);
      const lm = mk('limon', new THREE.CylinderGeometry(0.019, 0.019, 0.004, 32), [GM.lemonRind, GM.lemonFace, GM.lemonFace], -0.025, 0.118, 0); lm.rotation.z = Math.PI / 2; g.add(lm);
      const mn = mk('nane', new THREE.SphereGeometry(1, 10, 8), GM.mint, -0.004, 0.11, 0.012); mn.scale.set(0.009, 0.0018, 0.005); mn.rotation.y = 0.7; g.add(mn);
      return g;
    },
    simit() {
      const g = new THREE.Group(); g.name = 'simit';
      g.add(mk('tahta', new THREE.CylinderGeometry(0.064, 0.064, 0.008, 56), [GM.wood, GM.wood, GM.wood], 0, 0.004, 0));
      const R = 0.036, a = 0.0058, k = 9, tr = 0.0102, y0 = 0.008 + tr + a;
      [0, Math.PI].forEach((ph, i) => g.add(mk('hamur' + i, new THREE.TubeGeometry(new Twist(R, a, k, ph), 260, tr, 14, true), GM.simit, 0, y0, 0)));
      const N = 240, im = new THREE.InstancedMesh(new THREE.SphereGeometry(0.0019, 6, 4), GM.sesame, N); im.name = 'susam';
      const m = new THREE.Matrix4(), q = new THREE.Quaternion(), sc = new THREE.Vector3(1, 0.5, 1.8), p = new THREE.Vector3(), c = new Twist(R, a, k, 0), c2 = new Twist(R, a, k, Math.PI);
      for (let i = 0; i < N; i++) { const t = (i * 0.6180339) % 1, cc = i % 2 ? c : c2; cc.getPoint(t, p); const ang = ((i * 0.37) % 1 - 0.5) * 2.2; const u = t * Math.PI * 2; p.x += Math.cos(u) * Math.sin(ang) * tr * 0.95; p.z += Math.sin(u) * Math.sin(ang) * tr * 0.95; p.y += Math.cos(ang) * tr * 0.95 + y0; q.setFromEuler(new THREE.Euler(i * 0.3, i * 2.1, i * 0.7)); m.compose(p, q, sc); im.setMatrixAt(i, m); }
      im.castShadow = true; g.add(im); return g;
    },
    baklava() {
      const g = new THREE.Group(); g.name = 'baklava';
      g.add(mk('tepsi', lathe([[0, 0.0015], [0.058, 0.0015], [0.062, 0.003], [0.064, 0.011], [0.066, 0.012], [0.0655, 0.0128], [0.062, 0.0045], [0, 0.0045]], 72), GM.copper));
      const A = 0.019, Bz = 0.0132, sh = new THREE.Shape(); sh.moveTo(A, 0); sh.lineTo(0, Bz); sh.lineTo(-A, 0); sh.lineTo(0, -Bz); sh.closePath();
      const body = new THREE.ExtrudeGeometry(sh, { depth: 0.013, bevelEnabled: true, bevelThickness: 0.001, bevelSize: 0.001, bevelSegments: 2 }).rotateX(-Math.PI / 2);
      const top = new THREE.ShapeGeometry(sh).rotateX(-Math.PI / 2);
      const mat = GM.pastry.clone(); mat.map = GM.pastry.map.clone(); mat.map.needsUpdate = true; mat.map.repeat.set(40, 76);
      const crumbs = new THREE.InstancedMesh(new THREE.BoxGeometry(0.0022, 0.0014, 0.0022), GM.pistachio, 9 * 7); crumbs.name = 'fistik'; let ci = 0;
      const mm = new THREE.Matrix4(), qq = new THREE.Quaternion(), one = new THREE.Vector3(1, 1, 1);
      [[0, 0], [2 * A, 0], [-2 * A, 0], [A, Bz], [-A, Bz], [A, -Bz], [-A, -Bz], [0, 2 * Bz], [0, -2 * Bz]].forEach(([x, z], i) => {
        const pc = new THREE.Group(); pc.name = 'dilim' + i; pc.position.set(x, 0.0055, z); pc.scale.set(0.93, 1, 0.93); g.add(pc);
        pc.add(mk('dilim_govde' + i, body, mat)); pc.add(mk('dilim_serbet' + i, top, GM.syrup, 0, 0.0142, 0));
        for (let k = 0; k < 7; k++) { qq.setFromEuler(new THREE.Euler(k, k * 1.7, 0)); mm.compose(new THREE.Vector3(x * 0.93 + (k - 3) * 0.0026, 0.0205, z * 0.93 + ((k * 0.37) % 1 - 0.5) * 0.004), qq, one); crumbs.setMatrixAt(ci++, mm); }
      });
      g.add(crumbs); return g;
    },
    seeds() {
      const g = new THREE.Group(); g.name = 'cekirdek';
      const cone = mk('kulah', new THREE.ConeGeometry(0.03, 0.09, 40, 1, true), GM.paper); cone.rotation.z = Math.PI / 2 + 0.22; cone.position.set(-0.018, 0.029, 0); g.add(cone);
      const geo = new THREE.SphereGeometry(1, 10, 6), m = new THREE.Matrix4(), q = new THREE.Quaternion(), p = new THREE.Vector3(), s = new THREE.Vector3(0.0042, 0.0022, 0.0085);
      const dark = new THREE.InstancedMesh(geo, GM.seed, 70), mid = new THREE.InstancedMesh(geo, GM.seedStripe, 20), light = new THREE.InstancedMesh(geo, GM.shell, 22);
      dark.name = 'cekirdekler'; mid.name = 'cekirdekler_cizgili'; light.name = 'kabuklar';
      let di = 0, mi = 0, li = 0;
      for (let i = 0; i < 112; i++) {
        const t = (i * 0.618) % 1, a = (i * 2.399) % 6.283, spread = 0.008 + t * 0.042;
        p.set(0.028 + t * 0.05 + Math.cos(a) * 0.006, 0.0022 + (1 - t) * 0.012 * ((i % 5) / 4), Math.sin(a) * spread * 0.8);
        q.setFromEuler(new THREE.Euler((i % 5) * 0.3, a, (i % 7) * 0.2)); m.compose(p, q, s);
        if (i % 5 === 4 && li < 22) light.setMatrixAt(li++, m); else if (i % 4 === 3 && mi < 20) mid.setMatrixAt(mi++, m); else if (di < 70) dark.setMatrixAt(di++, m);
      }
      dark.count = di; mid.count = mi; light.count = li; [dark, mid, light].forEach((x) => { x.castShadow = true; g.add(x); });
      return g;
    },
    flower() {
      const g = new THREE.Group(); g.name = 'cicek';
      g.add(mk('vazo', lathe([[0, 0], [0.021, 0], [0.025, 0.006], [0.028, 0.04], [0.025, 0.074], [0.017, 0.094], [0.0155, 0.104], [0.0175, 0.11]], 56), GM.ceramic));
      g.add(ring('vazo_bant', 0.0279, 0.0013, GM.gold, 0.03));
      g.add(ring('vazo_yaldiz', 0.0175, 0.0011, GM.gold, 0.11));
      const sway = new THREE.Group(); sway.name = 'demet'; sway.position.y = 0.1; g.add(sway); g.userData.sway = sway;
      const petal = new THREE.SphereGeometry(1, 16, 12);
      [[0, 0.115, 0, 0], [-0.03, 0.1, 0.01, 1], [0.03, 0.098, -0.006, 2], [-0.012, 0.088, -0.024, 3], [0.016, 0.092, 0.026, 4]].forEach(([dx, h, dz, ci], i) => {
        const end = new THREE.Vector3(dx, h, dz), crv = new THREE.CatmullRomCurve3([new THREE.Vector3(0, 0, 0), new THREE.Vector3(dx * 0.25, h * 0.45, dz * 0.25), end]);
        sway.add(mk('sap' + i, new THREE.TubeGeometry(crv, 20, 0.0019, 8), GM.stem));
        const hd = new THREE.Group(); hd.name = 'lale' + i; hd.position.copy(end); hd.lookAt(end.clone().add(end.clone().sub(new THREE.Vector3(0, h * 0.45, 0)).normalize())); hd.rotateX(Math.PI / 2); sway.add(hd);
        for (let k = 0; k < 6; k++) { const outer = k < 3, a = (k / 3) * Math.PI * 2 + (outer ? 0 : Math.PI / 3), pt = mk('yaprak_' + i + k, petal, GM.petals[ci]); pt.scale.set(outer ? 0.0078 : 0.007, 0.019, 0.0042); pt.position.set(Math.cos(a) * 0.0048, 0.015, Math.sin(a) * 0.0048); pt.rotation.set(0, -a, 0); pt.rotateZ(outer ? 0.2 : 0.08); hd.add(pt); }
      });
      [[0.012, 0.6, 0.5], [-0.012, -0.7, 2.4]].forEach(([x, tz, ry], i) => { const lf = mk('yaprak' + i, petal, GM.leaf, x, 0.03, 0); lf.scale.set(0.007, 0.055, 0.0015); lf.rotation.set(0, ry, tz * 0.5); sway.add(lf); });
      return g;
    },
    cake() {
      const g = new THREE.Group(); g.name = 'pasta';
      g.add(mk('altlik', lathe([[0, 0], [0.03, 0], [0.032, 0.004], [0.012, 0.01], [0.009, 0.03], [0.058, 0.034], [0.061, 0.038], [0.058, 0.04], [0, 0.04]], 64), GM.porcelain));
      const y = 0.04;
      g.add(mk('pasta_govde', new THREE.CylinderGeometry(0.045, 0.045, 0.036, 64), GM.cakePink, 0, y + 0.018, 0));
      g.add(mk('krema_ust', new THREE.CylinderGeometry(0.0462, 0.0462, 0.004, 64), GM.icing, 0, y + 0.036, 0));
      g.add(ring('krema_kenar', 0.0455, 0.0036, GM.icing, y + 0.035));
      for (let i = 0; i < 16; i++) { const a = (i / 16) * Math.PI * 2, d = mk('damla' + i, new THREE.SphereGeometry(1, 10, 8), GM.icing, Math.cos(a) * 0.0457, y + 0.03 - (i % 3) * 0.003, Math.sin(a) * 0.0457); d.scale.set(0.0034, 0.008 + (i % 3) * 0.003, 0.0034); g.add(d); }
      for (let i = 0; i < 6; i++) {
        const a = (i / 6) * Math.PI * 2 + 0.25, sb = new THREE.Group(); sb.name = 'cilek' + i; sb.position.set(Math.cos(a) * 0.031, y + 0.038, Math.sin(a) * 0.031); sb.rotation.set(0.3 * Math.sin(a), a, 0.3 * Math.cos(a)); g.add(sb);
        const b = mk('cilek_govde' + i, new THREE.SphereGeometry(0.0062, 14, 10), GM.berry, 0, 0.005, 0); b.scale.set(1, 1.25, 1); sb.add(b);
        sb.add(mk('cilek_yaprak' + i, new THREE.ConeGeometry(0.0045, 0.003, 8), GM.berryTop, 0, 0.0128, 0));
      }
      g.add(mk('mum', new THREE.CylinderGeometry(0.0026, 0.0026, 0.032, 14), GM.candle, 0, y + 0.054, 0));
      const fl = flame(y + 0.076); g.add(fl); g.userData.flame = fl;
      return g;
    },
    trophy() {
      const g = new THREE.Group(); g.name = 'kupa';
      g.add(mk('kaide_alt', new THREE.BoxGeometry(0.074, 0.018, 0.074), GM.marble, 0, 0.009, 0));
      g.add(mk('kaide_ust', new THREE.BoxGeometry(0.058, 0.014, 0.058), GM.marble, 0, 0.025, 0));
      g.add(mk('kaide_bant', new THREE.BoxGeometry(0.0745, 0.002, 0.0745), GM.gold, 0, 0.0175, 0));
      g.add(mk('plaka', new THREE.PlaneGeometry(0.04, 0.012), GM.plaque, 0, 0.009, 0.0372));
      const cup = new THREE.Group(); cup.name = 'kupa_govde'; cup.position.y = 0.032; g.add(cup); g.userData.spin = cup;
      cup.add(mk('govde', lathe([[0, 0], [0.018, 0], [0.018, 0.004], [0.008, 0.01], [0.0055, 0.03], [0.011, 0.036], [0.006, 0.042], [0.012, 0.046], [0.028, 0.052], [0.035, 0.072], [0.038, 0.098], [0.039, 0.104], [0.036, 0.104], [0.034, 0.08], [0.02, 0.058], [0, 0.056]], 72), GM.gold));
      [-1, 1].forEach((sx, i) => { const h = mk('kulp' + i, new THREE.TorusGeometry(0.016, 0.0032, 12, 28, Math.PI * 1.2), GM.gold, sx * 0.037, 0.08, 0); h.rotation.z = sx > 0 ? -Math.PI / 2 - 0.3 : Math.PI / 2 + 0.3; cup.add(h); });
      const st = new THREE.Shape(); for (let i = 0; i < 10; i++) { const a = (i / 10) * Math.PI * 2 + Math.PI / 2, r = i % 2 ? 0.0062 : 0.0145; i ? st.lineTo(Math.cos(a) * r, Math.sin(a) * r) : st.moveTo(Math.cos(a) * r, Math.sin(a) * r); }
      const star = mk('yildiz', new THREE.ExtrudeGeometry(st, { depth: 0.0025, bevelEnabled: true, bevelThickness: 0.0006, bevelSize: 0.0006, bevelSegments: 1 }), GM.white, 0, 0.078, 0.034); star.rotation.x = -0.14; cup.add(star);
      return g;
    },
    rocket() {
      const g = new THREE.Group(); g.name = 'roket';
      g.add(mk('govde', lathe([[0, 0.022], [0.014, 0.022], [0.02, 0.035], [0.0235, 0.06], [0.0235, 0.085], [0.021, 0.105], [0.016, 0.12]], 56), GM.white));
      g.add(mk('burun', lathe([[0.016, 0.12], [0.012, 0.13], [0.006, 0.139], [0, 0.143]], 56), GM.red));
      g.add(ring('bant_alt', 0.0236, 0.0016, GM.red, 0.052));
      g.add(ring('bant_ust', 0.0222, 0.0014, GM.navy, 0.1));
      const wf = mk('pencere_cerceve', new THREE.TorusGeometry(0.0086, 0.0017, 12, 36), GM.gold, 0, 0.08, 0.0228); g.add(wf);
      const wn = mk('pencere', new THREE.SphereGeometry(0.0074, 18, 12), GM.win, 0, 0.08, 0.0215); wn.scale.z = 0.45; g.add(wn);
      const fs = new THREE.Shape(); fs.moveTo(0, 0); fs.lineTo(0.019, 0); fs.quadraticCurveTo(0.012, 0.022, 0.004, 0.046); fs.lineTo(0, 0.048); fs.closePath();
      const fg = new THREE.ExtrudeGeometry(fs, { depth: 0.0024, bevelEnabled: true, bevelThickness: 0.0006, bevelSize: 0.0006, bevelSegments: 1 }).translate(0, 0, -0.0012);
      for (let i = 0; i < 4; i++) { const p = new THREE.Group(); p.rotation.y = (i / 4) * Math.PI * 2 + Math.PI / 4; p.add(mk('kanat' + i, fg, GM.red, 0.012, 0, 0)); g.add(p); }
      g.add(mk('meme', lathe([[0.008, 0.022], [0.0095, 0.016], [0.0125, 0.007], [0.012, 0.006]], 32), GM.steel));
      const fl = new THREE.Group(); fl.name = 'motor_alev'; fl.position.y = 0.006; fl.visible = false; g.add(fl);
      const c1 = mk('alev', new THREE.ConeGeometry(0.012, 0.06, 18).rotateX(Math.PI), GM.flame, 0, -0.03, 0); c1.castShadow = false; fl.add(c1);
      const c2 = mk('alev_ic', new THREE.ConeGeometry(0.005, 0.03, 12).rotateX(Math.PI), GM.flameCore, 0, -0.016, 0); c2.castShadow = false; fl.add(c2);
      const gs = sprite('#FF9A3C', 0.9); gs.scale.setScalar(0.09); gs.position.y = -0.03; fl.add(gs);
      g.userData.flame = fl; g.userData.rocket = true; return g;
    },
  };
  return { B, GM };
}
