// Galaxy 101 — live starfield: parallax drift, twinkle, shooting stars. One canvas per screen.
(function () {
  function sprite(r) {
    const c = document.createElement('canvas'), s = r * 2; c.width = c.height = s;
    const x = c.getContext('2d'), g = x.createRadialGradient(r, r, 0, r, r, r);
    g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(.18, 'rgba(255,255,255,.85)'); g.addColorStop(.45, 'rgba(255,255,255,.18)'); g.addColorStop(1, 'rgba(255,255,255,0)');
    x.fillStyle = g; x.fillRect(0, 0, s, s); return c;
  }
  function tinted(base, hex) {
    const c = document.createElement('canvas'); c.width = base.width; c.height = base.height;
    const x = c.getContext('2d'); x.drawImage(base, 0, 0); x.globalCompositeOperation = 'source-atop';
    x.globalAlpha = .55; x.fillStyle = hex; x.fillRect(0, 0, c.width, c.height); return c;
  }
  class Stars {
    constructor(canvas, w, h) {
      (window.__galaxyFx = window.__galaxyFx || []).push(this); this.cv = canvas; this.w = w; this.h = h; this.scale = 1; this.tint = '#FFE7A3'; this.visible = true;
      this.base = sprite(16); this.spr = [this.base, tinted(this.base, this.tint)];
      const L = [[150, .35, .9, 3], [70, .7, 1.5, 8], [22, 1.1, 2.4, 16]];
      this.stars = [];
      L.forEach(([n, a, sz, sp], li) => { for (let i = 0; i < n; i++) this.stars.push({ x: Math.random() * w, y: Math.random() * h, a: a * (.6 + Math.random() * .4), s: sz * (.6 + Math.random() * .7), sp, tw: .4 + Math.random() * 2.2, ph: Math.random() * 6.28, t: Math.random() < .35 ? 1 : 0, li }); });
      this.shots = []; this.nextShot = performance.now() + 1500;
      this.tick = this.tick.bind(this); this.raf = requestAnimationFrame(this.tick);
      if (window.IntersectionObserver) { this.io = new IntersectionObserver((e) => { this.visible = e[0].isIntersecting; }); this.io.observe(canvas); }
    }
    set(tint, scale) {
      if (tint && tint !== this.tint) { this.tint = tint; this.spr[1] = tinted(this.base, tint); }
      if (scale) this.scale = scale;
    }
    destroy() { cancelAnimationFrame(this.raf); if (this.io) this.io.disconnect(); }
    tick(now) {
      this.raf = requestAnimationFrame(this.tick);
      if (!this.visible || document.hidden || (this.paused && this.prev)) { this.prev = now; return; }
      const dt = Math.min(.05, (now - (this.prev || now)) / 1000); this.prev = now;
      const k = Math.min(this.scale * (window.devicePixelRatio || 1), 2), W = Math.round(this.w * k), H = Math.round(this.h * k);
      if (this.cv.width !== W || this.cv.height !== H) { this.cv.width = W; this.cv.height = H; }
      const x = this.cv.getContext('2d'); x.setTransform(k, 0, 0, k, 0, 0); x.clearRect(0, 0, this.w, this.h);
      const t = now / 1000;
      x.globalCompositeOperation = 'lighter';
      for (const s of this.stars) {
        s.x -= s.sp * dt; if (s.x < -4) { s.x = this.w + 4; s.y = Math.random() * this.h; }
        const al = s.a * (.55 + .45 * Math.sin(t * s.tw + s.ph));
        x.globalAlpha = al; const r = s.s * 2.6;
        x.drawImage(this.spr[s.t], s.x - r, s.y - r, r * 2, r * 2);
      }
      if (now > this.nextShot) {
        const dir = Math.random() < .5 ? -1 : 1, ang = (dir < 0 ? 200 : -20) + (Math.random() * 25 - 5);
        const rad = ang * Math.PI / 180;
        this.shots.push({ x: dir < 0 ? this.w * (.45 + Math.random() * .55) : this.w * Math.random() * .55, y: Math.random() * this.h * .45, vx: Math.cos(rad) * 820 * (dir < 0 ? 1 : 1), vy: Math.abs(Math.sin(rad)) * 820 + 120, life: 0, max: .75 + Math.random() * .5, len: 120 + Math.random() * 90 });
        this.nextShot = now + 2200 + Math.random() * 4200;
      }
      for (let i = this.shots.length - 1; i >= 0; i--) {
        const m = this.shots[i]; m.life += dt; if (m.life > m.max) { this.shots.splice(i, 1); continue; }
        m.x += m.vx * dt; m.y += m.vy * dt;
        const p = m.life / m.max, fade = Math.sin(p * Math.PI), sp = Math.hypot(m.vx, m.vy), ux = m.vx / sp, uy = m.vy / sp;
        const tx = m.x - ux * m.len, ty = m.y - uy * m.len;
        const g = x.createLinearGradient(m.x, m.y, tx, ty);
        g.addColorStop(0, 'rgba(255,255,255,' + fade + ')'); g.addColorStop(.25, 'rgba(255,240,210,' + fade * .5 + ')'); g.addColorStop(1, 'rgba(255,255,255,0)');
        x.globalAlpha = 1; x.strokeStyle = g; x.lineWidth = 1.8; x.lineCap = 'round';
        x.beginPath(); x.moveTo(m.x, m.y); x.lineTo(tx, ty); x.stroke();
        x.globalAlpha = fade; x.drawImage(this.base, m.x - 7, m.y - 7, 14, 14);
      }
      x.globalAlpha = 1; x.globalCompositeOperation = 'source-over';
    }
  }
  window.GalaxyStars = { create: (canvas, w, h) => new Stars(canvas, w, h) };
})();
