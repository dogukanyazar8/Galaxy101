/* Galaxy 101 · logic/mockServer.js · v0.4.0
 * Gerçek sunucunun prova sürümü. İstemci durumu asla doğrudan değiştirmez: her işlem C2S mesajıyla buraya gelir,
 * buradan S2C olaylarıyla döner. Zamanlayıcılar, botlar, dağıtım töreni, zar, puanlama, bakiye hareketleri burada.
 * Kural motoru: logic/rules.js · botlar: logic/bots.js · mesaj kataloğu: protocol/protocol.json · süreler: protocol/timings.md
 * Rastgelelik yalnız seed'li RNG ({s}, rules.js rngNext). Saat dışarıdan verilir (clock.now/set/clear) → t parametresiyle dondurulabilir.
 * Yalnız prototipe ait kısımlar "// DEMO:" ile işaretli ve DEMO_LIST.md'de listeli.
 */
(function (root) {
  'use strict';
  var R = (function () { var m = null; function get() { return m || (m = root.G101Rules || (typeof require !== 'undefined' ? require('./rules.js') : null)); } return typeof Proxy !== 'undefined' ? new Proxy({}, { get: function (_, k) { return get()[k]; } }) : get(); })();   // tarayıcıda betik yükleme sırası garanti değil → ilk kullanımda çöz (C# portunda: doğrudan bağımlılık)
  var B = (function () { var m = null; function get() { return m || (m = root.G101Bots || (typeof require !== 'undefined' ? require('./bots.js') : null)); } return typeof Proxy !== 'undefined' ? new Proxy({}, { get: function (_, k) { return get()[k]; } }) : get(); })();   // tarayıcıda betik yükleme sırası garanti değil → ilk kullanımda çöz (C# portunda: doğrudan bağımlılık)
  var VERSION = '0.4.0';

  // ---------- Sunucu süreleri (ms) · protocol/timings.md ile birebir ----------
  var T = {
    preDealCountdown: 3000,      // 3-2-1 (yalnız maçın ilk eli)
    seatedToCountdown: 900,      // 4. koltuk dolunca geri sayıma geçiş (sahibi bot/demo olan masa)
    waitRoomCountdown: 3000,     // bekleme odası 3-2-1
    deal: { build: 2940, botRollDelay: 700, humanRollWait: 15000, roll1: 1570, roll2: 1990, betweenRolls: 650, afterDice: 1000, indicator: 2300, extra: 1300, distribute: 5100, center: 2000, fill: 2136 },
    turnMs: 30000,               // masa seçeneği 15/20/30/45 sn
    handEndWait: 15000,          // "Sonraki el · 15"
    inviteTtl: 30000,
    rankedAccept: 10000,
    rankedWiden: 30000,
    rankedLoading: 1800,         // "Masa hazırlanıyor" (prototip değeri)
    leaveToLobby: 1300,          // "Masadan kalktın" tostu → lobi
    connect: 850,                // DEMO: sahte bağlanma gecikmesi
    autoplayAfterTimeouts: 2,    // Q-10: bağlantısı kopuk oyuncuda üst üste 2 zaman aşımından sonra koltuğu sunucu oynar
    reconnectMatch: null,        // Q-10: maç bitene dek dönebilir (süre sınırı yok)
    reconnectWaitRoom: 0,        // Q-10: bekleme odasında kopan oyuncu hemen çıkarılır
    clientRetries: 5, clientRetryMs: 3000, // Q-10: istemci "Deneme n/5", 3 sn arayla; sonra "Lobiye dön"
    loginLock: 300000,           // Q-14: 3 yanlış şifre → 5 dk kilit
    tzOffset: 3 * 3600000        // Q-16: gün değişimi Europe/Istanbul (UTC+3, yaz saati yok)
  };
  function dealSchedule(humanDealer) {
    var d = T.deal, s = [], t = 0;
    s.push({ step: 'build', at: 0 }); t = d.build;
    s.push({ step: 'dice', at: t });
    return { steps: s, diceAt: t, rollWait: humanDealer ? d.humanRollWait : d.botRollDelay };
  }

  // ---------- Ekonomi (RULES.md §8) ----------
  var ECON = {
    rake: 0,                                                     // R-91 kesinti yok (Q-09)
    levelXp: function (n) { return 375 * n; },                   // R-92 seviye n → n+1 (Q-02)
    xp: { handWin: 340, hand: 60, place: [500, 200, 80, 0] },    // R-93 (Q-02)
    daily: [1000, 2000, 3000, 4000, 5000, 7500, 15000],          // günlük ödül 1.–7. gün (prototip)
    start: { guest: { chips: 10000, gems: 20 }, account: { chips: 25000, gems: 20 } }, // Q-15
    weekly: [30, 15, 5, 1],                                      // Q-11: maç sonu sırasına göre haftalık puan
    quests: [
      { id: 'q.win3', goal: 3, reward: 2000, gem: 0, on: 'handWin' },
      { id: 'q.openPairs', goal: 1, reward: 1500, gem: 0, on: 'openPairs' },
      { id: 'q.play5', goal: 5, reward: 1000, gem: 0, on: 'handPlayed' },
      { id: 'q.okeyFinish', goal: 1, reward: 5000, gem: 5, on: 'okeyFinish' },
      { id: 'q.add40', goal: 40, reward: 2500, gem: 0, on: 'added' }
    ],
    shop: {
      'chips.50k': { chips: 50000, price: 19.99 }, 'chips.250k': { chips: 275000, price: 79.99 }, 'chips.1m': { chips: 1250000, price: 249.99 }, 'chips.5m': { chips: 7500000, price: 899.99 },
      'gems.100': { gems: 100, price: 29.99 }, 'gems.550': { gems: 605, price: 139.99 }, 'gems.1200': { gems: 1440, price: 279.99 }, 'gems.2500': { gems: 3125, price: 549.99 },
      'deal.weekend': { chips: 1000000, gems: 50, price: 99.99 },
      'skin.galaxy': { gemCost: 800, skin: 'galaxy' }, 'skin.gold': { gemCost: 1200, skin: 'gold' }, 'skin.neon': { gemCost: 600, skin: 'neon' }, 'skin.marble': { gemCost: 400, skin: 'marble' }
    },
    gifts: { tea: 100, coffee: 150, cola: 120, simit: 200, baklava: 500, seeds: 80, flower: 1000, cake: 2500, trophy: 5000, rocket: 10000 },
    bets: [1000, 5000, 10000, 50000, 100000], hands: [3, 5, 7], turnSec: [15, 20, 30, 45]
  };
  // Dereceli (spec/27, g101-rank.js ile aynı)
  var RANK = {
    steps: 15,
    deltas: function (idx) { return idx <= 8 ? [30, 10, -10, -20] : [25, 5, -12, -22]; },   // sıra 1–4
    protectGames: 3
  };

  // ---------- Yardımcılar ----------
  function clone(o) { return JSON.parse(JSON.stringify(o)); }
  function rnd(rng) { return R.rngNext(rng) / 4294967296; }
  function pick(rng, a) { return a[R.rngInt(rng, a.length)]; }
  function dayOf(ms) { return Math.floor((ms + T.tzOffset) / 86400000); }          // Europe/Istanbul günü
  function weekOf(ms) { return Math.floor((dayOf(ms) + 3) / 7); }                    // pazartesi 00:00 başlar (gün 0 = perşembe)

  // ---------- Saatler ----------
  // Sanal saat: testler ve ?t= dondurma için. advance(ms) zamanlayıcıları sırayla (zaman, kayıt sırası) çalıştırır.
  function VirtualClock(start) {
    var now = start || 0, seq = 0, q = [];
    return {
      now: function () { return now; },
      set: function (fn, ms) { var id = ++seq; q.push({ id: id, at: now + Math.max(0, ms | 0), fn: fn }); return id; },
      clear: function (id) { q = q.filter(function (x) { return x.id !== id; }); },
      advance: function (ms) {
        var end = now + ms;
        for (;;) {
          q.sort(function (a, b) { return a.at - b.at || a.id - b.id; });
          if (!q.length || q[0].at > end) break;
          var x = q.shift(); now = x.at; x.fn();
        }
        now = end;
      },
      pending: function () { return q.length; }
    };
  }
  function RealClock(src) {
    var s = src || root;
    return { now: function () { return Date.now(); }, set: function (fn, ms) { return s.setTimeout(fn, ms); }, clear: function (id) { s.clearTimeout(id); } };
  }

  // ---------- DEMO verisi ----------
  // DEMO: prototipte gerçek oyuncu yok; dizin, lobi masaları, arkadaşlar ve bot adları sahte.
  var DEMO_PLAYERS = [
    { id: 'u.ay', name: 'Ayşe Y.', ini: 'AY', color: '#3D7BFF', level: 28, code: 'AY-2841', rankIdx: 7 },
    { id: 'u.mk', name: 'Mehmet K.', ini: 'MK', color: '#FF8A5B', level: 41, code: 'MK-5512', rankIdx: 8 },
    { id: 'u.sa', name: 'Selin A.', ini: 'SA', color: '#FF6B6B', level: 52, code: 'SA-9033', rankIdx: 9 },
    { id: 'u.bt', name: 'Burak T.', ini: 'BT', color: '#3CCB6E', level: 19, code: 'BT-1170', rankIdx: 5 },
    { id: 'u.ec', name: 'Elif C.', ini: 'EC', color: '#B57BFF', level: 33, code: 'EC-6402', rankIdx: 6 },
    { id: 'u.zo', name: 'Zeynep O.', ini: 'ZO', color: '#16B6A8', level: 37, code: 'ZO-3318', rankIdx: 7 },
    { id: 'u.ck', name: 'Can K.', ini: 'CK', color: '#F5A623', level: 24, code: 'CK-7725', rankIdx: 6 },
    { id: 'u.dy', name: 'Deniz Y.', ini: 'DY', color: '#5AC8FA', level: 45, code: 'DY-4096', rankIdx: 8 }
  ];
  var DEMO_ME = { name: 'Doğukan Y.', ini: 'DY', color: '#FF8A5B', level: 32, xp: 0, chips: 125400, gems: 320, code: 'GX-4821', rank: { idx: 7, lp: 72, prot: 0, peak: 7, games: 0, top: 0, hist: [] } };
  var DEMO_PIN = '1234', DEMO_CODE = 'GX-7Q2M';

  // ---------- Sunucu ----------
  function createServer(o) {
    o = o || {};
    var clock = o.clock || RealClock(), rng = { s: (o.seed >>> 0) || 1 };
    var users = {}, conns = {}, tables = {}, invites = {}, queue = {}, lobbySubs = {};
    var seqConn = 0, seqTable = 0, seqInvite = 0, seqMatch = 0, seqBot = 0;

    // ---- Hesaplar ----
    function newUser(id, base) {
      var u = { id: id, name: base.name, ini: base.ini, color: base.color, status: '', frame: 0, level: base.level || 1, xp: base.xp || 0, chips: base.chips || 0, gems: base.gems || 0,
        code: base.code, friends: [], requestsIn: [], requestsOut: [], skins: [], skin: 'classic',
        rank: base.rank ? clone(base.rank) : { idx: base.rankIdx || 0, lp: 0, prot: 0, peak: base.rankIdx || 0, games: 0, top: 0, hist: [] },
        daily: { lastDay: -1, streak: 0 }, quests: {}, questDay: -1, weekly: { week: -1, pts: 0 }, demo: !!base.demo, tableId: null, seat: -1, online: false };
      users[id] = u; return u;
    }
    DEMO_PLAYERS.forEach(function (p) { var u = newUser(p.id, p); u.demo = true; u.online = true; u.chips = 1000000; });
    function pub(u) { return { id: u.id, name: u.name, ini: u.ini, color: u.color, level: u.level, frame: u.frame, status: u.status, rankIdx: u.rank.idx, online: u.online, inGame: !!u.tableId }; }
    function me(u) { var p = pub(u); p.xp = u.xp; p.xpNext = ECON.levelXp(u.level); p.chips = u.chips; p.gems = u.gems; p.code = u.code; p.skin = u.skin; p.skins = u.skins.slice(); p.rank = clone(u.rank); return p; }

    // ---- Bağlantılar ----
    function connect(onMessage) {
      var id = 'c' + (++seqConn), c = { id: id, user: null, send: onMessage, alive: true };
      conns[id] = c;
      return { id: id, send: function (msg) { handle(c, msg); }, close: function () { drop(c); } };
    }
    function emit(c, t, data) { if (!c || !c.alive) return; var m = data ? clone(data) : {}; m.t = t; m.at = clock.now(); c.send(m); }
    function toUser(uid, t, data) { for (var k in conns) if (conns[k].user === uid) emit(conns[k], t, data); }
    function ack(c, rid, ok, error, data) { var m = { rid: rid, ok: ok }; if (error) m.error = error; if (data) for (var k in data) m[k] = data[k]; emit(c, 'ack', m); }
    function drop(c) {
      c.alive = false; delete conns[c.id]; delete lobbySubs[c.id];
      var u = c.user && users[c.user]; if (!u) return;
      var still = false; for (var k in conns) if (conns[k].user === u.id) still = true;
      if (!still) {
        u.online = false; presence(u); var tb = u.tableId && tables[u.tableId]; if (!tb) return;
        if (u.seat < 0) { tb.spectators = tb.spectators.filter(function (x) { return x !== u.id; }); u.tableId = null; return; }
        if (tb.status === 'waiting' || tb.status === 'countdown') return leaveWaitRoom(tb, u, 'disconnect');   // Q-10: bekleme odasında hemen çıkar
        tb.conn[u.seat] = false; tableEmit(tb, 'seatConnection', { seat: u.seat, connected: false });
      }
    }

    // ---- Mesaj işleme ----
    var H = {};
    function handle(c, msg) {
      if (!msg || typeof msg.t !== 'string') return ack(c, msg && msg.rid, false, 'E_BAD_MESSAGE');
      var h = H[msg.t]; if (!h) return ack(c, msg.rid, false, 'E_UNKNOWN_MESSAGE');
      if (h.auth !== false && !c.user) return ack(c, msg.rid, false, 'E_AUTH_REQUIRED');
      var r = h.fn(c, msg, c.user && users[c.user]);
      if (r && r.error) return ack(c, msg.rid, false, r.error, r.detail ? { detail: r.detail } : null);
      ack(c, msg.rid, true, null, r || null);
    }
    function on(t, fn, auth) { H[t] = { fn: fn, auth: auth }; }
    function E(code, detail) { return { error: code, detail: detail }; }

    // ---- Oturum ----
    function attach(c, u) {
      c.user = u.id; u.online = true; presence(u);
      emit(c, 'session', { me: me(u), serverVersion: VERSION, serverTime: clock.now() });
      snapshot(c, u);
    }
    on('hello', function (c, m) { // yeniden bağlanma: token = kullanıcı kimliği (DEMO: imzasız)
      var u = m.token && users[m.token]; if (!u) return E('E_SESSION_EXPIRED');
      var tb = u.tableId && tables[u.tableId];
      if (tb && u.seat >= 0) { tb.conn[u.seat] = true; tb.timeouts[u.seat] = 0; if (tb.auto[u.seat]) { tb.auto[u.seat] = false; if (tb.match && tb.match.status === 'playing' && !tb.deal && tb.match.hand.turn.seat === u.seat) { tb.turnDeadline = clock.now() + tb.turnSec * 1000; driveTurn(tb); } } }
      attach(c, u);
      if (tb && u.seat >= 0) tableEmit(tb, 'seatConnection', { seat: u.seat, connected: true, autoplay: false });
      return { token: u.id };
    }, false);
    on('register', function (c, m) {
      if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(m.email || '')) return E('E_EMAIL_INVALID');
      if ((m.pass || '').length < 6) return E('E_PASS_SHORT');
      var nm = (m.name || '').trim(); if (!/^[A-Za-z0-9_ÇĞİÖŞÜçğıöşü]{3,16}$/.test(nm)) return E('E_NAME_INVALID');
      for (var k in users) { if (users[k].email === m.email) return E('E_EMAIL_EXISTS'); if (users[k].name.toLowerCase() === nm.toLowerCase()) return E('E_NAME_TAKEN'); }
      var u = newUser('u.' + nm.toLowerCase(), { name: nm, ini: nm.slice(0, 2).toUpperCase(), color: '#FF8A5B', level: 1, chips: ECON.start.account.chips, gems: ECON.start.account.gems, code: 'GX-' + codeChars(4) });
      u.email = m.email; u.pass = m.pass; attach(c, u); return { token: u.id };
    }, false);
    // S03: hesap yok / yanlış şifre (kalan hak) / 3 yanlışta 5 dk kilit (Q-14).
    on('login', function (c, m) {
      for (var k in users) if (users[k].email && users[k].email === m.email) {
        var v = users[k]; if (v.lockedUntil && clock.now() < v.lockedUntil) return E('E_ACCOUNT_LOCKED', { until: v.lockedUntil });
        if (v.lockedUntil) { v.lockedUntil = 0; v.fails = 0; }
        if (v.pass !== m.pass) { v.fails = (v.fails || 0) + 1; if (v.fails >= 3) { v.lockedUntil = clock.now() + T.loginLock; return E('E_ACCOUNT_LOCKED', { until: v.lockedUntil }); } return E('E_PASS_WRONG', { left: 3 - v.fails }); }
        v.fails = 0; attach(c, v); return { token: k };
      }
      return E('E_ACCOUNT_UNKNOWN');
    }, false);
    // Şifremi unuttum (Q-14): kod e-postayla gider; şifre değişince kilit kalkar. DEMO: kod her zaman 123456, e-posta gönderilmez.
    on('passwordResetRequest', function (c, m) { return null; }, false); // hesap var/yok bilgisi sızdırılmaz
    on('passwordReset', function (c, m) {
      if (m.code !== '123456') return E('E_RESET_CODE');
      if ((m.pass || '').length < 6) return E('E_PASS_SHORT');
      for (var k in users) if (users[k].email && users[k].email === m.email) { users[k].pass = m.pass; users[k].fails = 0; users[k].lockedUntil = 0; return null; }
      return E('E_RESET_CODE');
    }, false);
    // Google ile devam et (S02). DEMO: kimlik belirteci doğrulanmaz; e-posta ile hesap bulunur ya da açılır.
    on('oauth', function (c, m) {
      if (m.provider !== 'google' || !/^[^@\s]+@[^@\s]+$/.test(m.email || '')) return E('E_OAUTH_FAILED');
      for (var k in users) if (users[k].email === m.email) { attach(c, users[k]); return { token: k }; }
      var nm = String(m.name || m.email.split('@')[0]).slice(0, 16), u = newUser('o.' + (++seqConn), { name: nm, ini: initials(nm), color: '#3D7BFF', level: 1, chips: ECON.start.account.chips, gems: ECON.start.account.gems, code: 'GX-' + codeChars(4) });
      u.email = m.email; attach(c, u); return { token: u.id };
    }, false);
    // S05 misafir: takma ad ≤16 (boşsa Misafir####), 8 renkten biri; ilerleme kaydedilmez.
    on('guest', function (c, m) {
      if (!m.name && !m.color) { var d = users['u.me'] || newUser('u.me', DEMO_ME); attach(c, d); return { token: d.id }; } // DEMO: varsayılan prototip oyuncusu
      var nm = String(m.name || '').trim() || ('Misafir' + (1000 + R.rngInt(rng, 9000))); if (nm.length > 16) return E('E_NAME_INVALID');
      var u = newUser('g.' + (++seqConn), { name: nm, ini: initials(nm), color: m.color || '#3D7BFF', level: 1, chips: ECON.start.guest.chips, gems: ECON.start.guest.gems, code: 'GX-' + codeChars(4) }); u.guest = true;
      attach(c, u); return { token: u.id };
    }, false);
    on('logout', function (c) { c.user = null; return null; });
    on('accountDelete', function (c, m, u) {
      var w = (m.confirm || '').trim().toLocaleUpperCase('tr-TR'); if (w !== 'SİL' && w !== 'DELETE') return E('E_CONFIRM_MISMATCH');
      if (u.tableId) return E('E_IN_GAME'); delete users[u.id]; c.user = null; return null;
    });
    on('resync', function (c, m, u) { snapshot(c, u); return null; });
    on('ping', function () { return { serverTime: clock.now() }; }, false);

    // Tam durum anlık görüntüsü: kopma sonrası hangi ekrana dönüleceği burada belirlenir.
    function snapshot(c, u) {
      var tb = u.tableId && tables[u.tableId], q = queue[u.id], s = { me: me(u), screen: 'menu' };
      if (tb) {
        s.table = tablePub(tb);
        if (tb.status === 'waiting' || tb.status === 'countdown') s.screen = tb.ranked ? 'ranked.loading' : 'waitroom';
        else { s.screen = 'table'; s.game = gameView(tb, u.seat); }
      } else if (q) { s.screen = q.found ? 'ranked.found' : 'ranked.search'; s.ranked = queuePub(q); }
      emit(c, 'snapshot', s);
    }

    // ---- Profil ----
    on('profileUpdate', function (c, m, u) {
      if (m.name != null) { var n = String(m.name).trim(); if (n.length < 3 || n.length > 16 || !/^[\p{L}\p{N} ._]+$/u.test(n)) return E('E_NAME_INVALID'); u.name = n; u.ini = initials(n); }
      if (m.status != null) { if (String(m.status).length > 40) return E('E_STATUS_LONG'); u.status = String(m.status); }
      if (m.color != null) u.color = m.color;
      if (m.frame != null) { if (m.frame === 2 && u.level < 30) return E('E_FRAME_LOCKED'); u.frame = m.frame; }
      toUser(u.id, 'profile', { me: me(u) }); presence(u); return null;
    });
    function initials(n) { return n.split(/\s+/).filter(Boolean).slice(0, 2).map(function (w) { return w.charAt(0).toLocaleUpperCase('tr-TR'); }).join('') || '?'; }

    // ---- Ekonomi ----
    function balance(u, dChips, dGems, reason, ref) {
      u.chips += dChips || 0; u.gems += dGems || 0;
      toUser(u.id, 'balance', { chips: u.chips, gems: u.gems, dChips: dChips || 0, dGems: dGems || 0, reason: reason, ref: ref || null });
    }
    function addXp(u, n, reason) {
      if (!n) return; u.xp += n; var lv = 0;
      while (u.xp >= ECON.levelXp(u.level)) { u.xp -= ECON.levelXp(u.level); u.level++; lv++; }
      toUser(u.id, 'xp', { gained: n, xp: u.xp, xpNext: ECON.levelXp(u.level), level: u.level, levelUp: lv, reason: reason });
    }
    on('shopBuy', function (c, m, u) {
      var it = ECON.shop[m.sku]; if (!it) return E('E_SKU_UNKNOWN');
      if (it.gemCost) { if (u.skins.indexOf(it.skin) >= 0) return E('E_ALREADY_OWNED'); if (u.gems < it.gemCost) return E('E_NO_GEMS', { missing: it.gemCost - u.gems }); u.skins.push(it.skin); balance(u, 0, -it.gemCost, 'shop', m.sku); toUser(u.id, 'profile', { me: me(u) }); return null; }
      // DEMO: gerçek ödeme yok; satın alma anında onaylanır.
      balance(u, it.chips || 0, it.gems || 0, 'purchase', m.sku); return null;
    });
    on('skinSet', function (c, m, u) { if (m.skin !== 'classic' && u.skins.indexOf(m.skin) < 0) return E('E_NOT_OWNED'); u.skin = m.skin; toUser(u.id, 'profile', { me: me(u) }); return null; });

    // ---- Günlük ödül ve görevler ----
    function dailyPub(u) {
      var d = dayOf(clock.now()), claimedToday = u.daily.lastDay === d, streak = u.daily.lastDay === d - 1 || claimedToday ? u.daily.streak : 0;
      var next = claimedToday ? streak : streak + 1; if (next > 7) next = 1;
      return { day: claimedToday ? ((streak - 1) % 7) + 1 : ((next - 1) % 7) + 1, claimedToday: claimedToday, amounts: ECON.daily.slice(), msToReset: 86400000 - (clock.now() % 86400000) };
    }
    function questsPub(u) {
      if (u.questDay !== dayOf(clock.now())) { u.questDay = dayOf(clock.now()); u.quests = {}; }
      return ECON.quests.map(function (q) { var s = u.quests[q.id] || { cur: 0, claimed: false }; return { id: q.id, cur: Math.min(s.cur, q.goal), goal: q.goal, reward: q.reward, gem: q.gem, claimed: s.claimed }; });
    }
    on('dailyGet', function (c, m, u) { emit(c, 'daily', { daily: dailyPub(u), quests: questsPub(u) }); return null; });
    on('dailyClaim', function (c, m, u) {
      var d = dayOf(clock.now()); if (u.daily.lastDay === d) return E('E_DAILY_CLAIMED');
      u.daily.streak = u.daily.lastDay === d - 1 ? u.daily.streak + 1 : 1; u.daily.lastDay = d;
      var amt = ECON.daily[(u.daily.streak - 1) % 7]; balance(u, amt, 0, 'daily', 'day' + (((u.daily.streak - 1) % 7) + 1));
      emit(c, 'daily', { daily: dailyPub(u), quests: questsPub(u) }); return { amount: amt };
    });
    on('questClaim', function (c, m, u) {
      var q = ECON.quests.filter(function (x) { return x.id === m.questId; })[0]; if (!q) return E('E_QUEST_UNKNOWN');
      questsPub(u); var s = u.quests[q.id] || { cur: 0, claimed: false };
      if (s.cur < q.goal) return E('E_QUEST_INCOMPLETE'); if (s.claimed) return E('E_QUEST_CLAIMED');
      s.claimed = true; u.quests[q.id] = s; balance(u, q.reward, q.gem, 'quest', q.id);
      emit(c, 'daily', { daily: dailyPub(u), quests: questsPub(u) }); return null;
    });
    function questTick(u, kind, n) {
      if (!u || u.demo) return; questsPub(u);
      ECON.quests.forEach(function (q) { if (q.on !== kind) return; var s = u.quests[q.id] || { cur: 0, claimed: false }; s.cur += n || 1; u.quests[q.id] = s; });
    }

    // ---- Arkadaşlar ----
    function presence(u) { u.friends.forEach(function (f) { toUser(f, 'presence', { user: pub(u) }); }); }
    on('friendsGet', function (c, m, u) { emit(c, 'friends', { friends: u.friends.map(function (f) { return pub(users[f]); }), requests: u.requestsIn.map(function (f) { return pub(users[f]); }) }); return null; });
    on('friendFind', function (c, m, u) {
      var q = String(m.query || '').replace(/[#\s]/g, '').toUpperCase(); if (/^[A-Z]{2}\d{4}$/.test(q)) q = q.slice(0, 2) + '-' + q.slice(2);
      for (var i = 0; i < u.friends.length; i++) if (users[u.friends[i]].code === q) return { result: 'friend', user: pub(users[u.friends[i]]) };
      for (var k in users) if (k !== u.id && users[k].code === q) return { result: 'player', user: pub(users[k]) };
      for (var k2 in users) if (k2 !== u.id && users[k2].name.toLocaleUpperCase('tr-TR').indexOf(q) >= 0) return { result: 'player', user: pub(users[k2]) };
      return { result: 'none' };
    });
    on('friendRequest', function (c, m, u) {
      var v = users[m.userId]; if (!v || v.id === u.id) return E('E_USER_UNKNOWN');
      if (u.friends.indexOf(v.id) >= 0) return E('E_ALREADY_FRIENDS'); if (u.requestsOut.indexOf(v.id) >= 0) return E('E_REQUEST_PENDING');
      u.requestsOut.push(v.id); v.requestsIn.push(u.id); toUser(v.id, 'friendRequestIn', { from: pub(u) });
      if (v.demo) clock.set(function () { respond(v, u.id, true); }, 1500); // DEMO: sahte oyuncular 1,5 sn sonra kabul eder
      return null;
    });
    function respond(v, fromId, accept) {
      var u = users[fromId]; if (!u) return;
      v.requestsIn = v.requestsIn.filter(function (x) { return x !== fromId; }); u.requestsOut = u.requestsOut.filter(function (x) { return x !== v.id; });
      if (accept) { v.friends.push(fromId); u.friends.push(v.id); }
      toUser(fromId, 'friendRequestResult', { user: pub(v), accepted: accept }); toUser(v.id, 'friendRequestResult', { user: pub(u), accepted: accept });
    }
    on('friendRespond', function (c, m, u) { if (u.requestsIn.indexOf(m.userId) < 0) return E('E_REQUEST_UNKNOWN'); respond(u, m.userId, !!m.accept); return null; });
    on('friendRemove', function (c, m, u) {
      if (u.friends.indexOf(m.userId) < 0) return E('E_NOT_FRIENDS'); var v = users[m.userId];
      u.friends = u.friends.filter(function (x) { return x !== m.userId; }); if (v) v.friends = v.friends.filter(function (x) { return x !== u.id; });
      toUser(u.id, 'friendRemoved', { userId: m.userId }); if (v) toUser(v.id, 'friendRemoved', { userId: u.id }); return null;
    });

    // ---- Liderlik (Q-11) ----
    // Haftalık: maç sonu sırasına göre 30/15/5/1 (çevrimiçi maçlar; özel odalar ve botlara karşı hariç), pazartesi 00:00 (İstanbul) sıfırlanır.
    // Sezon: dereceli LP (idx × 100 + lp). Arkadaşlar: arkadaşlar + sen arasında sezon LP.
    function weeklyPts(v) { return v.weekly.week === weekOf(clock.now()) ? v.weekly.pts : 0; }
    function weeklyAdd(u, n) { var w = weekOf(clock.now()); if (u.weekly.week !== w) { u.weekly.week = w; u.weekly.pts = 0; } u.weekly.pts += n; }
    on('leaderboardGet', function (c, m, u) {
      var board = m.board === 'season' || m.board === 'friends' ? m.board : 'weekly';
      var pool = board === 'friends' ? u.friends.concat([u.id]) : Object.keys(users);
      var rows = pool.map(function (k) { var v = users[k]; return { user: pub(v), rankIdx: v.rank.idx, lp: v.rank.idx * 100 + v.rank.lp, pts: board === 'weekly' ? weeklyPts(v) : v.rank.idx * 100 + v.rank.lp }; })
        .sort(function (a, b) { return b.pts - a.pts || (a.user.id < b.user.id ? -1 : 1); });
      rows.forEach(function (r, i) { r.pos = i > 0 && rows[i - 1].pts === r.pts ? rows[i - 1].pos : i + 1; });
      emit(c, 'leaderboard', { board: board, rows: rows.slice(0, 50), mine: rows.filter(function (r) { return r.user.id === u.id; })[0] || null, resetsAt: board === 'weekly' ? (weekOf(clock.now()) + 1) * 7 * 86400000 - 3 * 86400000 - T.tzOffset : null }); return null;
    });

    // ---- Lobi ----
    function tablePub(tb) {
      return { id: tb.id, code: tb.private ? tb.code : null, name: tb.name, mode: tb.mode, private: tb.private, locked: !!tb.pin, hands: tb.hands, bet: tb.bet, katlama: tb.katlama, katlamaPartner: tb.mode === 'pairs' && tb.katlama ? tb.katlamaPartner : null, turnSec: tb.turnSec,
        rules: clone(tb.rules), owner: tb.owner, ranked: tb.ranked, vsBots: tb.vsBots, status: tb.status, handNo: tb.match ? tb.match.handNo : 0,
        seats: tb.seats.map(function (s, i) { return s ? { user: s.bot ? { id: s.id, name: s.name, ini: s.ini, color: s.color, level: s.level } : pub(users[s.id]), bot: !!s.bot, connected: tb.conn[i] !== false } : null; }),
        spectators: tb.spectators.length, countdown: tb.cd || 0 };
    }
    function lobbyEmit(tb, removed) { for (var k in lobbySubs) if (conns[k]) emit(conns[k], removed ? 'lobbyTableRemoved' : 'lobbyTable', removed ? { id: tb.id } : { table: tablePub(tb) }); }
    function tableEmit(tb, t, data, except) {
      tb.seats.forEach(function (s, i) { if (s && !s.bot && i !== except) toUser(s.id, t, data); });
      tb.spectators.forEach(function (uid) { toUser(uid, t, data); });
    }
    on('lobbySubscribe', function (c) {
      lobbySubs[c.id] = true;
      emit(c, 'lobby', { tables: Object.keys(tables).map(function (k) { return tables[k]; }).filter(function (t) { return !t.ranked && !t.vsBots && !t.private; }).map(tablePub) });
      return null;
    });
    on('lobbyUnsubscribe', function (c) { delete lobbySubs[c.id]; return null; });

    function botSeat(rankIdx) {
      var names = ['Kemal B.', 'Nur S.', 'Ali V.', 'Gül T.', 'Emre D.', 'Ece K.', 'Okan M.', 'Sena P.']; // DEMO
      var n = pick(rng, names); return { bot: true, id: 'b' + (++seqBot), name: n, ini: initials(n), color: pick(rng, ['#3D7BFF', '#FF6B6B', '#3CCB6E', '#B57BFF', '#F5A623', '#16B6A8']), level: 10 + R.rngInt(rng, 40), rankIdx: rankIdx || 0 };
    }
    function makeTable(owner, m) {
      var id = 't' + (++seqTable);
      var tb = { id: id, code: 'GX-' + codeChars(4), name: m.name, mode: m.mode === 'pairs' ? 'pairs' : 'solo', private: !!m.private, pin: m.pin || null,
        hands: m.hands || 5, bet: m.bet == null ? 5000 : m.bet, katlama: !!m.katlama, katlamaPartner: m.katlamaPartner !== false, turnSec: m.turnSec || 30,
        rules: { spectate: m.rules ? m.rules.spectate !== false : true, chat: (m.rules && m.rules.chat) || 'open', gifts: m.rules ? m.rules.gifts !== false : true, isle: m.rules ? m.rules.isle !== false : true, penalties: m.rules ? m.rules.penalties !== false : true },
        owner: owner, ranked: !!m.ranked, vsBots: !!m.vsBots, status: 'waiting', seats: [null, null, null, null], conn: [true, true, true, true], spectators: [], match: null, timers: {}, cd: 0,
        timeouts: [0, 0, 0, 0], auto: [false, false, false, false], betsPaid: 0, ready: {}, chatRng: { s: R.rngNext(rng) }, seed: R.rngNext(rng), demoOwner: !!m.demoOwner };
      tables[id] = tb; return tb;
    }
    function codeChars(n) { var A = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789', s = ''; for (var i = 0; i < n; i++) s += A.charAt(R.rngInt(rng, A.length)); return s; }
    // DEMO: lobide hazır masalar (prototipteki örnek liste). Kod GX-7Q2M ve şifre 1234 bu masalara bağlı.
    (function seedLobby() {
      var a = makeTable('u.mk', { name: 'Mehmet’in masası', mode: 'solo', bet: 5000, hands: 5, turnSec: 30, demoOwner: true });
      a.seats[0] = { id: 'u.mk' }; a.seats[1] = { id: 'u.bt' };
      var b = makeTable('u.sa', { name: 'Akşam keyfi', mode: 'pairs', bet: 10000, hands: 7, turnSec: 30, pin: DEMO_PIN, demoOwner: true });
      b.seats[0] = { id: 'u.sa' }; b.seats[1] = { id: 'u.ec' }; b.seats[2] = { id: 'u.ck' };
      var c = makeTable('u.zo', { name: 'Özel oda', mode: 'solo', bet: 1000, hands: 3, turnSec: 20, private: true, demoOwner: true }); c.code = DEMO_CODE; c.seats[0] = { id: 'u.zo' };
      [a, b, c].forEach(function (t) { t.seats.forEach(function (s) { if (s) { users[s.id].tableId = t.id; } }); });
    })();

    on('tableCreate', function (c, m, u) {
      if (u.tableId) return E('E_ALREADY_SEATED');
      if (!m.name || !String(m.name).trim()) return E('E_TABLE_NAME');
      if (m.pin != null && !/^\d{4}$/.test(m.pin)) return E('E_PIN_FORMAT');
      if (ECON.bets.indexOf(m.bet) < 0 || ECON.hands.indexOf(m.hands) < 0 || ECON.turnSec.indexOf(m.turnSec) < 0) return E('E_TABLE_OPTIONS');
      if (u.chips < m.bet) return E('E_NO_CHIPS', { missing: m.bet - u.chips });
      var tb = makeTable(u.id, m); seatUser(tb, u, 0); lobbyEmit(tb);
      return { table: tablePub(tb) };
    });
    function seatUser(tb, u, seat) {
      if (tb.bet && !tb.ranked && !tb.vsBots) { balance(u, -tb.bet, 0, 'bet', tb.id); tb.betsPaid += tb.bet; }   // R-90 bahis oturunca düşülür
      tb.seats[seat] = { id: u.id }; u.tableId = tb.id; u.seat = seat; tb.conn[seat] = true;
      tableEmit(tb, 'tableState', { table: tablePub(tb) }); lobbyEmit(tb); maybeAutoStart(tb);
    }
    function joinCheck(tb, u, pin) {
      if (!tb) return E('E_TABLE_CLOSED'); if (u.tableId) return E('E_ALREADY_SEATED');
      if (tb.pin && pin !== tb.pin) return E('E_PIN_WRONG');
      var free = tb.seats.indexOf(null); if (free < 0) return E('E_TABLE_FULL');
      if (tb.bet && u.chips < tb.bet) return E('E_NO_CHIPS', { missing: tb.bet - u.chips });
      return { seat: free };
    }
    on('tableJoin', function (c, m, u) {
      var tb = tables[m.tableId];
      if (tb && tb.status === 'playing') return takeOver(c, tb, u, m);
      var r = joinCheck(tb, u, m.pin); if (r.error) return r;
      var seat = m.seat != null && tb.seats[m.seat] === null ? m.seat : r.seat;
      seatUser(tb, u, seat); return { table: tablePub(tb), seat: seat };
    });
    // R-65 "Oyuna katıl" (Q-13): oynanan masada botun ya da kalkan oyuncunun koltuğu devralınır; bahis o an ödenir, koltuğun toplamıyla devam edilir.
    function takeOver(c, tb, u, m) {
      if (u.tableId) return E('E_ALREADY_SEATED'); if (tb.ranked || tb.vsBots) return E('E_TABLE_PLAYING');
      if (tb.pin && m.pin !== tb.pin) return E('E_PIN_WRONG');
      var k = m.seat != null ? m.seat : -1;
      if (k < 0) for (var i = 0; i < 4; i++) if (tb.seats[i] && tb.seats[i].bot) { k = i; break; }
      if (k < 0 || !tb.seats[k] || !tb.seats[k].bot) return E('E_TABLE_FULL');
      if (tb.bet && u.chips < tb.bet) return E('E_NO_CHIPS', { missing: tb.bet - u.chips });
      var r = R.apply(tb.match, { t: 'takeSeat', seat: k }); if (!r.ok) return E(r.error);
      tb.match = r.state; cancel(tb, 'bot');
      if (tb.bet) { balance(u, -tb.bet, 0, 'bet', tb.id); tb.betsPaid += tb.bet; }
      tb.seats[k] = { id: u.id }; tb.conn[k] = true; tb.timeouts[k] = 0; tb.auto[k] = false; u.tableId = tb.id; u.seat = k;
      route(tb, r.events); tableEmit(tb, 'tableState', { table: tablePub(tb) }); lobbyEmit(tb);
      var S = tb.match;
      if (S.status === 'playing' && !tb.deal && S.hand.turn.seat === k) tb.turnDeadline = clock.now() + tb.turnSec * 1000;
      emit(c, 'snapshot', { me: me(u), screen: 'table', table: tablePub(tb), game: gameView(tb, k) });
      if (S.status === 'playing' && !tb.deal) driveTurn(tb);
      return { table: tablePub(tb), seat: k };
    }
    on('tableJoinCode', function (c, m, u) {
      var code = String(m.code || '').toUpperCase().replace(/\s/g, ''); if (!/^GX-[A-Z0-9]{4}$/.test(code)) return E('E_CODE_FORMAT');
      var tb = null; for (var k in tables) if (tables[k].code === code && tables[k].status !== 'over') tb = tables[k];
      if (!tb) return E('E_CODE_UNKNOWN');
      var r = joinCheck(tb, u, m.pin); if (r.error) return r; seatUser(tb, u, r.seat); return { table: tablePub(tb), seat: r.seat };
    });
    on('tableSpectate', function (c, m, u) {
      var tb = tables[m.tableId]; if (!tb) return E('E_TABLE_CLOSED'); if (!tb.rules.spectate) return E('E_SPECTATE_OFF'); if (u.tableId) return E('E_ALREADY_SEATED');
      tb.spectators.push(u.id); u.tableId = tb.id; u.seat = -1;
      emit(c, 'snapshot', { me: me(u), screen: tb.status === 'playing' ? 'table' : 'waitroom', table: tablePub(tb), game: tb.match ? gameView(tb, -1) : null, spectator: true }); return null;
    });
    on('tableLeave', function (c, m, u) {
      var tb = tables[u.tableId]; if (!tb) return E('E_NOT_SEATED');
      if (u.seat < 0) { tb.spectators = tb.spectators.filter(function (x) { return x !== u.id; }); u.tableId = null; return null; }
      var seat = u.seat;
      if (tb.status === 'waiting' || tb.status === 'countdown') return leaveWaitRoom(tb, u, 'leave');
      // R-60 maç içinde kalkma: bahis kaybedilir, maçta son, yerine bot.
      var r = R.apply(tb.match, { t: 'leave', seat: seat }); if (!r.ok) return E(r.error);
      tb.match = r.state; var bot = botSeat(0); bot.name = users[u.id].name; bot.replaces = u.id;
      tb.seats[seat] = bot; u.tableId = null; u.seat = -1;
      if (tb.ranked) rankApply(u, 3, tb);                                       // Dereceli: maçtan ayrılmak = 4. sıra
      route(tb, r.events); tableEmit(tb, 'tableState', { table: tablePub(tb) });
      emit(c, 'leftTable', { reason: 'leave', toLobbyIn: T.leaveToLobby });
      if (tb.match.status === 'playing' && !tb.deal && tb.match.hand.turn.seat === seat) driveTurn(tb);
      return null;
    });
    function leaveWaitRoom(tb, u, reason) {                                    // R-90 maç başlamadan çıkana bahis iadesi
      var seat = u.seat;
      if (tb.bet && !tb.ranked && !tb.vsBots) { balance(u, tb.bet, 0, 'betRefund', tb.id); tb.betsPaid -= tb.bet; }
      clearCountdown(tb); tb.seats[seat] = null; u.tableId = null; u.seat = -1;
      toUser(u.id, 'leftTable', { reason: reason, toLobbyIn: 0 });
      if (tb.owner === u.id) { closeTable(tb, 'ownerLeft'); return null; }
      tableEmit(tb, 'tableState', { table: tablePub(tb) }); lobbyEmit(tb); return null;
    }
    function closeTable(tb, reason) {
      Object.keys(tb.timers).forEach(function (k) { clock.clear(tb.timers[k]); });
      tb.seats.forEach(function (s) { if (s && !s.bot && users[s.id]) { var v = users[s.id]; if ((tb.status === 'waiting' || tb.status === 'countdown') && tb.bet && !tb.ranked && !tb.vsBots && v.id !== tb.owner) balance(v, tb.bet, 0, 'betRefund', tb.id); v.tableId = null; v.seat = -1; } });
      tableEmit(tb, 'tableClosed', { id: tb.id, reason: reason });
      tb.spectators.forEach(function (x) { if (users[x]) users[x].tableId = null; });
      tb.status = 'over'; lobbyEmit(tb, true); delete tables[tb.id];
    }

    // ---- Bekleme odası ----
    function ownerOnly(u) { var tb = tables[u.tableId]; if (!tb) return E('E_NOT_SEATED'); if (tb.owner !== u.id) return E('E_NOT_OWNER'); if (tb.status !== 'waiting') return E('E_STATUS'); return tb; }
    on('seatAddBot', function (c, m, u) {
      var tb = ownerOnly(u); if (tb.error) return tb; if (tb.seats[m.seat] !== null) return E('E_SEAT_TAKEN');
      tb.seats[m.seat] = botSeat(0); tableEmit(tb, 'tableState', { table: tablePub(tb) }); lobbyEmit(tb); return null;
    });
    on('seatKick', function (c, m, u) {
      var tb = ownerOnly(u); if (tb.error) return tb; var s = tb.seats[m.seat]; if (!s || s.id === u.id) return E('E_SEAT_EMPTY');
      if (!s.bot) { var v = users[s.id]; if (tb.bet) balance(v, tb.bet, 0, 'betRefund', tb.id); v.tableId = null; v.seat = -1; toUser(v.id, 'kicked', { tableId: tb.id }); }
      tb.seats[m.seat] = null; tableEmit(tb, 'tableState', { table: tablePub(tb) }); lobbyEmit(tb); return null;
    });
    on('tableStart', function (c, m, u) {
      var tb = ownerOnly(u); if (tb.error) return tb; if (tb.seats.indexOf(null) >= 0) return E('E_TABLE_NOT_FULL');
      startCountdown(tb); return null;
    });
    function maybeAutoStart(tb) {
      if (tb.status !== 'waiting' || tb.seats.indexOf(null) >= 0) return;
      var ownerHuman = tb.seats.some(function (s) { return s && !s.bot && s.id === tb.owner && !users[s.id].demo; });
      if (!ownerHuman) tb.timers.auto = clock.set(function () { startCountdown(tb); }, T.seatedToCountdown);
    }
    function startCountdown(tb) {
      if (tb.status !== 'waiting') return; tb.status = 'countdown'; tb.cd = 3;
      tableEmit(tb, 'countdown', { n: 3, startsIn: T.waitRoomCountdown }); lobbyEmit(tb);
      var tick = function () {
        tb.cd -= 1;
        if (tb.cd <= 0) { tb.cd = 0; return startMatch(tb); }
        tableEmit(tb, 'countdown', { n: tb.cd, startsIn: tb.cd * 1000 }); tb.timers.cd = clock.set(tick, 1000);
      };
      tb.timers.cd = clock.set(tick, 1000);
    }
    function clearCountdown(tb) { if (tb.status === 'countdown') { clock.clear(tb.timers.cd); tb.status = 'waiting'; tb.cd = 0; tableEmit(tb, 'countdown', { n: 0, cancelled: true }); } clock.clear(tb.timers.auto); }

    // ---- Davet ----
    on('inviteSend', function (c, m, u) {
      var tb = tables[u.tableId]; if (!tb) return E('E_NOT_SEATED'); if (tb.status !== 'waiting') return E('E_STATUS');
      var v = users[m.userId]; if (!v) return E('E_USER_UNKNOWN'); if (u.friends.indexOf(v.id) < 0 && !m.recent) return E('E_NOT_FRIENDS');
      if (!v.online) return E('E_USER_OFFLINE'); if (v.tableId) return E('E_USER_BUSY');
      if (tb.seats.indexOf(null) < 0) return E('E_TABLE_FULL');
      var id = 'i' + (++seqInvite), inv = { id: id, from: u.id, to: v.id, table: tb.id, seat: m.seat != null ? m.seat : tb.seats.indexOf(null), expiresAt: clock.now() + T.inviteTtl };
      invites[id] = inv; inv.timer = clock.set(function () { if (!invites[id]) return; delete invites[id]; toUser(u.id, 'inviteResult', { inviteId: id, userId: v.id, result: 'expired' }); toUser(v.id, 'inviteExpired', { inviteId: id }); }, T.inviteTtl);
      toUser(v.id, 'inviteIn', { inviteId: id, from: pub(u), table: tablePub(tb), expiresAt: inv.expiresAt });
      if (v.demo) clock.set(function () { inviteAnswer(inv, true); }, 1200 + R.rngInt(rng, 800)); // DEMO: sahte arkadaşlar 1,2–2 sn içinde kabul eder
      return { inviteId: id, expiresAt: inv.expiresAt };
    });
    function inviteAnswer(inv, accept) {
      if (!invites[inv.id]) return E('E_INVITE_EXPIRED'); delete invites[inv.id]; clock.clear(inv.timer);
      var tb = tables[inv.table], v = users[inv.to];
      if (!accept) { toUser(inv.from, 'inviteResult', { inviteId: inv.id, userId: v.id, result: 'declined' }); return null; }
      var r = joinCheck(tb, v, tb && tb.pin); if (r.error) { toUser(inv.from, 'inviteResult', { inviteId: inv.id, userId: v.id, result: 'failed', error: r.error }); return r; }
      var seat = tb.seats[inv.seat] === null ? inv.seat : r.seat;
      toUser(inv.from, 'inviteResult', { inviteId: inv.id, userId: v.id, result: 'accepted', seat: seat });
      seatUser(tb, v, seat); return null;
    }
    on('inviteRespond', function (c, m, u) { var inv = invites[m.inviteId]; if (!inv || inv.to !== u.id) return E('E_INVITE_EXPIRED'); return inviteAnswer(inv, !!m.accept); });
    on('inviteCancel', function (c, m, u) { var inv = invites[m.inviteId]; if (!inv || inv.from !== u.id) return E('E_INVITE_EXPIRED'); delete invites[inv.id]; clock.clear(inv.timer); toUser(inv.to, 'inviteExpired', { inviteId: inv.id }); return null; });

    // ---- Botlara karşı ----
    on('vsBotsStart', function (c, m, u) {
      if (u.tableId) return E('E_ALREADY_SEATED');
      var tb = makeTable(u.id, { name: 'Botlara karşı', mode: m.mode, bet: 0, hands: m.hands || 5, turnSec: m.turnSec || 30, katlama: !!m.katlama, katlamaPartner: m.katlamaPartner, vsBots: true, rules: { spectate: false, chat: 'open', gifts: false } });
      tb.seats[0] = { id: u.id }; u.tableId = tb.id; u.seat = 0; for (var i = 1; i < 4; i++) tb.seats[i] = botSeat(0);
      emit(c, 'tableState', { table: tablePub(tb) }); startMatch(tb); return { table: tablePub(tb) };
    });

    // ---- Dereceli eşleştirme ----
    function queuePub(q) { return { mode: q.mode, since: q.since, range: clock.now() - q.since >= T.rankedWiden ? 2 : 1, found: q.found ? { matchId: q.found.id, players: q.found.players, expiresAt: q.found.expiresAt, accepted: q.found.accepted } : null, filled: q.filled }; }
    on('rankedQueue', function (c, m, u) {
      if (u.tableId) return E('E_ALREADY_SEATED'); if (queue[u.id]) return E('E_ALREADY_QUEUED');
      if (['solo', 'duo', 'squad'].indexOf(m.mode) < 0) return E('E_MODE');
      var party = (m.party || []).filter(function (x) { return users[x]; });
      if (m.mode === 'duo' && party.length !== 1) return E('E_PARTY_SIZE'); if (m.mode === 'squad' && party.length !== 3) return E('E_PARTY_SIZE');
      for (var i = 0; i < party.length; i++) if (Math.abs(users[party[i]].rank.idx - u.rank.idx) > 3) return E('E_PARTY_RANGE');
      var q = { user: u.id, mode: m.mode, party: party, since: clock.now(), filled: 1 + party.length, found: null, timers: [] };
      queue[u.id] = q; emit(c, 'rankedSearch', queuePub(q));
      if (m.mode === 'squad') { q.timers.push(clock.set(function () { rankedFound(q); }, 600)); return null; }
      // DEMO: gerçek kuyruk yok; rakipler 1,2 sn arayla bulunur (aralık ±1 lig, 30 sn sonra ±2).
      var step = function () { if (!queue[u.id] || q.found) return; q.filled++; toUser(u.id, 'rankedSearch', queuePub(q)); if (q.filled >= 4) rankedFound(q); else q.timers.push(clock.set(step, 1200)); };
      q.timers.push(clock.set(step, 1200));
      q.timers.push(clock.set(function () { if (queue[u.id] && !q.found) toUser(u.id, 'rankedSearch', queuePub(q)); }, T.rankedWiden));
      return null;
    });
    function rankedFound(q) {
      var u = users[q.user], ids = [u.id].concat(q.party), players = ids.map(function (x) { return { user: pub(users[x]), bot: false }; });
      while (players.length < 4) { var b = botSeat(Math.max(0, Math.min(15, u.rank.idx + R.rngInt(rng, 3) - 1))); players.push({ user: { id: b.id, name: b.name, ini: b.ini, color: b.color, level: b.level, rankIdx: b.rankIdx }, bot: true, seat: b }); }
      q.found = { id: 'r' + (++seqMatch), players: players, expiresAt: clock.now() + T.rankedAccept, accepted: players.map(function (p) { return false; }) };
      toUser(u.id, 'rankedFound', queuePub(q));
      players.forEach(function (p, i) { if (p.bot || users[p.user.id].demo) q.timers.push(clock.set(function () { if (q.found) { q.found.accepted[i] = true; toUser(u.id, 'rankedAccepted', { index: i, accepted: q.found.accepted }); rankedCheck(q); } }, 800 + 400 * i)); }); // DEMO
      q.timers.push(clock.set(function () { if (q.found && q.found.accepted.indexOf(false) >= 0) { q.found = null; q.since = clock.now(); q.filled = 1 + q.party.length; toUser(u.id, 'rankedSearch', queuePub(q)); toUser(u.id, 'rankedCancelled', { reason: 'timeout' }); } }, T.rankedAccept));
    }
    function rankedCheck(q) {
      if (!q.found || q.found.accepted.indexOf(false) >= 0) return;
      var u = users[q.user], f = q.found; q.timers.forEach(clock.clear); delete queue[u.id];
      var tb = makeTable(u.id, { name: 'Dereceli', mode: q.mode === 'duo' ? 'pairs' : 'solo', bet: 0, hands: 5, turnSec: 30, ranked: true, rules: { spectate: false, chat: 'quick', gifts: false } });
      var order = q.mode === 'duo' ? [0, 2, 1, 3] : [0, 1, 2, 3];
      f.players.forEach(function (p, i) { var s = order[i]; if (p.bot) tb.seats[s] = p.seat; else { tb.seats[s] = { id: p.user.id }; users[p.user.id].tableId = tb.id; users[p.user.id].seat = s; } });
      tableEmit(tb, 'rankedReady', { table: tablePub(tb), loadingMs: T.rankedLoading });
      tb.timers.load = clock.set(function () { startMatch(tb); }, T.rankedLoading);
    }
    on('rankedAccept', function (c, m, u) {
      var q = queue[u.id]; if (!q || !q.found || q.found.id !== m.matchId) return E('E_MATCH_EXPIRED');
      if (!m.accept) { q.timers.forEach(clock.clear); delete queue[u.id]; emit(c, 'rankedCancelled', { reason: 'declined' }); return null; }
      q.found.accepted[0] = true; emit(c, 'rankedAccepted', { index: 0, accepted: q.found.accepted }); rankedCheck(q); return null;
    });
    on('rankedCancel', function (c, m, u) { var q = queue[u.id]; if (!q) return E('E_NOT_QUEUED'); q.timers.forEach(clock.clear); delete queue[u.id]; emit(c, 'rankedCancelled', { reason: 'cancel' }); return null; });
    function rankApply(u, place, tb, dOverride) {
      var r = u.rank, before = { idx: r.idx, lp: r.lp }, d = dOverride != null ? dOverride : RANK.deltas(r.idx)[place], prevLeague = Math.floor(r.idx / 3), prot = r.prot;
      r.games++; if (place === 0) r.top++; r.prot = Math.max(0, prot - 1);
      if (d >= 0) { r.lp += d; while (r.lp >= 100 && r.idx < RANK.steps) { r.lp -= 100; r.idx++; if (Math.floor(r.idx / 3) !== prevLeague) r.prot = RANK.protectGames; } if (r.idx >= RANK.steps) r.idx = RANK.steps; }
      else {
        var lp = r.lp + d;
        if (lp >= 0) r.lp = lp;
        else if (r.idx === 0) r.lp = 0;
        else if (r.idx % 3 === 0 && prot > 0) r.lp = 0;                    // koruma: lig düşüşü yok
        else { r.idx--; r.lp = 100 + lp; }
      }
      r.peak = Math.max(r.peak, r.idx); r.hist.unshift({ place: place + 1, lp: d, at: clock.now(), table: tb.id }); r.hist = r.hist.slice(0, 20);
      toUser(u.id, 'rankChange', { before: before, after: { idx: r.idx, lp: r.lp }, delta: d, place: place + 1, tierUp: r.idx > before.idx, leagueUp: Math.floor(r.idx / 3) > Math.floor(before.idx / 3), down: r.idx < before.idx, protectedLeft: r.prot });
    }

    // ---- Maç ----
    function cancel(tb, k) { if (tb.timers[k]) { clock.clear(tb.timers[k]); delete tb.timers[k]; } }
    function isBotSeat(tb, k) { var s = tb.seats[k]; return !s || s.bot || tb.auto[k] || (tb.match && tb.match.seats[k].bot); }
    function startMatch(tb) {
      tb.status = 'playing';
      tb.match = R.newMatch({ mode: tb.mode, hands: tb.hands, penalties: tb.rules.penalties, isle: tb.rules.isle, katlama: tb.katlama, katlamaPartner: tb.katlamaPartner }, tb.seed);
      tb.seats.forEach(function (s, k) { if (s && s.bot) tb.match.seats[k].bot = true; });
      tableEmit(tb, 'matchStart', { table: tablePub(tb), countdownMs: T.preDealCountdown }); lobbyEmit(tb);
      tb.timers.pre = clock.set(function () { startHand(tb); }, T.preDealCountdown);
    }
    // Dağıtım töreni: sunucu her adımı zamanında yayınlar; zar sonucu atılmadan istemciye gitmez.
    function startHand(tb) {
      tb.ready = {}; tb.timeouts = [0, 0, 0, 0];
      var rig = tb.match.handNo === 0 && o.rig ? o.rig : undefined;   // DEMO: senaryo (logic/scenarios) yalnız ilk el
      var r = R.apply(tb.match, { t: 'startHand', rig: rig }); tb.match = r.state;
      var H = tb.match.hand, d = H.deal, D = d.dealer, human = !isBotSeat(tb, D), t0 = clock.now();
      tb.deal = { step: 'build', dice: [], dealer: D, t0: t0 };
      tableEmit(tb, 'dealStart', { handNo: tb.match.handNo, dealer: D, stacks: 15, perStack: 7, buildMs: T.deal.build, humanDealer: human });
      tb.timers.deal = clock.set(function () {
        tb.deal.step = 'dice'; tableEmit(tb, 'dealStep', { step: 'dice', dealer: D, rollWaitMs: human ? T.deal.humanRollWait : T.deal.botRollDelay });
        tb.timers.roll = clock.set(function () { roll(tb); }, human ? T.deal.humanRollWait : T.deal.botRollDelay);
      }, T.deal.build);
    }
    function roll(tb) {
      var x = tb.deal; if (!x || x.step !== 'dice' || x.rolling) return; cancel(tb, 'roll');
      var i = x.dice.length, v = tb.match.hand.deal.dice[i], ms = i === 0 ? T.deal.roll1 : T.deal.roll2; x.rolling = true;
      tableEmit(tb, 'dealDie', { index: i, value: v, rollMs: ms });
      tb.timers.roll = clock.set(function () {
        x.rolling = false; x.dice.push(v);
        if (x.dice.length === 2) return dealRest(tb);
        var human = !isBotSeat(tb, x.dealer);
        tb.timers.roll = clock.set(function () { roll(tb); }, human ? T.deal.humanRollWait : T.deal.betweenRolls);
      }, ms);
    }
    function dealRest(tb) {
      var H = tb.match.hand, d = H.deal, D = T.deal, t = D.afterDice;
      tb.deal.step = 'indicator';
      tb.timers.d1 = clock.set(function () { tableEmit(tb, 'dealStep', { step: 'indicator', stack: d.indStack, level: d.indLevel, rolledLevel: d.rolledLevel, fakeSkip: d.fakeSkip, indicator: H.indicator, okey: H.okey }); }, t);
      t += D.indicator; tb.timers.d2 = clock.set(function () { tableEmit(tb, 'dealStep', { step: 'extra', stack: d.extraStack }); }, t);
      t += D.extra; tb.timers.d3 = clock.set(function () { tableEmit(tb, 'dealStep', { step: 'distribute', plan: d.plan }); }, t);
      t += D.distribute; tb.timers.d4 = clock.set(function () { tableEmit(tb, 'dealStep', { step: 'center', stacks: d.centerStacks, deck: H.deck.length }); }, t);
      t += D.center; tb.timers.d5 = clock.set(function () {
        // Her oyuncuya yalnız kendi ıstakası; diğerlerinin sayısı.
        tb.seats.forEach(function (s, k) { if (s && !s.bot) toUser(s.id, 'handDealt', { handNo: tb.match.handNo, rack: H.racks[k].slice(), counts: H.racks.map(function (x) { return x.length; }), deck: H.deck.length, indicator: H.indicator, okey: H.okey, starter: H.turn.seat }); });
        pushView(tb);
        tb.spectators.forEach(function (uid) { toUser(uid, 'handDealt', { handNo: tb.match.handNo, rack: null, counts: H.racks.map(function (x) { return x.length; }), deck: H.deck.length, indicator: H.indicator, okey: H.okey, starter: H.turn.seat }); });
      }, t);
      t += D.fill; tb.timers.d6 = clock.set(function () { tb.deal = null; startTurn(tb); }, t);
    }
    function startTurn(tb) {
      var S = tb.match, Tn = S.hand.turn, ms = tb.turnSec * 1000;
      tb.turnDeadline = clock.now() + ms; pushView(tb);
      tableEmit(tb, 'turn', { seat: Tn.seat, phase: Tn.phase, n: Tn.n, turnMs: ms, deadline: tb.turnDeadline, legal: null });
      tb.seats.forEach(function (s, k) { if (s && !s.bot && k === Tn.seat) toUser(s.id, 'legal', { legal: R.legal(S, k) }); });
      driveTurn(tb);
    }
    function driveTurn(tb) {
      var S = tb.match; if (S.status !== 'playing') return; var k = S.hand.turn.seat;
      cancel(tb, 'turn'); cancel(tb, 'bot');
      if (isBotSeat(tb, k)) { botStep(tb, null); return; }
      var left = Math.max(0, tb.turnDeadline - clock.now());
      tb.timers.turn = clock.set(function () { timeout(tb, k); }, left);
    }
    function botStep(tb, prev) {
      var S = tb.match; if (S.status !== 'playing') return;
      var k = S.hand.turn.seat, a = B.nextAction(S, k);
      tb.timers.bot = clock.set(function () {
        if (tb.match !== S || !isBotSeat(tb, k)) { if (tb.match.status === 'playing' && tb.match.hand.turn.seat === k) driveTurn(tb); return; } // durum arada değiştiyse (ör. masadan kalkma) yeniden planla
        var r = R.apply(S, a); if (!r.ok) r = R.apply(S, { t: 'timeout', seat: k });
        commit(tb, r, k);
        if (a.t === 'discard' && tb.rules.chat !== 'off') { var line = B.chatAfterDiscard(tb.chatRng); if (line) tableEmit(tb, 'chat', { seat: k, quick: line, bot: true }); }
      }, B.delayFor(prev, a));
    }
    function timeout(tb, k) {
      var S = tb.match; if (S.status !== 'playing' || S.hand.turn.seat !== k) return;
      tb.timeouts[k]++;
      var r = R.apply(S, { t: 'timeout', seat: k });
      if (T.autoplayAfterTimeouts && tb.timeouts[k] >= T.autoplayAfterTimeouts && tb.conn[k] === false && !tb.auto[k]) { tb.auto[k] = true; tableEmit(tb, 'seatConnection', { seat: k, connected: false, autoplay: true }); }
      commit(tb, r, k);
    }
    // Motor sonucu: durumu yaz, olayları görünürlük kuralıyla dağıt, sıradaki adımı başlat.
    function commit(tb, r, k) {
      if (!r.ok) return r;
      var before = tb.match; tb.match = r.state; route(tb, r.events); pushView(tb);
      var S = tb.match, turnChanged = r.events.some(function (e) { return e.e === 'turn'; });
      if (S.status === 'handOver' || S.status === 'matchOver') return handOver(tb, r.events);
      if (turnChanged) { tb.timeouts[S.hand.turn.seat] = tb.timeouts[S.hand.turn.seat]; startTurn(tb); }
      else if (isBotSeat(tb, S.hand.turn.seat)) botStep(tb, lastAction(r));
      else tb.seats.forEach(function (s, i) { if (s && !s.bot && i === S.hand.turn.seat) toUser(s.id, 'legal', { legal: R.legal(S, i) }); });
      return r;
    }
    function lastAction(r) { var e = r.events[r.events.length - 1]; return e ? { t: e.e === 'drew' ? 'draw' : 'open' } : null; }
    // Gizli bilgi: desteden çekilen taş yalnız çekene; eksik açılışta taşlar yalnız açmaya çalışana.
    function route(tb, events) {
      events.forEach(function (e) {
        tb.seats.forEach(function (s, i) { if (s && !s.bot) toUser(s.id, 'game', { ev: viewEvent(e, i) }); });
        tb.spectators.forEach(function (uid) { toUser(uid, 'game', { ev: viewEvent(e, -1) }); });
        questEvent(tb, e);
      });
    }
    function viewEvent(e, seat) {
      var v = clone(e);
      if (e.e === 'drew' && e.src === 'deck' && e.seat !== seat) v.tile = null;
      if (e.e === 'openShort' && e.seat !== seat) v.tiles = null;
      if (e.e === 'handStart') { v.dice = null; v.indicator = null; v.okey = null; }    // tören olaylarıyla zamanında gelir
      return v;
    }
    function questEvent(tb, e) {
      var uidAt = function (k) { var s = tb.seats[k]; return s && !s.bot ? users[s.id] : null; };
      if (e.e === 'opened' && e.mode === 'pair') questTick(uidAt(e.seat), 'openPairs');
      if (e.e === 'added') questTick(uidAt(e.seat), 'added');
      if (e.e === 'handEnd') {                                                    // R-93 el XP: bitiren +340, diğerleri +60 (taş bittiyse herkes +60)
        for (var x = 0; x < 4; x++) { var hu = uidAt(x); if (hu && !hu.demo) addXp(hu, x === e.result.finisher ? ECON.xp.handWin : ECON.xp.hand, 'hand'); }
        for (var k = 0; k < 4; k++) questTick(uidAt(k), 'handPlayed'); if (e.result.finisher >= 0) { questTick(uidAt(e.result.finisher), 'handWin'); if (e.result.okeyFinish) questTick(uidAt(e.result.finisher), 'okeyFinish'); } }
    }
    function handOver(tb, events) {
      cancel(tb, 'turn'); cancel(tb, 'bot');
      var S = tb.match;
      if (S.status === 'matchOver') return matchOver(tb);
      tb.nextAt = clock.now() + T.handEndWait;
      tableEmit(tb, 'handWait', { nextHandIn: T.handEndWait, nextAt: tb.nextAt, handNo: S.handNo, hands: S.opts.hands });
      tb.timers.next = clock.set(function () { startHand(tb); }, T.handEndWait);
    }
    function matchOver(tb) {
      var S = tb.match, rk = S.ranking, pool = tb.betsPaid, payouts = [0, 0, 0, 0];   // D-34: havuz = ödenen bahislerin toplamı
      var human = function (k) { var s = tb.seats[k]; return s && !s.bot; };
      if (pool && !tb.ranked && !tb.vsBots) {                                    // R-91 kesinti yok; beraberlikte bölünür; eşlide takım payı masada kalan ortaklara
        var win = rk.filter(function (u) { return u.rank === 1; }), share = Math.floor(pool * (1 - ECON.rake) / win.length);
        win.forEach(function (u) { var el = u.seats.filter(human); el.forEach(function (k) { payouts[k] += Math.floor(share / el.length); }); });
        tb.seats.forEach(function (s, k) { if (payouts[k]) balance(users[s.id], payouts[k], 0, 'win', tb.id); });
      }
      // R-59/R-95 paylaşılan sıra: her koltuk, biriminin (ve berabere birimlerin) kapladığı sıra konumlarının ortalamasını alır.
      var occ = [null, null, null, null], pos = 0, i = 0;
      while (i < rk.length) {
        var j = i, n = 0; while (j < rk.length && rk[j].rank === rk[i].rank) { n += rk[j].seats.length; j++; }
        var range = []; for (var p = pos; p < pos + n; p++) range.push(p);
        for (var q = i; q < j; q++) rk[q].seats.forEach(function (k) { occ[k] = range; });
        pos += n; i = j;
      }
      var shared = function (arr, k) { var r = occ[k], t = 0; r.forEach(function (p) { t += arr[p]; }); return Math.round(t / r.length); };
      var xpOut = [0, 0, 0, 0], lpOut = [null, null, null, null], wkOut = [0, 0, 0, 0];
      tb.seats.forEach(function (s, k) {
        if (!s || s.bot) return; var u = users[s.id];
        xpOut[k] = shared(ECON.xp.place, k); addXp(u, xpOut[k], 'match');                     // R-93 sıra bonusu (kalkan almaz)
        if (tb.ranked) { lpOut[k] = shared(RANK.deltas(u.rank.idx), k); rankApply(u, occ[k][0], tb, lpOut[k]); }
        if (!tb.vsBots && !tb.private) { wkOut[k] = shared(ECON.weekly, k); weeklyAdd(u, wkOut[k]); }   // R-95
      });
      tableEmit(tb, 'matchResult', { ranking: rk, totals: S.totals.slice(), history: S.history, pool: pool, rake: ECON.rake, payouts: payouts, xpBonus: xpOut, lp: lpOut, weekly: wkOut });
      tb.status = 'over'; lobbyEmit(tb, true);
      tb.seats.forEach(function (s) { if (s && !s.bot && users[s.id]) { users[s.id].tableId = null; users[s.id].seat = -1; } });
      delete tables[tb.id];                                                      // OPEN Q-12: maç sonrası masa kapanır
    }

    // ---- Masa hamleleri (C2S → kural motoru) ----
    function gameCtx(u) { var tb = tables[u.tableId]; if (!tb || !tb.match) return E('E_NOT_SEATED'); if (u.seat < 0) return E('E_SPECTATOR'); return tb; }
    function move(type, map) {
      on(type, function (c, m, u) {
        var tb = gameCtx(u); if (tb.error) return tb;
        if (tb.deal) return E('E_DEALING');
        var a = map(m); a.seat = u.seat;
        var r = R.apply(tb.match, a); if (!r.ok) return E(r.error, r.detail);
        tb.timeouts[u.seat] = 0; tb.auto[u.seat] = false; commit(tb, r, u.seat); return null;
      });
    }
    move('gDraw', function (m) { return { t: 'draw', src: m.src }; });
    move('gReturnSide', function () { return { t: 'returnSide' }; });
    move('gOpen', function (m) { return { t: 'open', melds: m.melds }; });
    move('gLay', function (m) { return { t: 'lay', melds: m.melds }; });
    move('gAdd', function (m) { return { t: 'add', tile: m.tile, meld: m.meld, side: m.side }; });
    move('gSwap', function (m) { return { t: 'swap', tile: m.tile, meld: m.meld, index: m.index }; });
    move('gUndo', function () { return { t: 'undo' }; });
    move('gDiscard', function (m) { return { t: 'discard', tile: m.tile }; });
    on('gRollDice', function (c, m, u) {
      var tb = gameCtx(u); if (tb.error) return tb; var x = tb.deal;
      if (!x || x.step !== 'dice' || x.rolling) return E('E_PHASE'); if (x.dealer !== u.seat) return E('E_NOT_DEALER');
      roll(tb); return null;
    });
    on('gReady', function (c, m, u) { // "Sonraki el" düğmesi: bütün insan oyuncular hazırsa 15 sn beklenmez
      var tb = gameCtx(u); if (tb.error) return tb; if (tb.match.status !== 'handOver') return E('E_PHASE');
      tb.ready[u.seat] = true;
      var all = tb.seats.every(function (s, k) { return !s || s.bot || tb.ready[k]; });
      tableEmit(tb, 'readyState', { ready: Object.keys(tb.ready).map(Number) });
      if (all) { cancel(tb, 'next'); startHand(tb); }
      return null;
    });
    on('gRackOrder', function (c, m, u) { // ıstaka dizilimi yalnız istemci görünümü; sunucu yalnız taş kümesini doğrular
      var tb = gameCtx(u); if (tb.error) return tb; var have = tb.match.hand.racks[u.seat].slice().sort().join(), got = (m.tiles || []).filter(Boolean).slice().sort().join();
      return have === got ? null : E('E_RACK_MISMATCH');
    });
    function gameView(tb, seat) {
      var S = tb.match; if (!S || !S.hand) return { status: S ? S.status : 'none', handNo: S ? S.handNo : 0, totals: S ? S.totals : [0, 0, 0, 0] };
      var H = S.hand, dealing = !!tb.deal;
      return {
        status: S.status, handNo: S.handNo, hands: S.opts.hands, dealer: S.dealer, totals: S.totals.slice(), history: clone(S.history), seats: clone(S.seats),
        deal: dealing ? { step: tb.deal.step, dice: tb.deal.dice.slice(), dealer: tb.deal.dealer, elapsed: clock.now() - tb.deal.t0 } : null,
        indicator: dealing && ['build', 'dice'].indexOf(tb.deal.step) >= 0 ? null : H.indicator, okey: dealing && ['build', 'dice'].indexOf(tb.deal.step) >= 0 ? null : H.okey,
        autoplay: tb.auto.slice(), connected: tb.conn.slice(), deck: H.deck.length, rack: seat >= 0 && !(dealing && tb.deal.step !== 'fill') ? H.racks[seat].slice() : null, counts: H.racks.map(function (x) { return x.length; }),
        discards: clone(H.discards), melds: clone(H.melds), opened: H.opened.slice(), openMode: H.openMode.slice(), pen: H.pen.slice(),
        turn: H.turn ? { seat: H.turn.seat, phase: H.turn.phase, n: H.turn.n, sideTake: H.turn.sideTake, sideMust: !!H.turn.sideMust, deadline: tb.turnDeadline } : null,
        legal: seat >= 0 ? R.legal(S, seat) : null, result: H.result ? clone(H.result) : null, racksRevealed: H.result ? clone(H.racks) : null, nextAt: S.status === 'handOver' ? tb.nextAt : null
      };
    }

    // tableView: her hamle/tur/dağıtım sonunda her insan oyuncuya yalnız kendi görünümü (gizli bilgi kuralı: rakip ıstakaları yalnız sayı).
    function pushView(tb) {
      tb.seats.forEach(function (s, i) { if (s && !s.bot) toUser(s.id, 'tableView', { view: gameView(tb, i) }); });
      tb.spectators.forEach(function (uid) { toUser(uid, 'tableView', { view: gameView(tb, -1) }); });
    }
    // ---- Sohbet ve hediye ----
    var QUICK = ['quick.1', 'quick.2', 'quick.3', 'quick.4', 'quick.5', 'quick.6', 'quick.7', 'quick.8']; // strings.csv anahtarları
    on('chatSend', function (c, m, u) {
      var tb = tables[u.tableId]; if (!tb) return E('E_NOT_SEATED');
      if (tb.rules.chat === 'off') return E('E_CHAT_OFF');
      if (m.text != null && tb.rules.chat === 'quick') return E('E_CHAT_QUICK_ONLY');
      if (m.text != null && (String(m.text).trim().length === 0 || String(m.text).length > 120)) return E('E_CHAT_LENGTH');
      if (m.quick != null && QUICK.indexOf(m.quick) < 0 && !/^emoji\.\d$/.test(m.quick)) return E('E_CHAT_QUICK_UNKNOWN');
      tableEmit(tb, 'chat', { seat: u.seat, user: u.id, text: m.text != null ? String(m.text) : null, quick: m.quick || null, spectator: u.seat < 0 });
      if (rnd(tb.chatRng) < B.CHAT.replyChance) { var k = [0, 1, 2, 3].filter(function (i) { return i !== u.seat && isBotSeat(tb, i); }); if (k.length) { var who = pick(tb.chatRng, k); clock.set(function () { if (tables[tb.id]) tableEmit(tb, 'chat', { seat: who, quick: pick(tb.chatRng, B.CHAT.lines), bot: true }); }, B.CHAT.replyDelay); } }
      return null;
    });
    on('giftSend', function (c, m, u) {
      var tb = tables[u.tableId]; if (!tb) return E('E_NOT_SEATED'); if (!tb.rules.gifts) return E('E_GIFTS_OFF');
      var price = ECON.gifts[m.gift]; if (!price) return E('E_GIFT_UNKNOWN');
      var to = m.to === 'all' ? [0, 1, 2, 3].filter(function (k) { return k !== u.seat && tb.seats[k]; }) : [m.to];
      if (to.some(function (k) { return k === u.seat || !tb.seats[k]; })) return E('E_GIFT_TARGET');
      var cost = price * to.length; if (u.chips < cost) return E('E_NO_CHIPS', { missing: cost - u.chips });
      balance(u, -cost, 0, 'gift', m.gift);
      tableEmit(tb, 'gift', { from: u.seat, to: to, gift: m.gift, cost: cost });
      to.forEach(function (k) { if (isBotSeat(tb, k)) clock.set(function () { if (tables[tb.id]) tableEmit(tb, 'chat', { seat: k, quick: 'bot.thanks', bot: true }); }, m.gift === 'rocket' ? B.CHAT.thanksDelayRocket : B.CHAT.thanksDelay); });
      return null;
    });

    return {
      version: VERSION, clock: clock, connect: connect, timings: T, econ: ECON,
      // yalnız testler ve durum kataloğu için (istemci kullanmaz)
      _debug: { users: users, tables: tables, queue: queue, gameView: gameView }
    };
  }

  var API = { createServer: createServer, VirtualClock: VirtualClock, RealClock: RealClock, TIMINGS: T, ECON: ECON, VERSION: VERSION };
  root.G101MockServer = API;
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
})(typeof self !== 'undefined' ? self : this);
