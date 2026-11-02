using System;
using System.Numerics;

namespace Galaxy101
{
    /// <summary>Source-space (right handed, +X nose) fixed-step port of ships3d.js. Owns no Unity resources.</summary>
    public sealed class ShipFlightSimulation
    {
        public const float StepSeconds = 1f / 60;
        private const double FixedStep = 1.0 / 60;
        private const float Tan = .2493280028f, Aspect = 16f / 9;
        private const int MaxPath = 2880, MaxChase = 600;
        private readonly Flight[] flights = { new Flight(4.2f), new Flight(3.6f), new Flight(4.1f), new Flight(1.8f), new Flight(1.8f) };
        private readonly Vector3[] chaseP = new Vector3[MaxChase], chaseV = new Vector3[MaxChase];
        private readonly ShipBolt[] bolts = new ShipBolt[32];
        private readonly ShipHit[] hits = new ShipHit[64];
        private uint random;
        private double accumulator;
        private long steps;
        private string planet = "Satürn";
        private bool table, chasing;
        private Vector3 center, chaseOffset;
        private float planetScale = 1, planetX = 640, planetY = 300, chaseWait, fireWait, rollAt, rollDirection;
        private int chaseN, chaseK, burst, gun, hitSerial;
        public double Seconds => steps * FixedStep;
        public int BoltCount { get; private set; }
        public int HitSerial => hitSerial;
        public int HitCount { get; private set; }
        public event Action<float> Stepped;

