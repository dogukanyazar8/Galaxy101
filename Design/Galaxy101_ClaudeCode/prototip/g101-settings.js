/* Galaxy 101 — ortak ayar deposu + masa görünüm verisi. Ana menü ve oyun masası aynı ayarları okur/yazar. */
(function () {
  if (window.G101Settings) return;
  const KEY = 'galaxy101.settings', EV = 'g101-settings';
  let cache = null;
  const load = () => { if (cache) return cache; try { cache = JSON.parse(localStorage.getItem(KEY) || '{}') || {}; } catch (e) { cache = {}; } return cache; };
  const api = {
    get() { return { ...load() }; },
    set(patch, src) { cache = { ...load(), ...patch }; try { localStorage.setItem(KEY, JSON.stringify(cache)); } catch (e) {} window.dispatchEvent(new CustomEvent(EV, { detail: { patch, src } })); },
    on(fn) { const h = (e) => fn(e.detail.patch, e.detail.src); window.addEventListener(EV, h); return () => window.removeEventListener(EV, h); }
  };
  const ANDRO_FELT = 'radial-gradient(ellipse 34% 14% at 50% 50%,#FFF6E0,#F5C98A 30%,rgba(143,176,255,.75) 62%,transparent 80%),radial-gradient(ellipse 60% 30% at 50% 50%,rgba(183,156,255,.35),transparent 70%),radial-gradient(#141a46,#04050f)';
  const TABLE_PREV = { station: { rim: 'linear-gradient(160deg,#6a7396,#2c3352)', line: 'inset 0 0 0 2px var(--a1)', glow: '0 0 0 2px var(--a1),0 0 18px var(--gl)' }, wood: { rim: 'linear-gradient(160deg,#7A4528,#3A2014)', line: 'inset 0 0 0 2px #D9B15A', glow: '0 3px 0 #22120a' }, holo: { rim: 'linear-gradient(160deg,rgba(120,160,255,.35),rgba(20,30,60,.6))', line: 'inset 0 0 0 1px var(--a1),inset 0 0 20px var(--gl)', glow: '0 0 0 1px var(--a1),0 0 24px var(--gl)' } };
  const TABLE_PREV_A = { andromeda: { rim: 'linear-gradient(135deg,#1b1f4a,#6a4fc0 30%,#2f7ad6 55%,#c56fd0 80%,#1b1f4a)', line: 'inset 0 0 0 1px #B79CFF', glow: '0 0 22px rgba(143,180,255,.55)' } };
  const RACK_PREV = { chrome: { back: 'linear-gradient(#c3c8d2,#7d8494)', ledge: '#e4e7ee', glow: 'inset 0 2px 0 var(--a1)' }, walnut: { back: 'linear-gradient(#7a4527,#4a2614)', ledge: '#8a5230', glow: 'inset 0 2px 0 #EBD5A8' }, sedef: { back: 'linear-gradient(#4A1E12,#2a0f08)', ledge: '#E4BE62', glow: 'inset 0 2px 0 #F6F0E6' }, carbon: { back: 'repeating-linear-gradient(45deg,#14161d 0 4px,#2c2f38 4px 8px)', ledge: '#14161d', glow: 'inset 0 2px 0 var(--a1)' }, neon: { back: 'linear-gradient(#0b0c14,#05060c)', ledge: 'var(--a1)', glow: '0 0 14px var(--gl),inset 0 2px 0 #fff' } };
  const RACK_PREV_A = { andromeda: { back: 'linear-gradient(90deg,#0c0f2e,#3a2a6e 35%,#FFF1D0 50%,#3a6fd6 65%,#0c0f2e)', ledge: '#B79CFF', glow: '0 0 14px rgba(143,180,255,.6),inset 0 2px 0 #CFE0FF' } };
  const TILE_PREV = { classic: { face: 'linear-gradient(#FFFDF6,#F1E9D6)', edge: 'inset 0 -3px 0 #E3D7BC,0 2px 0 #B9AB8C', c1: '#D7263D', c2: '#1F5FBF', jk: '#127A4A', ts: 'none' }, galaxy: { face: 'linear-gradient(#3B2F7A,#1C1648)', edge: 'inset 0 -3px 0 #120E30,0 2px 0 #0A0820,0 0 10px rgba(140,110,255,.5)', c1: '#FF3D5A', c2: '#FFC21A', jk: '#FFD964', ts: 'drop-shadow(0 0 1.5px rgba(6,4,24,.9)) drop-shadow(0 0 1.5px rgba(6,4,24,.9))' }, gold: { face: 'linear-gradient(#1d1d24,#050507)', edge: 'inset 0 -3px 0 #D9B25A,0 2px 0 #8A6A20,0 0 10px rgba(255,201,74,.4)', c1: '#FF3348', c2: '#3D95FF', jk: '#E9C46A', ts: 'drop-shadow(0 0 1px #B8892E) drop-shadow(0 0 1px #B8892E)' }, neon: { face: 'linear-gradient(#14202E,#0A1018)', edge: 'inset 0 -3px 0 #06090E,0 2px 0 #000,0 0 10px rgba(61,245,224,.5)', c1: '#2E9BFF', c2: '#FFD21F', jk: '#3DF5E0', ts: 'drop-shadow(0 0 1.5px #000) drop-shadow(0 0 1.5px #000)' }, marble: { face: 'linear-gradient(#FFFFFF,#E4E6EC)', edge: 'inset 0 -3px 0 #C9CCD6,0 2px 0 #9499A8', c1: '#2B3A55', c2: '#C21F36', jk: '#2B3A55', ts: 'none' } };
  const LOOK = {
    vars: '--f1:#2C3790;--f2:#11153F;--rim:#4a5080',
    views: ['seat', 'mid', 'top'], tables: ['station', 'wood', 'holo', 'andromeda'], racks: ['walnut', 'sedef', 'chrome', 'carbon', 'neon', 'andromeda'], tiles: ['classic', 'galaxy', 'gold', 'neon', 'marble'],
    TABLE_PREV, TABLE_PREV_A, RACK_PREV, RACK_PREV_A, TILE_PREV, ANDRO_FELT,
    felt(k) { return k === 'andromeda' ? ANDRO_FELT : k === 'holo' ? 'repeating-linear-gradient(0deg,rgba(255,255,255,.08) 0 1px,transparent 1px 9px),repeating-linear-gradient(90deg,rgba(255,255,255,.08) 0 1px,transparent 1px 9px),radial-gradient(var(--f2),#03050d)' : k === 'wood' ? 'radial-gradient(var(--f1),var(--f2) 120%)' : 'radial-gradient(var(--f1),var(--f2))'; },
    tr: { views: [['Oyuncu gözü', 'Alçak açı, ıstaka önde'], ['Yarı yüksek', 'Orta açı, ıstaka ve masa dengeli'], ['Kuşbakışı', 'Yüksek açı, tüm masa görünür']],
      tables: [['İstasyon', 'Metal kenar, ışık şeridi'], ['Ceviz salon', 'Cilalı ceviz, pirinç detay'], ['Hologram', 'Cam kenar, ışıklı ızgara'], ['Andromeda', 'Sarmal galaksi, yanardöner kenar']],
      racks: [['Klasik ceviz', 'Masif ceviz, akçaağaç kakma'], ['Sedef kakma', 'Gül ağacı, sedef desen'], ['Alüminyum', 'Eloksal metal, füme cam'], ['Karbon', 'Karbon fiber, ışıklı dikiş'], ['Neon cam', 'Füme cam, neon tüpler'], ['Andromeda', 'Galaksi panel, yanardöner']],
      tiles: [['Klasik', 'Fildişi, klasik renkler'], ['Galaksi', 'Nebula doku, parlayan rakam'], ['Altın', 'Siyah oniks, altın çerçeve'], ['Neon', 'Füme gövde, ışıklı kenar'], ['Mermer', 'Beyaz mermer, altın damar']] },
    en: { views: [['Player view', 'Low angle, rack up front'], ['Mid view', 'Medium angle, rack and table balanced'], ['Top view', 'High angle, whole table visible']],
      tables: [['Station', 'Metal rim, light strip'], ['Walnut lounge', 'Polished walnut, brass trim'], ['Hologram', 'Glass rim, glowing grid'], ['Andromeda', 'Spiral galaxy, iridescent rim']],
      racks: [['Classic walnut', 'Solid walnut, maple inlay'], ['Mother-of-pearl', 'Rosewood, pearl inlay'], ['Aluminium', 'Anodised metal, smoked glass'], ['Carbon', 'Carbon fibre, glowing seams'], ['Neon glass', 'Smoked glass, neon tubes'], ['Andromeda', 'Galaxy panel, iridescent']],
      tiles: [['Classic', 'Ivory, classic colours'], ['Galaxy', 'Nebula texture, glowing numbers'], ['Gold', 'Black onyx, gold frame'], ['Neon', 'Smoked body, glowing edge'], ['Marble', 'White marble, gold veins']] }
  };
  api.LOOK = LOOK;
  window.G101Settings = api;
})();
