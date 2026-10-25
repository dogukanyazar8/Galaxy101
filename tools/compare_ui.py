"""Match on-screen texts between the reference page dump and the Unity dump; print every box that is off.

Reference dump: tools/ref_capture.py <ID>.text.json ([text,x,y,w,h,"NNpx",weight,family,color,owner-id], or the older
{"items":[[text,x,y,w,h,fontSize,weight,family,color],...]}). Unity dump: G101StateRouter <ID>.text.json or the PlayMode
UiDump (<name>.unity.json). Usage: python compare_ui.py ref.json unity.json [tolerance px]
Exit code 1 when any text is off, missing or extra (usable as a gate), 0 when everything matches.
"""
import json, re, sys


def px(v):
    return float(str(v).replace("px", "")) if v not in (None, "") else 0.0


def norm(t):
    return re.sub(r"\s+", " ", str(t).replace(" ", " ")).strip()   # whole text: a differing tail is a different text


def load(path):
    with open(path, encoding="utf-8") as f: data = json.load(f)
    return data["items"] if isinstance(data, dict) else data


def compare(ref, uni, tol=3.0):
    """Returns {"off": [...], "missing": [...], "extra": [...], "matched": n, "total": len(ref)}."""
    pool = {}
    for u in uni: pool.setdefault(norm(u[0]), []).append(u)
    off, missing, matched = [], [], 0
    for r in ref:
        key = norm(r[0]); cands = pool.get(key)
        if not cands: missing.append(key); continue
        u = min(cands, key=lambda u: abs(u[1] - r[1]) + abs(u[2] - r[2])); cands.remove(u)
        dx, dy, dw, dh = u[1] - r[1], u[2] - r[2], u[3] - r[3], u[4] - r[4]
        dfs = px(u[5]) - px(r[5])
        if max(abs(dx), abs(dy)) > tol or abs(dw) > max(tol, r[3] * 0.06) or abs(dh) > max(tol, r[4] * 0.06) or abs(dfs) > 0.5:
            off.append({"text": key, "ref": r[1:6], "unity": u[1:6], "d": [dx, dy, dw, dh, round(dfs, 2)]})
        else: matched += 1
    extra = [k for k, v in pool.items() if v]
    return {"off": off, "missing": missing, "extra": extra, "matched": matched, "total": len(ref)}


if __name__ == "__main__":
    tol = float(sys.argv[3]) if len(sys.argv) > 3 else 3
    res = compare(load(sys.argv[1]), load(sys.argv[2]), tol)
    for o in res["off"]:
        r, u, d = o["ref"], o["unity"], o["d"]
        print(f"{o['text'][:32]:32s} ref({r[0]:4},{r[1]:4},{r[2]:4}x{r[3]:3} {px(r[4]):4.1f}px) unity({u[0]:4},{u[1]:4},{u[2]:4}x{u[3]:3} {px(u[4]):4.1f}px) d=({d[0]:+},{d[1]:+}) dw={d[2]:+} dfs={d[4]:+}")
    for key in res["missing"]: print("MISSING in Unity:", key)
    for key in res["extra"]: print("EXTRA in Unity:", key)
    print(f"{res['total']} reference texts, {len(res['off'])} off by more than {tol}px, {len(res['missing'])} missing, {len(res['extra'])} extra")
    sys.exit(1 if res["off"] or res["missing"] or res["extra"] else 0)
