/* Galaxy 101 · g101-clock.js · v0.5.0
 * Ortak saat + seed'li rastgelelik. Oyun sayfasında İLK yüklenen betik olmalı.
 *  ?seed=N   → bütün rastgelelik (Math.random dahil) bu seed'den türetilir (mulberry32). Yoksa seed tarayıcıdan bir kez çekilir ve G101Clock.seed'de görünür.
 *  ?ft=MS    → ("freeze time"; host kendi ?t belirtecini kullandığı için t değil) sayfa açıldıktan MS milisaniye sonra kare dondurulur: performance.now / Date.now / requestAnimationFrame zamanı sabitlenir,
 *              CSS/Web animasyonları duraklatılır, bekleyen zamanlayıcılar kuyruğa alınır. G101Clock.frozen === true.
 *  ?speed=K  → sanal saat K kat hızlı (yalnız t ile birlikte anlamlı değil; tek başına animasyon hızını değiştirir).
 * Unity karşılığı: tek bir GameClock (Time.time yerine) + System.Random(seed) — bkz. galaxy101_handoff_v4/DECISIONS.md D-60.
 */
(function () {
  if (window.G101Clock) return;
  var q = new URLSearchParams(location.search);
  var seedQ = q.get('seed'), tQ = q.get('ft'), speed = +(q.get('speed') || 1) || 1;
  var seed = seedQ != null && /^\d+$/.test(seedQ) ? (+seedQ >>> 0) : ((window.crypto && crypto.getRandomValues) ? crypto.getRandomValues(new Uint32Array(1))[0] : 1);

  function mulberry32(a) { return function () { a |= 0; a = (a + 0x6D2B79F5) | 0; var t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
  var main = mulberry32(seed);
  var nativeRandom = Math.random;
  Math.random = main;   // prototipteki bütün Math.random çağrıları seed'li akıştan beslenir

  var pNow = performance.now.bind(performance), dNow = Date.now, t0 = pNow(), d0 = dNow();
  var frozenAt = null;
  function vnow() { return frozenAt != null ? frozenAt : (pNow() - t0) * speed; }

  var C = {
    version: '0.5.0', seed: seed, speed: speed, frozen: false,
    /** Sayfa açılışından bu yana sanal milisaniye (tüm animasyonların ortak saati). */
    now: vnow,
    /** Bağımsız alt akış: aynı seed + aynı ad → her açılışta aynı dizi. */
    rng: function (name) { var h = seed ^ 0x9E3779B9; for (var i = 0; i < name.length; i++) h = Math.imul(h ^ name.charCodeAt(i), 0x01000193); return mulberry32(h >>> 0); },
    rand: main, nativeRandom: nativeRandom,
    freeze: function (ms) {
      // Kanonik donma anı: CANON + ms. Bütün animasyonlar (CSS, Web, JS tween, three.js) aynı sabit zamana oturur →
      // açılış animasyonları bitmiş, sonsuz döngüler (gezegen, nabız, yüzme) her açılışta aynı fazda. Gerçek yükleme süresinden bağımsız.
      if (C.frozen) return; var canon = 100000 + (ms != null ? ms : 0); frozenAt = canon; C.frozen = true;
      try { document.getAnimations().forEach(function (a) { try { a.pause(); a.currentTime = canon; } catch (e) {} }); } catch (e) {}
      var st = document.createElement('style'); st.id = 'g101-freeze'; st.textContent = '*,*::before,*::after{animation-play-state:paused!important;transition:none!important;caret-color:transparent!important}'; document.head.appendChild(st);
      document.documentElement.setAttribute('data-g101-frozen', String(frozenAt));
      window.dispatchEvent(new CustomEvent('g101:frozen', { detail: { t: frozenAt } }));
    },
  };
  performance.now = function () { return t0 + vnow(); };
  Date.now = function () { return d0 + vnow(); };
  var rAF = window.requestAnimationFrame.bind(window);
  window.requestAnimationFrame = function (cb) { return rAF(function () { cb(t0 + vnow()); }); };
  var sT = window.setTimeout.bind(window), held = [];
  window.setTimeout = function (fn, ms) { var a = [].slice.call(arguments, 2); return sT(function () { if (C.frozen) { held.push(function () { fn.apply(null, a); }); return; } if (typeof fn === 'function') fn.apply(null, a); }, ms); };
  var sI = window.setInterval.bind(window);
  window.setInterval = function (fn, ms) { var a = [].slice.call(arguments, 2); return sI(function () { if (!C.frozen && typeof fn === 'function') fn.apply(null, a); }, ms); };
  C.heldTimers = function () { return held.length; };

  if (tQ != null && /^\d+$/.test(tQ) && !q.get('state')) { var tm = Math.max(0, +tQ || 0); sT(function () { C.freeze(tm); }, tm / speed); }   // ?state varsa donmayı g101-state.js durum yoluna varınca yapar
  window.G101Clock = C;
})();
