/* Galaxy 101 · g101-state.js · v0.5.0
 * Durum yönlendirici: ?state=ID ile states.json'daki durumu açar, ?ft ile dondurur, ?dump=1 ile yerleşim dökümü verir.
 *   Galaxy 101 - Gerçek Ana Oyun.dc.html?state=G15&theme=saturn&lang=tr&seed=42&ft=1200
 * Bileşenler componentDidMount'ta window.__g101.<Ad> = this kaydeder (App, Menu, Lobi, Dereceli, Masa); yönlendirici yalnız bunları sürer.
 * Kayıt güvenliği: ?state / ?lang / ?theme varken App kaydı localStorage'a YAZMAZ (G101State.sandbox) — oyuncunun gerçek kaydı değişmez.
 * NOT: Bu bir prototip/test aracıdır (DEMO_LIST.md). Gerçek oyunda durumlar sunucu olaylarıyla oluşur.
 */
(function () {
  if (window.G101State) return;
  var q = new URLSearchParams(location.search);
  var SID = q.get('state'), LANG = q.get('lang'), THEME = q.get('theme'), TQ = q.get('ft'), DUMP = q.get('dump') === '1', NODL = q.get('nodl') === '1';
  var THEMES = { saturn: 'Satürn', mars: 'Mars', neptun: 'Neptün', jupiter: 'Jüpiter', venus: 'Venüs' };
  var SRC = './galaxy101_handoff_v4/states/states.json';
  window.__g101 = window.__g101 || {};
  var log = function () { try { console.info.apply(console, ['[G101 durum]'].concat([].slice.call(arguments))); } catch (e) {} };
  var sleep = function (ms) { return new Promise(function (r) { setTimeout(r, ms); }); };
  function until(fn, ms, label) {
    var t0 = performance.now(); ms = ms || 20000;
    return new Promise(function (res, rej) { (function poll() { var v; try { v = fn(); } catch (e) {} if (v) return res(v); if (performance.now() - t0 > ms) return rej(new Error('zaman aşımı: ' + label)); setTimeout(poll, 60); })(); });
  }
  var I = function (n) { return window.__g101[n]; };
  var SEATS = [null, { name: 'Ayşe K.', color: '#FF6B9A', lv: 31 }, { name: 'Burak T.', color: '#4FC3F7', lv: 18 }, { name: 'Zeynep A.', color: '#9CCC65', lv: 42 }];

  function resolveArg(a) {
    if (typeof a !== 'string' || a.indexOf('$tbl:') !== 0) return a;
    var L = I('Lobi'), kind = a.slice(5), ts = (L && L.state.tables) || [];
    var free = function (t) { return (t.players || []).some(function (p) { return !p; }); };
    var pick = ts.find(function (t) {
      if (kind === 'pass') return !!t.pass;
      if (kind === 'open' || kind === 'mine') return !t.pass && t.vis !== 'private' && free(t) && !t.live;
      if (kind === 'full') return !free(t);
      return false;
    });
    if (!pick) throw new Error('masa bulunamadı: ' + kind);
    return pick;
  }
  async function step(s) {
    var A = I('App');
    if (s.fresh) return;
    if (s.logout) { A.persist({ acct: null }); return; }
    if (s.acct) {
      clearInterval(A._spI);
      if (!A.state.save.acct || A.state.save.acct.id !== 'guest:state') A.loginAs({ id: 'guest:state', name: 'Doğukan Y.', color: '#FF8A5B', method: 'guest' }, 10000);   // durum koşusu her zaman aynı misafirle (kayda yazılmaz)
      await sleep(120); return;
    }
    if (s.go) { clearInterval(A._spI); var ex = {}; if (s.f) ex.f = Object.assign({}, A.state.f, s.f); A.go(s.go, ex); await sleep(80); return; }
    if (s.waitFor) { await until(function () { return I(s.waitFor); }, 20000, s.waitFor); await sleep(250); return; }
    if (s.wait) { await sleep(s.wait); return; }
    if (s.set) {
      var X = I(s.set); if (!X) throw new Error('bileşen yok: ' + s.set);
      if (s.s) X.setState(s.s);
      if (s.deep) X.setState(function (st) { var o = {}; Object.keys(s.deep).forEach(function (k) { o[k] = Object.assign({}, st[k], s.deep[k]); }); return o; });
      await sleep(80); return;
    }
    if (s.call) { var Y = I(s.call); if (!Y || typeof Y[s.fn] !== 'function') throw new Error('çağrı yok: ' + s.call + '.' + s.fn); Y[s.fn].apply(Y, (s.args || []).map(resolveArg)); await sleep(120); return; }
    if (s.scenario) {
      var sc = await fetch('./galaxy101_handoff_v4/logic/scenarios/' + s.scenario + '.json').then(function (r) { return r.json(); });
      window.G101Scenario = { id: sc.id, deal: sc.deal };
      return;
    }
    if (s.waitMy) { await until(function () { var M = I('Masa'), x = M && M.state; return x && !x.deal && x.active === 0 && x.phase === 'discard'; }, 90000, 'sıra bende'); await sleep(s.waitMy === true ? 600 : s.waitMy); return; }
    if (s.waitPen) { await until(function () { var M = I('Masa'), x = M && M.state; return x && x.pen && x.pen[s.waitPen.seat || 0] > 0; }, 30000, 'ceza'); await sleep(s.waitPen.after || 300); return; }
    if (s.click || s.type) {
      var sel = '[data-g101-id="' + (s.click || s.type) + '"]', el = await until(function () { return document.querySelector(sel); }, 8000, sel);
      if (s.click) el.click();
      else { var proto = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, s.text || ''); el.dispatchEvent(new Event('input', { bubbles: true })); }
      await sleep(160); return;
    }
    if (s.table) {
      if (s.table.bots) A.startBots(); else A.startGame({ table: { id: 'state', name: 'Test masası', type: 'tekli', bet: 5000, hands: 5, turn: 30, players: SEATS.slice() }, seats: SEATS.slice() });
      if (s.table.noWait) return;
      await until(function () { var M = I('Masa'); return M && M.T && A.state.screen !== 'loading'; }, 30000, 'masa');
      await sleep(400);
      if (s.table.flow) I('Masa').go(s.table.flow);
      return;
    }
    if (s.waitDeal !== undefined) {
      await until(function () { var M = I('Masa'), d = M && M.state.deal; return s.waitDeal === null ? (M && !d && M.state.phase !== 'deal') : (d && d.step === s.waitDeal); }, 60000, 'dağıtım ' + s.waitDeal);
      return;
    }
    if (s.masa) { var M = I('Masa'); M[s.masa].apply(M, s.args || []); await sleep(100); return; }
  }

  function dump(id) {
    var A = I('App'), k = (A && A.state.k) || 1, stage = document.querySelector('[data-g101-id="app.stage"]');
    var o = stage ? stage.getBoundingClientRect() : { left: 0, top: 0 };
    var P = ['color', 'backgroundColor', 'backgroundImage', 'fontFamily', 'fontWeight', 'fontSize', 'lineHeight', 'letterSpacing', 'textAlign', 'textTransform',
      'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft', 'borderTopWidth', 'borderTopColor', 'borderTopStyle', 'borderRadius', 'boxShadow', 'textShadow', 'opacity', 'transform', 'zIndex', 'filter', 'backdropFilter', 'mixBlendMode', 'display', 'visibility'];
    var items = [].slice.call(document.querySelectorAll('[data-g101-id]')).map(function (el) {
      var r = el.getBoundingClientRect(), cs = getComputedStyle(el), st = {};
      P.forEach(function (p) { st[p] = cs[p]; });
      return { id: el.getAttribute('data-g101-id'), box: { x: +((r.left - o.left) / k).toFixed(2), y: +((r.top - o.top) / k).toFixed(2), w: +(r.width / k).toFixed(2), h: +(r.height / k).toFixed(2) }, visible: r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && +cs.opacity > 0, text: el.children.length ? null : (el.textContent || '').trim().slice(0, 200), style: st };
    });
    var out = { version: '0.5.0', state: id, error: window.G101State && window.G101State.error || null, seed: window.G101Clock && G101Clock.seed, t: window.G101Clock && G101Clock.now(), unit: '1280x720 tasarım birimi', scale: k, count: items.length, items: items };
    window.G101Dump = out;
    try { if (window.parent !== window) window.parent.postMessage({ g101dump: out }, '*'); } catch (e) {}
    if (!NODL) { var a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([JSON.stringify(out, null, 1)], { type: 'application/json' })); a.download = id + '.layout.json'; document.body.appendChild(a); a.click(); a.remove(); }
    return out;
  }

  async function run() {
    var A = await until(function () { return I('App'); }, 30000, 'App');
    var patch = {};
    if (LANG === 'tr' || LANG === 'en') patch.lang = LANG;
    if (THEME && THEMES[THEME]) patch.planet = THEMES[THEME];
    var doc = SID ? await fetch(SRC).then(function (r) { return r.json(); }) : null, st = doc && doc.states.find(function (x) { return x.id === SID; });
    if (st && st.lang && !patch.lang) patch.lang = st.lang;
    if (st && st.theme && !patch.planet) patch.planet = THEMES[st.theme];
    if (Object.keys(patch).length) { A.persist(patch); await sleep(100); }
    if (!SID) return;
    if (!st) { log('bilinmeyen durum', SID); window.G101State.error = 'unknown'; return; }
    if (st.auto === false) { log('durum elle açılır', SID, st.note || ''); window.G101State.error = 'manual'; return; }
    window.G101State.current = SID;
    try { for (var i = 0; i < st.path.length; i++) await step(st.path[i]); }
    catch (e) { log('yol hatası', SID, e.message); window.G101State.error = e.message; }
    var hold = TQ != null && /^\d+$/.test(TQ) ? +TQ : (st.hold != null ? st.hold : 600);
    await sleep(hold);
    if (window.G101Clock) G101Clock.freeze();
    window.G101State.ready = true;
    document.documentElement.setAttribute('data-g101-state', SID);
    if (DUMP) dump(SID);
  }
  window.G101State = { id: SID, ready: false, error: null, dump: dump, run: run, sandbox: !!(SID || LANG || THEME) };
  if (SID || LANG || THEME) (document.readyState === 'loading' ? document.addEventListener('DOMContentLoaded', run) : run());
})();
