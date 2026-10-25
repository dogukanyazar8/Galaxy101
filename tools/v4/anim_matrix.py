"""docs/v4/matris/anim.md: one row per anim/css.json keyframe (used ones), tween3d.json group and easings curve, with
where the name occurs in the Unity client (a name hit is a lead, not proof; Faz 2 verifies each against the reference).

    python tools/v4/anim_matrix.py
"""
import json, os, re
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PKG = os.path.join(REPO, "Design", "Galaxy101_ClaudeCode", "prototip", "galaxy101_handoff_v4", "anim")
SRC = []
for top in ("Assets/Galaxy101", "Assets/Okey101/Presentation"):
    for root, _, files in os.walk(os.path.join(REPO, top)):
        if "Tests" in root: continue
        for f in files:
            if f.endswith((".cs", ".uss", ".uxml")):
                p = os.path.join(root, f)
                SRC.append((os.path.relpath(p, REPO).replace("\\", "/"), open(p, encoding="utf-8", errors="replace").read()))

def hits(name):
    rx = re.compile(r"\b" + re.escape(name) + r"\b", re.I)
    return [p for p, t in SRC if rx.search(t)]

css = json.load(open(os.path.join(PKG, "css.json"), encoding="utf-8"))
tw = json.load(open(os.path.join(PKG, "tween3d.json"), encoding="utf-8"))
ez = json.load(open(os.path.join(PKG, "easings.json"), encoding="utf-8"))
uses = {}
for u in css["uses"]:
    for part in u.get("parts", []):
        if "name" not in part: continue
        uses.setdefault(part["name"], []).append((u.get("element") or u.get("near") or "?", part.get("durationMs"), part.get("easing"), part.get("iterations")))
rows = ["# Fark matrisi · animasyon", "", "Üretildi: `python tools/v4/anim_matrix.py`. \"Unity'de ad\" sütunu ad eşleşmesidir (ipucu); değer karşılaştırması Faz 2'de durum durum yapılır (`DURUMLAR.md`).", "",
        f"## css.json · {len(css['keyframes'])} keyframe, {len(css['uses'])} kullanım, kullanılmayan {len(css['unused'])} (taşınmaz)", "",
        "| Kimlik | Pakette (kullanım: öğe · süre · eğri · tekrar) | Unity'de ad | Durum |", "|---|---|---|---|"]
for name in css["keyframes"]:
    if name in css["unused"]: continue
    u = uses.get(name, [])
    desc = "; ".join(f"{e} · {d} ms · {ea} · {it}" for e, d, ea, it in u[:3]) + (f" (+{len(u)-3})" if len(u) > 3 else "")
    h = hits(name)
    rows.append(f"| css.{name} | {desc or '—'} | {', '.join(h[:2]) or '—'} | {'ad var · değer Faz 2' if h else 'eksik (ad yok) · Faz 2'} |")
rows += ["", f"Kullanılmayanlar (taşınmaz): {', '.join(css['unused'])}", "", "## tween3d.json", "", "| Kimlik | Pakette | Unity'de ad | Durum |", "|---|---|---|---|"]
for k, v in tw.items():
    if k in ("version", "source", "units"): continue
    keys = list(v.keys()) if isinstance(v, dict) else [str(v)[:40]]
    for sub in keys:
        h = hits(sub) if isinstance(v, dict) else []
        rows.append(f"| tween3d.{k}.{sub} | {json.dumps(v[sub] if isinstance(v, dict) else v, ensure_ascii=False)[:120].replace('|','/')} | {', '.join(h[:2]) or '—'} | {'ad var · değer Faz 2' if h else 'Faz 2'} |")
rows += ["", "## easings.json", "", "| Kimlik | Pakette | Unity'de ad | Durum |", "|---|---|---|---|"]
for k, v in ez["curves"].items():
    h = hits(k)
    rows.append(f"| ease.{k} | {json.dumps(v, ensure_ascii=False)[:120].replace('|','/')} | {', '.join(h[:2]) or '—'} | {'ad var · değer Faz 2' if h else 'Faz 2'} |")
open(os.path.join(REPO, "docs", "v4", "matris", "anim.md"), "w", encoding="utf-8", newline="\n").write("\n".join(rows) + "\n")
print(len(rows), "rows")
