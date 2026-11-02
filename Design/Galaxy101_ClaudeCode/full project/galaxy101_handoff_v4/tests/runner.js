/* Galaxy 101 · tests/runner.js · v0.5.0 — altın test vektörü koşucusu (tarayıcı ve sunucu testleri için aynı mantık).
 * Vektör biçimi README'de: init | call | sim, steps[{do, expect}], expect{ok,error,events,state,throws}.
 * state yolları: "hand.racks.0" (nokta + sayı), sonek "#len" uzunluk, "#set" sırasız karşılaştırma; "invariants" → 106 taşın her biri tam bir kez.
 */
(function (root) {
  'use strict';
  function get(obj, path) {
    var parts = path.split('.'), o = obj;
    for (var i = 0; i < parts.length; i++) { if (o == null) return undefined; o = o[/^\d+$/.test(parts[i]) ? +parts[i] : parts[i]]; }
    return o;
  }
  function partial(exp, act) {
    if (exp === null || typeof exp !== 'object') return exp === act;
    if (Array.isArray(exp)) { if (!Array.isArray(act) || act.length !== exp.length) return false; for (var i = 0; i < exp.length; i++) if (!partial(exp[i], act[i])) return false; return true; }
    if (!act || typeof act !== 'object') return false;
    for (var k in exp) if (!partial(exp[k], act[k])) return false;
    return true;
  }
  function checkState(R, S, map, fails, where) {
    for (var p in map) {
      var want = map[p], got;
      if (p === 'invariants') got = R.invariants(S);
      else if (/#len$/.test(p)) { var a = get(S, p.slice(0, -4)); got = a ? a.length : undefined; }
      else if (/#set$/.test(p)) { got = (get(S, p.slice(0, -4)) || []).slice().sort(); want = want.slice().sort(); }
      else got = get(S, p);
      if (!partial(want, got)) fails.push(where + ' ' + p + ': beklenen ' + JSON.stringify(want) + ', gelen ' + JSON.stringify(got));
    }
  }
  function checkEvents(exp, evs, fails, where) {
    var j = 0;
    for (var i = 0; i < exp.length; i++) {
      while (j < evs.length && !partial(exp[i], evs[j])) j++;
      if (j >= evs.length) { fails.push(where + ' olay yok: ' + JSON.stringify(exp[i]) + ' · gelen: ' + JSON.stringify(evs.map(function (e) { return e.e; }))); return; }
      j++;
    }
  }
  function run(R, B, t) {
    var fails = [], S, i;
    try {
      if (t.call) {
        var c = t.call, out;
        if (c.fn === 'analyzeMeld') out = R.analyzeMeld(R.okeyFromIndicator(c.indicator), c.kind, c.tiles);
        else if (c.fn === 'rigFromHands') { try { R.rigFromHands(c.deal); out = { threw: null }; } catch (e) { out = { threw: String(e.message).split(' ')[0] }; } }
        else if (c.fn === 'fromSpec') { try { R.fromSpec(c.spec); out = { threw: null }; } catch (e) { out = { threw: String(e.message).split(' ')[0] }; } }
        else if (c.fn === 'openNeedFresh') { var S0 = R.newMatch({ katlama: true }, 1); S0 = R.apply(S0, { t: 'startHand' }).state; out = { run: R.openNeed(S0, 0, 'run'), pair: R.openNeed(S0, 0, 'pair') }; }
        else if (c.fn === 'autoDiscardTile') out = { tile: R.autoDiscardTile(R.fromSpec(c.spec), c.seat) };
        else if (c.fn === 'bestMelds') { var o = R.okeyFromIndicator(c.indicator), b = B.bestMelds(o, c.tiles); out = { value: b.value, left: b.left.length }; }
        if (!partial(t.expect, out)) fails.push('call ' + c.fn + ': beklenen ' + JSON.stringify(t.expect) + ', gelen ' + JSON.stringify(out));
        return fails;
      }
      if (t.sim) {
        var sim = B.simulate(t.sim.opts, t.sim.seed);
        var ff = sim.log.filter(function (l) { return l.fail || l.fatal; });
        if (ff.length) fails.push('sim reddedilen bot eylemi: ' + JSON.stringify(ff[0]));
        checkState(R, sim.state, t.expect.state || {}, fails, 'sim');
        return fails;
      }
      if (t.init.spec) S = R.fromSpec(t.init.spec);
      else if (t.init.scenario) { S = R.newMatch(t.init.scenario.opts || {}, t.init.scenario.seed || 7); S = R.apply(S, { t: 'startHand', rig: R.rigFromHands(t.init.scenario.deal) }).state; }
      else { S = R.newMatch(t.init.match.opts, t.init.match.seed); var r0 = R.apply(S, { t: 'startHand', rig: t.init.rig }); S = r0.state; }
      if (t.init.expect) checkState(R, S, t.init.expect, fails, 'init');
      for (i = 0; i < (t.steps || []).length; i++) {
        var st = t.steps[i], e = st.expect || {}, where = '#' + (i + 1) + ' ' + st.do.t, r;
        if (st.do.t === 'botTurn') {   // botun tüm turu: bots.nextAction sıra geçene dek
          var acc = [], cur = S, g = 20, rr = { ok: true };
          while (g-- > 0 && cur.status === 'playing' && cur.hand.turn.seat === st.do.seat) { rr = R.apply(cur, B.nextAction(cur, st.do.seat)); if (!rr.ok) break; acc = acc.concat(rr.events); cur = rr.state; }
          r = { ok: rr.ok, error: rr.error, events: acc, state: cur };
        } else r = R.apply(S, st.do);
        var wantOk = e.ok !== undefined ? e.ok : !e.error;
        if (r.ok !== wantOk) fails.push(where + ': ok beklenen ' + wantOk + ', gelen ' + r.ok + (r.error ? ' (' + r.error + ')' : ''));
        if (e.error && r.error !== e.error) fails.push(where + ': hata beklenen ' + e.error + ', gelen ' + r.error);
        if (e.events) checkEvents(e.events, r.events, fails, where);
        if (e.noEvents) e.noEvents.forEach(function (x) { r.events.forEach(function (ev) { if (partial(x, ev)) fails.push(where + ' istenmeyen olay: ' + JSON.stringify(ev)); }); });
        S = r.state;
        if (e.state) checkState(R, S, e.state, fails, where);
      }
    } catch (ex) { fails.push('istisna: ' + (ex && ex.message)); }
    return fails;
  }
  var API = { run: run, get: get, partial: partial };
  root.G101TestRunner = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof self !== 'undefined' ? self : this);
