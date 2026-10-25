// Galaxy 101 — procedural planet renderer (canvas 2D). Texture + geometry baked once per planet/size; per frame only a texture lookup.
(function () {
  const PI = Math.PI;
  const clamp = (x) => (x < 0 ? 0 : x > 1 ? 1 : x);
  const sm = (a, b, x) => { const t = clamp((x - a) / (b - a)); return t * t * (3 - 2 * t); };
  const mix = (c, d, t) => { c[0] += (d[0] - c[0]) * t; c[1] += (d[1] - c[1]) * t; c[2] += (d[2] - c[2]) * t; };
  const wrap = (a) => { a = (a + PI) % (2 * PI); if (a < 0) a += 2 * PI; return a - PI; };
  function ramp(tb, t, out) {
    if (t <= tb[0][0]) { out[0] = tb[0][1]; out[1] = tb[0][2]; out[2] = tb[0][3]; return; }
    for (let i = 1; i < tb.length; i++) {
      if (t <= tb[i][0]) {
        const a = tb[i - 1], b = tb[i], f = (t - a[0]) / (b[0] - a[0]);
        out[0] = a[1] + (b[1] - a[1]) * f; out[1] = a[2] + (b[2] - a[2]) * f; out[2] = a[3] + (b[3] - a[3]) * f; return;
      }
    }
    const l = tb[tb.length - 1]; out[0] = l[1]; out[1] = l[2]; out[2] = l[3];
  }
  function mkNoise(seed) {
    let s = seed >>> 0 || 1;
    const rnd = () => (s = (s * 1664525 + 1013904223) >>> 0) / 4294967296;
    const p = new Uint8Array(512), v = new Float32Array(256), a = [];
    for (let i = 0; i < 256; i++) a[i] = i;
    for (let i = 255; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); const t = a[i]; a[i] = a[j]; a[j] = t; }
    for (let i = 0; i < 512; i++) p[i] = a[i & 255];
    for (let i = 0; i < 256; i++) v[i] = rnd();
    const n3 = (x, y, z) => {
      const xi = Math.floor(x), yi = Math.floor(y), zi = Math.floor(z);
      const xf = x - xi, yf = y - yi, zf = z - zi;
      const X = xi & 255, Y = yi & 255, Z = zi & 255, X1 = (X + 1) & 255, Y1 = (Y + 1) & 255, Z1 = (Z + 1) & 255;
      const u = xf * xf * (3 - 2 * xf), w = yf * yf * (3 - 2 * yf), t = zf * zf * (3 - 2 * zf);
      const a00 = p[p[X] + Y], a01 = p[p[X] + Y1], a10 = p[p[X1] + Y], a11 = p[p[X1] + Y1];
      const c000 = v[p[a00 + Z]], c001 = v[p[a00 + Z1]], c010 = v[p[a01 + Z]], c011 = v[p[a01 + Z1]];
      const c100 = v[p[a10 + Z]], c101 = v[p[a10 + Z1]], c110 = v[p[a11 + Z]], c111 = v[p[a11 + Z1]];
      const x00 = c000 + (c100 - c000) * u, x01 = c001 + (c101 - c001) * u, x10 = c010 + (c110 - c010) * u, x11 = c011 + (c111 - c011) * u;
      const y0 = x00 + (x10 - x00) * w, y1 = x01 + (x11 - x01) * w;
      return y0 + (y1 - y0) * t;
    };
    return (x, y, z, o) => {
      let sum = 0, amp = 0.5, norm = 0;
      for (let i = 0; i < o; i++) { sum += amp * n3(x, y, z); norm += amp; amp *= 0.5; x *= 2.03; y *= 2.03; z *= 2.03; }
      return sum / norm;
    };
  }
  const cn = (f, k) => clamp((f - 0.5) * k + 0.5);
  const h1 = (i) => { const x = Math.sin(i * 127.1 + 311.7) * 43758.5453; return x - Math.floor(x); };
  const n1 = (x) => { const i = Math.floor(x), f = x - i, u = f * f * (3 - 2 * f); return h1(i) + (h1(i + 1) - h1(i)) * u; };

  const JUP = [[-1,118,100,86],[-.82,150,126,102],[-.62,202,178,148],[-.48,176,132,96],[-.34,228,208,184],[-.22,190,140,100],[-.12,238,224,204],[-.04,214,184,150],[.05,242,230,212],[.14,176,120,82],[.24,204,154,114],[.33,234,218,194],[.45,186,146,112],[.6,216,196,172],[.8,160,136,112],[1,122,104,90]];
  const SAT = [[-1,96,112,126],[-.9,116,128,136],[-.8,150,146,130],[-.7,182,164,124],[-.6,212,190,142],[-.52,186,158,110],[-.44,224,204,154],[-.34,192,160,104],[-.26,230,210,160],[-.14,246,232,188],[0,250,238,196],[.1,240,222,172],[.17,196,162,104],[.24,230,206,152],[.32,206,176,120],[.42,228,208,160],[.52,194,170,124],[.62,214,196,154],[.72,184,172,138],[.82,156,156,140],[.91,128,136,136],[1,108,118,124]];
  const MARS = [[0,70,32,22],[.34,96,46,30],[.45,146,70,40],[.56,184,96,56],[.72,206,124,78],[1,224,156,112]];
  const NEP = [[-1,36,56,142],[-.6,46,76,176],[-.3,56,96,206],[-.1,62,108,220],[0,66,114,226],[.15,58,98,206],[.4,50,84,190],[.7,44,70,168],[1,38,58,148]];
  const VEN = [[0,206,168,110],[.38,226,196,140],[.62,240,221,174],[1,250,241,212]];

  // Saturn ring profile, r in planet radii
  function ringAt(r, out) {
    let a = 0, c0 = 0, c1 = 0, c2 = 0;
    if (r > 1.24 && r < 1.53) { a = .1 + .12 * sm(1.24, 1.53, r); c0 = 150; c1 = 140; c2 = 124; }
    else if (r >= 1.53 && r < 1.95) { const t = (r - 1.53) / .42; a = .78 + .18 * Math.sin(t * PI); c0 = 214 + 22 * Math.sin(t * PI); c1 = 194 + 18 * Math.sin(t * PI); c2 = 152 + 10 * t; }
    else if (r >= 1.95 && r < 2.03) { a = .05; c0 = 120; c1 = 112; c2 = 100; }
    else if (r >= 2.03 && r < 2.27) { a = (r > 2.205 && r < 2.218) ? .05 : .62; c0 = 196; c1 = 182; c2 = 156; }
    else if (r >= 2.31 && r < 2.33) { a = .35; c0 = 210; c1 = 200; c2 = 180; }
    if (a > 0) {
      const g = .82 + .26 * n1(r * 140) + .08 * n1(r * 380);
      a *= .75 + .35 * n1(r * 210 + 7);
      out[0] = c0 * g; out[1] = c1 * g; out[2] = c2 * g;
    }
    return clamp(a);
  }

  const PLANETS = {
    'Jüpiter': { tilt: 6, flat: 1.07, k: .4, amb: .02, atm: [255, 226, 190], as: .22, seed: 11,
      gen(fA, fB, x, y, z, lat, lon, c) {
        const s = y + .055 * (fA(x * 2.5, y * 7, z * 2.5, 3) - .5) + .02 * (fB(x * 12, y * 36, z * 12, 2) - .5);
        ramp(JUP, s, c);
        const d = .84 + .32 * fB(x * 5, y * 24, z * 5, 3); c[0] *= d; c[1] *= d; c[2] *= d;
        const dl = wrap(lon - 1.2), dx = dl * Math.cos(lat) / .2, dy = (lat + .38) / .1, dd = dx * dx + dy * dy;
        if (dd < 2.4) {
          const g = .8 + .4 * fA(x * 18, y * 18, z * 18, 2);
          mix(c, [200 * g, 102 * g, 66 * g], sm(1.15, .3, dd));
          mix(c, [240, 224, 204], sm(.6, .95, dd) * sm(1.7, 1.05, dd) * .55);
        }
      } },
    'Satürn': { tilt: 22, flat: 1.1, k: .45, amb: .02, atm: [255, 222, 175], as: .2, ring: true, seed: 5,
      gen(fA, fB, x, y, z, lat, lon, c) {
        const w = .022 * (fA(x * 3, y * 9, z * 3, 3) - .5);
        ramp(SAT, y + w, c);
        const st = .92 + .16 * fB(x * 4, y * 60, z * 4, 2); c[0] *= st; c[1] *= st; c[2] *= st;
        // storm ovals in the southern temperate belt
        const sp = cn(fA(x * 9 + 3, y * 30, z * 9, 2), 2.4);
        if (y < -.3 && y > -.42) mix(c, [250, 240, 214], sm(.74, .9, sp) * .5);
        // north polar hexagon
        const co = PI / 2 - lat;
        if (co < .5) {
          const seg = PI / 3, ang = ((lon % seg) + seg) % seg - seg / 2;
          const rh = .3 / Math.cos(ang), e = co / rh;
          mix(c, [96, 112, 124], sm(1.08, .9, e) * .45);
          mix(c, [200, 192, 168], sm(.09, 0, Math.abs(e - 1)) * .18);
          mix(c, [78, 92, 110], sm(.3, 0, co) * .4);
        }
      } },
    'Mars': { tilt: 14, flat: 1, k: .12, amb: .02, atm: [255, 165, 125], as: .42, seed: 23,
      gen(fA, fB, x, y, z, lat, lon, c) {
        ramp(MARS, cn(fA(x * 1.6, y * 1.6, z * 1.6, 5), 2.6), c);
        const d = .84 + .32 * fB(x * 8, y * 8, z * 8, 3); c[0] *= d; c[1] *= d; c[2] *= d;
        const q = Math.abs(y) + .06 * (fB(x * 5, y * 5, z * 5, 2) - .5);
        mix(c, [240, 236, 230], (y > 0 ? sm(.92, .955, q) : sm(.95, .975, q)) * .92);
      } },
    'Neptün': { tilt: 18, flat: 1.02, k: .32, amb: .02, atm: [120, 185, 255], as: .75, seed: 37,
      gen(fA, fB, x, y, z, lat, lon, c) {
        ramp(NEP, y + .03 * (fA(x * 3, y * 8, z * 3, 3) - .5), c);
        const d = .92 + .16 * fB(x * 4, y * 20, z * 4, 2); c[0] *= d; c[1] *= d; c[2] *= d;
        const ci = cn(fB(x * 2.5 + 7, y * 28, z * 2.5, 4), 2.2);
        mix(c, [226, 236, 255], sm(.7, .9, ci) * (.2 + .8 * Math.abs(Math.sin(y * 7))) * .55);
        const dl = wrap(lon - 2.6), cl = Math.cos(lat);
        const dx = dl * cl / .16, dy = (lat + .35) / .085, dd = dx * dx + dy * dy;
        if (dd < 2) mix(c, [22, 36, 104], sm(1.1, .3, dd) * .9);
        const ex = dl * cl / .13, ey = (lat + .46) / .028, ee = ex * ex + ey * ey;
        if (ee < 1.2) mix(c, [236, 244, 255], sm(1, 0, ee) * .85);
      } },
    'Venüs': { tilt: 4, flat: 1, k: .28, amb: .03, atm: [255, 240, 212], as: .85, seed: 51,
      gen(fA, fB, x, y, z, lat, lon, c) {
        const wx = fA(x * 1.4, y * 1.4, z * 1.4, 3) - .5;
        ramp(VEN, cn(fB(x * 2 + wx * 1.6, y * 4.5 + wx * 1.2, z * 2 + wx * 1.6, 4), 2.1), c);
      } },
  };
  const texCache = {};
  function buildTex(name, W) {
    const key = name + W; if (texCache[key]) return texCache[key];
    const H = W / 2, P = PLANETS[name], fA = mkNoise(P.seed), fB = mkNoise(P.seed + 77);
    const tr = new Uint8ClampedArray(W * H), tg = new Uint8ClampedArray(W * H), tb = new Uint8ClampedArray(W * H), c = [0, 0, 0];
    for (let j = 0; j < H; j++) {
      const lat = (.5 - (j + .5) / H) * PI, cl = Math.cos(lat), y = Math.sin(lat);
      for (let i = 0; i < W; i++) {
        const lon = ((i + .5) / W) * 2 * PI - PI, x = cl * Math.sin(lon), z = cl * Math.cos(lon);
        P.gen(fA, fB, x, y, z, lat, lon, c);
        const o = j * W + i; tr[o] = c[0]; tg[o] = c[1]; tb[o] = c[2];
      }
    }
    return (texCache[key] = { tr, tg, tb, W, H });
  }
  function buildGeo(name, S, Wc, tex, img) {
    const P = PLANETS[name], a = P.tilt * PI / 180, ca = Math.cos(a), sa = Math.sin(a), W = tex.W, H = tex.H;
    let Lx = -.5, Ly = .45, Lz = .74; const ln = Math.hypot(Lx, Ly, Lz); Lx /= ln; Ly /= ln; Lz /= ln;
    const Lpx = Lx, Lpy = Ly * ca + Lz * sa, Lpz = -Ly * sa + Lz * ca;
    const R = S / 2 - 1, Cx = Wc / 2, Cy = S / 2, max = Wc * S, d = img.data, rc = [0, 0, 0];
    const pix = new Int32Array(max), u0 = new Float32Array(max), r0 = new Int32Array(max), fv = new Float32Array(max), sh = new Float32Array(max), rim = new Float32Array(max), ra = new Float32Array(max), rr = new Float32Array(max * 3);
    let n = 0;
    for (let py = 0; py < S; py++) for (let px = 0; px < Wc; px++) {
      const dx = (px + .5 - Cx) / R, dy = -(py + .5 - Cy) / R * P.flat, r2 = dx * dx + dy * dy, rl = Math.sqrt(r2);
      const pa = clamp((1 - rl) * R / P.flat + .5), o4 = (py * Wc + px) * 4;
      const nz = Math.sqrt(Math.max(0, 1 - Math.min(r2, .9999)));
      // ring sample for this pixel
      let ringA = 0;
      if (P.ring) {
        const z = -dy * ca / sa, qz = -dy * sa + z * ca, rad = Math.sqrt(dx * dx + qz * qz);
        if (rad > 1.2 && rad < 2.4 && (pa <= 0 || z > nz)) {
          ringA = ringAt(rad, rc);
          if (ringA > 0) {
            const b = dx * Lpx + qz * Lpz, cc = rad * rad - 1, disc = b * b - cc;
            const shadow = (b < 0 && disc > 0) ? .1 : 1;
            const lit = (.62 + .38 * Lpy) * shadow;
            rc[0] *= lit; rc[1] *= lit; rc[2] *= lit;
          }
        }
      }
      if (pa <= 0) {
        if (ringA > 0) { d[o4] = rc[0]; d[o4 + 1] = rc[1]; d[o4 + 2] = rc[2]; d[o4 + 3] = ringA * 255; }
        continue;
      }
      const diff = dx * Lx + dy * Ly + nz * Lz;
      const lam = clamp((diff + .06) / 1.06);
      let s = P.amb + (1 - P.amb) * Math.pow(lam, 1.15) * ((1 - P.k) + P.k * Math.pow(nz, .55));
      const qx = dx, qy = dy * ca + nz * sa, qz = -dy * sa + nz * ca;
      if (P.ring && Lpy > 0 && qy < 0) {
        const t = -qy / Lpy, hx = qx + t * Lpx, hz = qz + t * Lpz;
        s *= 1 - .85 * ringAt(Math.sqrt(hx * hx + hz * hz), [0, 0, 0]);
      }
      const lat = Math.asin(Math.max(-1, Math.min(1, qy))), lon = Math.atan2(qx, qz);
      let v = (.5 - lat / PI) * H - .5; v = v < 0 ? 0 : v > H - 1.001 ? H - 1.001 : v;
      pix[n] = o4; u0[n] = (lon + PI) / (2 * PI) * W - .5;
      r0[n] = (v | 0) * W; fv[n] = v - (v | 0); sh[n] = s;
      rim[n] = Math.pow(1 - nz, 2.4) * clamp(diff + .35) * P.as;
      ra[n] = ringA; rr[n * 3] = rc[0]; rr[n * 3 + 1] = rc[1]; rr[n * 3 + 2] = rc[2];
      d[o4 + 3] = Math.max(pa, ringA) * 255; n++;
    }
    return { n, pix, u0, r0, fv, sh, rim, ra, rr, atm: P.atm };
  }

  class Renderer {
    constructor(canvas) {
      this.cv = canvas; this.name = 'Satürn'; this.period = 40; this.scale = 1; this.visible = true;
      this.tick = this.tick.bind(this); this.raf = requestAnimationFrame(this.tick);
      if (window.IntersectionObserver) { this.io = new IntersectionObserver((e) => { this.visible = e[0].isIntersecting; }); this.io.observe(canvas); }
    }
    set(name, period, scale) { if (PLANETS[name]) this.name = name; if (period) this.period = period; if (scale) this.scale = scale; }
    destroy() { cancelAnimationFrame(this.raf); if (this.io) this.io.disconnect(); }
    tick(now) {
      this.raf = requestAnimationFrame(this.tick);
      if (!this.visible || document.hidden || (this.paused && this.gName === this.name && this.gS) || now - (this.last || 0) < 33) return;
      this.last = now;
      const S = Math.round(300 * Math.min(this.scale * (window.devicePixelRatio || 1), 2.2)), P = PLANETS[this.name];
      const Wc = P.ring ? Math.round(S * 2.4) : S;
      if (this.gName !== this.name || this.gS !== S) {
        this.tex = buildTex(this.name, S > 420 ? 1024 : 768);
        this.cv.width = Wc; this.cv.height = S; this.ctx = this.cv.getContext('2d');
        const cssW = 300 * Wc / S;
        Object.assign(this.cv.style, { position: 'absolute', left: (150 - cssW / 2) + 'px', top: '0px', width: cssW + 'px', height: '300px', borderRadius: '0' });
        this.img = this.ctx.createImageData(Wc, S);
        this.geo = buildGeo(this.name, S, Wc, this.tex, this.img);
        this.gName = this.name; this.gS = S;
      }
      const g = this.geo, t = this.tex, W = t.W, d = this.img.data, tr = t.tr, tg = t.tg, tb = t.tb;
      const [ar, ag, ab] = g.atm, off = ((now / 1000) / this.period * W) % W;
      const { n, pix, u0, r0, fv, sh, rim, ra, rr } = g;
      for (let k = 0; k < n; k++) {
        let u = u0[k] - off; u -= Math.floor(u / W) * W;
        const ui = u | 0, fu = u - ui, ui1 = ui + 1 === W ? 0 : ui + 1, f = fv[k];
        const a = r0[k] + ui, b = r0[k] + ui1, c = a + W, e = b + W;
        const s = sh[k], rm = rim[k], o = pix[k], A = ra[k], iA = 1 - A, k3 = k * 3;
        let top = tr[a] + (tr[b] - tr[a]) * fu, bot = tr[c] + (tr[e] - tr[c]) * fu; d[o] = ((top + (bot - top) * f) * s + ar * rm) * iA + rr[k3] * A;
        top = tg[a] + (tg[b] - tg[a]) * fu; bot = tg[c] + (tg[e] - tg[c]) * fu; d[o + 1] = ((top + (bot - top) * f) * s + ag * rm) * iA + rr[k3 + 1] * A;
        top = tb[a] + (tb[b] - tb[a]) * fu; bot = tb[c] + (tb[e] - tb[c]) * fu; d[o + 2] = ((top + (bot - top) * f) * s + ab * rm) * iA + rr[k3 + 2] * A;
      }
      this.ctx.putImageData(this.img, 0, 0);
    }
  }
  window.GalaxyPlanet = { create: (canvas) => new Renderer(canvas), names: Object.keys(PLANETS) };
})();
