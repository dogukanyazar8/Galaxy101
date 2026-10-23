"""Match on-screen texts between the reference page dump and the Unity dump; print every box that is off.

Reference dump: JSON from the browser ({"items":[[text,x,y,w,h,fontSize,weight,family,color],...]} or the bare list).
Unity dump: tests' UiDump (<name>.unity.json). Usage: python compare_ui.py ref.json unity.json [tolerance px]
"""
import json, sys, re
ref = json.load(open(sys.argv[1], encoding="utf-8")); ref = ref["items"] if isinstance(ref, dict) else ref
uni = json.load(open(sys.argv[2], encoding="utf-8"))
tol = float(sys.argv[3]) if len(sys.argv) > 3 else 3
norm = lambda t: re.sub(r"\s+", " ", t.replace(" ", " ")).strip()[:40]
pool = {}
for u in uni: pool.setdefault(norm(u[0]), []).append(u)
bad = 0; missing = []
for r in ref:
    key = norm(r[0]); cands = pool.get(key)
    if not cands: missing.append(key); continue
    u = min(cands, key=lambda u: abs(u[1] - r[1]) + abs(u[2] - r[2])); cands.remove(u)
    dx, dy, dw, dh = u[1] - r[1], u[2] - r[2], u[3] - r[3], u[4] - r[4]
    fs = float(str(r[5]).replace("px", "")); dfs = u[5] - fs
    if max(abs(dx), abs(dy)) > tol or abs(dw) > max(tol, r[3] * 0.06) or abs(dfs) > 0.5:
        bad += 1
        print(f"{key[:32]:32s} ref({r[1]:4},{r[2]:4},{r[3]:4}x{r[4]:3} {fs:4.1f}px) unity({u[1]:4},{u[2]:4},{u[3]:4}x{u[4]:3} {u[5]:4.1f}px) d=({dx:+},{dy:+}) dw={dw:+} dfs={dfs:+}")
for key in missing: print("MISSING in Unity:", key)
extra = [k for k, v in pool.items() if v]
for key in extra: print("EXTRA in Unity:", key)
print(f"{len(ref)} reference texts, {bad} off by more than {tol}px, {len(missing)} missing, {len(extra)} extra")
