/* Galaxy 101 · g101-sound.js · v0.4.0
 * Ses olayları: tek katalog (EVENTS) + oynatıcı. Oyun kodu yalnız G101Sound.play('olay.id', {seat}) çağırır.
 * Prototipte sesler WebAudio ile anlık sentezlenir (geçici); Unity'de her olay EVENTS[id].file adlı kliple çalınır.
 * Ayarlar (G101Settings): master, sfx, music (0–100), tileSnd (taş sesleri), chatSnd / notifSnd (sohbet ve bildirim sesleri).
 * ?state / ?lang / ?theme ile açılan durum koşularında ses kapalıdır; olaylar yine G101Sound.log'a yazılır (doğrulama için).
 */
(function () {
  if (window.G101Sound) return;
  var EVENTS = {
    'ui.tap':           { cat: 'ui',   prio: 1, poly: 2, ms: 45,   file: 'ui_tap',          trigger: 'İmleci pointer olan her öğeye basış (düğme, sekme, kart)' },
    'ui.open':          { cat: 'ui',   prio: 2, poly: 1, ms: 160,  file: 'ui_panel_open',   trigger: 'Panel/diyalog açılır (masa: hediye, sohbet, ayarlar, kalk onayı)' },
    'ui.close':         { cat: 'ui',   prio: 1, poly: 1, ms: 120,  file: 'ui_panel_close',  trigger: 'Panel/diyalog kapanır' },
    'ui.screen':        { cat: 'ui',   prio: 2, poly: 1, ms: 260,  file: 'ui_screen',       trigger: 'Ekran geçişi (açılış → giriş → menü → lobi → masa)' },
    'ui.error':         { cat: 'ui',   prio: 3, poly: 1, ms: 180,  file: 'ui_error',        trigger: 'Hata tostu / kırmızı sistem satırı (protocol ui: toast, shake, inline)' },
    'ui.reward':        { cat: 'ui',   prio: 3, poly: 1, ms: 700,  file: 'ui_reward_coins', trigger: 'Ödül alındı (günlük ödül, görev, maç kazancı; altın tost)' },
    'turn.mine':        { cat: 'game', prio: 4, poly: 1, ms: 420,  file: 'turn_mine',       trigger: 'turn olayı, seat = ben (dağıtım bitince)' },
    'turn.warn':        { cat: 'game', prio: 4, poly: 1, ms: 300,  file: 'turn_warn_tick',  trigger: 'Tur sayacı uyarı eşiğine girdi (ayar warn, vars. 5 sn)' },
    'count.tick':       { cat: 'game', prio: 3, poly: 1, ms: 120,  file: 'count_tick',      trigger: 'Geri sayım rakamı 3, 2, 1' },
    'count.go':         { cat: 'game', prio: 3, poly: 1, ms: 380,  file: 'count_go',        trigger: 'Geri sayım bitti, dağıtım başlıyor' },
    'deal.shuffle':     { cat: 'tile', prio: 3, poly: 1, ms: 900,  file: 'deal_shuffle',    trigger: 'dealStart: balyalar diziliyor' },
    'dice.roll':        { cat: 'tile', prio: 3, poly: 1, ms: 1450, file: 'dice_roll',       trigger: 'dealDie: zar yuvarlanıyor (1450 ms, 3 sekme)' },
    'deal.indicator':   { cat: 'tile', prio: 3, poly: 1, ms: 400,  file: 'deal_indicator',  trigger: 'dealStep:indicator: gösterge çevrildi' },
    'deal.distribute':  { cat: 'tile', prio: 2, poly: 1, ms: 2400, file: 'deal_distribute', trigger: 'dealStep:distribute: balyalar oyunculara kayıyor' },
    'tile.draw':        { cat: 'tile', prio: 2, poly: 2, ms: 90,   file: 'tile_draw',       trigger: 'game.drew (ben ya da rakip; rakipte %70 ses)' },
    'tile.discard':     { cat: 'tile', prio: 2, poly: 2, ms: 110,  file: 'tile_discard',    trigger: 'game.discarded (taş halkaya/yığına iner)' },
    'tile.slide':       { cat: 'tile', prio: 1, poly: 2, ms: 60,   file: 'tile_slide',      trigger: 'Istakada taş yer değiştirdi (yerel)' },
    'meld.lay':         { cat: 'tile', prio: 3, poly: 1, ms: 260,  file: 'meld_lay',        trigger: 'game.opened / game.laid (perler masaya)' },
    'meld.add':         { cat: 'tile', prio: 2, poly: 1, ms: 120,  file: 'meld_add',        trigger: 'game.added (işleme)' },
    'okey.swap':        { cat: 'tile', prio: 3, poly: 1, ms: 380,  file: 'okey_swap',       trigger: 'game.okeySwapped' },
    'penalty':          { cat: 'game', prio: 5, poly: 1, ms: 520,  file: 'penalty',         trigger: 'game.penalty (R-40…R-44)' },
    'finish':           { cat: 'game', prio: 6, poly: 1, ms: 1200, file: 'finish_fanfare',  trigger: 'game.finished (biri bitirdi)' },
    'finish.nuke':      { cat: 'game', prio: 6, poly: 1, ms: 1750, file: 'finish_nuke',     trigger: 'Okeyle / elden bitiş fırlatması (nuke) iniş anı' },
    'hand.end':         { cat: 'game', prio: 4, poly: 1, ms: 600,  file: 'hand_end',        trigger: 'game.handEnd: el sonu tablosu açıldı' },
    'gift.send':        { cat: 'chat', prio: 2, poly: 2, ms: 400,  file: 'gift_whoosh',     trigger: 'gift: ikram/hediye uçuşu başladı' },
    'gift.receive':     { cat: 'chat', prio: 3, poly: 1, ms: 600,  file: 'gift_sparkle',    trigger: 'Sana ikram geldi (sağ üst kart)' },
    'gift.rocket':      { cat: 'chat', prio: 3, poly: 1, ms: 5600, file: 'gift_rocket',     trigger: 'Roket uçuşu (5,6 sn)' },
    'chat.message':     { cat: 'chat', prio: 1, poly: 1, ms: 140,  file: 'chat_pop',        trigger: 'chat: rakipten mesaj/balon' }
  };
  var CAT = { ui: 'sfx', game: 'sfx', tile: 'sfx', chat: 'sfx', music: 'music' };
  var q = new URLSearchParams(location.search), MUTE = !!(q.get('state') || q.get('lang') || q.get('theme'));
  var ctx = null, live = {}, log = [];
  function S() { var d = { master: 80, sfx: 90, music: 55, tileSnd: true, chatSnd: true, notifSnd: true }, g = (window.G101Settings && window.G101Settings.get()) || {}; for (var k in g) d[k] = g[k]; return d; }
  function gainFor(ev) {
    var s = S(), v = (s.master / 100) * ((ev.cat === 'music' ? s.music : s.sfx) / 100);
    if (ev.cat === 'tile' && s.tileSnd === false) return 0;
    if (ev.cat === 'chat' && (s.chatSnd === false || s.notifSnd === false)) return 0;
    return Math.max(0, Math.min(1, v));
  }
  function ac() { if (!ctx) { var C = window.AudioContext || window.webkitAudioContext; if (!C) return null; ctx = new C(); } if (ctx.state === 'suspended') ctx.resume(); return ctx; }
  ['pointerdown', 'keydown'].forEach(function (t) { window.addEventListener(t, function () { if (!MUTE) ac(); }, { capture: true, passive: true }); });
  // --- geçici sentez parçaları ---
  function env(c, out, t, a, d, peak) { var g = c.createGain(); g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(peak, t + a); g.gain.exponentialRampToValueAtTime(0.0001, t + a + d); g.connect(out); return g; }
  function tone(c, out, t, f, d, type, peak, f2) { var o = c.createOscillator(); o.type = type || 'sine'; o.frequency.setValueAtTime(f, t); if (f2) o.frequency.exponentialRampToValueAtTime(f2, t + d); o.connect(env(c, out, t, 0.005, d, peak || 0.3)); o.start(t); o.stop(t + d + 0.05); }
  var nbuf = null;
  function noise(c, out, t, d, fq, qv, peak, type) { if (!nbuf) { nbuf = c.createBuffer(1, c.sampleRate, c.sampleRate); var a = nbuf.getChannelData(0); for (var i = 0; i < a.length; i++) a[i] = Math.random() * 2 - 1; } var s = c.createBufferSource(); s.buffer = nbuf; var f = c.createBiquadFilter(); f.type = type || 'bandpass'; f.frequency.value = fq; f.Q.value = qv || 1; s.connect(f); f.connect(env(c, out, t, 0.002, d, peak || 0.3)); s.start(t, Math.random() * 0.5); s.stop(t + d + 0.05); }
  function clack(c, out, t, p) { p = p || 1; noise(c, out, t, 0.05, 2400 * p, 1.6, 0.5); tone(c, out, t, 190 * p, 0.07, 'triangle', 0.35, 120 * p); }
  var SYN = {
    'ui.tap': function (c, o, t) { tone(c, o, t, 1300, 0.04, 'sine', 0.18, 900); },
    'ui.open': function (c, o, t) { tone(c, o, t, 520, 0.12, 'sine', 0.16, 880); },
    'ui.close': function (c, o, t) { tone(c, o, t, 760, 0.1, 'sine', 0.13, 420); },
    'ui.screen': function (c, o, t) { noise(c, o, t, 0.26, 900, 0.7, 0.12, 'lowpass'); tone(c, o, t, 330, 0.24, 'sine', 0.1, 660); },
    'ui.error': function (c, o, t) { tone(c, o, t, 180, 0.09, 'square', 0.12); tone(c, o, t + 0.09, 150, 0.09, 'square', 0.12); },
    'ui.reward': function (c, o, t) { [988, 1319, 1568, 1976].forEach(function (f, i) { tone(c, o, t + i * 0.07, f, 0.28, 'triangle', 0.18); }); },
    'turn.mine': function (c, o, t) { tone(c, o, t, 659, 0.3, 'sine', 0.22); tone(c, o, t + 0.12, 988, 0.34, 'sine', 0.2); },
    'turn.warn': function (c, o, t) { tone(c, o, t, 880, 0.05, 'square', 0.08); tone(c, o, t + 0.2, 880, 0.05, 'square', 0.08); },
    'count.tick': function (c, o, t) { tone(c, o, t, 660, 0.1, 'triangle', 0.22); },
    'count.go': function (c, o, t) { tone(c, o, t, 990, 0.34, 'triangle', 0.24); },
    'deal.shuffle': function (c, o, t) { for (var i = 0; i < 9; i++) clack(c, o, t + i * 0.09 + Math.random() * 0.03, 0.9 + Math.random() * 0.3); },
    'dice.roll': function (c, o, t) { [0, 0.42, 0.78, 1.02, 1.18, 1.28].forEach(function (x, i) { noise(c, o, t + x, 0.04, 3200, 2, 0.35 * (1 - i * 0.12)); tone(c, o, t + x, 260, 0.05, 'triangle', 0.22 * (1 - i * 0.12)); }); },
    'deal.indicator': function (c, o, t) { clack(c, o, t, 1.2); tone(c, o, t + 0.05, 1175, 0.3, 'sine', 0.12); },
    'deal.distribute': function (c, o, t) { for (var i = 0; i < 8; i++) clack(c, o, t + i * 0.3, 0.85 + (i % 3) * 0.08); },
    'tile.draw': function (c, o, t, x) { clack(c, o, t, 1.15 + (x.r || 0)); },
    'tile.discard': function (c, o, t, x) { clack(c, o, t, 0.9 + (x.r || 0)); noise(c, o, t + 0.02, 0.07, 700, 0.8, 0.2, 'lowpass'); },
    'tile.slide': function (c, o, t) { noise(c, o, t, 0.05, 1800, 0.9, 0.16); },
    'meld.lay': function (c, o, t) { clack(c, o, t, 1); clack(c, o, t + 0.07, 1.05); clack(c, o, t + 0.14, 1.1); },
    'meld.add': function (c, o, t) { clack(c, o, t, 1.08); },
    'okey.swap': function (c, o, t) { clack(c, o, t, 1); tone(c, o, t + 0.06, 784, 0.28, 'sine', 0.16, 1175); },
    'penalty': function (c, o, t) { tone(c, o, t, 440, 0.22, 'triangle', 0.24); tone(c, o, t + 0.18, 349, 0.32, 'triangle', 0.24); },
    'finish': function (c, o, t) { [523, 659, 784, 1047].forEach(function (f, i) { tone(c, o, t + i * 0.1, f, 0.5, 'triangle', 0.2); }); },
    'finish.nuke': function (c, o, t) { noise(c, o, t + 1.0, 0.7, 180, 0.6, 0.6, 'lowpass'); tone(c, o, t + 1.0, 70, 0.7, 'sine', 0.5, 35); tone(c, o, t, 300, 1.0, 'sawtooth', 0.05, 1200); },
    'hand.end': function (c, o, t) { [392, 494, 587].forEach(function (f) { tone(c, o, t, f, 0.6, 'sine', 0.12); }); },
    'gift.send': function (c, o, t) { noise(c, o, t, 0.36, 1400, 0.6, 0.14, 'bandpass'); },
    'gift.receive': function (c, o, t) { [1568, 2093, 2637].forEach(function (f, i) { tone(c, o, t + i * 0.08, f, 0.3, 'sine', 0.12); }); },
    'gift.rocket': function (c, o, t) { noise(c, o, t, 5.4, 600, 0.5, 0.12, 'lowpass'); tone(c, o, t, 120, 5.2, 'sawtooth', 0.03, 480); },
    'chat.message': function (c, o, t) { tone(c, o, t, 880, 0.08, 'sine', 0.14, 1320); }
  };
  function play(id, x) {
    x = x || {}; var ev = EVENTS[id]; if (!ev) return false;
    var at = (window.G101Clock && window.G101Clock.now) ? window.G101Clock.now() : Date.now();
    log.push({ id: id, t: at, seat: x.seat != null ? x.seat : null }); if (log.length > 200) log.shift();
    try { window.dispatchEvent(new CustomEvent('g101-sfx', { detail: { id: id, seat: x.seat } })); } catch (e) {}
    if (MUTE || API.muted) return true;
    var g = gainFor(ev) * (x.vol != null ? x.vol : 1); if (g <= 0.001) return true;
    var c = ac(); if (!c || c.state !== 'running') return true;
    if (ev.prio >= 5) duck();
    live[id] = (live[id] || []).filter(function (e) { return e > c.currentTime; });
    if (live[id].length >= ev.poly) return true;
    live[id].push(c.currentTime + ev.ms / 1000);
    var out = c.createGain(); out.gain.value = g; out.connect(c.destination);
    try { SYN[id](c, out, c.currentTime + 0.005, { r: (Math.random() - 0.5) * 0.12 }); } catch (e) {}
    return true;
  }
  // Genel dokunma sesi: imleci pointer olan öğeye basış.
  document.addEventListener('pointerdown', function (e) {
    var el = e.target; for (var i = 0; el && el.nodeType === 1 && i < 6; i++, el = el.parentElement) {
      if (el.tagName === 'CANVAS') return;
      if (el.style && el.style.cursor === 'pointer') { play('ui.tap'); return; }
    }
  }, { capture: true, passive: true });
  // --- müzik: iki prosedürel döngü (menü, masa). Aynı tanım hem oyunda çalar hem WAV'a işlenir. ---
  var NOTE = function (n) { return 440 * Math.pow(2, (n - 69) / 12); };
  var MUSIC = {
    menu:  { bar: 4, gain: 0.55, cut: 1100, chords: [[48, 55, 64, 71], [45, 52, 60, 67], [41, 48, 57, 64], [43, 50, 59, 65], [48, 55, 64, 71], [45, 52, 60, 64], [41, 48, 57, 62], [43, 50, 55, 62]], arp: [0, 2, 1, 3, 2, 1, 3, 2], step: 0.5, bell: 0.05 },
    table: { bar: 5, gain: 0.42, cut: 800, chords: [[50, 57, 64, 69], [46, 53, 62, 69], [41, 48, 57, 64], [40, 48, 55, 64], [50, 57, 65, 69], [46, 53, 60, 67], [43, 50, 58, 62], [45, 52, 57, 61]], arp: [3, 1, 2, 0], step: 1.25, bell: 0.035 }
  };
  function musicLen(id) { var m = MUSIC[id]; return m.bar * m.chords.length; }
  function schedMusic(c, out, id, t0) {
    var m = MUSIC[id], i, j;
    for (i = 0; i < m.chords.length; i++) {
      var ch = m.chords[i], ts = t0 + i * m.bar, te = ts + m.bar;
      var lp = c.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = m.cut; lp.Q.value = 0.4; lp.connect(out);
      var g = c.createGain(); g.gain.setValueAtTime(0.0001, ts); g.gain.linearRampToValueAtTime(m.gain * 0.11, ts + m.bar * 0.35); g.gain.setValueAtTime(m.gain * 0.11, te - m.bar * 0.3); g.gain.linearRampToValueAtTime(0.0001, te + 0.6); g.connect(lp);
      for (j = 0; j < ch.length; j++) [-6, 6].forEach(function (dt) { var o = c.createOscillator(); o.type = 'sawtooth'; o.frequency.value = NOTE(ch[j]); o.detune.value = dt; o.connect(g); o.start(ts); o.stop(te + 0.7); });
      var bs = c.createOscillator(); bs.type = 'sine'; bs.frequency.value = NOTE(ch[0] - 12); var bg = c.createGain(); bg.gain.setValueAtTime(0.0001, ts); bg.gain.linearRampToValueAtTime(m.gain * 0.16, ts + 0.8); bg.gain.linearRampToValueAtTime(0.0001, te + 0.4); bs.connect(bg); bg.connect(out); bs.start(ts); bs.stop(te + 0.5);
      for (var k = 0; k * m.step < m.bar - 0.01; k++) { var n = ch[m.arp[k % m.arp.length]] + 12, tb = ts + k * m.step; tone(c, out, tb, NOTE(n), Math.min(1.6, m.step * 2.4), 'sine', m.gain * m.bell); }
    }
  }
  var mus = { want: null, cur: null, gain: null, timer: null, next: 0 };
  function musicGain() { var s = S(); return (s.master / 100) * (s.music / 100); }
  function musicTick() {
    var c = ctx; if (!c || c.state !== 'running' || !mus.cur) return;
    while (mus.next < c.currentTime + 6) { schedMusic(c, mus.gain, mus.cur, mus.next); mus.next += musicLen(mus.cur); }
  }
  function setMusic(id) {
    mus.want = id || null; if (MUTE) return;
    var c = ctx; if (!c || c.state !== 'running') return;
    if (mus.cur === mus.want) return;
    if (mus.gain) { var old = mus.gain; old.gain.cancelScheduledValues(c.currentTime); old.gain.setValueAtTime(old.gain.value, c.currentTime); old.gain.linearRampToValueAtTime(0.0001, c.currentTime + 1.2); setTimeout(function () { try { old.disconnect(); } catch (e) {} }, 1500); }
    mus.cur = mus.want; clearInterval(mus.timer); mus.gain = null; if (!mus.cur) return;
    mus.gain = c.createGain(); mus.gain.gain.setValueAtTime(0.0001, c.currentTime); mus.gain.gain.linearRampToValueAtTime(musicGain(), c.currentTime + 1.5); mus.gain.connect(c.destination);
    mus.next = c.currentTime + 0.05; musicTick(); mus.timer = setInterval(musicTick, 1000);
  }
  function duck() { if (!mus.gain || !ctx) return; var t = ctx.currentTime, g = musicGain(); mus.gain.gain.cancelScheduledValues(t); mus.gain.gain.setValueAtTime(mus.gain.gain.value, t); mus.gain.gain.linearRampToValueAtTime(g * 0.4, t + 0.3); mus.gain.gain.linearRampToValueAtTime(g, t + 2.4); }
  ['pointerdown', 'keydown'].forEach(function (t) { window.addEventListener(t, function () { if (MUTE) return; setTimeout(function () { if (mus.want && mus.cur !== mus.want) setMusic(mus.want); }, 60); }, { capture: true, passive: true }); });
  if (window.G101Settings) window.G101Settings.on(function () { if (mus.gain && ctx) mus.gain.gain.setTargetAtTime(musicGain(), ctx.currentTime, 0.1); });
  // --- dışa aktarma: aynı sentez OfflineAudioContext'te → 16-bit 44,1 kHz WAV ---
  function wav(buf) {
    var ch = buf.numberOfChannels, len = buf.length, sr = buf.sampleRate, ab = new ArrayBuffer(44 + len * ch * 2), v = new DataView(ab), p = 0;
    var w = function (s) { for (var i = 0; i < s.length; i++) v.setUint8(p++, s.charCodeAt(i)); }, u32 = function (x) { v.setUint32(p, x, true); p += 4; }, u16 = function (x) { v.setUint16(p, x, true); p += 2; };
    w('RIFF'); u32(36 + len * ch * 2); w('WAVE'); w('fmt '); u32(16); u16(1); u16(ch); u32(sr); u32(sr * ch * 2); u16(ch * 2); u16(16); w('data'); u32(len * ch * 2);
    var data = []; for (var c = 0; c < ch; c++) data.push(buf.getChannelData(c));
    for (var i = 0; i < len; i++) for (c = 0; c < ch; c++) { var s = Math.max(-1, Math.min(1, data[c][i])); v.setInt16(p, s < 0 ? s * 0x8000 : s * 0x7FFF, true); p += 2; }
    return ab;
  }
  function render(id) {
    var isM = id.indexOf('music.') === 0, key = isM ? id.slice(6) : id, ev = EVENTS[key];
    var dur = isM ? musicLen(key) : Math.max(0.3, (ev ? ev.ms : 500) / 1000 + 0.6), sr = 44100, O = window.OfflineAudioContext || window.webkitOfflineAudioContext;
    var c = new O(2, Math.ceil((isM ? dur * 2 + 1.2 : dur) * sr), sr), out = c.createGain(); out.gain.value = 0.9; out.connect(c.destination);
    if (isM) { schedMusic(c, out, key, 0); schedMusic(c, out, key, dur); } else SYN[key](c, out, 0.005, { r: 0 });
    return c.startRendering().then(function (b) {
      if (isM) { var n = Math.round(dur * sr), o2 = new AudioBuffer({ numberOfChannels: 2, length: n, sampleRate: sr }); for (var k = 0; k < 2; k++) o2.copyToChannel(b.getChannelData(k).subarray(n, 2 * n), k); b = o2; }   // 2. tur: 1. turun kuyruğunu içerir → kesintisiz döngü
      return wav(b);
    });
  }
  var API = { EVENTS: EVENTS, CAT: CAT, MUSIC: MUSIC, play: play, setMusic: setMusic, render: render, musicLen: musicLen, log: log, muted: false };
  window.G101Sound = API;
})();
