// Galaxy 101 — 2D masa (DOM). Eski masa v1 görünümü; table3d.js ile aynı API (sync, anchor, zar, hediye, efekt).
// Masa 2D görünümde this.T yerine bunu kullanır; etkileşimler aynı geri çağrılara gider (onMove/onDraw/onDiscard/onReturn/onMeldDrop/onGroupMove).
const W = 1280, H = 720;
const TC = { R: '#D7263D', B: '#1F5FBF', Y: '#E39B12', K: '#1B1B1B' };
// Yerleşim "Eski - Oyun Masası (v1)": ıstaka left 222 top 494 genişlik 836 (hücre 50×70, aralık 4) · YANDAN AL 148,572 (56×76) · TAŞ AT 1076,560 (64×86)
const RK = { x0: 234, y0: 504, sw: 54, rh: 74, tw: 50, th: 70 };
const MT = { tw: 24, th: 34, step: 26, lh: 40, gap: 12 };   // açılan per taşı (v1: 24×34)
// Açılan perler sahibinin yanında: üst oyuncu avatarının altında, yandakiler kendi tarafında, benimkiler ortada destenin altında
const MZ = { 2: { x0: 330, x1: 950, y: [178, 216], al: 'c' }, 3: { x0: 165, x1: 495, y: [232, 272, 312, 352], al: 'l' }, 1: { x0: 785, x1: 1115, y: [232, 272, 312, 352], al: 'r' }, 0: { x0: 240, x1: 1040, y: [356], al: 'c' } };
const DZ = { 0: [1076, 560, 64, 86], 3: [148, 572, 56, 76] };
const DP = { 0: [1083, 568], 1: [1160, 362], 2: [478, 92], 3: [151, 575] };
const ANC = { a1: [1188, 292], a2: [640, 116], a3: [92, 292], disc: [1108, 552], ind: [-400, -400], deck: [640, 290] };
const CL = { x: 640, y: 290 };
const BD = { x0: 0, y0: 0, cw: 0, rh: 0 };   // 2D'de ortak tahta yok (perler sahibinin yanında); ayraç/sıra noktaları gizli
const ease = 'cubic-bezier(.22,1,.36,1)';
const css = (el, o) => { Object.assign(el.style, o); return el; };
const div = (p, o, html) => { const d = document.createElement('div'); if (o) css(d, o); if (html != null) d.innerHTML = html; if (p) p.appendChild(d); return d; };

