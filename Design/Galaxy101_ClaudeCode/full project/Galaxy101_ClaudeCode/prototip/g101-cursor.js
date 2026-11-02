/* Galaxy 101 — oyun imleç seti. Tek dosya: çizimler (SVG üretici) + canlı imleç yöneticisi.
   Durum, üzerindeki öğenin inline `cursor` stilinden, input/textarea'dan, role="slider"dan veya data-cursor özniteliğinden okunur. */
(function () {
  if (window.G101Cursor) return;
  const INK = '#0A0F2C';
  const HOT = { default: [4, 3], pointer: [4, 3], grab: [16, 15], grabbing: [16, 16], drop: [16, 16], text: [16, 16], no: [4, 3], wait: [16, 16], progress: [4, 3], ew: [16, 16], ns: [16, 16] };
  const NAMES = Object.keys(HOT);
  const ARROW = '4,3 4,23.6 9.3,19 13.2,27.3 16.9,25.6 13.1,17.6 20.1,17.4';
  const sh = (id) => `<filter id="${id}" x="-30%" y="-30%" width="160%" height="160%"><feDropShadow dx="0" dy="1.6" stdDeviation="1.3" flood-color="#000" flood-opacity=".45"/></filter>`;
  const dia = (x, y, s, fill) => `<rect x="${x - s / 2}" y="${y - s / 2}" width="${s}" height="${s}" rx="${s * 0.22}" transform="rotate(45 ${x} ${y})" fill="${fill}"/>`;
  const arrow = (u, fill, glow) => (glow ? `<polygon points="${ARROW}" fill="none" stroke="${glow}" stroke-width="6" stroke-linejoin="round" opacity=".28"/>` : '') +
    `<polygon points="${ARROW}" fill="${fill}" stroke="${INK}" stroke-width="2" stroke-linejoin="round"/><path d="M5.6 6.6 V19.6" stroke="#fff" stroke-width="1.1" stroke-linecap="round" opacity=".55"/>`;
  const hand = (closed, acc) => {
    const R = closed
      ? [[9, 11, 3.6, 7.5], [12.8, 10, 3.6, 8.5], [16.6, 10.5, 3.6, 8], [20.4, 12, 3.3, 6.5], [9, 14, 14.7, 13, 5]]
      : [[9, 6, 3.6, 13], [12.8, 4, 3.6, 14], [16.6, 5, 3.6, 13], [20.4, 8, 3.3, 11], [9, 13, 14.7, 14, 5]];
    const th = closed ? '<rect x="6.2" y="16.2" width="3.6" height="8.4" rx="1.8" transform="rotate(-62 8 20.4)"/>' : '<rect x="6.2" y="15.2" width="3.6" height="9.6" rx="1.8" transform="rotate(-42 8 20)"/>';
    const shapes = R.map(([x, y, w, h, r]) => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r || w / 2}"/>`).join('') + th;
    const ln = closed ? [[12.7, 12.4, 16], [16.5, 12, 16], [20.3, 13.4, 16]] : [[12.7, 8.5, 15.2], [16.5, 7.5, 15.2], [20.3, 10.5, 15.2]];
    return `<g fill="${INK}" stroke="${INK}" stroke-width="2.4" stroke-linejoin="round">${shapes}</g><g fill="#F7F8FD">${shapes}</g>` +
      ln.map(([x, a, b]) => `<path d="M${x} ${a}V${b}" stroke="${INK}" stroke-width="1" stroke-linecap="round" opacity=".5"/>`).join('') + dia(16.4, 21, 3.2, acc);
  };
  const orbit = (cx, cy, r, ang, spin, pr) => {
    const g = spin ? `<g style="transform-box:view-box;transform-origin:${cx}px ${cy}px;animation:g101spin 1.1s linear infinite">` : `<g transform="rotate(${ang || 0} ${cx} ${cy})">`;
    return `${g}<circle cx="${cx}" cy="${cy - r}" r="${pr}" fill="#fff" stroke="${INK}" stroke-width="1.4"/></g>`;
  };
  function svg(name, o) {
    o = o || {}; const c1 = o.c1 || '#FFC94A', c2 = o.c2 || '#F5A623', id = 'g' + Math.random().toString(36).slice(2, 7), spin = o.angle == null;
    const grad = `<linearGradient id="${id}a" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${c1}"/><stop offset="1" stop-color="${c2}"/></linearGradient><linearGradient id="${id}w" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFFFFF"/><stop offset="1" stop-color="#DCE2F3"/></linearGradient><linearGradient id="${id}m" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#D3D8E6"/><stop offset="1" stop-color="#9BA3BC"/></linearGradient>`;
    let b = '';
    if (name === 'default') b = arrow(0, `url(#${id}w)`) + dia(7.2, 15.2, 2.8, c1);
    else if (name === 'pointer') b = arrow(0, `url(#${id}a)`, c1) + dia(7.2, 15.2, 2.8, '#fff');
    else if (name === 'grab') b = hand(false, c1);
    else if (name === 'grabbing') b = `<circle cx="16" cy="17" r="13" fill="${c1}" fill-opacity=".1" stroke="${c1}" stroke-opacity=".55" stroke-width="1.3"/>` + hand(true, c1);
    else if (name === 'drop') b = `<circle cx="16" cy="17" r="13" fill="#3CCB6E" fill-opacity=".12" stroke="#3CCB6E" stroke-opacity=".6" stroke-width="1.3"/>` + hand(true, '#3CCB6E') + `<circle cx="25" cy="25" r="5.4" fill="#3CCB6E" stroke="${INK}" stroke-width="1.6"/><path d="M22.4 25.2l1.8 1.8 3.4-3.6" fill="none" stroke="#fff" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"/>`;
    else if (name === 'text') { const d = 'M11.4 6.5Q16 6.5 16 9.6Q16 6.5 20.6 6.5M16 9.6V22.4M11.4 25.5Q16 25.5 16 22.4Q16 25.5 20.6 25.5'; b = `<path d="${d}M13.4 16H18.6" fill="none" stroke="${INK}" stroke-width="4.4" stroke-linecap="round" stroke-linejoin="round"/><path d="${d}" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/><path d="M13.4 16H18.6" stroke="${c1}" stroke-width="2.2" stroke-linecap="round"/>`; }
    else if (name === 'no') b = arrow(0, `url(#${id}m)`) + dia(7.2, 15.2, 2.8, '#7A84A3') + `<circle cx="23" cy="23" r="6" fill="#FF5E6C" stroke="${INK}" stroke-width="1.6"/><circle cx="23" cy="23" r="3.2" fill="none" stroke="#fff" stroke-width="1.7"/><path d="M20.8 25.2l4.4-4.4" stroke="#fff" stroke-width="1.7" stroke-linecap="round"/>`;
    else if (name === 'wait') b = `<circle cx="16" cy="16" r="10.5" fill="none" stroke="${INK}" stroke-width="4" opacity=".5"/><circle cx="16" cy="16" r="10.5" fill="none" stroke="#fff" stroke-width="1.4" opacity=".55" stroke-dasharray="2 3.5"/><circle cx="16" cy="16" r="5.6" fill="url(#${id}a)" stroke="${INK}" stroke-width="1.6"/><path d="M13.6 14.2a3 3 0 0 1 2.4-1.4" fill="none" stroke="#fff" stroke-width="1.3" stroke-linecap="round" opacity=".8"/>` + orbit(16, 16, 10.5, o.angle, spin, 2.5);
    else if (name === 'progress') b = arrow(0, `url(#${id}w)`) + dia(7.2, 15.2, 2.8, c1) + `<circle cx="24" cy="24" r="6.4" fill="${INK}" opacity=".88"/><circle cx="24" cy="24" r="2.6" fill="url(#${id}a)"/>` + orbit(24, 24, 4.4, o.angle, spin, 1.5);
    else if (name === 'ew' || name === 'ns') { const p = 'M4.5 16L10.2 10.3V13.8H21.8V10.3L27.5 16L21.8 21.7V18.2H10.2V21.7Z'; b = `<g${name === 'ns' ? ' transform="rotate(90 16 16)"' : ''}><path d="${p}" fill="url(#${id}w)" stroke="${INK}" stroke-width="2" stroke-linejoin="round"/>${dia(16, 16, 3, c1)}</g>`; }
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${o.size || 32}" height="${o.size || 32}" viewBox="0 0 32 32"><defs>${sh(id + 's')}${grad}</defs><g filter="url(#${id}s)">${b}</g></svg>`;
  }
  const API = { svg, HOT, NAMES, on: false, size: 1, state: 'default' };
  window.G101Cursor = API;
  if (typeof document === 'undefined') return;

  const KEY = 'galaxy101.cursor';
  let pref = { mode: 'Normal' };
  try { const j = JSON.parse(localStorage.getItem(KEY) || 'null'); if (j && j.mode) pref = j; } catch (e) {}
  const fine = !window.matchMedia || matchMedia('(any-pointer: fine)').matches;
  const css = document.createElement('style');
  css.textContent = '@keyframes g101spin{to{transform:rotate(360deg)}}@keyframes g101rip{0%{transform:translate(-50%,-50%) scale(.25);opacity:.9}100%{transform:translate(-50%,-50%) scale(1);opacity:0}}';
  document.head.appendChild(css);
  const hide = document.createElement('style');
  hide.textContent = 'html,html *,html *::before,html *::after{cursor:none!important}';
  const root = document.createElement('div');
  root.setAttribute('aria-hidden', 'true');
  root.style.cssText = 'position:fixed;left:0;top:0;width:0;height:0;z-index:2147483647;pointer-events:none;opacity:0;transition:opacity .18s;will-change:transform;contain:layout style';
  const layers = {};
  let x = -100, y = -100, cur = 'default', pressed = false, held = '', lastEl = null, acc = ['', ''], shown = false;

  function build() {
    root.innerHTML = ''; const S = API.size * 32;
    NAMES.forEach((n) => {
      const [hx, hy] = HOT[n], k = API.size, el = document.createElement('div');
      el.style.cssText = `position:absolute;left:${-hx * k}px;top:${-hy * k}px;width:${S}px;height:${S}px;transform-origin:${hx * k}px ${hy * k}px;opacity:0;transform:scale(.6);transition:opacity .12s ease-out,transform .22s cubic-bezier(.2,1.5,.4,1)`;
      el.innerHTML = svg(n, { c1: acc[0] || undefined, c2: acc[1] || undefined, size: S });
      root.appendChild(el); layers[n] = el;
    });
    paint(true);
  }
  function paint(force) {
    NAMES.forEach((n) => {
      const el = layers[n], on = n === cur;
      let t = on ? 'scale(1)' : 'scale(.6)';
      if (on && pressed && (n === 'default' || n === 'pointer' || n === 'no')) t = n === 'pointer' ? 'scale(.84) rotate(-8deg)' : 'scale(.86)';
      if (on && !pressed && n === 'pointer') t = 'scale(1.06) rotate(-6deg)';
      if (on && n === 'grabbing') t = 'scale(.96)';
      if (on && n === 'no' && !pressed) t = 'scale(1)';
      el.style.opacity = on ? '1' : '0'; el.style.transform = t;
    });
  }
  const MAP = { pointer: 'pointer', grab: 'grab', '-webkit-grab': 'grab', grabbing: 'grabbing', '-webkit-grabbing': 'grabbing', move: 'grab', copy: 'drop', alias: 'drop', text: 'text', 'vertical-text': 'text', 'not-allowed': 'no', 'no-drop': 'no', wait: 'wait', progress: 'progress', 'ew-resize': 'ew', 'col-resize': 'ew', 'e-resize': 'ew', 'w-resize': 'ew', 'ns-resize': 'ns', 'row-resize': 'ns', 'n-resize': 'ns', 's-resize': 'ns', default: 'default', none: 'none' };
  const TXT = /^(text|email|password|search|tel|url|number)$/;
  function scrollBar(e) {
    if (e === document.body || e === document.documentElement) return '';
    const vw = e.offsetWidth - e.clientWidth, hh = e.offsetHeight - e.clientHeight; if (vw < 4 && hh < 4) return '';
    const r = e.getBoundingClientRect();
    if (vw >= 4 && e.scrollHeight > e.clientHeight && x >= r.right - vw - 2 && x <= r.right && y >= r.top && y <= r.bottom) return 'ns';
    if (hh >= 4 && e.scrollWidth > e.clientWidth && y >= r.bottom - hh - 2 && y <= r.bottom && x >= r.left && x <= r.right) return 'ew';
    return '';
  }
  function resolve(el) {
    if (held) return held;
    const lk = el && el.closest && el.closest('[data-cursor-lock]'), lv = lk && lk.getAttribute('data-cursor-lock'); if (lv && HOT[lv]) return lv;
    for (let e = el; e && e.nodeType === 1; e = e.parentElement) {
      const sb = scrollBar(e); if (sb) return sb;
      const dc = e.getAttribute('data-cursor'); if (dc && (HOT[dc] || dc === 'none')) return dc;
      const tag = e.tagName;
      if ((tag === 'INPUT' && TXT.test(e.type || 'text')) || tag === 'TEXTAREA' || e.isContentEditable) return e.disabled || e.readOnly ? 'no' : 'text';
      const c = e.style && e.style.cursor;
      if (c && c !== 'auto' && c !== 'inherit') { const m = MAP[c.split(',').pop().trim()]; if (m) return m; }
      if (e.getAttribute('role') === 'slider') return 'ew';
      if (e.getAttribute('aria-disabled') === 'true' || (e.disabled && (tag === 'BUTTON' || tag === 'SELECT'))) return 'no';
      if (tag === 'BUTTON' || tag === 'A' || tag === 'SELECT' || tag === 'LABEL' || tag === 'SUMMARY') return 'pointer';
    }
    return 'default';
  }
  function accentOf(el) {
    try { const cs = getComputedStyle(el); return [cs.getPropertyValue('--a1').trim(), cs.getPropertyValue('--a2').trim()]; } catch (e) { return ['', '']; }
  }
  function update(target) {
    if (!target) return;
    const a = accentOf(target);
    if (a[0] && (a[0] !== acc[0] || a[1] !== acc[1])) { acc = a; build(); }
    let s = resolve(target);
    if (pressed && s === 'grab') s = 'grabbing';
    if (s === 'none') { root.style.opacity = '0'; return; }
    if (shown) root.style.opacity = '1';
    if (s !== cur) { cur = s; API.state = s; paint(); }
  }
  function move(e) {
    if (e.pointerType === 'touch') { root.style.opacity = '0'; shown = false; return; }
    x = e.clientX; y = e.clientY;
    root.style.transform = `translate3d(${x}px,${y}px,0)`;
    if (!shown) { shown = true; root.style.opacity = '1'; }
    lastEl = e.target; dirty = true;
  }
  function ripple(color) {
    const r = document.createElement('div');
    r.style.cssText = `position:fixed;left:${x}px;top:${y}px;width:${38 * API.size}px;height:${38 * API.size}px;border-radius:50%;border:2px solid ${color};box-shadow:0 0 12px ${color};pointer-events:none;z-index:2147483646;animation:g101rip .45s cubic-bezier(.2,.8,.3,1) forwards`;
    document.body.appendChild(r); setTimeout(() => r.remove(), 480);
  }
  const onDown = (e) => { if (e.pointerType === 'touch' || e.button !== 0) return; pressed = true; update(e.target); if (cur === 'ew' || cur === 'ns') held = cur; paint(); if (cur === 'default' || cur === 'pointer') ripple(acc[0] || '#FFC94A'); };
  const onUp = (e) => { pressed = false; held = ''; update(document.elementFromPoint(x, y) || e.target); paint(); };
  const onDrag = (e) => { if (!e.clientX && !e.clientY) return; x = e.clientX; y = e.clientY; root.style.transform = `translate3d(${x}px,${y}px,0)`; if (cur !== 'grabbing' && cur !== 'drop') { cur = 'grabbing'; paint(); } };
  const onLeave = (e) => { if (!e.relatedTarget) { root.style.opacity = '0'; shown = false; } };
  const onBlur = () => { root.style.opacity = '0'; shown = false; pressed = false; held = ''; };
  let tick = 0, dirty = false;
  function poll() {
    if (!API.on) return;
    if (shown) { if (dirty) { dirty = false; update(lastEl); } else if (++tick % 8 === 0 && !pressed) { const t = document.elementFromPoint(x, y); if (t) update(t); } }
    requestAnimationFrame(poll);
  }

  API.enable = function (on) {
    on = !!on && fine;
    if (on === API.on) return; API.on = on;
    if (on) {
      if (!root.isConnected) document.documentElement.appendChild(root);
      document.head.appendChild(hide); build();
      addEventListener('pointermove', move, { capture: true, passive: true });
      addEventListener('pointerdown', onDown, true); addEventListener('pointerup', onUp, true); addEventListener('pointercancel', onUp, true);
      addEventListener('dragover', onDrag, true); document.addEventListener('mouseout', onLeave, true); addEventListener('blur', onBlur);
      requestAnimationFrame(poll);
    } else {
      hide.remove(); root.remove(); shown = false;
      removeEventListener('pointermove', move, { capture: true }); removeEventListener('pointerdown', onDown, true); removeEventListener('pointerup', onUp, true); removeEventListener('pointercancel', onUp, true);
      removeEventListener('dragover', onDrag, true); document.removeEventListener('mouseout', onLeave, true); removeEventListener('blur', onBlur);
    }
  };
  API.setMode = function (mode) {
    pref = { mode }; try { localStorage.setItem(KEY, JSON.stringify(pref)); } catch (e) {}
    API.mode = mode;
    if (mode === 'Sistem') return API.enable(false);
    const k = mode === 'Büyük' ? 1.3 : 1;
    if (k !== API.size) { API.size = k; if (API.on) build(); }
    API.enable(true);
  };
  API.mode = pref.mode;
  API.size = pref.mode === 'Büyük' ? 1.3 : 1;
  const start = () => { if (pref.mode !== 'Sistem') API.enable(true); };
  if (document.body) start(); else document.addEventListener('DOMContentLoaded', start);
})();
