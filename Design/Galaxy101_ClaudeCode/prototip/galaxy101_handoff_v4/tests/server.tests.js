/* Galaxy 101 · tests/server.tests.js · v0.5.0
 * mockServer akış testleri (sanal saat). Gerçek sunucu aynı senaryoları entegrasyon testi olarak koşar.
 * Her test: { id, title, run(env) → hata dizisi }.
 */
(function (root) {
  'use strict';
  function env(seed, rig) {
    var M = root.G101MockServer, clk = M.VirtualClock(0), srv = M.createServer({ clock: clk, seed: seed || 42, rig: rig }), rid = 0;
    function client() {
      var inbox = [], c = srv.connect(function (m) { inbox.push(m); });
      return { inbox: inbox, send: function (t, o) { var m = o || {}; m.t = t; m.rid = ++rid; c.send(m); var a = inbox.filter(function (x) { return x.t === 'ack' && x.rid === m.rid; })[0]; return a; }, close: c.close,
        of: function (t) { return inbox.filter(function (x) { return x.t === t; }); }, last: function (t) { var a = this.of(t); return a[a.length - 1]; } };
    }
    return { srv: srv, clk: clk, client: client, until: function (fn, max) { var g = 0; while (!fn() && g++ < (max || 5000)) clk.advance(500); return fn(); } };
  }
  function eq(f, name, a, b) { if (JSON.stringify(a) !== JSON.stringify(b)) f.push(name + ': beklenen ' + JSON.stringify(b) + ', gelen ' + JSON.stringify(a)); }
  function ok(f, name, v) { if (!v) f.push(name); }

  var TESTS = [
    { id: 'SRV-SC1', title: 'Senaryo R43 sunucuda: sabit dağıtım, attığın K9 ile bot açar, sana +90 (game.penalty)', run: function () {
      var R = root.G101Rules, deal = {"dealer":3,"dice":[4,3],"indicator":"Y4a","racks":[["K9b","R1a","R3a","R5b","R7b","R9a","R11b","B2a","B5a","B7b","B11a","B13b","K4a","K7b","Y1a","Y3b","Y6a","Y10b","Y12b","R10b","B4b","K2b"],["R13a","B13a","Y13a","R12a","B12a","Y12a","K10a","K11a","R2a","R4a","R6a","R8a","B1a","B3a","B6a","B9a","K1a","K3a","K5a","Y8a","Y11a"]]};
      var f = [], E = env(7, R.rigFromHands(deal)), c = E.client(); c.send('guest'); c.send('vsBotsStart', { mode: 'solo', hands: 1 });
      E.until(function () { return c.of('turn').length; });
      var hd = c.of('handDealt')[0];
      eq(f, 'ıstaka', (hd && hd.rack || []).slice().sort(), deal.racks[0].slice().sort());
      eq(f, 'ilk tur bende', c.of('turn')[0].seat, 0);
      ok(f, 'K9 atılamadı', c.send('gDiscard', { tile: 'K9b' }).ok);
      E.until(function () { return c.of('game').some(function (m) { return m.ev.e === 'penalty'; }); }, 200);
      var p = c.of('game').filter(function (m) { return m.ev.e === 'penalty'; })[0];
      ok(f, 'ceza gelmedi', !!p);
      if (p) { eq(f, 'ceza kodu', p.ev.code, 'P_OPENED_WITH'); eq(f, 'ceza', p.ev.amount, 90); eq(f, 'kime', p.ev.seat, 0); }
      return f; } },
    { id: 'SRV-01', title: 'Botlara karşı tam maç: dağıtım → turlar → el sonu → maç sonu, hata yok', run: function () {
      var f = [], E = env(42), c = E.client(); c.send('guest'); c.send('vsBotsStart', { mode: 'solo', hands: 2 });
      ok(f, 'matchResult gelmedi', E.until(function () { return c.of('matchResult').length; }, 4000));
      eq(f, 'reddedilen ack', c.of('ack').filter(function (a) { return !a.ok; }).length, 0);
      eq(f, 'handDealt sayısı', c.of('handDealt').length, 2);
      eq(f, 'handWait sayısı', c.of('handWait').length, 1);
      return f; } },
    { id: 'SRV-02', title: 'Dağıtım töreni bot dağıtıcıyla 21 686 ms', run: function () {
      var f = [], E = env(42), c = E.client(); c.send('guest'); c.send('vsBotsStart', { mode: 'solo', hands: 1 });
      E.until(function () { return c.of('turn').length; });
      var ds = c.last('dealStart'), t = c.of('turn')[0];
      if (ds.humanDealer) f.push('seed 42 insan dağıtıcı verdi; test bot dağıtıcı bekler');
      eq(f, 'dealStart→turn', t.at - ds.at, 21686);
      eq(f, 'adım sırası', c.of('dealStep').map(function (x) { return x.step; }), ['dice', 'indicator', 'extra', 'distribute', 'center']);
      return f; } },
    { id: 'SRV-03', title: 'Gizli bilgi: rakibin desteden çektiği taş, zar ve gösterge önceden gitmez', run: function () {
      var f = [], E = env(7), c = E.client(); c.send('guest'); c.send('vsBotsStart', { mode: 'solo', hands: 1 });
      E.until(function () { return c.of('matchResult').length; }, 4000);
      var leak = c.of('game').filter(function (m) { return m.ev.e === 'drew' && m.ev.src === 'deck' && m.ev.seat !== 0 && m.ev.tile; });
      eq(f, 'sızan çekiş', leak.length, 0);
      var hs = c.of('game').filter(function (m) { return m.ev.e === 'handStart'; });
      hs.forEach(function (m) { if (m.ev.dice || m.ev.indicator || m.ev.okey) f.push('handStart gizli alan taşıyor'); });
      var die = c.of('dealDie')[0], ind = c.of('dealStep').filter(function (x) { return x.step === 'indicator'; })[0];
      ok(f, 'gösterge zardan sonra gelmeli', die && ind && ind.at > die.at);
      c.of('handDealt').forEach(function (h) { if (!h.rack || h.rack.length < 21) f.push('kendi ıstakası eksik'); if (Object.keys(h).indexOf('racks') >= 0) f.push('rakip ıstakaları gitti'); });
      return f; } },
    { id: 'SRV-04', title: 'Kodla katıl + şifre: yanlış PIN reddedilir, doğru PIN oturur, bahis düşülür ve çıkışta iade edilir', run: function () {
      var f = [], E = env(42), c = E.client(); c.send('guest');
      var chips0 = c.last('session').me.chips, tb = null, T = E.srv._debug.tables;
      for (var k in T) if (T[k].name === 'Akşam keyfi') tb = T[k];
      eq(f, 'yanlış pin', c.send('tableJoin', { tableId: tb.id, pin: '0000' }).error, 'E_PIN_WRONG');
      ok(f, 'doğru pin', c.send('tableJoin', { tableId: tb.id, pin: '1234' }).ok);
      eq(f, 'bahis düşüldü', c.last('balance').chips, chips0 - 10000);
      c.send('tableLeave'); eq(f, 'iade', c.last('balance').chips, chips0);
      eq(f, 'kod biçimi', c.send('tableJoinCode', { code: 'XX' }).error, 'E_CODE_FORMAT');
      eq(f, 'bilinmeyen kod', c.send('tableJoinCode', { code: 'GX-0000' }).error, 'E_CODE_UNKNOWN');
      ok(f, 'GX-7Q2M', c.send('tableJoinCode', { code: 'GX-7Q2M' }).ok);
      return f; } },
    { id: 'SRV-05', title: 'Masa aç doğrulaması ve yetersiz bakiye', run: function () {
      var f = [], E = env(1), c = E.client(); c.send('guest');
      eq(f, 'boş ad', c.send('tableCreate', { name: ' ', mode: 'solo', bet: 1000, hands: 5, turnSec: 30 }).error, 'E_TABLE_NAME');
      eq(f, 'pin biçimi', c.send('tableCreate', { name: 'x', mode: 'solo', bet: 1000, hands: 5, turnSec: 30, pin: '12' }).error, 'E_PIN_FORMAT');
      E.srv._debug.users['u.me'].chips = 500; // yalnız test
      eq(f, 'bakiye', c.send('tableCreate', { name: 'x', mode: 'solo', bet: 1000, hands: 5, turnSec: 30 }).error, 'E_NO_CHIPS');
      return f; } },
    { id: 'SRV-06', title: 'Bekleme odası: bot ekle, başlat, 3-2-1, maç başlar', run: function () {
      var f = [], E = env(3), c = E.client(); c.send('guest');
      c.send('tableCreate', { name: 'Test', mode: 'pairs', bet: 1000, hands: 3, turnSec: 15 });
      eq(f, 'dolmadan başlat', c.send('tableStart').error, 'E_TABLE_NOT_FULL');
      [1, 2, 3].forEach(function (s) { c.send('seatAddBot', { seat: s }); });
      ok(f, 'başlat', c.send('tableStart').ok);
      E.clk.advance(3000);
      eq(f, 'geri sayım', c.of('countdown').map(function (x) { return x.n; }), [3, 2, 1]);
      ok(f, 'matchStart', c.of('matchStart').length === 1);
      return f; } },
    { id: 'SRV-07', title: 'Maçta masadan kalkma: bahis iade edilmez, bot devralır, oyun sürer', run: function () {
      var f = [], E = env(5), c = E.client(), d = E.client(); c.send('guest');
      c.send('tableCreate', { name: 'Test', mode: 'solo', bet: 1000, hands: 3, turnSec: 15 });
      [1, 2, 3].forEach(function (s) { c.send('seatAddBot', { seat: s }); }); c.send('tableStart');
      E.until(function () { return c.of('turn').length; });
      var chips = c.last('balance').chips; ok(f, 'kalk', c.send('tableLeave').ok);
      ok(f, 'leftTable', c.of('leftTable').length === 1); eq(f, 'iade yok', c.last('balance').chips, chips);
      var tb = null, T = E.srv._debug.tables; for (var k in T) if (T[k].name === 'Test') tb = T[k];
      ok(f, 'bot devraldı', tb && tb.seats[0].bot && tb.match.seats[0].left);
      E.until(function () { return !T[tb.id]; }, 12000); ok(f, 'maç bitti', !T[tb.id]);
      return f; } },
    { id: 'SRV-08', title: 'Davet: demo arkadaş kabul eder; süre 30 sn', run: function () {
      var f = [], E = env(9), c = E.client(); c.send('guest');
      c.send('friendRequest', { userId: 'u.dy' }); E.clk.advance(2000);
      c.send('tableCreate', { name: 'Davet', mode: 'solo', bet: 1000, hands: 3, turnSec: 30 });
      var a = c.send('inviteSend', { userId: 'u.dy', seat: 2 }); ok(f, 'davet', a.ok); eq(f, 'süre', a.expiresAt - E.clk.now(), 30000);
      E.clk.advance(2500); eq(f, 'sonuç', (c.last('inviteResult') || {}).result, 'accepted');
      eq(f, 'yabancıya davet', c.send('inviteSend', { userId: 'u.sa' }).error, 'E_NOT_FRIENDS');
      return f; } },
    { id: 'SRV-09', title: 'Dereceli solo: arama → bulundu → kabul → maç → LP', run: function () {
      var f = [], E = env(11), c = E.client(); c.send('register', { email: 'r@g.co', pass: '123456', name: 'Ranker' });
      c.send('rankedQueue', { mode: 'solo' }); E.until(function () { return c.of('rankedFound').length; });
      var fd = c.last('rankedFound'); ok(f, 'kabul', c.send('rankedAccept', { matchId: fd.found.matchId, accept: true }).ok);
      ok(f, 'rankChange', E.until(function () { return c.of('rankChange').length; }, 8000));
      var rc = c.last('rankChange'), mr = c.last('matchResult'), d = [30, 10, -10, -20][rc.place - 1];
      eq(f, 'LP deltası', rc.delta, d);
      eq(f, 'bahis yok', mr.pool, 0);
      return f; } },
    { id: 'SRV-10', title: 'Dereceli: kabul süresi dolarsa aramaya dönülür', run: function () {
      var f = [], E = env(12), c = E.client(); c.send('register', { email: 'r2@g.co', pass: '123456', name: 'Ranker2' });
      c.send('rankedQueue', { mode: 'solo' }); E.until(function () { return c.of('rankedFound').length; });
      E.clk.advance(10000); eq(f, 'iptal', (c.last('rankedCancelled') || {}).reason, 'timeout');
      return f; } },
    { id: 'SRV-11', title: 'Yeniden bağlanma: snapshot masa ekranına döner, kendi ıstakası var, rakipler yok', run: function () {
      var f = [], E = env(42), c = E.client(); c.send('guest'); c.send('vsBotsStart', { mode: 'solo', hands: 1 });
      E.until(function () { return c.of('turn').length; }); c.close();
      E.clk.advance(4000);
      var d = E.client(); ok(f, 'hello', d.send('hello', { token: 'u.me' }).ok);
      var s = d.last('snapshot'); eq(f, 'ekran', s.screen, 'table');
      ok(f, 'ıstaka', s.game.rack && s.game.rack.length >= 21);
      ok(f, 'rakip ıstakası yok', JSON.stringify(s.game).indexOf('"racks"') < 0);
      return f; } },
    { id: 'SRV-12', title: 'Günlük ödül: bir kez alınır, görev tamamlanmadan alınamaz', run: function () {
      var f = [], E = env(1), c = E.client(); c.send('guest');
      var a = c.send('dailyClaim'); ok(f, 'ilk', a.ok); eq(f, 'miktar', a.amount, 1000);
      eq(f, 'ikinci', c.send('dailyClaim').error, 'E_DAILY_CLAIMED');
      E.clk.advance(86400000); eq(f, '2. gün', c.send('dailyClaim').amount, 2000);
      eq(f, 'görev', c.send('questClaim', { questId: 'q.win3' }).error, 'E_QUEST_INCOMPLETE');
      return f; } },
    { id: 'SRV-13', title: 'Sohbet seçenekleri: kapalı / yalnız hızlı', run: function () {
      var f = [], E = env(2), c = E.client(); c.send('guest');
      c.send('tableCreate', { name: 'S', mode: 'solo', bet: 1000, hands: 3, turnSec: 30, rules: { chat: 'quick' } });
      eq(f, 'yazı', c.send('chatSend', { text: 'selam' }).error, 'E_CHAT_QUICK_ONLY');
      ok(f, 'hızlı', c.send('chatSend', { quick: 'quick.1' }).ok);
      return f; } },
    { id: 'SRV-14', title: 'Giriş: hesap yok / yanlış şifre (kalan hak) / 3. yanlışta kilit', run: function () {
      var f = [], E = env(1), c = E.client(); c.send('register', { email: 'l@g.co', pass: '123456', name: 'Login1' }); c.send('logout');
      eq(f, 'yok', c.send('login', { email: 'x@g.co', pass: '1' }).error, 'E_ACCOUNT_UNKNOWN');
      var a = c.send('login', { email: 'l@g.co', pass: 'x' }); eq(f, 'yanlış', [a.error, a.detail.left], ['E_PASS_WRONG', 2]);
      c.send('login', { email: 'l@g.co', pass: 'x' }); eq(f, 'kilit', c.send('login', { email: 'l@g.co', pass: 'x' }).error, 'E_ACCOUNT_LOCKED');
      return f; } },
    { id: 'SRV-15', title: 'Aynı seed → aynı olay akışı (determinizm)', run: function () {
      var f = [];
      function trace() { var E = env(99), c = E.client(); c.send('guest'); c.send('vsBotsStart', { mode: 'pairs', hands: 1 }); E.until(function () { return c.of('matchResult').length; }, 4000); return JSON.stringify(c.inbox.map(function (m) { return [m.t, m.at, m.ev ? m.ev.e : 0, m.ev ? m.ev.tile : 0]; })); }
      eq(f, 'iki koşu aynı', trace() === trace(), true);
      return f; } },
    { id: 'SRV-16', title: 'Q-10 kopma: kalktı sayılmaz; üst üste 2 zaman aşımından sonra koltuğu sunucu oynar; dönünce oyuncuya geçer', run: function () {
      var f = [], E = env(42), c = E.client(), o = E.client(); c.send('guest'); o.send('register', { email: 'o@g.co', pass: '123456', name: 'Other1' });
      c.send('tableCreate', { name: 'K', mode: 'solo', bet: 1000, hands: 3, turnSec: 15 }); var tb = null, T = E.srv._debug.tables; for (var k in T) if (T[k].name === 'K') tb = T[k];
      o.send('tableJoin', { tableId: tb.id }); [2, 3].forEach(function (s) { c.send('seatAddBot', { seat: s }); }); c.send('tableStart');
      E.until(function () { return c.of('turn').length; }); var oSeat = 1; o.close();
      ok(f, 'kalktı sayılmamalı', !tb.match.seats[oSeat].left);
      ok(f, 'otomatik oyuna geçmedi', E.until(function () { return tb.auto[oSeat]; }, 3000));
      var autos = c.of('game').filter(function (m) { return m.ev.e === 'auto' && m.ev.seat === oSeat; }).length; eq(f, '2 zaman aşımı', autos, 2);
      var d = E.client(); ok(f, 'dönüş', d.send('hello', { token: 'u.other1' }).ok); ok(f, 'sunucu bıraktı', !tb.auto[oSeat]); eq(f, 'ekran', d.last('snapshot').screen, 'table');
      return f; } },
    { id: 'SRV-17', title: 'Q-10 bekleme odasında kopan oyuncu hemen çıkarılır, bahsi iade edilir', run: function () {
      var f = [], E = env(4), c = E.client(), o = E.client(); c.send('guest'); o.send('register', { email: 'w@g.co', pass: '123456', name: 'Waiter1' });
      c.send('tableCreate', { name: 'W', mode: 'solo', bet: 1000, hands: 3, turnSec: 30 }); var tb = null, T = E.srv._debug.tables; for (var k in T) if (T[k].name === 'W') tb = T[k];
      o.send('tableJoin', { tableId: tb.id }); var u = E.srv._debug.users['u.waiter1']; eq(f, 'bahis', u.chips, 24000);
      o.close(); eq(f, 'koltuk boş', tb.seats[1], null); eq(f, 'iade', u.chips, 25000); eq(f, 'masa bağı', u.tableId, null);
      return f; } },
    { id: 'SRV-18', title: 'Q-13 oynanan masada kalkanın koltuğu devralınır; bahis o an ödenir; koltuğun toplamı korunur', run: function () {
      var f = [], E = env(5), c = E.client(), n = E.client(); c.send('guest'); n.send('register', { email: 'n@g.co', pass: '123456', name: 'Newbie1' });
      c.send('tableCreate', { name: 'D', mode: 'solo', bet: 1000, hands: 3, turnSec: 15 }); var tb = null, T = E.srv._debug.tables; for (var k in T) if (T[k].name === 'D') tb = T[k];
      [1, 2, 3].forEach(function (s) { c.send('seatAddBot', { seat: s }); }); c.send('tableStart');
      E.until(function () { return tb.match && tb.match.status === 'handOver'; }, 6000); var tot = tb.match.totals[1];
      var a = n.send('tableJoin', { tableId: tb.id }); ok(f, 'devralma', a.ok); eq(f, 'koltuk', a.seat, 1);
      eq(f, 'bahis ödendi', E.srv._debug.users['u.newbie1'].chips, 24000); eq(f, 'toplam', tb.match.totals[1], tot); eq(f, 'havuz', tb.betsPaid, 2000);
      eq(f, 'ekran', n.last('snapshot').screen, 'table'); ok(f, 'maç bitti', E.until(function () { return !T[tb.id]; }, 12000));
      ok(f, 'sonuç geldi', n.of('matchResult').length === 1);
      return f; } },
    { id: 'SRV-19', title: 'Q-14 kilit 5 dk; şifre sıfırlanınca kilit kalkar', run: function () {
      var f = [], E = env(1), c = E.client(); c.send('register', { email: 'k@g.co', pass: '123456', name: 'Lock1' }); c.send('logout');
      [1, 2, 3].forEach(function () { c.send('login', { email: 'k@g.co', pass: 'x' }); });
      eq(f, 'kilitli', c.send('login', { email: 'k@g.co', pass: '123456' }).error, 'E_ACCOUNT_LOCKED');
      E.clk.advance(300000); ok(f, '5 dk sonra açık', c.send('login', { email: 'k@g.co', pass: '123456' }).ok); c.send('logout');
      [1, 2, 3].forEach(function () { c.send('login', { email: 'k@g.co', pass: 'x' }); });
      ok(f, 'sıfırla', c.send('passwordReset', { email: 'k@g.co', code: '123456', pass: 'yeni123' }).ok);
      ok(f, 'kilit kalktı', c.send('login', { email: 'k@g.co', pass: 'yeni123' }).ok);
      return f; } },
    { id: 'SRV-20', title: 'Q-15 başlangıç: misafir 10.000 çip, hesap 25.000 çip, herkese 20 elmas', run: function () {
      var f = [], E = env(1), a = E.client(), b = E.client(), g = E.client();
      a.send('guest', { name: 'Konuk' }); b.send('register', { email: 's@g.co', pass: '123456', name: 'Start1' }); g.send('oauth', { provider: 'google', email: 'gg@g.co', name: 'Goo' });
      eq(f, 'misafir', [a.last('session').me.chips, a.last('session').me.gems], [10000, 20]);
      eq(f, 'e-posta', [b.last('session').me.chips, b.last('session').me.gems], [25000, 20]);
      eq(f, 'google', [g.last('session').me.chips, g.last('session').me.gems], [25000, 20]);
      return f; } },
    { id: 'SRV-21', title: 'Q-11 haftalık puan 30/15/5/1; botlara karşı ve özel oda sayılmaz; sezon = LP', run: function () {
      var f = [], E = env(42), c = E.client(); c.send('register', { email: 'h@g.co', pass: '123456', name: 'Week1' });
      c.send('vsBotsStart', { mode: 'solo', hands: 1 }); E.until(function () { return c.of('matchResult').length; }, 4000);
      eq(f, 'botlara karşı 0', E.srv._debug.users['u.week1'].weekly.pts, 0);
      c.send('tableCreate', { name: 'H', mode: 'solo', bet: 1000, hands: 3, turnSec: 15 }); [1, 2, 3].forEach(function (s) { c.send('seatAddBot', { seat: s }); }); c.send('tableStart');
      E.until(function () { return c.of('matchResult').length === 2; }, 12000);
      var mr = c.last('matchResult'), place = 0; mr.ranking.forEach(function (u) { if (u.seats.indexOf(0) >= 0) place = u.rank; });
      var tied = mr.ranking.filter(function (u) { return u.rank === place; }).length, W = [30, 15, 5, 1], want = 0; for (var p = place - 1; p < place - 1 + tied; p++) want += W[p]; want = Math.round(want / tied);
      eq(f, 'haftalık (beraberlikte paylaşılır)', E.srv._debug.users['u.week1'].weekly.pts, want); eq(f, 'matchResult.weekly', mr.weekly[0], want);
      c.send('leaderboardGet', { board: 'weekly' }); var lb = c.last('leaderboard'); ok(f, 'tabloda', lb.mine && lb.mine.pts === want);
      c.send('leaderboardGet', { board: 'season' }); ok(f, 'sezon LP', c.last('leaderboard').rows[0].pts >= c.last('leaderboard').rows[1].pts);
      return f; } },
    { id: 'SRV-22', title: 'Q-16 gün değişimi İstanbul saatiyle; bir gün atlanırsa seri 1. güne döner; 7. günden sonra baştan', run: function () {
      var f = [], E = env(1), c = E.client(); c.send('guest');
      E.clk.advance(21 * 3600000 - 1); var a1 = c.send('dailyClaim'); eq(f, 'gün 1', a1.amount, 1000);
      E.clk.advance(2); eq(f, 'İstanbul gece yarısı geçti', c.send('dailyClaim').amount, 2000);
      E.clk.advance(2 * 86400000); eq(f, 'atlandı → 1. gün', c.send('dailyClaim').amount, 1000);
      for (var i = 0; i < 6; i++) { E.clk.advance(86400000); c.send('dailyClaim'); }
      E.clk.advance(86400000); eq(f, '8. gün → 1. gün', c.send('dailyClaim').amount, 1000);
      return f; } },
    { id: 'SRV-23', title: 'Q-01 katlamalı masa: seçenek lobi kartında ve motorda; eşlide "ortağın açılışı yükseltsin" varsayılan açık', run: function () {
      var f = [], E = env(8), c = E.client(); c.send('guest');
      var a = c.send('tableCreate', { name: 'Kat', mode: 'pairs', bet: 1000, hands: 3, turnSec: 30, katlama: true });
      eq(f, 'kart', [a.table.katlama, a.table.katlamaPartner], [true, true]); c.send('tableLeave');
      var b = c.send('tableCreate', { name: 'Kat2', mode: 'pairs', bet: 1000, hands: 3, turnSec: 30, katlama: true, katlamaPartner: false });
      eq(f, 'kapalı', b.table.katlamaPartner, false);
      [1, 2, 3].forEach(function (s) { c.send('seatAddBot', { seat: s }); }); c.send('tableStart'); E.clk.advance(3500);
      var tb = null, T = E.srv._debug.tables; for (var k in T) if (T[k].name === 'Kat2') tb = T[k];
      eq(f, 'motor', [tb.match.opts.katlama, tb.match.opts.katlamaPartner], [true, false]);
      var s = c.send('tableCreate', { name: 'x', mode: 'solo', bet: 1000, hands: 3, turnSec: 30, katlama: true }); eq(f, 'oturmuşken', s.error, 'E_ALREADY_SEATED');
      return f; } },
    { id: 'SRV-24', title: 'Q-02 XP: el bitiren +340, diğerleri +60; maç sonu 500/200/80/0; seviye n → n+1 = 375·n', run: function () {
      var f = [], E = env(42), c = E.client(); c.send('register', { email: 'x@g.co', pass: '123456', name: 'Xp1' });
      c.send('vsBotsStart', { mode: 'solo', hands: 2 }); E.until(function () { return c.of('matchResult').length; }, 4000);
      var hands = c.of('game').filter(function (m) { return m.ev.e === 'handEnd'; }).map(function (m) { return m.ev.result.finisher === 0 ? 340 : 60; });
      var mr = c.last('matchResult'), xs = c.of('xp');
      eq(f, 'el XP', xs.filter(function (x) { return x.reason === 'hand'; }).map(function (x) { return x.gained; }), hands);
      eq(f, 'bonus', (xs.filter(function (x) { return x.reason === 'match'; })[0] || { gained: 0 }).gained, mr.xpBonus[0]);
      ok(f, 'bonus değeri', [500, 200, 80, 0, 350, 140, 40, 260, 93, 195].indexOf(mr.xpBonus[0]) >= 0);
      eq(f, 'eğri', c.last('xp').xpNext, 375 * c.last('xp').level);
      return f; } },
    { id: 'SRV-25', title: 'Q-07 eşlide kalkan: takım son sayılmaz, kalkan ödül/XP almaz; takım kazanırsa ödülün tamamı ortağa; Q-09 kesinti yok', run: function () {
      var f = [], E = env(13), c = E.client(), p = E.client(); c.send('guest'); p.send('register', { email: 'pp@g.co', pass: '123456', name: 'Partner1' });
      c.send('tableCreate', { name: 'E', mode: 'pairs', bet: 1000, hands: 3, turnSec: 15 }); var tb = null, T = E.srv._debug.tables; for (var k in T) if (T[k].name === 'E') tb = T[k];
      p.send('tableJoin', { tableId: tb.id, seat: 2 }); [1, 3].forEach(function (s) { c.send('seatAddBot', { seat: s }); }); c.send('tableStart');
      E.until(function () { return p.of('turn').length; }); p.send('tableLeave'); var px = p.of('xp').length;
      E.until(function () { return c.of('matchResult').length; }, 12000);
      var mr = c.last('matchResult'), team0 = mr.ranking.filter(function (u) { return u.seats.indexOf(0) >= 0; })[0];
      eq(f, 'takım son değil (2 birim)', mr.ranking.length, 2);
      eq(f, 'ortağın ödülü', mr.payouts[0], team0.rank === 1 ? 4000 : 0); eq(f, 'kalkana ödül yok', mr.payouts[2], 0);
      eq(f, 'kalkana XP yok', p.of('xp').length, px); eq(f, 'kesinti', mr.rake, 0);
      return f; } }
  ];
  var API = { TESTS: TESTS, run: function () { return TESTS.map(function (t) { var r; try { r = t.run(); } catch (e) { r = ['istisna: ' + e.message]; } return { id: t.id, title: t.title, fails: r }; }); } };
  root.G101ServerTests = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof self !== 'undefined' ? self : this);