export function createTable2D(host, o = {}) {
  const root = div(host, { position: 'absolute', left: 0, top: 0, width: W + 'px', height: H + 'px', overflow: 'hidden', touchAction: 'none', userSelect: 'none', fontFamily: 'var(--fB)', pointerEvents: 'auto' });
  const felt = div(root, { position: 'absolute', left: '28px', top: '64px', width: '1224px', height: '640px', borderRadius: '46px', background: 'radial-gradient(ellipse 70% 62% at 50% 42%, var(--f1,#2C3790), var(--f2,#11153F) 120%)', boxShadow: 'inset 0 0 0 2px rgba(255,255,255,.12), inset 0 0 90px rgba(0,0,0,.35), 0 18px 50px rgba(0,0,0,.5)' });
  div(felt, { position: 'absolute', left: '14px', top: '14px', right: '14px', bottom: '14px', borderRadius: '36px', boxShadow: 'inset 0 0 0 1.5px rgba(255,255,255,.08)' });
  div(root, { position: 'absolute', left: '370px', top: '60px', width: '540px', height: '540px', borderRadius: '50%', border: '1.5px dashed rgba(255,255,255,.12)', boxSizing: 'border-box', pointerEvents: 'none' });
  div(root, { position: 'absolute', left: '370px', top: '60px', width: '540px', height: '540px', pointerEvents: 'none' }, '<svg width="540" height="540" viewBox="0 0 540 540"><defs><path id="g2arc" d="M60 270 A210 210 0 0 0 480 270"></path></defs><text font-family="Montserrat,sans-serif" font-weight="800" font-size="22" letter-spacing="10" fill="rgba(255,255,255,.12)"><textPath href="#g2arc" startOffset="50%" text-anchor="middle">GALAXY 101</textPath></text></svg>');
  const pzLine = div(null, { position: 'absolute', left: BD.x0 + 14.5 * BD.cw + 'px', top: BD.y0 - 6 + 'px', width: '0', height: 8 * BD.rh + 'px', borderLeft: '1.5px dashed rgba(255,255,255,.28)', opacity: 0, transition: 'opacity .4s' });
  const pzLab = div(null, { position: 'absolute', left: BD.x0 + 15 * BD.cw + 'px', top: BD.y0 - 22 + 'px', width: 5 * BD.cw + 'px', textAlign: 'center', font: '900 10px var(--fB)', letterSpacing: '2px', color: 'rgba(255,255,255,.55)', opacity: 0, transition: 'opacity .4s' });
  const rowDots = Array.from({ length: 8 }, (_, i) => div(null, { position: 'absolute', left: BD.x0 - 16 + 'px', top: BD.y0 + i * BD.rh + MT.th / 2 - 4 + 'px', width: '8px', height: '8px', borderRadius: '50%', opacity: 0, transition: 'opacity .3s' }));
  // ıstaka
  const rack = div(root, { position: 'absolute', left: '222px', top: '494px', width: '836px', height: '168px', borderRadius: '20px', background: 'linear-gradient(var(--pnH,#1A2C5C),var(--sd,#0E1B3E))', boxShadow: 'inset 0 2px 0 rgba(255,255,255,.1), 0 8px 0 var(--pnS,#050B1E), 0 18px 34px rgba(0,0,0,.4)' });
  for (let r = 0; r < 2; r++) for (let i = 0; i < 15; i++) div(rack, { position: 'absolute', left: 12 + i * RK.sw + 'px', top: 10 + r * RK.rh + 'px', width: RK.tw + 'px', height: RK.th + 'px', borderRadius: '9px', background: 'rgba(0,0,0,.22)', boxShadow: 'inset 0 2px 4px rgba(0,0,0,.35)' });
  const zone = (k, label) => { const [x, y, w, hh] = DZ[k], z = div(root, { position: 'absolute', left: x + 'px', top: y + 'px', width: w + 'px', height: hh + 'px', borderRadius: '13px', border: '2px dashed rgba(255,255,255,.25)', boxSizing: 'border-box', transition: 'border-color .2s, background .2s, transform .2s' }); div(root, { position: 'absolute', left: x + w / 2 - 51 + 'px', top: y + hh + 4 + 'px', width: '102px', textAlign: 'center', font: '900 9px var(--fB)', letterSpacing: '.5px', color: 'var(--ic,#C9D6FF)' }, label); return z; };
  const zDisc = zone(0, o.en ? 'DISCARD' : 'TAŞ AT'), zSide = zone(3, o.en ? 'TAKE LEFT' : 'YANDAN AL');
  // deste kümesi
  const cluster = div(root, { position: 'absolute', left: 0, top: 0, transition: 'transform .6s ' + ease });
  const deck = div(cluster, { position: 'absolute', left: '-26px', top: '-36px', width: '52px', height: '72px', borderRadius: '10px', background: 'linear-gradient(160deg,#2a3486,#141a4c)', boxShadow: 'inset 0 0 0 2px rgba(255,255,255,.12), 0 4px 0 #0a0d2a, 0 8px 0 #070a20, 0 14px 24px rgba(0,0,0,.45)', display: 'grid', placeItems: 'center', font: '800 15px var(--fH)', color: 'var(--a1,#FFD964)', cursor: 'default', transition: 'box-shadow .3s' }, '101');
  const deckN = div(cluster, { position: 'absolute', left: '-30px', top: '42px', width: '60px', textAlign: 'center', font: '900 10px var(--fB)', letterSpacing: '1.4px', color: 'rgba(255,255,255,.75)' });
  const okT = div(cluster, { position: 'absolute', left: '62px', top: '-32px', display: 'none' }), okL = div(cluster, { position: 'absolute', left: '50px', top: '42px', width: '70px', textAlign: 'center', font: '900 10px var(--fB)', letterSpacing: '1.4px', color: 'var(--a1,#FFD964)' }, o.en ? 'OKEY' : 'OKEY');
  const indL = div(cluster, { position: 'absolute', left: '-122px', top: '42px', width: '76px', textAlign: 'center', font: '900 10px var(--fB)', letterSpacing: '1.4px', color: 'rgba(255,255,255,.75)' }, o.en ? 'INDICATOR' : 'GÖSTERGE');
  const markL = div(root, { position: 'absolute', inset: 0, pointerEvents: 'none' });
  const fxL = div(root, { position: 'absolute', inset: 0, pointerEvents: 'none', zIndex: 50 });
  const tiles = new Map(), desc = new Map(), anchors = {}; let fl = { dbl: true }, thumbs = {}, sel = null, drag = null, lastClick = { id: null, t: 0 }, alive = true;
  const fmtFace = (t, c, n, w, h) => {
    if (!c) { t.face.innerHTML = ''; css(t.el, { background: 'linear-gradient(160deg,#2a3486,#141a4c)' }); return; }
    css(t.el, { background: 'linear-gradient(#FFFDF6,#EEE2C6)' });
    if (c === 'O') { t.face.innerHTML = `<div style="font:900 ${Math.round(h * 0.42)}px/1 var(--fH);color:#E0A021">★</div><div style="font:900 ${Math.max(7, Math.round(h * 0.13))}px var(--fB);letter-spacing:1px;color:#127A4A;margin-top:2px">OKEY</div>`; return; }
    const col = c === 'J' ? '#127A4A' : TC[c] || '#1B1B1B', txt = c === 'J' ? '★' : n;
    t.face.innerHTML = `<div style="font:800 ${Math.round(h * (String(txt).length > 1 ? 0.44 : 0.5))}px/1 var(--fH);color:${col};letter-spacing:-1px">${txt}</div><div style="width:${Math.max(4, Math.round(h * 0.1))}px;height:${Math.max(4, Math.round(h * 0.1))}px;border-radius:50%;background:${col};margin-top:${Math.round(h * 0.05)}px"></div>`;
  };
  function makeTile(id) {
    const el = div(root, { position: 'absolute', left: 0, top: 0, borderRadius: '8px', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', transition: `transform .34s ${ease}, width .25s, height .25s, box-shadow .2s, opacity .3s`, willChange: 'transform', zIndex: 2 });
    el.dataset.tid = id; const face = div(el, { display: 'flex', flexDirection: 'column', alignItems: 'center', pointerEvents: 'none' }); const tag = div(el, { position: 'absolute', right: '-5px', top: '-5px', width: '16px', height: '16px', borderRadius: '50%', display: 'none', placeItems: 'center', font: '900 11px/1 var(--fB)', boxShadow: '0 2px 4px rgba(0,0,0,.4)', pointerEvents: 'none' });
    return { id, el, face, tag, x: 640, y: 330, w: 0, h: 0, key: '' };
  }
  const okeyOf = (c, n) => ({ c, n: (n % 13) + 1 });
  let MP = {};   // açılan per taşlarının konumu: id → {x,y}
  function layoutMelds(list) {
    MP = {}; const by = { 0: [], 1: [], 2: [], 3: [] };
    list.filter((d) => d.at[0] === 'm').sort((a, b) => a.at[3] - b.at[3] || a.at[2] - b.at[2]).forEach((d) => { const k = d.at[1], arr = by[k], last = arr[arr.length - 1], pd = last && last[last.length - 1]; if (pd && pd.at[3] === d.at[3] && pd.at[2] === d.at[2] - 1) last.push(d); else arr.push([d]); });
    [0, 1, 2, 3].forEach((k) => {
      const Z = MZ[k], segs = [...by[k].filter((g) => g.length > 2), ...by[k].filter((g) => g.length === 2)], W = Z.x1 - Z.x0, lines = [[]], lw = [0];
      segs.forEach((g) => { const w = g.length * MT.step - 2; let li = lines.length - 1; if (lw[li] && lw[li] + MT.gap + w > W && lines.length < Z.y.length) { lines.push([]); lw.push(0); li++; } lines[li].push(g); lw[li] += (lw[li] ? MT.gap : 0) + w; });
      lines.forEach((ln, li) => { let x = Z.al === 'l' ? Z.x0 : Z.al === 'r' ? Z.x1 - lw[li] : Z.x0 + (W - lw[li]) / 2; const y = Z.y[Math.min(li, Z.y.length - 1)]; ln.forEach((g) => { g.forEach((d, j) => { MP[d.id] = { x: Math.round(x + j * MT.step), y }; }); x += g.length * MT.step - 2 + MT.gap; }); });
    });
  }
  function poseOf(d) {
    const [k, s, a, b] = d.at;
    if (k === 'r') return s === 0 ? { x: RK.x0 + b * RK.sw, y: RK.y0 + a * RK.rh, w: RK.tw, h: RK.th, show: true, rack: true } : { show: false };
    if (k === 'm') { const p = MP[d.id] || { x: 640, y: 200 }; return { x: p.x, y: p.y, w: MT.tw, h: MT.th, show: true, z: 5 }; }
    if (k === 'd') { const [x, y] = DP[s] || [640, 330]; return { x: x + Math.min(a, 4) * 1.5, y: y - Math.min(a, 4) * 1.5, w: s === 0 || s === 3 ? 50 : 42, h: s === 0 || s === 3 ? 70 : 60, show: true, z: 3 + a }; }
    if (k === 'i') return { x: CL.x - 112, y: CL.y - 34, w: 48, h: 68, show: true, z: 4 };
    if (k === 'f') return { x: CL.x - 36, y: CL.y - 50, w: 72, h: 100, show: true, z: 40, fin: true };
    return { show: false };
  }
  const clusterXY = () => CL;   // v1: gösterge · deste · okey hep ortada
  function place(t, p, dl) { const key = `${p.x}|${p.y}|${p.w}|${p.h}`; t.el.style.transitionDelay = dl ? dl + 'ms' : '0ms'; if (t.w !== p.w || t.h !== p.h) { css(t.el, { width: p.w + 'px', height: p.h + 'px', borderRadius: Math.round(p.w * 0.17) + 'px' }); t.w = p.w; t.h = p.h; t.faceKey = ''; } t.x = p.x; t.y = p.y; t.el.style.zIndex = p.z || (p.rack ? 6 : 2); if (key !== t.key) { t.key = key; t.el.style.transform = `translate(${p.x}px,${p.y}px)${sel === t.id && p.rack ? ' translateY(-10px)' : ''}`; } }
  function sync(list, flags, opts = {}) {
    fl = { ...fl, ...(flags || {}) }; const seen = new Set(); layoutMelds(list);
    for (const d of list) {
      const p = poseOf(d), prev = desc.get(d.id); desc.set(d.id, d); seen.add(d.id);
      let t = tiles.get(d.id), fresh = false;
      if (!t) { t = makeTile(d.id); tiles.set(d.id, t); fresh = true; const c = clusterXY(); t.el.style.transition = 'none'; t.el.style.transform = `translate(${c.x - 23}px,${c.y - 32}px)`; t.el.style.opacity = '0'; }
      if (!p.show) { t.el.style.display = 'none'; continue; } t.el.style.display = 'flex';
      const fk = (d.c || '') + (d.n || '') + p.h; if (t.faceKey !== fk || t.h !== p.h) { if (t.h !== p.h) css(t.el, { width: p.w + 'px', height: p.h + 'px', borderRadius: Math.round(p.w * 0.17) + 'px' }); t.w = p.w; t.h = p.h; fmtFace(t, d.c, d.n, p.w, p.h); t.faceKey = fk; }
      const hi = d.glow ? ',0 0 0 2.5px #3CCB6E' : '', se = sel === d.id ? ',0 0 0 3px var(--a1,#FFD964),0 0 18px var(--gl,rgba(255,201,74,.5))' : '';
      t.el.style.boxShadow = d.c ? `inset 0 -${Math.round(p.h * 0.06)}px 0 #D9C9A2, 0 ${Math.round(p.h * 0.05)}px 0 #A8925F, 0 ${Math.round(p.h * 0.1)}px ${Math.round(p.h * 0.18)}px rgba(0,0,0,.35)${hi}${se}` : '0 3px 0 #070a20, 0 6px 10px rgba(0,0,0,.4)';
      if (d.tag) { css(t.tag, { display: 'grid', background: d.tag === 'side' ? '#FFB547' : '#43D6FF', color: d.tag === 'side' ? '#2A1600' : '#04222E' }); t.tag.textContent = d.tag === 'side' ? '←' : '+'; } else t.tag.style.display = 'none';
      if (fresh) { setTimeout(() => { t.el.style.transition = `transform .5s ${ease}, width .25s, height .25s, box-shadow .2s, opacity .3s`; t.el.style.opacity = '1'; place(t, p, d.dl || 0); }, 16); }
      else if (!(drag && (drag.id === d.id || (drag.group && drag.group.some((m) => m.id === d.id))))) {
        if (opts.instant) { t.el.style.transition = 'none'; place(t, p, 0); setTimeout(() => { t.el.style.transition = `transform .34s ${ease}, width .25s, height .25s, box-shadow .2s, opacity .3s`; }, 16); }
        else place(t, p, 0);
        t.el.style.opacity = '1';
      }
      if (d.at[0] === 'f' && d.nuke && (!prev || prev.at[0] !== 'f')) finishFx(t, d.fxs || 'nuke');
      if (d.at[0] === 'i') { const ok = okeyOf(d.c, d.n); okT.style.display = 'flex'; okT.innerHTML = ''; const tt = { el: okT, face: div(okT) }; css(okT, { width: '46px', height: '64px', borderRadius: '8px', background: 'linear-gradient(#FFFDF6,#EEE2C6)', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', boxShadow: 'inset 0 -4px 0 #D9C9A2, 0 3px 0 #A8925F, 0 0 0 2.5px var(--a1,#FFD964), 0 0 22px var(--gl,rgba(255,201,74,.55))' }); css(tt.face, { display: 'flex', flexDirection: 'column', alignItems: 'center' }); fmtFace(tt, ok.c, ok.n, 46, 64); css(okT, { background: 'linear-gradient(#FFFDF6,#EEE2C6)' }); }
    }
    for (const [id, t] of tiles) if (!seen.has(id)) { t.el.remove(); tiles.delete(id); desc.delete(id); if (sel === id) sel = null; }
    const c = clusterXY(); cluster.style.transform = `translate(${c.x}px,${c.y}px)`; deckN.textContent = (o.en ? 'DECK ' : 'DESTE ') + (fl.deck ?? '');
    deck.style.boxShadow = 'inset 0 0 0 2px rgba(255,255,255,.12), 0 4px 0 #0a0d2a, 0 8px 0 #070a20, 0 14px 24px rgba(0,0,0,.45)' + (fl.canDraw ? ', 0 0 0 3px var(--a1,#FFD964), 0 0 26px var(--gl,rgba(255,201,74,.6))' : '');
    deck.style.cursor = fl.canDraw ? 'pointer' : 'default'; deck.style.opacity = fl.deck === 0 ? '.35' : '1';
    if (!list.some((d) => d.at[0] === 'i')) okT.style.display = 'none'; indL.style.display = okL.style.display = list.some((d) => d.at[0] === 'i') ? 'block' : 'none';
    zDisc.style.borderColor = fl.canDiscard ? 'var(--a1,#FFD964)' : 'rgba(255,255,255,.25)'; zDisc.style.background = fl.canDiscard ? 'rgba(255,217,100,.08)' : 'transparent';
    zSide.style.borderColor = fl.canDraw && fl.sideId ? 'var(--a1,#FFD964)' : fl.returnId ? 'rgba(255,181,71,.7)' : 'rgba(255,255,255,.25)';
    pzLine.style.opacity = pzLab.style.opacity = fl.pairZone ? 1 : 0; pzLab.textContent = fl.pairZone || '';
    rowDots.forEach((dt, i) => { const r = (fl.boardRows || [])[i]; dt.style.opacity = r ? (r.active ? 1 : 0.6) : 0; if (r) css(dt, { background: r.color, boxShadow: r.active ? `0 0 10px ${r.color}` : 'none' }); });
    drawMarks();
  }
  // işleme hedefleri: seçili / sürüklenen taş için tahtadaki uygun yerler
  function targetsFor(id) { return (fl.canMeld && id && fl.targets && fl.targets[id]) || []; }
  const tgXY = (tg) => { const p = MP[tg.id] || { x: -99, y: -99 }; return { x: p.x + (tg.kind === 'pre' ? -MT.step : tg.kind === 'post' ? MT.step : 0), y: p.y }; };
  function drawMarks(hot) {
    markL.innerHTML = ''; const id = drag && drag.kind === 'rack' && !drag.group ? drag.id : sel;
    targetsFor(id).forEach((tg) => { const p = tgXY(tg), ok = tg.kind === 'okey', on = hot === tg; div(markL, { position: 'absolute', left: p.x - 3 + 'px', top: p.y - 3 + 'px', width: MT.tw + 6 + 'px', height: MT.th + 6 + 'px', borderRadius: '8px', border: `2px dashed ${ok ? '#5CF29A' : '#FFC94A'}`, boxSizing: 'border-box', background: on ? (ok ? 'rgba(92,242,154,.25)' : 'rgba(255,201,74,.25)') : 'transparent', boxShadow: `0 0 ${on ? 18 : 10}px ${ok ? 'rgba(92,242,154,.6)' : 'rgba(255,201,74,.55)'}`, zIndex: 8 }); });
  }
  // ---------- işaretçi ----------
  const pt = (e) => { const r = root.getBoundingClientRect(), k = r.width / W; return { x: (e.clientX - r.left) / k, y: (e.clientY - r.top) / k }; };
  const inBox = (p, x, y, w, h, pad = 0) => p.x >= x - pad && p.x <= x + w + pad && p.y >= y - pad && p.y <= y + h + pad;
  const slotAt = (p) => (inBox(p, RK.x0 - 10, RK.y0 - 14, 15 * RK.sw + 10, 2 * RK.rh + 10) ? { r: p.y > RK.y0 + RK.rh - 6 ? 1 : 0, i: Math.max(0, Math.min(14, Math.round((p.x - RK.x0 - RK.tw / 2) / RK.sw))) } : null);
  const inDisc = (p) => inBox(p, DZ[0][0], DZ[0][1], DZ[0][2], DZ[0][3], 26), inSide = (p) => inBox(p, DZ[3][0], DZ[3][1], DZ[3][2], DZ[3][3], 30);
  function nearTarget(id, p) { let best = null, bd = 34; targetsFor(id).forEach((tg) => { const q = tgXY(tg), d = Math.hypot(q.x + MT.tw / 2 - p.x, q.y + MT.th / 2 - p.y); if (d < bd) { bd = d; best = tg; } }); return best; }
  function boardHit(p) { let best = null, bd = 24; desc.forEach((d, id) => { if (d.at[0] !== 'm') return; const t = tiles.get(id); if (!t) return; const dd = Math.hypot(t.x + MT.tw / 2 - p.x, t.y + MT.th / 2 - p.y); if (dd < bd) { bd = dd; best = id; } }); return best; }
  function grid() { const g = [Array(15).fill(null), Array(15).fill(null)]; desc.forEach((d, id) => { if (d.at[0] === 'r' && d.at[1] === 0) g[d.at[2]][d.at[3]] = id; }); return g; }
  function singlePlan(from, slot, id) {
    const N = 15, g = grid(); g[from.r][from.i] = null; const row = g[slot.r], i = slot.i;
    if (row[i]) { let lE = -1, rE = -1; for (let k = i - 1; k >= 0; k--) if (!row[k]) { lE = k; break; } for (let k = i + 1; k < N; k++) if (!row[k]) { rE = k; break; }
      if (rE >= 0 && (lE < 0 || rE - i <= i - lE)) for (let k = rE; k > i; k--) row[k] = row[k - 1]; else if (lE >= 0) for (let k = lE; k < i; k++) row[k] = row[k + 1]; else { const o2 = g[1 - slot.r]; let b = -1, bd = 99; for (let k = 0; k < N; k++) if (!o2[k] && Math.abs(k - i) < bd) { bd = Math.abs(k - i); b = k; } if (b >= 0) o2[b] = row[i]; } }
    row[i] = id; const plan = {}; g.forEach((rw, r) => rw.forEach((x, k) => { if (x) plan[x] = { r, i: k }; })); return plan;
  }
  function down(e) {
    if (e.button !== 0) return; const p = pt(e), tEl = e.target.closest && e.target.closest('[data-tid]'), id = tEl && tEl.dataset.tid, d = id && desc.get(id);
    if (d && d.at[0] === 'r' && d.at[1] === 0 && fl.dragRack) { drag = { kind: 'rack', id, from: { r: d.at[2], i: d.at[3] }, seg: d.seg, s: p, moved: false }; if (d.seg) drag.holdT = setTimeout(startGroup, 360); }
    else if (id && id === fl.sideId && fl.canDraw) drag = { kind: 'side', id, s: p, moved: false };
    else if (fl.canDraw && e.target.closest && e.target.closest('[data-deck]')) drag = { kind: 'deck', s: p, moved: false };
    else if (sel && fl.canDiscard && inDisc(p)) { const sd = desc.get(sel); sel = null; if (sd) o.onDiscard && o.onDiscard({ r: sd.at[2], i: sd.at[3] }); return; }
    else if (sel && fl.canMeld) { const tg = nearTarget(sel, p), sd = desc.get(sel); if (tg && sd) { sel = null; drawMarks(); o.onMeldDrop && o.onMeldDrop({ r: sd.at[2], i: sd.at[3] }, tg.id, tg.kind); } return; }
    else return;
    try { root.setPointerCapture(e.pointerId); } catch (x) {}
  }
  function startGroup() {
    if (!drag || drag.kind !== 'rack' || drag.group) return; const d0 = desc.get(drag.id); if (!d0 || !d0.seg) return;
    const mem = [...desc.values()].filter((d) => d.seg === d0.seg && d.at[0] === 'r' && d.at[1] === 0).sort((a, b) => a.at[3] - b.at[3]); if (mem.length < 2) return;
    drag.group = mem.map((d) => ({ id: d.id, di: d.at[3] - d0.at[3], from: { r: d.at[2], i: d.at[3] } })); drag.moved = true; sel = null; o.onGroupHint && o.onGroupHint(true); move({ clientX: drag.lastX, clientY: drag.lastY, synthetic: true });
  }
  function follow(id, x, y, sc) { const t = tiles.get(id); if (!t) return; t.el.style.transition = 'box-shadow .2s'; t.el.style.zIndex = 60; t.el.style.transform = `translate(${x}px,${y}px) scale(${sc || 1.08})`; t.key = ''; }
  function move(e) {
    if (!drag) return; if (!e.synthetic) { drag.lastX = e.clientX; drag.lastY = e.clientY; } if (drag.lastX == null) return;
    const r = root.getBoundingClientRect(), k = r.width / W, p = { x: (drag.lastX - r.left) / k, y: (drag.lastY - r.top) / k };
    if (!drag.moved && Math.hypot(p.x - drag.s.x, p.y - drag.s.y) > 5) { drag.moved = true; if (drag.holdT) { clearTimeout(drag.holdT); drag.holdT = null; } sel = null; if (drag.kind === 'deck') { drag.ghost = div(root, { position: 'absolute', left: 0, top: 0, width: RK.tw + 'px', height: RK.th + 'px', borderRadius: '8px', background: 'linear-gradient(160deg,#2a3486,#141a4c)', boxShadow: '0 0 0 2px var(--a1,#FFD964), 0 10px 20px rgba(0,0,0,.5)', zIndex: 60, pointerEvents: 'none' }); } }
    if (!drag.moved) return;
    if (drag.group) { drag.group.forEach((m) => follow(m.id, p.x - RK.tw / 2 + m.di * RK.sw, p.y - RK.th / 2, 1.05)); return; }
    let x = p.x - RK.tw / 2, y = p.y - RK.th / 2; drag.target = null; drag.zone = null;
    if (drag.kind === 'rack') { if (fl.canDiscard && inDisc(p)) { drag.zone = 'disc'; x = DZ[0][0] + 7; y = DZ[0][1] + 8; } else if (fl.returnId === drag.id && inSide(p)) { drag.zone = 'side'; x = DZ[3][0] + 3; y = DZ[3][1] + 3; } else if (fl.canMeld && !slotAt(p)) { const tg = nearTarget(drag.id, p); if (tg) { drag.target = tg; const q = tgXY(tg); x = q.x - 11; y = q.y - 14; } } drawMarks(drag.target); }
    zDisc.style.transform = drag.zone === 'disc' ? 'scale(1.08)' : 'none';
    if (drag.ghost) css(drag.ghost, { transform: `translate(${x}px,${y}px) scale(1.06)` }); else follow(drag.id, x, y);
  }
  function up(e) {
    if (!drag) return; const d = drag; drag = null; if (d.holdT) clearTimeout(d.holdT); zDisc.style.transform = 'none'; const p = pt(e);
    if (d.ghost) d.ghost.remove();
    const back = () => { const list = d.group ? d.group.map((m) => m.id) : [d.id]; list.forEach((id) => { const t = tiles.get(id), dd = desc.get(id); if (t && dd) { t.el.style.transition = `transform .3s ${ease}, box-shadow .2s`; t.key = ''; place(t, poseOf(dd), 0); } }); drawMarks(); };
    if (d.group) { o.onGroupHint && o.onGroupHint(false); const sl = slotAt(p); if (sl) o.onGroupMove && o.onGroupMove(d.group.map((m) => m.from), d.from, sl); back(); return; }
    if (!d.moved) {
      if (d.kind === 'deck' || d.kind === 'side') { o.onDraw && o.onDraw(d.kind, null); return; }
      const now = performance.now();
      if (fl.dbl && lastClick.id === d.id && now - lastClick.t < 380 && fl.canDiscard) { sel = null; lastClick = { id: null, t: 0 }; o.onDiscard && o.onDiscard(d.from); return; }
      lastClick = { id: d.id, t: now }; sel = sel === d.id ? null : d.id; const t = tiles.get(d.id), dd = desc.get(d.id); if (t && dd) { t.key = ''; place(t, poseOf(dd), 0); } drawMarks(); sync([...desc.values()], null); return;
    }
    const sl = slotAt(p);
    if (d.kind === 'deck' || d.kind === 'side') { o.onDraw && o.onDraw(d.kind, sl); if (d.kind === 'side') back(); return; }
    if (d.zone === 'side' || (fl.returnId === d.id && inSide(p))) { o.onReturn && o.onReturn(); back(); return; }
    if (d.zone === 'disc' || (fl.canDiscard && inDisc(p))) { o.onDiscard && o.onDiscard(d.from); return; }
    if (d.target) { o.onMeldDrop && o.onMeldDrop(d.from, d.target.id, d.target.kind); back(); return; }
    if (!sl && fl.canMeld) { const bh = boardHit(p); if (bh) { o.onMeldDrop && o.onMeldDrop(d.from, bh); back(); return; } }
    if (sl && (sl.r !== d.from.r || sl.i !== d.from.i)) { const plan = singlePlan(d.from, sl, d.id); if (o.onGroupMove) o.onGroupMove(null, d.from, sl, plan); else o.onMove && o.onMove(d.from, sl); return; }
    back();
  }
  deck.dataset.deck = '1';
  root.addEventListener('pointerdown', down); root.addEventListener('pointermove', move); root.addEventListener('pointerup', up); root.addEventListener('pointercancel', () => { drag = null; });
  // ---------- efektler ----------
  const A = (el, kf, dur, delay, ez, x) => el.animate(kf, { duration: dur, delay: delay || 0, easing: ez || ease, fill: 'both', ...(x || {}) });
  function ring(x, y, col, dur, max, dl) { const r = div(fxL, { position: 'absolute', left: x - 40 + 'px', top: y - 40 + 'px', width: '80px', height: '80px', borderRadius: '50%', border: `4px solid ${col}`, boxSizing: 'border-box', boxShadow: `0 0 24px ${col}` }); A(r, [{ transform: 'scale(.1)', opacity: 1 }, { transform: `scale(${max})`, opacity: 0 }], dur, dl, 'cubic-bezier(.16,1,.3,1)').onfinish = () => r.remove(); }
  function sparks(x, y, cols, n, spd, dl) { for (let i = 0; i < n; i++) { const a = Math.random() * 6.283, r = spd * (0.4 + Math.random()), s = div(fxL, { position: 'absolute', left: x - 3 + 'px', top: y - 3 + 'px', width: '6px', height: '6px', borderRadius: '50%', background: cols[i % cols.length], boxShadow: `0 0 8px ${cols[i % cols.length]}` }); A(s, [{ transform: 'translate(0,0) scale(1)', opacity: 1 }, { transform: `translate(${Math.cos(a) * r}px,${Math.sin(a) * r + 30}px) scale(.3)`, opacity: 0 }], 700 + Math.random() * 500, dl, 'cubic-bezier(.2,.8,.4,1)').onfinish = () => s.remove(); } }
  function glow(x, y, col, size, dur, dl) { const g = div(fxL, { position: 'absolute', left: x - size / 2 + 'px', top: y - size / 2 + 'px', width: size + 'px', height: size + 'px', borderRadius: '50%', background: `radial-gradient(circle, ${col}, transparent 68%)`, mixBlendMode: 'screen' }); A(g, [{ transform: 'scale(.2)', opacity: 0 }, { transform: 'scale(1)', opacity: 1, offset: 0.25 }, { transform: 'scale(1.3)', opacity: 0 }], dur, dl).onfinish = () => g.remove(); }
  function shake(d) { A(root, [{ transform: 'none' }, { transform: 'translate(-6px,3px)' }, { transform: 'translate(5px,-4px)' }, { transform: 'translate(-4px,-2px)' }, { transform: 'translate(3px,4px)' }, { transform: 'none' }], Math.round((d || 0.5) * 1000), 0, 'linear', { fill: 'none' }); }
  const FX = {
    nuke: (x, y) => { glow(x, y, '#FFF4D6', 520, 1300); ring(x, y, '#FFFFFF', 800, 7); ring(x, y, '#FFD964', 1000, 6, 110); ring(x, y, '#FF6A2E', 1300, 5, 240); sparks(x, y, ['#FFE08A', '#FF7A3D', '#fff'], 60, 260); shake(1); },
    blackhole: (x, y) => { const core = div(fxL, { position: 'absolute', left: x - 80 + 'px', top: y - 80 + 'px', width: '160px', height: '160px', borderRadius: '50%', background: 'radial-gradient(circle,#030108 0 38%,#E6DDFF 44%,#B79CFF 52%,rgba(58,42,144,.6) 62%,transparent 72%)', boxShadow: '0 0 60px rgba(183,156,255,.7)' }); A(core, [{ transform: 'scale(0) rotate(0deg)' }, { transform: 'scale(1) rotate(200deg)', offset: 0.35 }, { transform: 'scale(1.05) rotate(520deg)', offset: 0.75 }, { transform: 'scale(0) rotate(720deg)' }], 1500).onfinish = () => core.remove(); sparks(x, y, ['#B79CFF', '#7FE3FF'], 40, -180, 0); setTimeout(() => { glow(x, y, '#EDE6FF', 460, 1100); ring(x, y, '#FFFFFF', 700, 7); ring(x, y, '#B79CFF', 1000, 6, 120); ring(x, y, '#7FE3FF', 1200, 5, 240); sparks(x, y, ['#B79CFF', '#7FE3FF', '#fff'], 60, 260); shake(0.9); }, 1150); },
    supernova: (x, y) => { glow(x, y, '#FFFFFF', 620, 1500); for (let i = 0; i < 16; i++) { const r = div(fxL, { position: 'absolute', left: x + 'px', top: y - 2 + 'px', width: '300px', height: '4px', borderRadius: '2px', background: `linear-gradient(90deg,${i % 2 ? '#9FD8FF' : '#fff'},transparent)`, transformOrigin: '0 50%' }); A(r, [{ transform: `rotate(${i * 22.5}deg) scaleX(0)`, opacity: 1 }, { transform: `rotate(${i * 22.5}deg) scaleX(1)`, opacity: 0 }], 1100, i * 12).onfinish = () => r.remove(); } ring(x, y, '#FFFFFF', 900, 8); ring(x, y, '#9FD8FF', 1200, 6.5, 120); ring(x, y, '#FFD27A', 1500, 5, 260); sparks(x, y, ['#fff', '#9FD8FF', '#FFD27A'], 70, 300); shake(1); },
    meteor: (x, y) => { glow(x, y, '#FFF1C0', 480, 1200); ring(x, y, '#FFD27A', 900, 6); ring(x, y, '#FF7A3D', 1200, 5, 140); sparks(x, y, ['#FFB347', '#FF6A2E', '#FFE08A'], 70, 240); for (let i = 0; i < 8; i++) { const ex = x + (Math.random() - 0.5) * 520, ey = y + (Math.random() - 0.5) * 300, m = div(fxL, { position: 'absolute', left: ex - 6 + 'px', top: ey - 6 + 'px', width: '12px', height: '12px', borderRadius: '50%', background: '#FFE08A', boxShadow: '0 0 14px #FF7A3D, -30px -30px 22px -6px rgba(255,122,61,.7)' }); A(m, [{ transform: 'translate(160px,-260px)', opacity: 1 }, { transform: 'translate(0,0)', opacity: 1 }], 420, 150 + i * 110, 'cubic-bezier(.5,0,1,.6)').onfinish = () => { m.remove(); ring(ex, ey, '#FF7A3D', 600, 2.5); sparks(ex, ey, ['#FFB347', '#FF6A2E'], 12, 70); }; } shake(1.1); },
  };
  function finishFx(t, style) {
    const tx = CL.x - 36, ty = CL.y - 50; t.el.style.transition = 'none'; t.key = '';
    const from = `translate(${t.x}px,${t.y}px)`;
    const kf = style === 'meteor' ? [{ transform: from }, { transform: `translate(${t.x}px,${t.y - 700}px) scale(1.2)`, offset: 0.32 }, { transform: `translate(${tx + 420}px,${ty - 600}px) scale(1.4) rotate(-30deg)`, offset: 0.5 }, { transform: `translate(${tx}px,${ty}px) scale(1) rotate(0deg)` }]
      : [{ transform: from }, { transform: `translate(${tx}px,${ty - 150}px) scale(1.6) rotate(-8deg)`, offset: 0.45 }, { transform: `translate(${tx}px,${ty - 165}px) scale(1.75) rotate(6deg)`, offset: 0.62 }, { transform: `translate(${tx}px,${ty}px) scale(1) rotate(0deg)` }];
    const an = t.el.animate(kf, { duration: 1750, easing: 'cubic-bezier(.5,0,.6,1)', fill: 'forwards' });
    an.onfinish = () => { t.el.style.transform = `translate(${tx}px,${ty}px)`; an.cancel(); t.x = tx; t.y = ty; (FX[style] || FX.nuke)(CL.x, CL.y); };
  }
  function anchorXY(k) { if (k === 0 || k === 'me') return { x: 55, y: 650 }; const a = ANC[typeof k === 'number' ? 'a' + k : k]; return a ? { x: a[0], y: a[1] } : { x: 640, y: 360 }; }
  function bindAnchor(k, el) { if (!el) { delete anchors[k]; return; } anchors[k] = el; const a = ANC[k]; if (a) { el.style.transform = `translate3d(${a[0]}px,${a[1]}px,0)`; el.style.visibility = 'visible'; } }
  function rollDie(seat, v) {
    const P = { 1: [[1, 1]], 2: [[0, 0], [2, 2]], 3: [[0, 0], [1, 1], [2, 2]], 4: [[0, 0], [2, 0], [0, 2], [2, 2]], 5: [[0, 0], [2, 0], [1, 1], [0, 2], [2, 2]], 6: [[0, 0], [2, 0], [0, 1], [2, 1], [0, 2], [2, 2]] };
    clearDice(); const s = seat === 0 ? { x: 640, y: 620 } : anchorXY(seat), e = { x: 600 + Math.random() * 80, y: 300 + Math.random() * 60 };
    const d = div(fxL, { position: 'absolute', left: '-20px', top: '-20px', width: '40px', height: '40px', borderRadius: '10px', background: 'linear-gradient(145deg,#FFFDF7,#E7DDC6)', boxShadow: '0 6px 14px rgba(0,0,0,.45), inset 0 -3px 0 rgba(0,0,0,.12)' });
    P[v].forEach(([i, j]) => div(d, { position: 'absolute', left: 6 + i * 10 + 'px', top: 6 + j * 10 + 'px', width: '8px', height: '8px', borderRadius: '50%', background: v === 1 ? '#C81E33' : '#141733' }));
    const mx = (s.x + e.x) / 2, my = Math.min(s.y, e.y) - 120;
    A(d, [{ transform: `translate(${s.x}px,${s.y}px) rotate(0deg) scale(.6)` }, { transform: `translate(${mx}px,${my}px) rotate(380deg) scale(1.15)`, offset: 0.35 }, { transform: `translate(${e.x - 30}px,${e.y}px) rotate(620deg) scale(1)`, offset: 0.55 }, { transform: `translate(${e.x - 12}px,${e.y - 22}px) rotate(700deg)`, offset: 0.68 }, { transform: `translate(${e.x - 4}px,${e.y}px) rotate(750deg)`, offset: 0.8 }, { transform: `translate(${e.x}px,${e.y - 6}px) rotate(712deg)`, offset: 0.9 }, { transform: `translate(${e.x}px,${e.y}px) rotate(720deg)` }], 1500, 0, 'cubic-bezier(.3,.6,.4,1)');
    d.dataset.die = '1'; setTimeout(() => alive && ring(e.x, e.y, 'var(--a1,#FFD964)', 700, 1.4), 1450); return 1500;
  }
  function clearDice() { fxL.querySelectorAll('[data-die]').forEach((d) => A(d, [{ opacity: 1 }, { opacity: 0 }], 300).onfinish = () => d.remove()); }
  function celebrate(k) { const a = anchorXY(k); for (let i = 0; i < 6; i++) setTimeout(() => alive && sparks(a.x + (Math.random() - 0.5) * 80, a.y - 20, ['#FFD964', '#FF6A9A', '#7FE3FF', '#8EF0B0', '#B79CFF'], 26, 180), i * 220); ring(a.x, a.y, 'var(--a1,#FFD964)', 900, 3); }
  function dealFx() { ring(640, 330, 'var(--a1,#FFD964)', 900, 5); ring(640, 330, '#fff', 1100, 4, 160); }
  function sendGift(kind, from, to) {
    const a = anchorXY(from), b = anchorXY(to), url = thumbs[kind]; const g = div(fxL, { position: 'absolute', left: '-32px', top: '-32px', width: '64px', height: '64px', background: url ? `center/contain no-repeat url(${url})` : 'none', filter: 'drop-shadow(0 8px 12px rgba(0,0,0,.45))', display: 'grid', placeItems: 'center', fontSize: '40px' }); if (!url) g.textContent = '🎁';
    const mx = (a.x + b.x) / 2, my = Math.min(a.y, b.y) - 140, rk = kind === 'rocket';
    A(g, [{ transform: `translate(${a.x}px,${a.y}px) scale(.3) rotate(0deg)`, opacity: 0 }, { transform: `translate(${mx}px,${my}px) scale(${rk ? 1.5 : 1.2}) rotate(${rk ? -40 : -12}deg)`, opacity: 1, offset: 0.45 }, { transform: `translate(${b.x}px,${b.y - 60}px) scale(1) rotate(0deg)`, opacity: 1, offset: 0.7 }, { transform: `translate(${b.x}px,${b.y - 60}px) scale(1)`, opacity: 1, offset: 0.93 }, { transform: `translate(${b.x}px,${b.y - 80}px) scale(.8)`, opacity: 0 }], rk ? 5200 : 3800, 0, 'cubic-bezier(.33,.8,.4,1)').onfinish = () => g.remove();
    setTimeout(() => alive && sparks(b.x, b.y - 60, ['#FFE08A', '#fff'], 14, 80), rk ? 3600 : 2600); return 1;
  }
  const noop = () => {};
  const api = {
    is2D: () => true, sync, anchorXY, bindAnchor, rollDie, clearDice, celebrate, dealFx, sendGift, shake, setThumbs: (t) => { thumbs = t || {}; },
    clearGifts: noop, markStack: noop, setCamera: noop, setView2D: noop, setTheme: noop, setSkins: noop, setTileSkin: noop, setTileDepth: noop, setFont: noop, setScale: noop, renderNow: noop, suspend: noop,
    setOptions: (op) => { fl = { ...fl, ...op }; }, debugSelect: (id) => { sel = id; drawMarks(); }, debugDrag: noop, giftCount: () => 0, thumbs: async () => ({}),
    show: (v) => { root.style.display = v ? 'block' : 'none'; },
    destroy() { alive = false; root.remove(); },
  };
  return api;
}
