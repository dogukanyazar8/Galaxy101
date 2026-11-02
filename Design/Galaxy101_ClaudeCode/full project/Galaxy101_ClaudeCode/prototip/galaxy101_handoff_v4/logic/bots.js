/* Galaxy 101 · logic/bots.js · v0.5.0
 * Saf bot karar modülü. Sunucuda da aynı kural motoruyla (rules.js) çalışır.
 * Bot yalnız kendi ıstakasını ve açık bilgiyi (masadaki perler, atılan taşlar, gösterge) kullanır; deste sırasını ve rakip ıstakalarını görmez.
 * Rastgelelik yalnız sohbet içindir ve dışarıdan verilen seed'li RNG ile ({s}) gelir; oyun kararları deterministiktir.
 */
(function (root) {
  'use strict';
  var R = (function () { var m = null; function get() { return m || (m = root.G101Rules || (typeof require !== 'undefined' ? require('./rules.js') : null)); } return typeof Proxy !== 'undefined' ? new Proxy({}, { get: function (_, k) { return get()[k]; } }) : get(); })();   // tarayıcıda betik yükleme sırası garanti değil → ilk kullanımda çöz (C# portunda: doğrudan bağımlılık)

  // Düşünme süreleri (ms) — prototip zamanlamasından: tur başı → çek 600, her per/işle 430, atış 650 (per sonrası) / 730 (doğrudan).
  var TIMING = { draw: 600, meld: 430, discardAfterMeld: 650, discard: 730, turnStartDelay: 120 };
  // Sohbet: her atıştan sonra %16 olasılıkla bot.1–bot.6 satırlarından biri; oyuncu yazınca %60 olasılıkla 1400 ms sonra yanıt; hediyeye 1900 ms (roket 6300 ms) sonra teşekkür.
  var CHAT = { afterDiscard: 0.16, lines: ['bot.1', 'bot.2', 'bot.3', 'bot.4', 'bot.5', 'bot.6'], replyChance: 0.6, replyDelay: 1400, thanksDelay: 1900, thanksDelayRocket: 6300 };

  var SHAPES = null;
  function shapes() {
    if (SHAPES) return SHAPES;
    var at = [], c, s, L, n, j;
    for (j = 0; j < 52; j++) at.push([]);
    function add(sh) { var seen = {}; sh.keys.forEach(function (q) { if (!seen[q]) { seen[q] = 1; at[q].push(sh); } }); sh.value = sh.keys.reduce(function (a, q) { return a + (q % 13) + 1; }, 0); }
    for (c = 0; c < 4; c++) {
      for (s = 1; s <= 11; s++) for (L = 3; s + L - 1 <= 13; L++) { var k1 = []; for (j = 0; j < L; j++) k1.push(c * 13 + s - 1 + j); add({ kind: 'run', keys: k1 }); }
    }
    for (n = 0; n < 13; n++) {
      var all = [0, 1, 2, 3].map(function (x) { return x * 13 + n; });
      add({ kind: 'group', keys: all });
      for (var skip = 0; skip < 4; skip++) add({ kind: 'group', keys: all.filter(function (x, i) { return i !== skip; }) });
    }
    SHAPES = at; return at;
  }
  function keyOf(ok, id) { var r = R.realOf(ok, id); return R.COLORS.indexOf(r.c) * 13 + r.n - 1; }

  // En yüksek toplamlı seri/grup kümesi; okey eksik yerleri doldurur.
  function bestMelds(ok, ids) {
    var at = shapes(), cnt = [], pool = [], memo = {}, i;
    for (i = 0; i < 52; i++) { cnt.push(0); pool.push([]); }
    var jok = [], rest = [];
    ids.forEach(function (id) { if (R.isOkey(ok, id)) jok.push(id); else rest.push(id); });
    rest.slice().sort().forEach(function (id) { var k = keyOf(ok, id); cnt[k]++; pool[k].push(id); });
    function dfs(p, jl) {
      while (p < 52 && !cnt[p]) p++;
      if (p >= 52) return { v: 0, m: [] };
      var mk = p + '|' + jl + '|' + cnt.slice(p).join('');
      if (memo[mk]) return memo[mk];
      cnt[p]--; var best = dfs(p, jl); cnt[p]++;
      var list = at[p];
      for (var a = 0; a < list.length; a++) {
        var sh = list[a], real = [], jq = [], seenP = false;
        for (var b = 0; b < sh.keys.length; b++) { var q = sh.keys[b]; if (q === p && !seenP) { seenP = true; real.push(q); } else if (q > p && cnt[q] > 0) real.push(q); else jq.push(b); }
        if (jq.length > jl) continue;
        real.forEach(function (q) { cnt[q]--; });
        var r = dfs(p, jl - jq.length), v = r.v + sh.value;
        real.forEach(function (q) { cnt[q]++; });
        if (v > best.v) best = { v: v, m: [{ sh: sh, jq: jq }].concat(r.m) };
      }
      memo[mk] = best; return best;
    }
    var res = dfs(0, jok.length), js = jok.slice(), pl = pool.map(function (x) { return x.slice(); });
    var melds = res.m.map(function (o) { return { kind: o.sh.kind, tiles: o.sh.keys.map(function (q, b) { return o.jq.indexOf(b) >= 0 ? js.shift() : pl[q].shift(); }) }; });
    var left = []; pl.forEach(function (x) { left = left.concat(x); }); left = left.concat(js);
    return { value: res.v, melds: melds, left: left };
  }

  // Çiftler: aynı taşlar; okey en yüksek tek taşla, iki okey birbiriyle eşleşir.
  function bestPairs(ok, ids) {
    var by = {}, order = [], pairs = [], singles = [], js = [];
    ids.slice().sort().forEach(function (id) {
      if (R.isOkey(ok, id)) { js.push(id); return; }
      var r = R.realOf(ok, id), k = r.c + r.n; if (!by[k]) { by[k] = []; order.push(k); } by[k].push(id);
    });
    order.forEach(function (k) { var a = by[k]; while (a.length >= 2) pairs.push({ kind: 'pair', tiles: [a.shift(), a.shift()] }); if (a.length) singles.push(a.shift()); });
    singles.sort(function (a, b) { return R.realOf(ok, b).n - R.realOf(ok, a).n || (a < b ? -1 : 1); });
    while (js.length && singles.length) pairs.push({ kind: 'pair', tiles: [singles.shift(), js.shift()] });
    if (js.length === 2) pairs.push({ kind: 'pair', tiles: [js.shift(), js.shift()] });
    return { pairs: pairs, left: singles.concat(js) };
  }

  function sumMelds(ok, melds) { var s = 0; melds.forEach(function (m) { s += R.analyzeMeld(ok, m.kind, m.tiles).value; }); return s; }
  function contains(melds, id) { for (var i = 0; i < melds.length; i++) if (melds[i].tiles.indexOf(id) >= 0) return true; return false; }

  // Istakada en az bir taş kalmalı (R-35): hiç taş kalmıyorsa en küçük peri ya da 4+'lık bir perin ucunu bırak.
  function keepOne(ok, melds, min, must) {
    var opts = [], i;
    for (i = 0; i < melds.length; i++) {
      if (must && melds[i].tiles.indexOf(must) >= 0) continue;
      var rest = melds.filter(function (x, j) { return j !== i; });
      if (rest.length && (min == null || score(rest) >= min)) opts.push(rest);
      var m = melds[i];
      if (m.tiles.length >= 4) {
        [m.tiles.slice(1), m.tiles.slice(0, -1)].forEach(function (t2) {
          if (must && m.tiles.indexOf(must) >= 0 && t2.indexOf(must) < 0) return;
          if (!R.analyzeMeld(ok, m.kind, t2).ok) return;
          var alt = melds.map(function (x, j) { return j === i ? { kind: m.kind, tiles: t2 } : x; });
          if (min == null || score(alt) >= min) opts.push(alt);
        });
      }
    }
    function score(ms) { return ms[0] && ms[0].kind === 'pair' ? ms.length : sumMelds(ok, ms); }
    opts.sort(function (a, b) { return score(b) - score(a); });
    return opts[0] || null;
  }

  function runPlan(ok, rack, must, need) {
    var b = bestMelds(ok, rack); if (!b.melds.length) return null;
    var ms = b.melds;
    if (b.left.length === 0) ms = keepOne(ok, ms, need, must);
    if (!ms || sumMelds(ok, ms) < need) return null;
    if (must && !contains(ms, must)) return null;
    return ms;
  }
  function pairPlan(ok, rack, must, need) {
    var b = bestPairs(ok, rack);
    if (b.pairs.length < need) return null;
    var ms = b.pairs;
    if (b.left.length === 0) {
      var di = -1; for (var i = ms.length - 1; i >= 0; i--) if (!must || ms[i].tiles.indexOf(must) < 0) { di = i; break; }
      if (di < 0) return null; ms = ms.filter(function (x, j) { return j !== di; });
    }
    if (ms.length < need) return null;
    if (must && !contains(ms, must)) return null;
    return ms;
  }
  function left(rack, ms) { var n = rack.length; ms.forEach(function (m) { n -= m.tiles.length; }); return n; }

  function openingPlan(S, seat, extra, must) {
    var H = S.hand, ok = H.okey, rack = H.racks[seat].concat(extra ? [extra] : []);
    var rp = runPlan(ok, rack, must, R.openNeed(S, seat, 'run')), pp = pairPlan(ok, rack, must, R.openNeed(S, seat, 'pair'));
    if (pp && left(rack, pp) <= 1 && !(rp && left(rack, rp) <= 1)) return pp;            // elden çift bitişi
    return rp || pp || null;
  }

  function wantSide(S, seat, t) {
    var H = S.hand, ok = H.okey, rack = H.racks[seat];
    if (!H.opened[seat]) return !!openingPlan(S, seat, t, t);
    if (H.openMode[seat] === 'pair') return rack.some(function (id) { return R.analyzeMeld(ok, 'pair', [id, t]).ok; });
    var a = bestMelds(ok, rack), b = bestMelds(ok, rack.concat([t]));
    if (b.value > a.value && contains(b.melds, t)) return true;
    return S.opts.isle && R.isIslek(S, t);
  }

  // Botun sıradaki tek eylemi. Sunucu bunu uygular, TIMING kadar bekler, tekrar sorar.
  function nextAction(S, seat) {
    var H = S.hand, T = H.turn, ok = H.okey, rack = H.racks[seat];
    if (T.phase === 'draw') {
      var pile = H.discards[R.leftOf(seat)];
      if (!T.sideReturned && pile.length && wantSide(S, seat, pile[pile.length - 1])) return { t: 'draw', seat: seat, src: 'side' };
      return { t: 'draw', seat: seat, src: 'deck' };
    }
    if (!H.opened[seat]) {
      var plan = openingPlan(S, seat, null, T.sideMust ? T.sideTake : null);
      if (plan) return { t: 'open', seat: seat, melds: plan };
      if (T.sideMust) return { t: 'returnSide', seat: seat };
      return { t: 'discard', seat: seat, tile: R.autoDiscardTile(S, seat) };
    }
    var lay = null;
    if (H.openMode[seat] === 'pair') { var bp = bestPairs(ok, rack); if (bp.pairs.length) { lay = bp.pairs; if (!bp.left.length) lay = lay.slice(0, -1); } }
    else { var bm = bestMelds(ok, rack); if (bm.melds.length) { lay = bm.melds; if (!bm.left.length) lay = keepOne(ok, lay, null, null); } }
    if (lay && lay.length) return { t: 'lay', seat: seat, melds: lay };
    if (S.opts.isle) {
      var i, m, ids = rack.slice().sort();
      if (rack.length >= 2) for (i = 0; i < ids.length; i++) {
        if (R.isOkey(ok, ids[i]) && rack.length > 2) continue;                              // okeyi yalnız bitişe yakın işle
        for (m = 0; m < H.melds.length; m++) { var sides = R.addSides(S, H.melds[m].id, ids[i]); if (sides.length) return { t: 'add', seat: seat, tile: ids[i], meld: H.melds[m].id, side: sides[0] }; }
      }
      for (m = 0; m < H.melds.length; m++) {
        var g = H.melds[m];
        for (var x = 0; x < g.tiles.length; x++) {
          if (!R.isOkey(ok, g.tiles[x])) continue;
          if (R.partnerOf(S, seat) === g.placedBy[x]) continue;                              // ortağın okeyini alma
          for (i = 0; i < ids.length; i++) if (R.swapFits(S, g.id, x, ids[i])) return { t: 'swap', seat: seat, tile: ids[i], meld: g.id, index: x };
        }
      }
    }
    return { t: 'discard', seat: seat, tile: R.autoDiscardTile(S, seat) };
  }

  function delayFor(prev, next) {
    if (next.t === 'draw') return TIMING.draw;
    if (next.t === 'discard') return prev && prev.t !== 'draw' ? TIMING.discardAfterMeld : TIMING.discard;
    return TIMING.meld;
  }
  function chatAfterDiscard(rng) {
    if (R.rngNext(rng) / 4294967296 >= CHAT.afterDiscard) return null;
    return CHAT.lines[R.rngInt(rng, CHAT.lines.length)];
  }

  // Dört botla tam maç (test ve denge için). Döner: { state, log }.
  function simulate(opts, seed, maxSteps) {
    var S = R.newMatch(opts, seed), log = [], r = R.apply(S, { t: 'startHand' }); S = r.state;
    var guard = maxSteps || 20000;
    while (guard-- > 0) {
      if (S.status === 'matchOver') break;
      if (S.status === 'handOver') { S = R.apply(S, { t: 'nextHand' }).state; continue; }
      var seat = S.hand.turn.seat, a = nextAction(S, seat), res = R.apply(S, a);
      if (!res.ok) { log.push({ fail: res.error, action: a }); res = R.apply(S, { t: 'timeout', seat: seat }); if (!res.ok) { log.push({ fatal: res.error }); break; } }
      log.push({ a: a.t, seat: seat, ev: res.events.map(function (e) { return e.e; }) });
      S = res.state;
    }
    return { state: S, log: log };
  }

  var API = { TIMING: TIMING, CHAT: CHAT, bestMelds: bestMelds, bestPairs: bestPairs, openingPlan: openingPlan, wantSide: wantSide, nextAction: nextAction, delayFor: delayFor, chatAfterDiscard: chatAfterDiscard, simulate: simulate };
  root.G101Bots = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof self !== 'undefined' ? self : this);