        public ShipFlightSimulation(int seed = 42)
        {
            random = unchecked((uint)seed); if (random == 0) random = 1;
            SetCenter();
            for (int i = 0; i < flights.Length; i++) flights[i].Phase = Next() * 6;
            ResetFlights();
        }
        public ShipPose GetShip(int index)
        {
            if ((uint)index >= flights.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return flights[index].Pose;
        }
        public float GetPhase(int index) => flights[index].Phase;
        public ShipBolt GetBolt(int index) => index >= 0 && index < BoltCount ? bolts[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public ShipHit GetHit(int index) => index >= 0 && index < HitCount ? hits[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public void Configure(string planetName, bool tableLayout)
        {
            string next = NormalizePlanet(planetName);
            if (planet == next && table == tableLayout) return;
            bool layoutChanged = table != tableLayout;
            planet = next; table = tableLayout; SetCenter();
            if (layoutChanged) { ResetFlights(); return; }
            // ships3d.js set(): a theme changes future routes, never an active flight or combat timeline.
            for (int i = 0; i < 3; i++) if (flights[i].Wait > 0) Plan(flights[i]);
        }
        internal static string NormalizePlanet(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "mars": return "Mars";
                case "neptün": case "neptun": case "neptune": return "Neptün";
                case "jüpiter": case "jupiter": return "Jüpiter";
                case "venüs": case "venus": return "Venüs";
                default: return "Satürn";
            }
        }
        private void SetCenter()
        {
            planetX = table ? 300 : 640; planetY = table ? 118 : 300; planetScale = table ? .82f : 1;
            float unit = 2 * Tan * 30 / 720;
            center = new Vector3((planetX - 640) * unit, (360 - planetY) * unit, 0);
        }
        private void ResetFlights()
        {
            for (int i = 0; i < 3; i++) Launch(flights[i], i == 0 ? .25f : -(2 + i * 5));
            flights[3].Pose.Visible = flights[4].Pose.Visible = false;
            chasing = false; chaseWait = 4 + Next() * 5; BoltCount = HitCount = 0;
        }
        public void Advance(double deltaSeconds, bool running)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!running) return;
            accumulator += deltaSeconds;
            while (accumulator + 1e-10 >= FixedStep)
            {
                accumulator -= FixedStep; steps++; Tick(); Stepped?.Invoke(StepSeconds);
            }
        }
        // g101-clock.js replaces Math.random with mulberry32. Keep a private stream so gameplay RNG never depends on rendering.
        private float Next()
        {
            random = unchecked(random + 0x6D2B79F5u);
            uint t = unchecked((random ^ (random >> 15)) * (1u | random));
            t = unchecked((t + (t ^ (t >> 7)) * (61u | t)) ^ t);
            return Math.Min(.99999994f, (float)((t ^ (t >> 14)) / 4294967296.0));
        }
        public Vector2 Project(Vector3 position)
        {
            float h = Tan * (30 - position.Z);
            return new Vector2((position.X / (h * Aspect) + 1) * 640, (1 - position.Y / h) * 360);
        }
        public bool HitsPlanet(Vector3 p, float length)
        {
            var xy = Project(p); float dx = xy.X - planetX, dy = xy.Y - planetY;
            const float c = .959492974f, sn = .275637356f;
            float x = dx * c - dy * sn, y = dx * sn + dy * c;
            float flat = planet == "Satürn" ? 1.1f : planet == "Jüpiter" ? 1.07f : planet == "Neptün" ? 1.02f : 1;
            float margin = length * .5f * 720 / (2 * Tan * (30 - p.Z)) + 14, radius = 149 * planetScale + margin;
            if (x * x + y * y * flat * flat < radius * radius) return true;
            float rx = 352 * planetScale + margin, ry = 118 * planetScale + margin;
            return planet == "Satürn" && x * x / (rx * rx) + y * y / (ry * ry) < 1;
        }
        private Vector3 Acceleration(Vector3 p)
        {
            var d = center - p; float d2 = d.LengthSquared();
            float gravity = planet == "Jüpiter" ? 17 : planet == "Satürn" ? 14 : planet == "Neptün" ? 11 : planet == "Venüs" ? 9 : 7;
            return d2 < 1e-10f ? Vector3.Zero : d * (gravity * planetScale / ((d2 + 1.5f) * (float)Math.Sqrt(d2)));
        }
        private bool Plan(Flight s)
        {
            float minimum = 149 * planetScale * (2 * Tan * 30 / 720) * (planet == "Satürn" ? 2.5f : 1) + s.Length * .6f + .8f;
            bool far = Next() < .4f;
            for (int attempt = 0; attempt < (table ? 400 : 140); attempt++)
            {
                float z = table ? (far ? -22 + Next() * 14 : 4 + Next() * 6) : (far ? -52 + Next() * 30 : -16 + Next() * 22);
                float hh = Tan * (30 - z), hw = hh * Aspect; bool horizontal = Next() < .75f; float side = Next() < .5f ? -1 : 1;
                var p = horizontal ? new Vector3(side * (hw + 3), table ? hh * (1 - (10 + Next() * 150) / 360) : (Next() * 1.4f - .7f) * hh, z) : new Vector3((Next() * 1.2f - .6f) * hw, side * (hh + 3), z);
                float spread = planet == "Satürn" ? 1.8f : 1.1f;
                var aim = new Vector3(center.X + (Next() - .5f) * hw * spread, center.Y + (Next() - .5f) * hh * spread, z + (Next() - .5f) * 8);
                var v = Unit(aim - p) * (2.3f + Next() * 1.1f);
                float closest = float.MaxValue; int inside = 0, inBand = 0; bool exited = false, rejected = false; int count = 0;
                for (int k = 0; k < MaxPath; k++)
                {
                    v += Acceleration(p) * StepSeconds; p += v * StepSeconds;
                    s.P[k] = p; s.V[k] = v; count++;
                    closest = Math.Min(closest, Vector3.Distance(p, center));
                    float h = Tan * (30 - p.Z), w = h * Aspect;
                    bool inView = Math.Abs(p.X) < w + s.Length * .5f && Math.Abs(p.Y) < h + s.Length * .5f;
                    if (inView) { inside++; if (table && k % 3 == 0) { float y = Project(p).Y; if (y > 10 && y < 160) inBand += 3; } }
                    else if (inside > 0 && (Math.Abs(p.X) > w + 4 || Math.Abs(p.Y) > h + 4)) { exited = true; break; }
                    if (p.Z > 12 || p.Z < -75 || closest < minimum || (!table && HitsPlanet(p, s.Length))) { rejected = true; break; }
                }
                if (exited && !rejected && inside > 360 && (!table || inBand > inside * .8f)) { s.Count = count; return true; }
            }
            s.Count = 0; return false;
        }
        private void Launch(Flight s, float start)
        {
            Plan(s); s.Cursor = start > 0 && start < 1 ? (int)(s.Count * start) : 0; s.Wait = start <= 0 ? -start : 0; s.Pose.Visible = false;
            if (start > 0 && s.Count > 2) Orient(s, s.P[s.Cursor], s.V[s.Cursor], Acceleration(s.P[s.Cursor]), .5f, 1.4f, 2.5f, 0);
        }
        private void Tick()
        {
            for (int i = 0; i < 3; i++)
            {
                var s = flights[i];
                if (s.Wait > 0 || s.Count == 0) { s.Wait -= StepSeconds; s.Pose.Visible = false; if (s.Wait <= 0 && s.Count == 0) Launch(s, -3); continue; }
                s.Cursor++;
                if (s.Cursor >= s.Count - 2) { Launch(s, -(3 + Next() * 7)); continue; }
                Orient(s, s.P[s.Cursor], s.V[s.Cursor], Acceleration(s.P[s.Cursor]), .5f, 1.4f, 2.5f, 0);
            }
            TickChase();
        }
        private void Orient(Flight s, Vector3 p, Vector3 velocity, Vector3 acceleration, float cameraWeight, float bankWeight, float responsiveness, float roll)
        {
            var f = Unit(velocity); var lateral = acceleration - f * Vector3.Dot(acceleration, f);
            var cd = Unit(new Vector3(0, 0, 30) - p); cd -= f * Vector3.Dot(cd, f);
            var up = Unit(Unit(Vector3.UnitY - f * f.Y) + cd * cameraWeight + lateral * bankWeight);
            s.Up = Unit(Vector3.Lerp(s.Up, up, 1 - (float)Math.Exp(-StepSeconds * responsiveness)));
            var side = Unit(Vector3.Cross(f, s.Up)); up = Vector3.Cross(side, f);
            s.Pose = new ShipPose { Position = p, Velocity = velocity, Up = up, Visible = true, Roll = roll };
        }
        private bool PlanChase()
        {
            const float lag = .55f;
            for (int attempt = 0; attempt < 160; attempt++)
            {
                bool far = Next() < .4f;
                float z = table ? (far ? -18 + Next() * 12 : 4 + Next() * 5) : (far ? -34 + Next() * 22 : Next() * 9);
                float hh = Tan * (30 - z), hw = hh * Aspect, side = Next() < .5f ? -1 : 1, duration = 5.2f + Next() * 2;
                float x0 = hw + 4.5f, vx = 2 * x0 / duration;
                float y0 = table ? hh * (1 - (10 + Next() * 150) / 360) : (Next() * 1.2f - .6f) * hh;
                float y1 = table ? hh * (1 - (10 + Next() * 150) / 360) : (Next() * 1.2f - .6f) * hh;
                float ay = table ? .35f + Next() * .25f : .8f + Next() * .9f, w1 = 1.5f + Next() * 1.2f, f1 = Next() * 6;
                float az = table ? .8f : 1.2f + Next() * 1.6f, w2 = .9f + Next() * .8f, f2 = Next() * 6;
                int n = (int)Math.Round((duration + lag) * 60) + 1; bool valid = true; int inView = 0, inBand = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / 60f;
                    var p = new Vector3(-side * x0 + side * vx * t, y0 + (y1 - y0) * Math.Min(1, t / duration) + ay * Sin(w1 * t + f1) + .35f * ay * Sin(2.7f * w1 * t + f1 * 1.7f), z + az * Sin(w2 * t + f2));
                    chaseP[i] = p;
                    var xy = Project(p);
                    if (p.Z > 11) valid = false;
                    if (xy.X > -60 && xy.X < 1340 && xy.Y > -60 && xy.Y < 780) { inView++; if (table && xy.Y > 0 && xy.Y < 170) inBand++; if (!table && HitsPlanet(p, 2.4f)) valid = false; }
                }
                if (!valid || inView < 30 || (table && inBand < inView * .85f)) continue;
                for (int i = 0; i < n; i++) { int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1); chaseV[i] = (chaseP[b] - chaseP[a]) * (60f / (b - a)); }
                chaseN = n; chaseK = 0; fireWait = 1.1f + Next() * .6f; burst = 0; rollAt = lag + 1.4f + Next() * (duration - 3.2f); rollDirection = Next() < .5f ? -1 : 1;
                chaseOffset = new Vector3(0, .18f * (Next() < .5f ? -1 : 1), .35f); return true;
            }
            return false;
        }
        private void TickChase()
        {
            var a = flights[3]; var b = flights[4]; float time = (float)Seconds;
            if (!chasing)
            {
                a.Pose.Visible = b.Pose.Visible = false; chaseWait -= StepSeconds;
                if (chaseWait <= 0) { chasing = PlanChase(); if (!chasing) chaseWait = 3; }
            }
            else if (++chaseK >= chaseN - 35) { chasing = false; chaseWait = 9 + Next() * 12; a.Pose.Visible = b.Pose.Visible = false; }
            else
            {
                float u = ((chaseK + 33) / 60f - rollAt) / .8f;
                float roll = u > 0 && u < 1 ? rollDirection * (float)Math.PI * 2 * (u * u * (3 - 2 * u)) : 0;
                roll += (float)Math.Exp(-(time - a.HitAt) * 9) * .25f * Sin((time - a.HitAt) * 40);
                int ai = chaseK + 33;
                Orient(a, chaseP[ai], chaseV[ai], (chaseV[ai + 1] - chaseV[ai]) * 60, .35f, .2f, 7, roll);
                Orient(b, chaseP[chaseK] + chaseOffset, chaseV[chaseK], (chaseV[chaseK + 1] - chaseV[chaseK]) * 60, .35f, .2f, 7, 0);
                var ap = Project(a.Pose.Position); var bp = Project(b.Pose.Position); fireWait -= StepSeconds;
                if (fireWait <= 0 && ap.X > 0 && ap.X < 1280 && ap.Y > 0 && ap.Y < 720 && bp.X > -13 && bp.X < 1293 && bp.Y > -7 && bp.Y < 727)
                {
                    if (burst <= 0) burst = 2 + (int)(Next() * 3); Fire(); burst--; fireWait = burst > 0 ? .085f : .6f + Next() * .7f;
                }
            }
            for (int i = BoltCount - 1; i >= 0; i--)
            {
                var bolt = bolts[i]; bolt.Age += StepSeconds; bolt.Position += bolt.Velocity * StepSeconds;
                bool hit = bolt.WillHit && a.Pose.Visible && Vector3.Distance(bolt.Position, a.Pose.Position) < .7f;
                if (hit)
                {
                    a.HitAt = time; hitSerial++;
                    if (HitCount == hits.Length) { hits[0] = hits[--HitCount]; }
                    hits[HitCount++] = new ShipHit { Position = bolt.Position, Serial = hitSerial };
                }
                if (hit || bolt.Age > .9f) { bolts[i] = bolts[--BoltCount]; } else bolts[i] = bolt;
            }
            for (int i = HitCount - 1; i >= 0; i--) { var hit = hits[i]; hit.Age += StepSeconds; if (hit.Age >= .5f) hits[i] = hits[--HitCount]; else hits[i] = hit; }
        }
        private void Fire()
        {
            if (BoltCount == bolts.Length) return;
            var a = flights[3].Pose; var b = flights[4].Pose; var f = Unit(b.Velocity); var side = Unit(Vector3.Cross(f, b.Up));
            var muzzle = b.Position + f * .48f + b.Up * -.012f + side * (gun++ % 2 == 0 ? -1 : 1);
            float distance = Vector3.Distance(muzzle, a.Position);
            var target = a.Position + a.Velocity * (distance / 46); bool hit = Next() < .38f;
            if (!hit) target += Unit(new Vector3(Next() - .5f, Next() - .5f, Next() - .5f)) * (.6f + Next() * .7f);
            var direction = Unit(target - muzzle);
            bolts[BoltCount++] = new ShipBolt { Position = muzzle + direction * .35f, Velocity = direction * 46 + b.Velocity, WillHit = hit, Serial = gun };
        }
        public float ShieldOpacity => .42f * (float)Math.Exp(-((float)Seconds - flights[3].HitAt) * 6);
        private static float Sin(float v) => (float)Math.Sin(v);
        private static Vector3 Unit(Vector3 value) => value.LengthSquared() > 1e-12f ? Vector3.Normalize(value) : Vector3.UnitY;
        private sealed class Flight
        {
            public readonly Vector3[] P = new Vector3[MaxPath], V = new Vector3[MaxPath];
            public readonly float Length;
            public int Count, Cursor;
            public float Wait, Phase, HitAt = -9;
            public Vector3 Up = Vector3.UnitY;
            public ShipPose Pose;
            public Flight(float length) { Length = length; }
        }
    }
    public struct ShipPose { public Vector3 Position, Velocity, Up; public float Roll; public bool Visible; }
    public struct ShipBolt { public Vector3 Position, Velocity; public float Age; public bool WillHit; public int Serial; }
    public struct ShipHit { public Vector3 Position; public float Age; public int Serial; }
}
