/* Galaxy 101 — dereceli sistem: lig verisi, amblem/madalyon SVG üretici, kurallar.
   Tema: gökyüzü yolculuğu — Bronz Asteroit, Gümüş Hilal, Altın Güneş, Elmas Yıldız kristali, Usta Süpernova, Galaksi Sarmal. */
(function () {
  const LEAGUES = [
    { key: 'bronze', tr: 'Bronz', en: 'Bronze', motif: ['Asteroit', 'Asteroid'], c: ['#FFE1C2', '#F0A766', '#A85A28', '#43200C'], face: ['#8A4520', '#2A1006'], txt: '#F5BE8E', bar: 'linear-gradient(90deg,#A85A28,#F0A766 60%,#FFE1C2)', glow: 'rgba(240,167,102,.55)', dist: '~%25' },
    { key: 'silver', tr: 'Gümüş', en: 'Silver', motif: ['Hilal', 'Crescent'], c: ['#FFFFFF', '#D3DDEA', '#8292AA', '#28303F'], face: ['#41598A', '#101a34'], txt: '#DCE5F1', bar: 'linear-gradient(90deg,#8292AA,#D3DDEA 60%,#FFFFFF)', glow: 'rgba(211,221,234,.5)', dist: '~%30' },
    { key: 'gold', tr: 'Altın', en: 'Gold', motif: ['Güneş', 'Sun'], c: ['#FFF6CF', '#FFCB3D', '#C47E0A', '#412602'], face: ['#B5520E', '#3A1402'], txt: '#FFD45C', bar: 'linear-gradient(90deg,#C47E0A,#FFCB3D 60%,#FFF6CF)', glow: 'rgba(255,203,61,.6)', dist: '~%25' },
    { key: 'diamond', tr: 'Elmas', en: 'Diamond', motif: ['Yıldız kristali', 'Star crystal'], c: ['#EAFDFF', '#74E6FF', '#2C86D8', '#0A2A58'], face: ['#1F64C2', '#061A40'], txt: '#7FE6FF', bar: 'linear-gradient(90deg,#2C86D8,#74E6FF 60%,#EAFDFF)', glow: 'rgba(116,230,255,.6)', dist: '~%13' },
    { key: 'master', tr: 'Usta', en: 'Master', motif: ['Süpernova', 'Supernova'], c: ['#FFE6F5', '#FF72C6', '#B3228A', '#3E0732'], face: ['#9A1C80', '#2A0628'], txt: '#FF8FD2', bar: 'linear-gradient(90deg,#B3228A,#FF72C6 60%,#FFE6F5)', glow: 'rgba(255,114,198,.6)', dist: '~%6' },
    { key: 'galaxy', tr: 'Galaksi', en: 'Galaxy', motif: ['Sarmal galaksi', 'Spiral galaxy'], c: ['#E2FFFB', '#3DF5E0', '#8C6EFF', '#1A1150'], face: ['#4A2FC0', '#0A0628'], txt: '#B9A6FF', bar: 'linear-gradient(90deg,#8C6EFF,#5FB4F0 55%,#3DF5E0)', glow: 'rgba(140,110,255,.65)', dist: 'İlk 100' }
  ];
  const BY = {}; LEAGUES.forEach((l, i) => { l.i = i; BY[l.key] = l; });
  const ROMAN = ['', 'I', 'II', 'III'];
  let uid = 0;
  const f1 = (n) => +n.toFixed(2);
  const star = (cx, cy, R, r, n, rot) => { let s = ''; for (let i = 0; i < n * 2; i++) { const a = (rot || -90) * Math.PI / 180 + i * Math.PI / n, q = i % 2 ? r : R; s += `${f1(cx + q * Math.cos(a))},${f1(cy + q * Math.sin(a))} `; } return s.trim(); };
  const pol = (cx, cy, r, deg) => [cx + r * Math.cos(deg * Math.PI / 180), cy + r * Math.sin(deg * Math.PI / 180)];
  const SH = 'M60,12 C78,12 92,17 99,23 C100,40 100,52 99,64 C96,86 80,100 60,110 C40,100 24,86 21,64 C20,52 20,40 21,23 C28,17 42,12 60,12 Z';
  const sc = (s, cx, cy) => `translate(${cx} ${cy}) scale(${s}) translate(${-cx} ${-cy})`;

  function defs(L, id) {
    const c = L.c, gal = L.key === 'galaxy';
    return `<defs>
<linearGradient id="${id}m" x1="0" y1="0" x2="0.35" y2="1"><stop offset="0" stop-color="${c[0]}"/><stop offset=".42" stop-color="${gal ? '#3DF5E0' : c[1]}"/><stop offset="1" stop-color="${c[2]}"/></linearGradient>
<linearGradient id="${id}e" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${c[1]}"/><stop offset="1" stop-color="${c[3]}"/></linearGradient>
<radialGradient id="${id}f" gradientUnits="userSpaceOnUse" cx="60" cy="44" r="58"><stop offset="0" stop-color="${L.face[0]}"/><stop offset="1" stop-color="${L.face[1]}"/></radialGradient>
<radialGradient id="${id}i" cx=".36" cy=".3" r=".8"><stop offset="0" stop-color="#fff"/><stop offset=".25" stop-color="${c[0]}"/><stop offset=".7" stop-color="${c[1]}"/><stop offset="1" stop-color="${c[2]}"/></radialGradient>
<radialGradient id="${id}g"><stop offset="0" stop-color="${c[1]}" stop-opacity=".9"/><stop offset=".5" stop-color="${c[1]}" stop-opacity=".25"/><stop offset="1" stop-color="${c[1]}" stop-opacity="0"/></radialGradient>
<radialGradient id="${id}w"><stop offset="0" stop-color="#fff"/><stop offset=".35" stop-color="${c[0]}" stop-opacity=".9"/><stop offset="1" stop-color="${c[1]}" stop-opacity="0"/></radialGradient>
<linearGradient id="${id}s" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".55"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>
<clipPath id="${id}c"><path d="${SH}" transform="${sc(.8, 60, 61)}"/></clipPath>
<filter id="${id}b" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="3"/></filter>
</defs>`;
  }

  /* merkez simge — (60,60) etrafında ~ r22 */
  function icon(k, id, small, cx, cy) {
    cx = cx || 60; cy = cy || 60;
    const I = `url(#${id}i)`, W = `url(#${id}w)`, F = `url(#${id}f)`;
    let s = small ? '' : `<circle cx="${cx}" cy="${cy}" r="26" fill="url(#${id}g)"/>`;
    if (k === 0) { // asteroit
      s += `<path d="M${cx - 16},${cy - 3} C${cx - 15},${cy - 14} ${cx - 3},${cy - 18} ${cx + 7},${cy - 15} C${cx + 17},${cy - 12} ${cx + 18},${cy} ${cx + 15},${cy + 9} C${cx + 11},${cy + 17} ${cx - 2},${cy + 18} ${cx - 10},${cy + 13} C${cx - 16},${cy + 9} ${cx - 17},${cy + 3} ${cx - 16},${cy - 3} Z" fill="${I}"/>`;
      if (!small) s += `<circle cx="${cx + 5}" cy="${cy - 4}" r="4.6" fill="#000" opacity=".22"/><circle cx="${cx + 5.6}" cy="${cy - 3.4}" r="3.6" fill="#fff" opacity=".12"/><circle cx="${cx - 6}" cy="${cy + 6}" r="3.4" fill="#000" opacity=".22"/><circle cx="${cx + 7}" cy="${cy + 9}" r="2.2" fill="#000" opacity=".2"/><circle cx="${cx - 8}" cy="${cy - 7}" r="2" fill="#000" opacity=".18"/>`;
      if (!small) s += `<circle cx="${cx + 20}" cy="${cy - 14}" r="2.4" fill="${I}"/><circle cx="${cx - 19}" cy="${cy + 15}" r="1.8" fill="${I}"/>`;
    }
    if (k === 1) { // hilal
      s += `<circle cx="${cx}" cy="${cy}" r="18" fill="${I}"/><circle cx="${cx + 9}" cy="${cy - 6}" r="15.5" fill="${F}"/>`;
      s += `<polygon points="${star(cx + 9, cy + 4, 7, 2.2, 4)}" fill="#fff"/>`;
      if (!small) s += `<polygon points="${star(cx + 15, cy - 12, 3.5, 1.1, 4)}" fill="#fff" opacity=".8"/>`;
    }
    if (k === 2) { // güneş
      for (let i = 0; i < 12; i++) { const a = i * 30, [x1, y1] = pol(cx, cy, 14, a - 9), [x2, y2] = pol(cx, cy, i % 2 ? 21 : 25, a), [x3, y3] = pol(cx, cy, 14, a + 9); s += `<path d="M${f1(x1)},${f1(y1)} Q${f1(x2)},${f1(y2)} ${f1(x2)},${f1(y2)} Q${f1(x2)},${f1(y2)} ${f1(x3)},${f1(y3)} Z" fill="${I}" stroke="${I}" stroke-width="2.4" stroke-linejoin="round"/>`; }
      s += `<circle cx="${cx}" cy="${cy}" r="13.5" fill="${I}"/>`;
      if (!small) s += `<circle cx="${cx}" cy="${cy}" r="9" fill="#fff" opacity=".35"/>`;
    }
    if (k === 3) { // yıldız kristali
      const t = cy - 18, m = cy - 6, b = cy + 20;
      s += `<polygon points="${cx - 9},${t} ${cx + 9},${t} ${cx + 18},${m} ${cx},${b} ${cx - 18},${m}" fill="${I}"/>`;
      if (!small) s += `<polygon points="${cx - 9},${t} ${cx + 9},${t} ${cx + 5},${m} ${cx - 5},${m}" fill="#fff" opacity=".55"/><polygon points="${cx - 18},${m} ${cx - 5},${m} ${cx},${b}" fill="#fff" opacity=".28"/><polygon points="${cx + 18},${m} ${cx + 5},${m} ${cx},${b}" fill="#000" opacity=".14"/><polygon points="${cx - 9},${t} ${cx - 18},${m} ${cx - 5},${m}" fill="#fff" opacity=".4"/><polygon points="${star(cx + 13, cy - 16, 8, 1.8, 4)}" fill="#fff"/>`;
    }
    if (k === 4) { // süpernova
      s += `<polygon points="${star(cx, cy, 25, 6, 4)}" fill="${W}"/><polygon points="${star(cx, cy, 17, 5, 4, -45)}" fill="${I}" opacity=".9"/><polygon points="${star(cx, cy, 22, 7, 4)}" fill="${I}"/><circle cx="${cx}" cy="${cy}" r="6.5" fill="#fff"/>`;
      if (!small) s += `<circle cx="${cx}" cy="${cy}" r="11" fill="#fff" opacity=".25"/>`;
    }
    if (k === 5) { // sarmal galaksi
      s += `<ellipse cx="${cx}" cy="${cy}" rx="24" ry="22" fill="url(#${id}g)"/>`;
      const arm = (r) => `<path d="M${cx},${cy} C${cx + 4},${cy - 9} ${cx + 16},${cy - 8} ${cx + 18},${cy + 2} C${cx + 20},${cy + 14} ${cx + 6},${cy + 21} ${cx - 6},${cy + 19}" transform="rotate(${r} ${cx} ${cy})" fill="none" stroke="${I}" stroke-width="${small ? 5 : 4.5}" stroke-linecap="round"/>`;
      s += arm(0) + arm(180);
      if (!small) s += `<path d="M${cx},${cy} C${cx + 3},${cy - 6} ${cx + 11},${cy - 6} ${cx + 13},${cy + 1}" transform="rotate(90 ${cx} ${cy})" fill="none" stroke="#fff" stroke-opacity=".5" stroke-width="2" stroke-linecap="round"/><path d="M${cx},${cy} C${cx + 3},${cy - 6} ${cx + 11},${cy - 6} ${cx + 13},${cy + 1}" transform="rotate(270 ${cx} ${cy})" fill="none" stroke="#fff" stroke-opacity=".5" stroke-width="2" stroke-linecap="round"/>`;
      s += `<circle cx="${cx}" cy="${cy}" r="7" fill="${W}"/><circle cx="${cx}" cy="${cy}" r="3.6" fill="#fff"/>`;
      if (!small) s += `<circle cx="${cx - 16}" cy="${cy - 12}" r="1.4" fill="#fff"/><circle cx="${cx + 17}" cy="${cy + 13}" r="1.2" fill="#fff"/><circle cx="${cx + 13}" cy="${cy - 16}" r="1" fill="#DFFFFB"/>`;
    }
    return s;
  }

  function back(k, L, id, small) {
    const c = L.c, M = `url(#${id}m)`, E = c[3];
    let s = '';
    if (k >= 2 && !small) s += `<circle cx="60" cy="58" r="58" fill="url(#${id}g)" opacity=".75"/>`;
    if (k === 2 || k === 5) { // ışınlar
      const n = k === 5 ? 16 : 12;
      for (let i = 0; i < n; i++) { const a = -90 + i * 360 / n, R = i % 2 ? 50 : 58, [x1, y1] = pol(60, 60, 34, a - 7), [x2, y2] = pol(60, 60, R, a), [x3, y3] = pol(60, 60, 34, a + 7); s += `<path d="M${f1(x1)},${f1(y1)} L${f1(x2)},${f1(y2)} L${f1(x3)},${f1(y3)} Z" fill="${k === 5 ? (i % 2 ? '#8C6EFF' : '#3DF5E0') : c[1]}" opacity="${i % 2 ? .55 : .85}" stroke="${k === 5 ? '#1A1150' : c[2]}" stroke-width="1.2" stroke-linejoin="round"/>`; }
    }
    if (k === 3) { // kristal kıymıklar
      [[-90, 60, 9], [-58, 52, 7], [-122, 52, 7], [-30, 44, 6], [-150, 44, 6]].forEach(([a, R, w]) => {
        const [bx, by] = pol(60, 60, 30, a), [tx, ty] = pol(60, 60, R, a), [lx, ly] = pol(bx + (tx - bx) * .55, by + (ty - by) * .55, w, a - 90), [rx, ry] = pol(bx + (tx - bx) * .55, by + (ty - by) * .55, w, a + 90);
        s += `<polygon points="${f1(bx)},${f1(by)} ${f1(lx)},${f1(ly)} ${f1(tx)},${f1(ty)} ${f1(rx)},${f1(ry)}" fill="url(#${id}i)" stroke="${E}" stroke-width="1.6" stroke-linejoin="round"/><polyline points="${f1(bx)},${f1(by)} ${f1(tx)},${f1(ty)}" stroke="#fff" stroke-opacity=".55" stroke-width="1"/>`;
      });
    }
    if (k === 4) { // alevler
      for (let i = 0; i < 9; i++) { const a = -170 + i * 20, [bx, by] = pol(60, 62, 32, a), [tx, ty] = pol(60, 62, i % 2 ? 54 : 60, a), [l1, l2] = pol(bx, by, 9, a - 90), [r1, r2] = pol(bx, by, 9, a + 90);
        s += `<path d="M${f1(l1)},${f1(l2)} Q${f1((l1 + tx) / 2)},${f1((l2 + ty) / 2 - 2)} ${f1(tx)},${f1(ty)} Q${f1((r1 + tx) / 2)},${f1((r2 + ty) / 2 - 2)} ${f1(r1)},${f1(r2)} Z" fill="${i % 2 ? '#FFB36B' : c[1]}" stroke="${E}" stroke-width="1.4" stroke-linejoin="round" opacity="${i % 2 ? .85 : 1}"/>`; }
    }
    if (k === 5) s += `<ellipse cx="60" cy="62" rx="62" ry="17" transform="rotate(-16 60 62)" fill="none" stroke="#3DF5E0" stroke-opacity=".55" stroke-width="3"/>`;
    // kanatlar
    if (k >= 1) {
      const n = k >= 3 ? 4 : 3, big = k >= 3 ? 1.18 : 1;
      let w = '';
      for (let i = n - 1; i >= 0; i--) { const cx = 16 - i * 3.2 * big, cy = 40 + i * 12, rx = (17 - i * 1.6) * big, rot = -28 + i * 24;
        w += `<ellipse cx="${f1(cx)}" cy="${cy}" rx="${f1(rx)}" ry="${f1(5.8 * big)}" transform="rotate(${rot} ${f1(cx)} ${cy})" fill="${M}" stroke="${E}" stroke-width="1.8"/>`;
        if (!small) w += `<ellipse cx="${f1(cx - 2)}" cy="${cy - 1.6}" rx="${f1(rx * .6)}" ry="1.5" transform="rotate(${rot} ${f1(cx)} ${cy})" fill="#fff" opacity=".45"/>`; }
      s += `<g>${w}</g><g transform="translate(120 0) scale(-1 1)">${w}</g>`;
    }
    return s;
  }

  function crown(k, L, id) {
    const M = `url(#${id}m)`, E = L.c[3];
    if (k === 4) return `<path d="M42,20 L40,4 L50,12 L60,-2 L70,12 L80,4 L78,20 Q60,25 42,20 Z" fill="${M}" stroke="${E}" stroke-width="1.8" stroke-linejoin="round"/><circle cx="40" cy="3" r="3.2" fill="#fff" stroke="${E}" stroke-width="1.4"/><circle cx="80" cy="3" r="3.2" fill="#fff" stroke="${E}" stroke-width="1.4"/><circle cx="60" cy="-3" r="4" fill="url(#${id}i)" stroke="${E}" stroke-width="1.4"/>`;
    if (k === 5) return `<polygon points="${star(60, 6, 16, 4.5, 4)}" fill="url(#${id}w)"/><polygon points="${star(60, 6, 12, 3.4, 4)}" fill="#fff"/>`;
    if (k === 2) return `<path d="M47,17 L46,7 L53,12 L60,3 L67,12 L74,7 L73,17 Q60,21 47,17 Z" fill="${M}" stroke="${E}" stroke-width="1.6" stroke-linejoin="round"/>`;
    return '';
  }

  /* tier: 3|2|1 (III, II, I) — 0 = işaretsiz. rank: Galaksi sıra numarası. */
  function emblem(key, o) {
    o = o || {};
    const L = BY[key] || LEAGUES[0], k = L.i, c = L.c, size = o.size || 52, small = size < 40, id = 'g1r' + (++uid);
    const M = `url(#${id}m)`, F = `url(#${id}f)`, E = c[3];
    let s = `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="-8 -8 136 136" style="display:block;overflow:visible">${defs(L, id)}`;
    s += back(k, L, id, small);
    // kalkan
    s += `<path d="${SH}" fill="${E}" transform="translate(0 3)" opacity=".55"/>`;
    s += `<path d="${SH}" fill="${M}" stroke="${E}" stroke-width="2.2"/>`;
    if (!small) s += `<path d="${SH}" fill="none" stroke="url(#${id}s)" stroke-width="1.6" transform="${sc(.95, 60, 60)}"/>`;
    s += `<path d="${SH}" transform="${sc(.8, 60, 61)}" fill="${F}" stroke="${E}" stroke-width="2"/>`;
    if (!small) s += `<g clip-path="url(#${id}c)"><path d="${SH}" transform="${sc(.8, 60, 61)}" fill="none" stroke="#000" stroke-opacity=".45" stroke-width="7" filter="url(#${id}b)"/><ellipse cx="60" cy="30" rx="30" ry="10" fill="#fff" opacity=".1"/></g>`;
    if (k === 0 && !small) [[30, 30], [90, 30], [60, 102]].forEach(([x, y]) => { s += `<circle cx="${x}" cy="${y}" r="2.6" fill="${c[0]}" stroke="${E}" stroke-width="1.2"/>`; });
    s += `<g clip-path="${small ? '' : `url(#${id}c)`}">${icon(k, id, small, 60, 60)}</g>`;
    if (!small && k >= 3) s += `<circle cx="60" cy="60" r="34" fill="none" stroke="${c[0]}" stroke-opacity=".18" stroke-width="1" clip-path="url(#${id}c)"/>`;
    s += crown(k, L, id);
    if (k === 5) s += `<path d="M-1.6,72.6 A62,17 -16 0 0 121.6,51.4" transform="rotate(0)" fill="none" stroke="#3DF5E0" stroke-width="3" stroke-linecap="round"/>${small ? '' : `<polygon points="${star(4, 34, 6, 1.6, 4)}" fill="#DFFFFB"/><polygon points="${star(116, 92, 5, 1.4, 4)}" fill="#DFFFFB"/><polygon points="${star(108, 16, 4, 1.2, 4)}" fill="#fff"/>`}`;
    // kademe kurdelesi
    if (k < 5 && o.tier) {
      const n = 4 - o.tier, w = 18 + n * 13;
      s += `<rect x="${60 - w / 2}" y="101" width="${w}" height="${small ? 20 : 18}" rx="9" fill="${E}" stroke="${M}" stroke-width="2.4"/>`;
      for (let i = 0; i < n; i++) { const x = 60 - (n - 1) * 6.5 + i * 13; s += `<circle cx="${x}" cy="${small ? 111 : 110}" r="${small ? 5.2 : 4.6}" fill="url(#${id}i)"/>${small ? '' : `<circle cx="${x - 1.4}" cy="108.6" r="1.4" fill="#fff"/>`}`; }
    }
    if (k === 5 && o.rank && !small) {
      const t = '#' + o.rank, w = 22 + t.length * 10;
      s += `<rect x="${60 - w / 2}" y="99" width="${w}" height="22" rx="11" fill="#1A1150" stroke="#3DF5E0" stroke-width="2.4"/><text x="60" y="114.5" text-anchor="middle" font-family="Montserrat,system-ui,sans-serif" font-weight="800" font-size="15" fill="#fff">${t}</text>`;
    }
    return s + `</svg>`;
  }

  /* çerçeve kenar süsü: lig simgeli madalyon */
  function medal(key, size) {
    const L = BY[key] || LEAGUES[0], id = 'g1m' + (++uid), small = size < 22;
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 120 120" style="display:block;overflow:visible">${defs(L, id)}<circle cx="60" cy="63" r="54" fill="${L.c[3]}" opacity=".6"/><circle cx="60" cy="60" r="54" fill="url(#${id}m)" stroke="${L.c[3]}" stroke-width="5"/><circle cx="60" cy="60" r="42" fill="url(#${id}f)" stroke="${L.c[3]}" stroke-width="4"/><g transform="${sc(1.45, 60, 60)}">${icon(L.i, id, true, 60, 60)}</g>${small ? '' : `<ellipse cx="60" cy="30" rx="26" ry="8" fill="#fff" opacity=".18"/>`}</svg>`;
  }

  /* 0..14 = Bronz III … Usta I, 15 = Galaksi */
  const STEPS = 15;
  const at = (idx) => idx >= STEPS ? { league: LEAGUES[5], tier: 0 } : { league: LEAGUES[Math.floor(idx / 3)], tier: 3 - (idx % 3) };
  const label = (idx, lang) => { const a = at(idx); return a.league[lang || 'tr'] + (a.tier ? ' ' + ROMAN[a.tier] : ''); };
  const deltas = (idx) => (idx <= 8 ? [30, 10, -10, -20] : [25, 5, -12, -22]);
  const seasonReset = (idx) => (idx <= 3 ? idx : Math.max(3, Math.min(idx, 14) - 4));
  const REWARD_CHIPS = [5000, 15000, 40000, 100000, 250000, 1000000];
  function frame(key) {
    const L = BY[key] || LEAGUES[0], c = L.c;
    if (key === 'galaxy') return { bg: 'conic-gradient(from 200deg,#1A1150,#8C6EFF 14%,#3DF5E0 30%,#E2FFFB 38%,#3DF5E0 46%,#8C6EFF 62%,#3DF5E0 78%,#4B2FC0 90%,#1A1150)', sh: '0 0 0 2px #1A1150,0 0 22px rgba(140,110,255,.7),0 0 40px rgba(61,245,224,.25)', gap: '#1A1150' };
    return { bg: `conic-gradient(from 210deg,${c[3]},${c[0]} 10%,${c[1]} 22%,${c[0]} 34%,${c[2]} 50%,${c[1]} 64%,${c[0]} 74%,${c[2]} 88%,${c[3]})`, sh: `0 0 0 2px ${c[3]},0 0 16px ${L.glow}`, gap: c[3] };
  }

  window.G101Rank = { LEAGUES, BY, ROMAN, emblem, medal, at, label, deltas, seasonReset, REWARD_CHIPS, frame, STEPS };
  const R = () => window.React;
  window.G101Emblem = function (p) {
    const size = +p.size || 52;
    return R().createElement('span', { style: { display: 'block', width: size, height: size, flex: 'none' }, dangerouslySetInnerHTML: { __html: emblem(p.league || 'bronze', { size, tier: +p.tier || 0, rank: p.rank || '' }) } });
  };
  window.G101Medal = function (p) {
    const size = +p.size || 20;
    return R().createElement('span', { style: { display: 'block', width: size, height: size, flex: 'none' }, dangerouslySetInnerHTML: { __html: medal(p.league || 'bronze', size) } });
  };
  /* Sezon çerçeveli avatar: üstte amblem, iki yanda ve alt çaprazlarda madalyon. */
  window.G101FramedAvatar = function (p) {
    const h = R().createElement, size = +p.size || 110, f = frame(p.league), m = Math.round(size * .2), ms = Math.round(size * .13), top = Math.round(size * .34);
    const at2 = (deg, s) => { const r = size / 2, a = deg * Math.PI / 180; return { position: 'absolute', left: r + r * Math.cos(a) - s / 2, top: r + r * Math.sin(a) - s / 2, width: s, height: s }; };
    return h('div', { style: { position: 'relative', width: size, height: size, flex: 'none' } },
      h('div', { style: { position: 'absolute', inset: 0, borderRadius: '50%', background: f.bg, boxShadow: f.sh } }),
      h('div', { style: { position: 'absolute', inset: Math.round(size * .085), borderRadius: '50%', background: p.color || '#3D7BFF', border: `3px solid ${f.gap}`, display: 'grid', placeItems: 'center', font: `800 ${Math.round(size * .33)}px Montserrat,system-ui`, color: '#fff' } }, p.ini || 'C'),
      h('div', { style: at2(180, m) }, h(window.G101Medal, { league: p.league, size: m })),
      h('div', { style: at2(0, m) }, h(window.G101Medal, { league: p.league, size: m })),
      h('div', { style: at2(135, ms) }, h(window.G101Medal, { league: p.league, size: ms })),
      h('div', { style: at2(45, ms) }, h(window.G101Medal, { league: p.league, size: ms })),
      h('div', { style: { position: 'absolute', left: size / 2 - top / 2, top: -top * .62, width: top, height: top } }, h(window.G101Emblem, { league: p.league, size: top }))
    );
  };
})();
