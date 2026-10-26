/* Galaxy 101 · logic/rules.js · v0.4.0
 * Saf 101 Okey kural motoru. DOM, three.js, zamanlayıcı ve Math.random YOK.
 * Rastgelelik yalnız durumdaki seed'li RNG'den (mulberry32) gelir.
 * Arayüz ve mockServer yalnız bu modülü çağırır. C#'a satır satır çevrilecek şekilde yazıldı:
 * düz nesneler, diziler, string taş kimlikleri, saf fonksiyonlar. Kural kimlikleri (R-xx) RULES.md ile eşleşir.
 */
(function (root) {
  'use strict';

  var VERSION = '0.4.0';
  var COLORS = ['R', 'B', 'Y', 'K'];            // Kırmızı, Mavi, Sarı, Siyah
  var C = {
    OPEN_RUN_MIN: 101,        // R-20
    OPEN_PAIR_MIN: 5,         // R-21
    PENALTY: 101,             // R-40..R-44
    OPENED_WITH_X: 10,        // R-43
    NOT_OPENED: 202,          // R-51
    FINISH: -101,             // R-50
    OKEY_IN_HAND: 101,        // R-52
    STACKS: 15, STACK_H: 7, DECK: 20
  };

  function clone(o) { return JSON.parse(JSON.stringify(o)); }

  // ---------- RNG (mulberry32, uint32) ----------
  function rngNext(r) {
    r.s = (r.s + 0x6D2B79F5) >>> 0;
    var t = r.s;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return (t ^ (t >>> 14)) >>> 0;
  }
  function rngInt(r, n) { return rngNext(r) % n; }

  // ---------- Taşlar (R-01..R-05) ----------
  // Kimlik: renk + sayı + kopya ('R5a', 'R5b'); sahte okey 'Ja', 'Jb'. Her kimlik oyunda tam bir kez bulunur → 2 kopya sınırı yapısal.
  function allTileIds() {
    var a = [];
    for (var ci = 0; ci < 4; ci++) for (var n = 1; n <= 13; n++) { a.push(COLORS[ci] + n + 'a'); a.push(COLORS[ci] + n + 'b'); }
    a.push('Ja'); a.push('Jb');
    return a;
  }
  var ALL = allTileIds();
  var ALLSET = {}; for (var _i = 0; _i < ALL.length; _i++) ALLSET[ALL[_i]] = true;
  function isTile(id) { return typeof id === 'string' && ALLSET[id] === true; }
  function isFake(id) { return id.charAt(0) === 'J'; }
  function colorOf(id) { return id.charAt(0); }
  function numOf(id) { return isFake(id) ? 0 : parseInt(id.slice(1, id.length - 1), 10); }
  function codeOf(id) { return isFake(id) ? 'J' : id.slice(0, id.length - 1); }
  function okeyFromIndicator(ind) { return { c: colorOf(ind), n: (numOf(ind) % 13) + 1 }; }
  function isOkey(ok, id) { return !isFake(id) && colorOf(id) === ok.c && numOf(id) === ok.n; }
  function realOf(ok, id) { return isFake(id) ? { c: ok.c, n: ok.n } : { c: colorOf(id), n: numOf(id) }; }
  function handValueOf(ok, id) { return isOkey(ok, id) ? C.OKEY_IN_HAND : realOf(ok, id).n; }   // R-52

  // ---------- Per çözümleme (R-10..R-16) ----------
  // kind: 'run' (seri) | 'group' (grup) | 'pair' (çift). tiles sıralı; okeyin değeri konumundan çıkar.
  function analyzeMeld(ok, kind, tiles) {
    var BAD = { ok: false };
    if (!tiles || !tiles.length) return BAD;
    var L = tiles.length, reals = [], i;
    for (i = 0; i < L; i++) if (!isOkey(ok, tiles[i])) { var r = realOf(ok, tiles[i]); reals.push({ i: i, c: r.c, n: r.n }); }
    if (kind === 'pair') {
      if (L !== 2) return BAD;
      if (reals.length === 2 && (reals[0].c !== reals[1].c || reals[0].n !== reals[1].n)) return BAD;
      var pr = reals.length ? { c: reals[0].c, n: reals[0].n } : { c: ok.c, n: ok.n };
      return { ok: true, kind: 'pair', value: 0, repr: [pr, pr] };
    }
    if (!reals.length) return BAD;                                   // R-14
    if (kind === 'run') {
      if (L < 3 || L > 13) return BAD;
      var r0 = reals[0], st = r0.n - r0.i;                             // R-12 sarma yok: 13'ten sonra 1 gelmez
      if (st < 1 || st + L - 1 > 13) return BAD;
      for (i = 0; i < reals.length; i++) if (reals[i].c !== r0.c || reals[i].n !== st + reals[i].i) return BAD;
      var repr = [], val = 0;
      for (i = 0; i < L; i++) { repr.push({ c: r0.c, n: st + i }); val += st + i; }
      return { ok: true, kind: 'run', value: val, c: r0.c, start: st, end: st + L - 1, repr: repr };
    }
    if (kind === 'group') {
      if (L < 3 || L > 4) return BAD;
      var seen = {};
      for (i = 0; i < reals.length; i++) { if (reals[i].n !== reals[0].n || seen[reals[i].c]) return BAD; seen[reals[i].c] = true; }
      var miss = COLORS.filter(function (c) { return !seen[c]; }), repr2 = [], mi = 0;
      for (i = 0; i < L; i++) repr2.push(isOkey(ok, tiles[i]) ? { c: miss[mi++], n: reals[0].n } : { c: realOf(ok, tiles[i]).c, n: reals[0].n });
      return { ok: true, kind: 'group', value: reals[0].n * L, n: reals[0].n, repr: repr2 };
    }
    return BAD;
  }

  // ---------- Maç ----------
  function newMatch(opts, seed) {
    var o = opts || {};
    return {
      v: VERSION,
      opts: { mode: o.mode === 'pairs' ? 'pairs' : 'solo', hands: o.hands || 5, penalties: o.penalties !== false, isle: o.isle !== false, katlama: !!o.katlama, katlamaPartner: o.katlamaPartner !== false },
      seed: seed >>> 0, rng: { s: seed >>> 0 },
      status: 'lobby', handNo: 0, dealer: -1,
      totals: [0, 0, 0, 0], history: [],
      seats: [0, 1, 2, 3].map(function () { return { bot: false, left: false, leftSeq: 0 }; }),
      leaveSeq: 0, hand: null, ranking: null
    };
  }

  function blankHand() {
    return {
      indicator: null, okey: null, deck: [], racks: [[], [], [], []], discards: [[], [], [], []],
      melds: [], meldSeq: 0, opened: [0, 0, 0, 0], openMode: [null, null, null, null], openedTurn: [-1, -1, -1, -1],
      pen: [0, 0, 0, 0], penLog: [], finishTile: null, out: [],
      turn: null, deal: null, result: null
    };
  }
  function newTurn(seat, phase, n) { return { seat: seat, phase: phase, n: n, sideTake: null, sideMust: false, sideReturned: false, snap: null, pending: [], drawn: null }; }

  // R-02..R-05 Dağıtım töreni. rig: yalnız test vektörleri için {order, dealer, dice}.
  function dealHand(S, rig) {
    var H = blankHand();
    var D = rig && rig.dealer != null ? rig.dealer : (S.handNo === 1 ? rngInt(S.rng, 4) : (S.dealer + 1) % 4);
    var ids;
    if (rig && rig.order) ids = rig.order.slice();
    else { ids = ALL.slice(); for (var i = ids.length - 1; i > 0; i--) { var j = rngInt(S.rng, i + 1), t = ids[i]; ids[i] = ids[j]; ids[j] = t; } }
    var stacks = [], si, k;
    for (si = 0; si < C.STACKS; si++) stacks.push(ids.slice(si * 7, si * 7 + 7));           // lv 0 = alt, 6 = üst
    var initialStacks = clone(stacks), extra = ids[105];
    var a = rig && rig.dice ? rig.dice[0] : 1 + rngInt(S.rng, 6);
    var b = rig && rig.dice ? rig.dice[1] : 1 + rngInt(S.rng, 6);
    var ind = a - 1, lv = b - 1, st = stacks[ind], lvF = lv;
    while (lvF < 7 && isFake(st[lvF])) lvF++;                                                // R-04 sahte okey gösterge olamaz: üstteki
    if (lvF === 7) { lvF = lv - 1; while (lvF >= 0 && isFake(st[lvF])) lvF--; }                // üstte yoksa alttaki
    var indicator = st.splice(lvF, 1)[0];
    var s1 = (ind + 1) % 15; stacks[s1].push(extra);                                           // R-03 fazla taş
    var S_ = function (x) { return (ind + x) % 15; }, plan = [];
    for (k = 0; k < 4; k++) plan.push({ seat: (D + 1 + k) % 4, stacks: [S_(1 + k), S_(5 + k)] });
    for (k = 0; k < 4; k++) plan.push({ seat: (D + 1 + k) % 4, stacks: [S_(9 + k)] });
    plan.forEach(function (p) { p.stacks.forEach(function (x) { var top = stacks[x].slice().reverse(); for (var q = 0; q < top.length; q++) H.racks[p.seat].push(top[q]); }); });
    var c13 = S_(13), c14 = S_(14), right = Math.max(c13, c14), left = Math.min(c13, c14);
    H.deck = stacks[ind].concat(stacks[right], stacks[left]);                                 // R-05 deste: H.deck[0] ilk çekilen
    H.indicator = indicator; H.okey = okeyFromIndicator(indicator);
    H.deal = { dealer: D, order: ids, stacks: initialStacks, extra: extra, dice: [a, b], indStack: ind, indLevel: lvF, rolledLevel: lv, fakeSkip: lvF !== lv, extraStack: s1, plan: plan, centerStacks: [ind, right, left] };
    var starter = (D + 1) % 4;
    H.turn = newTurn(starter, 'play', 1);                                                       // R-06 22 taşlı oyuncu çekmeden atar
    S.dealer = D;
    return H;
  }

  // Senaryo: istenen ıstakalardan dealHand'in tersini kurar → {dealer, dice, order}. Dağıtım töreni gerçek kalır (aynı balyalar, aynı zar).
  // sc = {dealer, dice:[a,b], indicator, racks:[[...],[...],[...],[...]] (eksikler artanlarla dolar), deckTop:[...] (desteden ilk çekilecekler)}
  function rigFromHands(sc) {
    var D = sc.dealer != null ? sc.dealer : 3, a = sc.dice ? sc.dice[0] : 1, b = sc.dice ? sc.dice[1] : 1, ind = a - 1, lv = b - 1;
    var used = {}, take = function (id) { if (!isTile(id)) throw new Error('E_SPEC_TILE ' + id); if (used[id]) throw new Error('E_SPEC_DUP ' + id); used[id] = true; return id; };
    if (!sc.indicator || isFake(sc.indicator)) throw new Error('E_SPEC_IND');
    take(sc.indicator);
    var racks = [0, 1, 2, 3].map(function (k) { return ((sc.racks && sc.racks[k]) || []).map(take); });
    var deckTop = (sc.deckTop || []).map(take);
    var rest = ALL.filter(function (id) { return !used[id]; });
    rest.sort(function (x, y) { var hx = (numOf(x) * 7 + COLORS.indexOf(colorOf(x)) * 5) % 13, hy = (numOf(y) * 7 + COLORS.indexOf(colorOf(y)) * 5) % 13; return hx - hy || (x < y ? -1 : 1); });
    var starter = (D + 1) % 4;
    racks.forEach(function (r, k) { var need = k === starter ? 22 : 21; if (r.length > need) throw new Error('E_SPEC_RACK ' + k); while (r.length < need) r.push(rest.shift()); });
    var S_ = function (x) { return (ind + x) % 15; }, stacks = [], extra = null, k, i;
    for (i = 0; i < 15; i++) stacks.push(null);
    for (k = 0; k < 4; k++) {
      var seat = (D + 1 + k) % 4, r = racks[seat], p = 0, first = S_(1 + k);
      if (first === S_(1)) { extra = r[p++]; }
      stacks[first] = r.slice(p, p + 7).reverse(); p += 7;
      stacks[S_(5 + k)] = r.slice(p, p + 7).reverse(); p += 7;
      stacks[S_(9 + k)] = r.slice(p, p + 7).reverse();
    }
    var deck = deckTop.concat(rest);
    if (deck.length !== 6 + 7 + 7) throw new Error('E_SPEC_COUNT ' + deck.length);
    var c13 = S_(13), c14 = S_(14), right = Math.max(c13, c14), left = Math.min(c13, c14);
    var indSt = deck.slice(0, 6); indSt.splice(lv, 0, sc.indicator);
    stacks[ind] = indSt; stacks[right] = deck.slice(6, 13); stacks[left] = deck.slice(13, 20);
    var order = []; stacks.forEach(function (s) { order = order.concat(s); }); order.push(extra);
    return { dealer: D, dice: [a, b], order: order };
  }

  function startHand(S, rig, ev) {
    if (!(S.status === 'lobby' || S.status === 'handOver')) return err('E_STATUS');
    S.handNo += 1;
    S.hand = dealHand(S, rig);
    S.status = 'playing';
    var H = S.hand;
    ev.push({ e: 'handStart', handNo: S.handNo, dealer: H.deal.dealer, dice: H.deal.dice, indicator: H.indicator, okey: H.okey, starter: H.turn.seat, deck: H.deck.length, counts: H.racks.map(function (r) { return r.length; }) });
    ev.push({ e: 'turn', seat: H.turn.seat, phase: H.turn.phase, n: H.turn.n });
    return null;
  }

  // Testler için doğrudan el kurma. Kimlik tekrarı → E_SPEC_DUP.
  function fromSpec(sp) {
    var S = newMatch(sp.opts || {}, sp.seed || 1);
    S.status = 'playing'; S.handNo = sp.handNo || 1; S.dealer = sp.dealer != null ? sp.dealer : 3;
    if (sp.totals) S.totals = sp.totals.slice();
    if (sp.seats) S.seats = clone(sp.seats);
    var H = blankHand(), used = {};
    function take(id) { if (!isTile(id)) throw new Error('E_SPEC_TILE ' + id); if (used[id]) throw new Error('E_SPEC_DUP ' + id); used[id] = true; return id; }
    H.indicator = take(sp.indicator); H.okey = okeyFromIndicator(H.indicator);
    var k;
    for (k = 0; k < 4; k++) H.racks[k] = ((sp.racks && sp.racks[k]) || []).map(take);
    for (k = 0; k < 4; k++) H.discards[k] = ((sp.discards && sp.discards[k]) || []).map(take);
    (sp.melds || []).forEach(function (m) {
      var tiles = m.tiles.map(take), a = analyzeMeld(H.okey, m.kind, tiles);
      if (!a.ok) throw new Error('E_SPEC_MELD ' + tiles.join(','));
      H.melds.push({ id: 'm' + (++H.meldSeq), owner: m.owner, kind: m.kind, tiles: tiles, placedBy: tiles.map(function () { return m.owner; }) });
    });
    if (sp.deck) H.deck = sp.deck.map(take);
    var rest = ALL.filter(function (id) { return !used[id]; });
    if (!sp.deck) { var n = sp.deckCount != null ? sp.deckCount : C.DECK; H.deck = rest.slice(0, n); rest = rest.slice(n); }
    H.out = rest;                                                                               // yalnız test: oyunda olmayan taşlar
    if (sp.opened) H.opened = sp.opened.slice();
    if (sp.openMode) H.openMode = sp.openMode.slice();
    for (k = 0; k < 4; k++) if (H.opened[k] && !H.openMode[k]) H.openMode[k] = 'run';
    for (k = 0; k < 4; k++) H.openedTurn[k] = H.opened[k] ? 0 : -1;
    if (sp.pen) H.pen = sp.pen.slice();
    var t = sp.turn || {};
    H.turn = newTurn(t.seat || 0, t.phase || 'draw', t.n || 5);
    S.hand = H;
    return S;
  }

  // ---------- Yardımcılar ----------
  function err(code, extra) { var e = { error: code }; if (extra) for (var k in extra) e[k] = extra[k]; return e; }
  function idxOf(arr, id) { for (var i = 0; i < arr.length; i++) if (arr[i] === id) return i; return -1; }
  function removeFrom(arr, id) { var i = idxOf(arr, id); if (i >= 0) arr.splice(i, 1); return i >= 0; }
  function meldById(H, id) { for (var i = 0; i < H.melds.length; i++) if (H.melds[i].id === id) return H.melds[i]; return null; }
  function partnerOf(S, k) { return S.opts.mode === 'pairs' ? (k + 2) % 4 : -1; }
  function leftOf(k) { return (k + 3) % 4; }     // R-30 soldaki = bir önceki oyuncu
  function nextOf(k) { return (k + 1) % 4; }

  function addPenalty(S, p, ev) {
    if (!S.opts.penalties) return;                                                              // R-62
    var H = S.hand; H.pen[p.seat] += p.amount; H.penLog.push(p);
    ev.push({ e: 'penalty', seat: p.seat, code: p.code, amount: p.amount, by: p.by, tile: p.tile, total: H.pen[p.seat] });
  }
  function snap(H) {
    var T = H.turn; if (T.snap) return;
    var k = T.seat;
    T.snap = { rack: H.racks[k].slice(), melds: clone(H.melds), meldSeq: H.meldSeq, opened: H.opened[k], openMode: H.openMode[k], openedTurn: H.openedTurn[k], pending: clone(T.pending), sideMust: T.sideMust };
  }
  function restore(H) {
    var T = H.turn, p = T.snap, k = T.seat; if (!p) return false;
    H.racks[k] = p.rack; H.melds = p.melds; H.meldSeq = p.meldSeq; H.opened[k] = p.opened; H.openMode[k] = p.openMode; H.openedTurn[k] = p.openedTurn; T.pending = p.pending; T.sideMust = p.sideMust; T.snap = null;
    return true;
  }

  // R-41 işlek: masadaki herhangi bir pere eklenebilen ya da bir okeyin yerine geçebilen taş.
  function addSides(H, meld, tile) {
    var out = [];
    if (meld.kind === 'pair') return out;
    if (meld.kind === 'group') { if (groupKeepsOkeys(H.okey, meld.tiles, meld.tiles.concat([tile]))) out.push('post'); return out; }
    if (analyzeMeld(H.okey, 'run', [tile].concat(meld.tiles)).ok) out.push('pre');
    if (analyzeMeld(H.okey, 'run', meld.tiles.concat([tile])).ok) out.push('post');
    return out;
  }
  function swapFits(H, meld, index, tile) {
    if (meld.kind === 'pair' || isOkey(H.okey, tile) || !isOkey(H.okey, meld.tiles[index])) return false;
    var t2 = meld.tiles.slice(); t2[index] = tile;
    if (meld.kind === 'group') {                                                         // R08/R09: yalnız okeyin temsil ettiği taş; diğer okey yüzünü korur
      var was = analyzeMeld(H.okey, 'group', meld.tiles), real = realOf(H.okey, tile);
      if (!was.ok || was.repr[index].c !== real.c || was.repr[index].n !== real.n) return false;
      return groupKeepsOkeys(H.okey, meld.tiles, t2);
    }
    return analyzeMeld(H.okey, meld.kind, t2).ok;
  }
  // R08: gruptaki okey masaya konduğu andaki yüzünü korur; onu başka renge çevirecek değişiklik reddedilir.
  function groupKeepsOkeys(ok, before, after) {
    var a = analyzeMeld(ok, 'group', after); if (!a.ok) return false;
    var b = analyzeMeld(ok, 'group', before); if (!b.ok) return true;
    for (var i = 0; i < before.length; i++)
      if (isOkey(ok, before[i]) && isOkey(ok, after[i]) && (a.repr[i].c !== b.repr[i].c || a.repr[i].n !== b.repr[i].n)) return false;
    return true;
  }
  function isIslek(H, tile) {
    for (var m = 0; m < H.melds.length; m++) {
      var g = H.melds[m]; if (g.kind === 'pair') continue;
      if (addSides(H, g, tile).length) return true;
      for (var i = 0; i < g.tiles.length; i++) if (swapFits(H, g, i, tile)) return true;
    }
    return false;
  }
  function tileValueInMeld(H, meld, id) {
    var a = analyzeMeld(H.okey, meld.kind, meld.tiles), i = idxOf(meld.tiles, id);
    if (a.ok && i >= 0 && a.repr) return a.repr[i].n;
    return realOf(H.okey, id).n;
  }

  // R-71 zaman aşımında atılacak taş: okey/işlek/yerden alınan dışında, ıstakada en az bağlantısı olan; eşitse değeri yüksek; eşitse kimliği küçük.
  function autoDiscardTile(S, seat) {
    var H = S.hand, rack = H.racks[seat], ok = H.okey, T = H.turn, best = null;
    function conn(id) {
      if (isOkey(ok, id)) return 99;
      var r = realOf(ok, id), n = 0;
      for (var i = 0; i < rack.length; i++) {
        var o = rack[i]; if (o === id) continue;
        if (isOkey(ok, o)) { n++; continue; }
        var q = realOf(ok, o);
        if (q.c === r.c && q.n !== r.n && Math.abs(q.n - r.n) <= 2) n++;
        else if (q.n === r.n && q.c !== r.c) n++;
      }
      return n;
    }
    function rank(id) {
      var tier = 0;
      if (isOkey(ok, id)) tier = 3; else if (S.opts.isle && isIslek(H, id)) tier = 2;
      return [tier, conn(id), -handValueOf(ok, id), id];
    }
    function less(a, b) { for (var i = 0; i < 3; i++) if (a[i] !== b[i]) return a[i] < b[i]; return a[3] < b[3]; }
    for (var i = 0; i < rack.length; i++) {
      var id = rack[i]; if (id === T.sideTake) continue;
      var r = rank(id); if (!best || less(r, best.r)) best = { id: id, r: r };
    }
    return best ? best.id : null;
  }

  // ---------- Eylemler ----------
  // apply(S, action) → { ok, error, state, events }. Girdi değişmez; dönen state yeni nesnedir.
  function apply(S0, a) {
    var S = clone(S0), ev = [];
    var r = step(S, a, ev);
    if (r && r.error) return { ok: false, error: r.error, detail: r, state: S0, events: [] };
    return { ok: true, error: null, state: S, events: ev };
  }

  function step(S, a, ev) {
    if (!a || !a.t) return err('E_ACTION');
    if (a.t === 'startHand' || a.t === 'nextHand') return startHand(S, a.rig, ev);
    if (a.t === 'leave') return doLeave(S, a, ev);
    if (a.t === 'takeSeat') return doTakeSeat(S, a, ev);
    if (S.status !== 'playing') return err('E_HAND_OVER');
    var H = S.hand, T = H.turn;
    if (a.seat !== T.seat) return err('E_NOT_YOUR_TURN');
    switch (a.t) {
      case 'draw': return doDraw(S, a, ev);
      case 'returnSide': return doReturnSide(S, ev);
      case 'open': return doOpen(S, a, ev);
      case 'lay': return doLay(S, a, ev);
      case 'add': return doAdd(S, a, ev);
      case 'swap': return doSwap(S, a, ev);
      case 'undo': return doUndo(S, ev);
      case 'discard': return doDiscard(S, a, ev);
      case 'timeout': return doTimeout(S, ev);
    }
    return err('E_ACTION');
  }

  // R-30..R-33
  function doDraw(S, a, ev) {
    var H = S.hand, T = H.turn, k = T.seat;
    if (T.phase !== 'draw') return err('E_PHASE');
    var id;
    if (a.src === 'side') {
      if (T.sideReturned) return err('E_SIDE_RETURNED');                                     // R-33
      var pile = H.discards[leftOf(k)]; if (!pile.length) return err('E_SIDE_EMPTY');
      id = pile.pop();
      T.sideTake = id; T.sideMust = !H.opened[k];                                              // R-31
    } else {
      if (!H.deck.length) return err('E_DECK_EMPTY');
      id = H.deck.shift();
    }
    H.racks[k].push(id); T.drawn = id; T.phase = 'play';
    ev.push({ e: 'drew', seat: k, src: a.src === 'side' ? 'side' : 'deck', tile: id, from: a.src === 'side' ? leftOf(k) : null, deck: H.deck.length, mustOpen: T.sideMust });
    return null;
  }

  // R-32 geri bırak: turdaki tüm işlemler geri alınır, taş yığına döner, oyuncu yalnız desteden çekebilir.
  function doReturnSide(S, ev) {
    var H = S.hand, T = H.turn, k = T.seat;
    if (T.phase !== 'play' || !T.sideTake) return err('E_RETURN_INVALID');
    restore(H);
    if (!removeFrom(H.racks[k], T.sideTake)) return err('E_RETURN_INVALID');
    H.discards[leftOf(k)].push(T.sideTake);
    ev.push({ e: 'sideReturned', seat: k, tile: T.sideTake, to: leftOf(k) });
    T.sideTake = null; T.sideMust = false; T.sideReturned = true; T.phase = 'draw'; T.drawn = null; T.pending = []; T.snap = null;
    return null;
  }

  function checkMelds(H, k, melds) {
    if (!melds || !melds.length) return err('E_MELD_INVALID', { index: -1 });
    var rack = H.racks[k], seen = {}, out = [], kinds = {};
    for (var m = 0; m < melds.length; m++) {
      var g = melds[m];
      for (var i = 0; i < g.tiles.length; i++) {
        var id = g.tiles[i];
        if (idxOf(rack, id) < 0) return err('E_TILE_NOT_IN_RACK', { tile: id });
        if (seen[id]) return err('E_MELD_INVALID', { index: m });
        seen[id] = true;
      }
      var an = analyzeMeld(H.okey, g.kind, g.tiles);
      if (!an.ok) return err('E_MELD_INVALID', { index: m });
      kinds[g.kind === 'pair' ? 'pair' : 'run'] = true;
      out.push(an);
    }
    if (kinds.pair && kinds.run) return err('E_MELD_MIX');
    var used = Object.keys(seen).length;
    if (rack.length - used < 1) return err('E_LAST_TILE');                                       // R-35
    return { list: out, mode: kinds.pair ? 'pair' : 'run', used: seen };
  }
  function placeMelds(H, k, melds) {
    var ids = [];
    melds.forEach(function (g) {
      var m = { id: 'm' + (++H.meldSeq), owner: k, kind: g.kind, tiles: g.tiles.slice(), placedBy: g.tiles.map(function () { return k; }) };
      H.melds.push(m); ids.push(m.id);
      g.tiles.forEach(function (id) { removeFrom(H.racks[k], id); });
    });
    return ids;
  }

  // R-20, R-21, R-64 açılış eşiği. Katlamalı: aynı türde önceki açılışın değeri + 1 (seri ve çift bağımsız).
  // Eşlide katlamaPartner kapalıysa yalnız rakip takımın açılışları eşiği yükseltir.
  function openNeed(S, k, mode) {
    var H = S.hand, need = mode === 'pair' ? C.OPEN_PAIR_MIN : C.OPEN_RUN_MIN;
    if (!S.opts.katlama) return need;
    for (var j = 0; j < 4; j++) {
      if (j === k || !H.opened[j] || H.openMode[j] !== mode) continue;
      if (S.opts.mode === 'pairs' && !S.opts.katlamaPartner && j === partnerOf(S, k)) continue;
      if (H.opened[j] + 1 > need) need = H.opened[j] + 1;
    }
    return need;
  }

  // R-20..R-24, R-53
  function doOpen(S, a, ev) {
    var H = S.hand, T = H.turn, k = T.seat;
    if (T.phase !== 'play') return err('E_PHASE');
    if (H.opened[k]) return err('E_ALREADY_OPENED');
    var c = checkMelds(H, k, a.melds); if (c.error) return c;
    if (T.sideMust && !c.used[T.sideTake]) return err('E_SIDE_MUST_OPEN');                       // R-31
    var total = 0;
    if (c.mode === 'pair') total = a.melds.length; else c.list.forEach(function (x) { total += x.value; });
    var need = openNeed(S, k, c.mode);                                                           // R-53 elden bitişte de eşik aranır
    if (total < need) {                                                                         // R-23 eksik açılış
      ev.push({ e: 'openShort', seat: k, mode: c.mode, value: total, need: need, tiles: Object.keys(c.used) });
      addPenalty(S, { seat: k, code: 'P_OPEN_SHORT', amount: C.PENALTY, by: k, tile: null, value: total }, ev);
      return null;
    }
    snap(H);
    var ids = placeMelds(H, k, a.melds);
    H.opened[k] = total; H.openMode[k] = c.mode; H.openedTurn[k] = T.n;
    if (T.sideTake && c.used[T.sideTake] && T.sideMust) {                                        // R-43 açtıran taş
      var m = null; for (var i = 0; i < H.melds.length; i++) if (idxOf(H.melds[i].tiles, T.sideTake) >= 0) m = H.melds[i];
      var v = tileValueInMeld(H, m, T.sideTake);
      T.pending.push({ seat: leftOf(k), code: 'P_OPENED_WITH', amount: v * C.OPENED_WITH_X, by: k, tile: T.sideTake, value: v });
    }
    T.sideMust = false;
    ev.push({ e: 'opened', seat: k, mode: c.mode, value: total, melds: ids.map(function (id) { return clone(meldById(H, id)); }) });
    return null;
  }

  // R-24 yere aç: seri açan seri/grup ve çift açabilir (açtığı turda da, sonraki turlarda da); çift açan yalnız çift açar
  function doLay(S, a, ev) {
    var H = S.hand, T = H.turn, k = T.seat;
    if (T.phase !== 'play') return err('E_PHASE');
    if (!H.opened[k]) return err('E_NOT_OPENED');
    var c = checkMelds(H, k, a.melds); if (c.error) return c;
    if (H.openMode[k] === 'pair' && c.mode !== 'pair') return err('E_OPEN_MODE');
    snap(H);
    var ids = placeMelds(H, k, a.melds);
    ev.push({ e: 'laid', seat: k, melds: ids.map(function (id) { return clone(meldById(H, id)); }) });
    return null;
  }

  // R-25 işle
  function doAdd(S, a, ev) {
    var H = S.hand, T = H.turn, k = T.seat;
    if (T.phase !== 'play') return err('E_PHASE');
    if (!S.opts.isle) return err('E_ISLE_DISABLED');
    if (!H.opened[k]) return err('E_NOT_OPENED');
    if (idxOf(H.racks[k], a.tile) < 0) return err('E_TILE_NOT_IN_RACK', { tile: a.tile });
    if (H.racks[k].length < 2) return err('E_LAST_TILE');
    var m = meldById(H, a.meld); if (!m) return err('E_MELD_NOT_FOUND');
    var sides = addSides(H, m, a.tile);
    var side = m.kind === 'group' ? 'post' : a.side;
    if (idxOf(sides, side) < 0) return err('E_ISLE_INVALID');
    snap(H);
    removeFrom(H.racks[k], a.tile);
    if (side === 'pre') { m.tiles.unshift(a.tile); m.placedBy.unshift(k); } else { m.tiles.push(a.tile); m.placedBy.push(k); }
    ev.push({ e: 'added', seat: k, tile: a.tile, meld: m.id, side: side, owner: m.owner });
    return null;
  }

  // R-26 okey alma
  function doSwap(S, a, ev) {
    var H = S.hand, T = H.turn, k = T.seat;
    if (T.phase !== 'play') return err('E_PHASE');
    if (!S.opts.isle) return err('E_ISLE_DISABLED');
    if (!H.opened[k]) return err('E_NOT_OPENED');
    if (idxOf(H.racks[k], a.tile) < 0) return err('E_TILE_NOT_IN_RACK', { tile: a.tile });
    var m = meldById(H, a.meld); if (!m) return err('E_MELD_NOT_FOUND');
    if (!swapFits(H, m, a.index, a.tile)) return err('E_SWAP_INVALID', { need: isOkey(H.okey, m.tiles[a.index]) ? analyzeMeld(H.okey, m.kind, m.tiles).repr[a.index] : null });
    snap(H);
    var okeyId = m.tiles[a.index], owner = m.placedBy[a.index];
    m.tiles[a.index] = a.tile; m.placedBy[a.index] = k;
    removeFrom(H.racks[k], a.tile); H.racks[k].push(okeyId);
    if (owner !== k) T.pending.push({ seat: owner, code: 'P_OKEY_TAKEN', amount: C.PENALTY, by: k, tile: okeyId });   // R-44
    ev.push({ e: 'okeySwapped', seat: k, tile: a.tile, okey: okeyId, meld: m.id, index: a.index, owner: owner });
    return null;
  }

  // R-34 geri topla
  function doUndo(S, ev) {
    var H = S.hand, T = H.turn;
    if (T.phase !== 'play' || !T.snap) return err('E_NO_UNDO');
    restore(H);
    ev.push({ e: 'undone', seat: T.seat });
    return null;
  }

  // R-36..R-38, R-40..R-42
  function doDiscard(S, a, ev) {
    var H = S.hand, T = H.turn, k = T.seat, ok = H.okey;
    if (T.phase !== 'play') return err('E_PHASE');
    if (idxOf(H.racks[k], a.tile) < 0) return err('E_TILE_NOT_IN_RACK', { tile: a.tile });
    if (T.sideTake && a.tile === T.sideTake) return err('E_SIDE_DISCARD');                       // R-32
    if (T.sideMust && idxOf(H.racks[k], T.sideTake) >= 0) return err('E_SIDE_MUST_OPEN');      // R-31
    removeFrom(H.racks[k], a.tile);
    if (!H.racks[k].length) {
      if (!H.opened[k]) return err('E_NOT_OPENED');
      commitPending(S, ev);
      H.finishTile = a.tile;
      ev.push({ e: 'finished', seat: k, tile: a.tile });
      return endHand(S, k, ev);
    }
    H.discards[k].push(a.tile);
    ev.push({ e: 'discarded', seat: k, tile: a.tile });
    if (isOkey(ok, a.tile)) addPenalty(S, { seat: k, code: 'P_OKEY_DISCARD', amount: C.PENALTY, by: k, tile: a.tile }, ev);      // R-42
    else if (S.opts.isle && isIslek(H, a.tile)) addPenalty(S, { seat: k, code: 'P_ISLEK', amount: C.PENALTY, by: k, tile: a.tile }, ev);  // R-41
    commitPending(S, ev);
    if (!H.deck.length) return endHand(S, -1, ev);                                               // R-55 taş bitti
    var n = nextOf(k);
    H.turn = newTurn(n, 'draw', T.n + 1);
    ev.push({ e: 'turn', seat: n, phase: 'draw', n: T.n + 1 });
    return null;
  }
  function commitPending(S, ev) { var T = S.hand.turn; T.pending.forEach(function (p) { addPenalty(S, p, ev); }); T.pending = []; T.snap = null; }

  // R-70..R-71 zaman aşımı otomatik oyun
  function doTimeout(S, ev) {
    var H = S.hand, T = H.turn, k = T.seat, r;
    ev.push({ e: 'auto', seat: k, phase: T.phase });
    if (T.phase === 'play' && T.sideMust && idxOf(H.racks[k], T.sideTake) >= 0) { r = doReturnSide(S, ev); if (r) return r; }
    if (H.turn.phase === 'draw') { r = doDraw(S, { src: 'deck' }, ev); if (r) return r; }
    var id = autoDiscardTile(S, k);
    if (id == null && H.turn.sideTake != null) {                                         // yalnız yandan alınan taş kaldı (R-32 atılamaz): geri ver, desteden çek
      r = doReturnSide(S, ev); if (r) return r;
      r = doDraw(S, { src: 'deck' }, ev); if (r) return r;
      id = autoDiscardTile(S, k);
    }
    return doDiscard(S, { tile: id }, ev);
  }

  // R-65 devralma: botun ya da kalkan oyuncunun koltuğu yeni oyuncuya geçer; koltuğun toplamıyla devam eder, "kalktı" işareti kalkar.
  function doTakeSeat(S, a, ev) {
    var st = S.seats[a.seat]; if (!st || !st.bot) return err('E_SEAT_NOT_BOT');
    if (S.status === 'matchOver') return err('E_STATUS');
    st.bot = false; st.left = false; st.leftSeq = 0;
    ev.push({ e: 'seatTaken', seat: a.seat, total: S.totals[a.seat] });
    return null;
  }

  // R-60 masadan kalkma
  function doLeave(S, a, ev) {
    var st = S.seats[a.seat]; if (!st || st.left) return err('E_LEAVE_INVALID');
    st.left = true; st.bot = true; st.leftSeq = ++S.leaveSeq;
    ev.push({ e: 'seatLeft', seat: a.seat, botTakesOver: S.status === 'playing' || S.status === 'handOver' });
    return null;
  }

  // ---------- Puanlama (R-50..R-57) ----------
  function endHand(S, fin, ev) {
    var H = S.hand, ok = H.okey, k, res = { reason: fin >= 0 ? 'finish' : 'deckOut', finisher: fin, elden: false, okeyFinish: false, pairFinish: false, mul: 1, base: [0, 0, 0, 0], pairX: [1, 1, 1, 1], pen: H.pen.slice(), score: [0, 0, 0, 0], teamScore: null };
    if (fin >= 0) {
      res.elden = H.openedTurn[fin] === H.turn.n;                                                  // R-53
      res.okeyFinish = isOkey(ok, H.finishTile);                                                   // R-54
      res.pairFinish = H.openMode[fin] === 'pair';                                                 // R-54
      res.mul = (res.elden ? 2 : 1) * (res.okeyFinish ? 2 : 1) * (res.pairFinish ? 2 : 1);
      for (k = 0; k < 4; k++) {
        if (k === fin) { res.base[k] = C.FINISH; res.score[k] = C.FINISH * res.mul + H.pen[k]; continue; }
        if (k === partnerOf(S, fin)) { res.base[k] = 0; res.score[k] = H.pen[k]; continue; }    // R-57
        var base = C.NOT_OPENED;
        if (H.opened[k]) { base = 0; for (var i = 0; i < H.racks[k].length; i++) base += handValueOf(ok, H.racks[k][i]); }
        res.base[k] = base;
        res.pairX[k] = H.openMode[k] === 'pair' ? 2 : 1;                                           // R-56
        res.score[k] = base * res.mul * res.pairX[k] + H.pen[k];                                   // R-58 ceza çarpılmaz
      }
    } else {
      for (k = 0; k < 4; k++) {                                                                    // R-55 taş bitti: herkes ıstakasını yazar, çarpan yok, −101 yok
        var b2 = C.NOT_OPENED;
        if (H.opened[k]) { b2 = 0; for (var i2 = 0; i2 < H.racks[k].length; i2++) b2 += handValueOf(ok, H.racks[k][i2]); }
        res.base[k] = b2; res.pairX[k] = H.openMode[k] === 'pair' ? 2 : 1;
        res.score[k] = b2 * res.pairX[k] + H.pen[k];
      }
    }
    if (S.opts.mode === 'pairs') res.teamScore = [res.score[0] + res.score[2], res.score[1] + res.score[3]];
    for (k = 0; k < 4; k++) S.totals[k] += res.score[k];
    H.result = res; H.turn.phase = 'over';
    S.history.push({ handNo: S.handNo, score: res.score.slice(), reason: res.reason, finisher: fin, mul: res.mul });
    ev.push({ e: 'handEnd', handNo: S.handNo, result: clone(res), totals: S.totals.slice(), racks: clone(H.racks) });
    if (S.handNo >= S.opts.hands) { S.status = 'matchOver'; S.ranking = ranking(S); ev.push({ e: 'matchEnd', ranking: clone(S.ranking), totals: S.totals.slice() }); }
    else S.status = 'handOver';
    return null;
  }

  // R-59 sıralama: düşük toplam önde; eşitlik aynı sıra (1,1,3); kalkanlar en sonda, geç kalkan önde.
  function ranking(S) {
    var units = [];
    if (S.opts.mode === 'pairs') [[0, 2], [1, 3]].forEach(function (t, i) {
      var lq = Math.max(S.seats[t[0]].leftSeq, S.seats[t[1]].leftSeq);
      units.push({ team: i, seats: t, total: S.totals[t[0]] + S.totals[t[1]], left: false, leftSeq: 0, leavers: t.filter(function (x) { return S.seats[x].left; }) });   // R-60: eşlide takım son sayılmaz
    });
    else for (var k = 0; k < 4; k++) units.push({ seats: [k], total: S.totals[k], left: S.seats[k].left, leftSeq: S.seats[k].leftSeq });
    units.sort(function (a, b) {
      if (a.left !== b.left) return a.left ? 1 : -1;
      if (a.left) return b.leftSeq - a.leftSeq;
      if (a.total !== b.total) return a.total - b.total;
      return a.seats[0] - b.seats[0];
    });
    for (var i = 0; i < units.length; i++) {
      var u = units[i], p = units[i - 1];
      u.rank = i > 0 && !u.left && !p.left && p.total === u.total ? p.rank : i + 1;
    }
    return units;
  }

  // ---------- Görünüm yardımcıları (arayüz/sunucu) ----------
  function legal(S, seat) {
    var out = { draw: false, side: false, discard: false, open: false, undo: false, returnSide: false, add: false };
    if (S.status !== 'playing') return out;
    var H = S.hand, T = H.turn; if (T.seat !== seat) return out;
    if (T.phase === 'draw') { out.draw = H.deck.length > 0; out.side = !T.sideReturned && H.discards[leftOf(seat)].length > 0; return out; }
    out.discard = true; out.open = !H.opened[seat]; out.undo = !!T.snap; out.returnSide = !!T.sideTake && (idxOf(H.racks[seat], T.sideTake) >= 0 || !!T.snap); out.add = !!H.opened[seat] && S.opts.isle;
    return out;
  }
  function invariants(S) {
    var H = S.hand, seen = {}, bad = [];
    function mark(id) { if (seen[id]) bad.push('dup ' + id); seen[id] = true; }
    if (!H) return [];
    if (H.indicator) mark(H.indicator);
    H.deck.forEach(mark); H.racks.forEach(function (r) { r.forEach(mark); }); H.discards.forEach(function (r) { r.forEach(mark); });
    H.melds.forEach(function (m) { m.tiles.forEach(mark); }); if (H.finishTile) mark(H.finishTile); (H.out || []).forEach(mark);
    ALL.forEach(function (id) { if (!seen[id]) bad.push('missing ' + id); });
    return bad;
  }

  var API = {
    VERSION: VERSION, COLORS: COLORS, CONST: C, ALL_TILES: ALL,
    rngNext: rngNext, rngInt: rngInt,
    isTile: isTile, isFake: isFake, colorOf: colorOf, numOf: numOf, codeOf: codeOf, okeyFromIndicator: okeyFromIndicator,
    isOkey: isOkey, realOf: realOf, handValueOf: handValueOf, analyzeMeld: analyzeMeld,
    newMatch: newMatch, fromSpec: fromSpec, rigFromHands: rigFromHands, apply: apply,
    isIslek: function (S, id) { return isIslek(S.hand, id); },
    addSides: function (S, meldId, id) { var m = meldById(S.hand, meldId); return m ? addSides(S.hand, m, id) : []; },
    swapFits: function (S, meldId, index, id) { var m = meldById(S.hand, meldId); return !!m && swapFits(S.hand, m, index, id); },
    openNeed: openNeed, autoDiscardTile: autoDiscardTile, legal: legal, invariants: invariants, ranking: ranking, clone: clone, leftOf: leftOf, nextOf: nextOf, partnerOf: partnerOf
  };
  root.G101Rules = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof self !== 'undefined' ? self : this);
