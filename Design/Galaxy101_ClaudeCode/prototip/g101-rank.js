/* Galaxy 101 — dereceli sistem: lig verisi, madalya SVG üretici, kurallar.
   Üslup: gerçekçi metal + değerli taş, yuvarlak madalya, merkezde okey taşı (Bronz 1 · Gümüş 5 · Altın 9 · Elmas 12 · Usta 13 · Galaksi ★ okey). Ligler malzeme ve taşla ilerler:
   Bronz·Kehribar → Gümüş·Safir → Altın·Zümrüt → Platin·Elmas → Gül altın·Yakut → Titanyum·Galaksi opali. */
(function () {
  const LEAGUES = [
    { key: 'bronze', tr: 'Bronz', en: 'Bronze', motif: ['Bronz · Kehribar', 'Bronze · Amber'],
      metal: ['#FFE0BC', '#E8A46A', '#A65B2B', '#4E230C'], plate: ['#4A2612', '#140803'], gem: ['#4A1600', '#B9520A', '#FFAE3A', '#FFF1C8'],
      c: ['#FFE0BC', '#E8A46A', '#A65B2B', '#3E1C0A'], txt: '#F5BE8E', bar: 'linear-gradient(90deg,#A65B2B,#E8A46A 60%,#FFE0BC)', glow: 'rgba(255,174,58,.45)', dist: '~%25' },
    { key: 'silver', tr: 'Gümüş', en: 'Silver', motif: ['Gümüş · Safir', 'Silver · Sapphire'],
      metal: ['#FFFFFF', '#D8E0EA', '#8794A8', '#2E3544'], plate: ['#1C2A48', '#070C1A'], gem: ['#05124A', '#1A48D0', '#6CA6FF', '#E4F0FF'],
      c: ['#FFFFFF', '#D8E0EA', '#8794A8', '#28303F'], txt: '#DCE5F1', bar: 'linear-gradient(90deg,#8794A8,#D8E0EA 60%,#FFFFFF)', glow: 'rgba(108,166,255,.45)', dist: '~%30' },
    { key: 'gold', tr: 'Altın', en: 'Gold', motif: ['Altın · Zümrüt', 'Gold · Emerald'],
      metal: ['#FFF6D2', '#F6C640', '#B07A0C', '#4A2C02'], plate: ['#0E2C1E', '#030E08'], gem: ['#022814', '#08884A', '#3EE38E', '#DAFFEA'],
      c: ['#FFF6D2', '#F6C640', '#B07A0C', '#412602'], txt: '#FFD45C', bar: 'linear-gradient(90deg,#B07A0C,#F6C640 60%,#FFF6D2)', glow: 'rgba(246,198,64,.5)', dist: '~%25' },
    { key: 'diamond', tr: 'Elmas', en: 'Diamond', motif: ['Platin · Elmas', 'Platinum · Diamond'],
      metal: ['#FFFFFF', '#E6EEF6', '#A2B2C8', '#3E4A5E'], plate: ['#14304E', '#050E1E'], gem: ['#4E6A8C', '#B4CCE6', '#F2F9FF', '#FFFFFF'],
      c: ['#EAFDFF', '#9FE6FF', '#5A8EC8', '#0A2A58'], txt: '#9FE6FF', bar: 'linear-gradient(90deg,#5A8EC8,#9FE6FF 60%,#EAFDFF)', glow: 'rgba(170,225,255,.55)', dist: '~%13', prism: true },
    { key: 'master', tr: 'Usta', en: 'Master', motif: ['Gül altın · Yakut', 'Rose gold · Ruby'],
      metal: ['#FFE6EE', '#EBA1B4', '#9A4A62', '#341020'], plate: ['#3A0818', '#120006'], gem: ['#3A0010', '#B4122E', '#FF5470', '#FFE2E8'],
      c: ['#FFE6EE', '#FF6F8E', '#B4122E', '#3A0716'], txt: '#FF8FA6', bar: 'linear-gradient(90deg,#B4122E,#FF6F8E 60%,#FFE6EE)', glow: 'rgba(255,84,112,.5)', dist: '~%6' },
    { key: 'galaxy', tr: 'Galaksi', en: 'Galaxy', motif: ['Titanyum · Opal', 'Titanium · Opal'],
      metal: ['#E2FFFB', '#7FD8F0', '#5A44B8', '#120A34'], plate: ['#1E1060', '#05021A'], gem: ['#1A0A50', '#6A3FE0', '#3DF5E0', '#FFFFFF'],
      c: ['#E2FFFB', '#3DF5E0', '#8C6EFF', '#1A1150'], txt: '#B9A6FF', bar: 'linear-gradient(90deg,#8C6EFF,#5FB4F0 55%,#3DF5E0)', glow: 'rgba(140,110,255,.6)', dist: 'İlk 100', iri: true }
  ];
  const BY = {}; LEAGUES.forEach((l, i) => { l.i = i; BY[l.key] = l; });
  const ROMAN = ['', 'I', 'II', 'III'];
  let uid = 0;
  const f1 = (n) => +n.toFixed(2);
  const P = (r, deg) => [100 + r * Math.cos(deg * Math.PI / 180), 100 + r * Math.sin(deg * Math.PI / 180)];
  const pt = (p) => `${f1(p[0])},${f1(p[1])}`;
  const hx = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
  const mix = (a, b, t) => { const A = hx(a), B = hx(b); return '#' + A.map((v, i) => Math.round(v + (B[i] - v) * t).toString(16).padStart(2, '0')).join(''); };
  const ramp = (g, t) => (t < .5 ? mix(g[0], g[1], t * 2) : t < .85 ? mix(g[1], g[2], (t - .5) / .35) : mix(g[2], g[3], (t - .85) / .15));
  const star4 = (cx, cy, R, r) => { let s = ''; for (let i = 0; i < 8; i++) { const a = -Math.PI / 2 + i * Math.PI / 4, q = i % 2 ? r : R; s += `${f1(cx + q * Math.cos(a))},${f1(cy + q * Math.sin(a))} `; } return s.trim(); };

  function metalGrad(id, m, iri, rev) {
    const st = iri
      ? [[0, '#E2FFFB'], [.14, '#3DF5E0'], [.3, '#2A1A7A'], [.46, '#8C6EFF'], [.6, '#E2FFFB'], [.74, '#3DF5E0'], [.88, '#5A44B8'], [1, '#120A34']]
      : [[0, m[1]], [.16, m[0]], [.32, m[1]], [.5, m[2]], [.62, m[3]], [.76, m[2]], [.9, m[1]], [1, m[2]]];
    return `<linearGradient id="${id}" x1="${rev ? 1 : 0}" y1="${rev ? 1 : 0}" x2="${rev ? 0 : 1}" y2="${rev ? 0 : 1}">${st.map(([o, c]) => `<stop offset="${o}" stop-color="${c}"/>`).join('')}</linearGradient>`;
  }

  /* briyant kesim taş, üstten — merkez (100,100), yarıçap R */
  function gem(L, id, R, small) {
    const g = L.gem, light = -130, T = R * .52;
    let s = `<circle cx="100" cy="100" r="${R}" fill="${g[0]}"/>`;
    const G = [], Tb = [];
    for (let j = 0; j < 8; j++) { G.push(P(R, j * 45 - 90)); Tb.push(P(T, j * 45 - 67.5)); }
    const sh = (deg, flip) => { let t = (1 + Math.cos((deg - light) * Math.PI / 180)) / 2; if (flip) t = .2 + (1 - t) * .55; return t; };
    for (let j = 0; j < 8; j++) {
      const a = j * 45 - 90, n = (j + 1) % 8;
      let c1 = ramp(g, sh(a + 22.5, false) * .9), c2 = ramp(g, sh(a, true));
      if (L.prism && !small) { if (j === 2) c1 = mix(c1, '#9FF0FF', .45); if (j === 5) c1 = mix(c1, '#FFC2E6', .4); if (j === 6) c2 = mix(c2, '#FFF0A0', .35); }
      if (L.iri) { c1 = mix(c1, j % 2 ? '#3DF5E0' : '#8C6EFF', .35); c2 = mix(c2, j % 3 ? '#6A3FE0' : '#3DF5E0', .3); }
      s += `<polygon points="${pt(Tb[j])} ${pt(G[j])} ${pt(G[n])}" fill="${c1}"/>`;
      s += `<polygon points="${pt(G[j])} ${pt(Tb[(j + 7) % 8])} ${pt(Tb[j])}" fill="${c2}"/>`;
    }
    s += `<polygon points="${Tb.map(pt).join(' ')}" fill="url(#${id}t)"/>`;
    if (!small) {
      s += `<g fill="none" stroke="${g[3]}" stroke-opacity=".35" stroke-width=".9" stroke-linejoin="round">`;
      for (let j = 0; j < 8; j++) s += `<polyline points="${pt(G[j])} ${pt(Tb[j])} ${pt(G[(j + 1) % 8])}"/>`;
      s += `<polygon points="${Tb.map(pt).join(' ')}"/><polygon points="${G.map(pt).join(' ')}"/></g>`;
      s += `<polygon points="${pt(Tb[5])} ${pt(Tb[6])} ${pt(P(T * .2, -100))}" fill="#fff" opacity=".35"/>`;
      s += `<ellipse cx="${100 - R * .32}" cy="${100 - R * .36}" rx="${R * .22}" ry="${R * .1}" transform="rotate(-40 ${100 - R * .32} ${100 - R * .36})" fill="#fff" opacity=".75" filter="url(#${id}b)"/>`;
      s += `<polygon points="${star4(100 - R * .28, 100 - R * .32, R * .32, R * .05)}" fill="#fff"/>`;
      s += `<polygon points="${star4(100 + R * .42, 100 + R * .3, R * .16, R * .03)}" fill="#fff" opacity=".85"/>`;
    }
    return s;
  }

  /* okey taşı — merkez (100,100) */
  const TILE = ['1', '5', '9', '12', '13', '★'];
  function tile(L, id, small) {
    const g = L.gem, w = small ? 84 : 76, h = small ? 104 : 100, x = 100 - w / 2, y = 100 - h / 2 - (small ? 0 : 3), num = TILE[L.i];
    let s = '';
    if (!small) s += `<rect x="${x + 3}" y="${y + 9}" width="${w}" height="${h}" rx="12" fill="#000" opacity=".45" filter="url(#${id}b)"/>`;
    s += `<rect x="${x}" y="${y + 6}" width="${w}" height="${h}" rx="12" fill="#B9A988" stroke="${L.metal[3]}" stroke-width="${small ? 4 : 2}"/>`;
    s += `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="12" fill="url(#${id}k)" stroke="${L.metal[3]}" stroke-width="${small ? 4 : 2}"/>`;
    if (small) return s + `<circle cx="100" cy="${y + h * .42}" r="22" fill="url(#${id}s)"/>`;
    s += `<rect x="${x + 5}" y="${y + 5}" width="${w - 10}" height="${h - 10}" rx="8" fill="none" stroke="#fff" stroke-opacity=".7" stroke-width="1.5"/>`;
    s += `<path d="M${x + 10},${y + 4} H${x + w - 10}" stroke="#fff" stroke-opacity=".9" stroke-width="2" stroke-linecap="round"/>`;
    const fs = num.length > 1 ? 40 : 48, ny = y + h * .5;
    s += `<text x="100" y="${ny + 2}" text-anchor="middle" font-family="Montserrat,Arial,sans-serif" font-weight="900" font-size="${fs}" fill="#fff" opacity=".7">${num}</text>`;
    s += `<text x="100" y="${ny}" text-anchor="middle" font-family="Montserrat,Arial,sans-serif" font-weight="900" font-size="${fs}" fill="url(#${id}n)" stroke="${g[0]}" stroke-width="1.2">${num}</text>`;
    const cy = y + h * .74;
    s += `<circle cx="100" cy="${cy + 1}" r="10.5" fill="${L.metal[3]}" opacity=".5"/><circle cx="100" cy="${cy}" r="10" fill="url(#${id}m)" stroke="${L.metal[3]}" stroke-width="1.2"/><circle cx="100" cy="${cy}" r="7" fill="url(#${id}s)"/><circle cx="97.6" cy="${cy - 2.6}" r="2" fill="#fff"/>`;
    return s;
  }

  function defs(L, id, small) {
    const m = L.metal, g = L.gem, p = L.plate;
    return `<defs>${metalGrad(id + 'm', m, L.iri)}${metalGrad(id + 'r', m, L.iri, true)}
<radialGradient id="${id}p" cx=".5" cy=".42" r=".6"><stop offset="0" stop-color="${p[0]}"/><stop offset="1" stop-color="${p[1]}"/></radialGradient>
<radialGradient id="${id}t" cx=".38" cy=".3" r=".85"><stop offset="0" stop-color="${g[3]}"/><stop offset=".35" stop-color="${g[2]}"/><stop offset=".8" stop-color="${g[1]}"/><stop offset="1" stop-color="${g[0]}"/></radialGradient>
<radialGradient id="${id}s" cx=".35" cy=".3" r=".8"><stop offset="0" stop-color="#fff"/><stop offset=".3" stop-color="${g[2]}"/><stop offset="1" stop-color="${g[0]}"/></radialGradient>
<radialGradient id="${id}h"><stop offset="0" stop-color="${g[2]}" stop-opacity=".55"/><stop offset="1" stop-color="${g[2]}" stop-opacity="0"/></radialGradient>
<linearGradient id="${id}k" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFFEF8"/><stop offset=".6" stop-color="#F6EEDC"/><stop offset="1" stop-color="#E4D7BA"/></linearGradient>
<linearGradient id="${id}n" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${L.iri ? '#3DF5E0' : g[2]}"/><stop offset=".55" stop-color="${g[1]}"/><stop offset="1" stop-color="${L.iri ? '#4B2FC0' : g[0]}"/></linearGradient>
<linearGradient id="${id}w" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".7"/><stop offset=".5" stop-color="#fff" stop-opacity="0"/></linearGradient>
${small ? '' : `<filter id="${id}b" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="2"/></filter><filter id="${id}d" x="-20%" y="-20%" width="140%" height="150%"><feGaussianBlur stdDeviation="4"/></filter>`}</defs>`;
  }

  /* tier: 3|2|1 (III, II, I) — 0 = işaretsiz. rank: Galaksi sıra numarası. */
  function emblem(key, o) {
    o = o || {};
    const L = BY[key] || LEAGUES[0], k = L.i, size = o.size || 52, small = size < 40, id = 'g1r' + (++uid), m = L.metal;
    const M = `url(#${id}m)`, Rv = `url(#${id}r)`;
    let s = `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 200 200" style="display:block;overflow:visible">${defs(L, id, small)}`;
    if (!small) s += `<circle cx="100" cy="100" r="98" fill="url(#${id}h)"/><ellipse cx="100" cy="106" rx="88" ry="86" fill="#000" opacity=".45" filter="url(#${id}d)"/>`;
    // dış kenar
    s += `<circle cx="100" cy="100" r="90" fill="${M}" stroke="${m[3]}" stroke-width="2.5"/>`;
    if (!small) s += `<circle cx="100" cy="100" r="85" fill="none" stroke="${m[3]}" stroke-opacity=".35" stroke-width="5" stroke-dasharray="1.6 3.2"/>`;
    // eğim halkası
    s += `<circle cx="100" cy="100" r="78" fill="${Rv}" stroke="${m[3]}" stroke-width="${small ? 3 : 1.5}"/>`;
    // iç plaka + guilloche
    s += `<circle cx="100" cy="100" r="70" fill="url(#${id}p)" stroke="${m[3]}" stroke-width="2"/>`;
    if (!small) {
      let gl = '';
      for (let i = 0; i < 48; i++) { const a = i * 7.5, p1 = P(50, a), p2 = P(69, a); gl += `M${pt(p1)}L${pt(p2)}`; }
      s += `<path d="${gl}" stroke="#fff" stroke-opacity=".07" stroke-width="1"/>`;
      for (let r = 52; r <= 68; r += 4) s += `<circle cx="100" cy="100" r="${r}" fill="none" stroke="#fff" stroke-opacity=".045" stroke-width="1"/>`;
      if (L.iri) [[62, 52, 1.6], [138, 60, 1.2], [70, 146, 1.3], [142, 136, 1.7], [100, 36, 1.1], [44, 100, 1.2], [156, 100, 1]].forEach(([x, y, r]) => { s += `<circle cx="${x}" cy="${y}" r="${r}" fill="#fff" opacity=".85"/>`; });
    }
    // üst lig süsleri (malzeme odaklı, sade)
    if (k >= 3 && !small) for (let i = 0; i < 8; i++) { const a = i * 45 - 90; if (a === 90) continue; const q = P(74, a); s += `<circle cx="${f1(q[0])}" cy="${f1(q[1])}" r="2.6" fill="url(#${id}s)" stroke="${m[3]}" stroke-width=".8"/>`; }
    // merkez: okey taşı (sayı lig renginde, altında lig taşı)
    s += tile(L, id, small);
    // parlaklık
    if (!small) s += `<path d="M28,78 A74,74 0 0 1 120,27" fill="none" stroke="url(#${id}w)" stroke-width="5" stroke-linecap="round" opacity=".9"/><circle cx="100" cy="100" r="89" fill="none" stroke="#fff" stroke-opacity=".25" stroke-width="1"/>`;
    // kademe taşları — kenarın altında
    if (k < 5 && o.tier) {
      const n = 4 - o.tier, angs = n === 1 ? [90] : n === 2 ? [79, 101] : [70, 90, 110], r0 = small ? 13 : 9;
      angs.forEach((a) => { const q = P(86, a); s += `<circle cx="${f1(q[0])}" cy="${f1(q[1])}" r="${r0 + 2.5}" fill="${M}" stroke="${m[3]}" stroke-width="2"/><circle cx="${f1(q[0])}" cy="${f1(q[1])}" r="${r0}" fill="url(#${id}s)"/>${small ? '' : `<circle cx="${f1(q[0] - 2.8)}" cy="${f1(q[1] - 3)}" r="2.2" fill="#fff" opacity=".9"/>`}`; });
    }
    if (k === 5 && o.rank && !small) {
      const t = '#' + o.rank, w = 34 + t.length * 13;
      s += `<rect x="${100 - w / 2}" y="168" width="${w}" height="30" rx="15" fill="#120A34" stroke="${M}" stroke-width="3"/><text x="100" y="189.5" text-anchor="middle" font-family="Montserrat,system-ui,sans-serif" font-weight="800" font-size="19" fill="#fff">${t}</text>`;
    }
    return s + `</svg>`;
  }

  /* çerçeve kenar süsü: metal yuvada küçük lig taşı */
  function medal(key, size) {
    const L = BY[key] || LEAGUES[0], id = 'g1m' + (++uid), small = size < 22;
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 200 200" style="display:block;overflow:visible">${defs(L, id, true)}<circle cx="100" cy="108" r="92" fill="#000" opacity=".35"/><circle cx="100" cy="100" r="92" fill="url(#${id}m)" stroke="${L.metal[3]}" stroke-width="6"/>${gem(L, id, 70, true)}${small ? '' : `<polygon points="${star4(78, 72, 26, 4)}" fill="#fff"/>`}<ellipse cx="80" cy="66" rx="22" ry="10" transform="rotate(-35 80 66)" fill="#fff" opacity=".45"/></svg>`;
  }

  const STEPS = 15;
  const at = (idx) => idx >= STEPS ? { league: LEAGUES[5], tier: 0 } : { league: LEAGUES[Math.floor(idx / 3)], tier: 3 - (idx % 3) };
  const label = (idx, lang) => { const a = at(idx); return a.league[lang || 'tr'] + (a.tier ? ' ' + ROMAN[a.tier] : ''); };
  const deltas = (idx) => (idx <= 8 ? [30, 10, -10, -20] : [25, 5, -12, -22]);
  const seasonReset = (idx) => (idx <= 3 ? idx : Math.max(3, Math.min(idx, 14) - 4));
  const REWARD_CHIPS = [5000, 15000, 40000, 100000, 250000, 1000000];
  function frame(key) {
    const L = BY[key] || LEAGUES[0], m = L.metal;
    if (L.iri) return { bg: 'conic-gradient(from 200deg,#120A34,#8C6EFF 12%,#E2FFFB 22%,#3DF5E0 30%,#2A1A7A 44%,#8C6EFF 56%,#E2FFFB 66%,#3DF5E0 76%,#5A44B8 88%,#120A34)', sh: '0 0 0 2px #120A34,0 0 22px rgba(140,110,255,.6),0 0 40px rgba(61,245,224,.22)', gap: '#120A34' };
    return { bg: `conic-gradient(from 210deg,${m[3]},${m[1]} 8%,${m[0]} 16%,${m[1]} 26%,${m[2]} 40%,${m[3]} 50%,${m[2]} 60%,${m[1]} 72%,${m[0]} 80%,${m[1]} 88%,${m[3]})`, sh: `0 0 0 2px ${m[3]},0 0 16px ${L.glow}`, gap: m[3] };
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
  /* Sezon çerçeveli avatar: üstte lig madalyası, iki yanda ve alt çaprazlarda yuvaya oturtulmuş lig taşı. */
  /* Sezon çerçevesi: çentikli metal halka — ince diş kenar, 4 ana yönde V çentik ve içinde lig taşı (eşkenar dörtgen). */
  function ring(key, size) {
    const L = BY[key] || LEAGUES[0], m = L.metal, id = 'g1f' + (++uid);
    const pts = [];
    const N = 72;
    for (let i = 0; i < N; i++) {
      const a = i * 360 / N - 90, d = ((a + 90) % 90 + 90) % 90, near = Math.min(d, 90 - d);
      let R = i % 2 ? 97 : 100;
      if (near < 9) R = 100 - (9 - near) * 1.4;
      pts.push(pt(P(R, a)));
    }
    let s = `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 200 200" style="display:block;overflow:visible;position:absolute;inset:0">${defs(L, id, false)}`;
    s += `<circle cx="100" cy="100" r="99" fill="url(#${id}h)" transform="${'translate(100 100) scale(1.12) translate(-100 -100)'}"/>`;
    s += `<polygon points="${pts.join(' ')}" fill="url(#${id}m)" stroke="${m[3]}" stroke-width="2.4" stroke-linejoin="round"/>`;
    s += `<circle cx="100" cy="100" r="87" fill="url(#${id}r)" stroke="${m[3]}" stroke-width="1.6"/>`;
    s += `<circle cx="100" cy="100" r="93" fill="none" stroke="${m[3]}" stroke-opacity=".35" stroke-width="1.2" stroke-dasharray="2 4"/>`;
    [-90, 0, 90, 180].forEach((a) => { const c = P(91, a), u = 7, ang = a + 90;
      s += `<g transform="rotate(${ang} ${f1(c[0])} ${f1(c[1])})"><polygon points="${f1(c[0])},${f1(c[1] - u - 2)} ${f1(c[0] + u - 1)},${f1(c[1])} ${f1(c[0])},${f1(c[1] + u + 2)} ${f1(c[0] - u + 1)},${f1(c[1])}" fill="url(#${id}m)" stroke="${m[3]}" stroke-width="1.4"/><polygon points="${f1(c[0])},${f1(c[1] - u + 1.5)} ${f1(c[0] + u - 4)},${f1(c[1])} ${f1(c[0])},${f1(c[1] + u - 1.5)} ${f1(c[0] - u + 4)},${f1(c[1])}" fill="url(#${id}s)"/></g>`; });
    [-45, 45, 135, 225].forEach((a) => { const c = P(91, a); s += `<polygon points="${star4(c[0], c[1], 4.5, 1.2)}" fill="#fff" opacity=".75"/>`; });
    s += `<path d="M22,70 A82,82 0 0 1 112,19" fill="none" stroke="url(#${id}w)" stroke-width="4" stroke-linecap="round"/>`;
    return s + `</svg>`;
  }
  window.G101Rank.ring = ring;
  window.G101Ring = function (p) {
    const size = +p.size || 62;
    return R().createElement('span', { style: { position: 'absolute', inset: 0, display: 'block', pointerEvents: 'none' }, dangerouslySetInnerHTML: { __html: ring(p.league || 'gold', size) } });
  };
  window.G101FramedAvatar = function (p) {
    const h = R().createElement, size = +p.size || 110, L = BY[p.league] || LEAGUES[0];
    return h('div', { style: { position: 'relative', width: size, height: size, flex: 'none' } },
      h('span', { dangerouslySetInnerHTML: { __html: ring(p.league, size) } }),
      h('div', { style: { position: 'absolute', inset: Math.round(size * .14), borderRadius: '50%', background: p.color || '#3D7BFF', boxShadow: `0 0 0 ${Math.max(2, Math.round(size * .02))}px ${L.metal[3]}, inset 0 -6px 12px rgba(0,0,0,.25)`, display: 'grid', placeItems: 'center', font: `800 ${Math.round(size * .3)}px Montserrat,system-ui`, color: '#fff' } }, p.ini || 'C')
    );
  };
})();
