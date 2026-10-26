"""Compare every captured Unity state with the prototype reference and write a per-state result JSON.

    uv run --with pillow python tools/v4/compare_states.py <unity capture dir> [--ref artifacts/v4/ref] [--out <json>]

Per state: text boxes (tools/compare_ui.py, ±3 px), named elements present in both layout dumps (box ±3 px),
and the pixel difference inside the reference's UI element boxes (full-screen layers excluded, so the animated star
and planet canvas — EK-001 — does not count). Thresholds are the ones in docs/v4/DURUMLAR.md.
"""
import argparse, json, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from compare_ui import compare, load  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FULL = ("app.stage", "menu.starsA", "menu.planetCanvas")
# Texts Unity draws as artwork, not as a text element (docs/v4/UNITY_FARKLARI.md UF-03): never "missing".
ARTWORK = {"GALAXY"}


def boxes(layout):
    out = {}
    for it in (layout or {}).get("items", []):
        if it.get("visible"): out.setdefault(it["id"], it["box"])
    return out


def ui_mask(layout, size):
    from PIL import Image, ImageDraw
    m = Image.new("L", size, 0); d = ImageDraw.Draw(m)
    for it in (layout or {}).get("items", []):
        b = it["box"]
        if not it.get("visible") or it["id"] in FULL or (b["w"] >= 1270 and b["h"] >= 710): continue
        d.rectangle([b["x"], b["y"], b["x"] + b["w"], b["y"] + b["h"]], fill=255)
    d.rectangle([1150, 696, 1280, 720], fill=0)   # Unity's "Development Build" watermark (UNITY_FARKLARI UF-01)
    return m


def pixel(ref_png, uni_png, layout):
    try:
        from PIL import Image, ImageChops
    except ImportError: return None
    a, b = Image.open(ref_png).convert("RGB"), Image.open(uni_png).convert("RGB")
    if a.size != b.size: b = b.resize(a.size)
    mask = ui_mask(layout, a.size)
    diff = ImageChops.difference(a, b).convert("L").point(lambda v: 255 if v > 24 else 0)
    inside = ImageChops.multiply(diff, mask)
    area = sum(1 for v in mask.getdata() if v) or 1
    return round(100.0 * sum(1 for v in inside.getdata() if v) / area, 2)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("unity")
    ap.add_argument("--ref", default=os.path.join(REPO, "artifacts", "v4", "ref"))
    ap.add_argument("--out")
    a = ap.parse_args()
    results = {}
    for f in sorted(os.listdir(a.unity)):
        if not f.endswith(".text.json"): continue
        sid = f[:-10]
        rt, rl, rp = (os.path.join(a.ref, sid + s) for s in (".text.json", ".layout.json", ".png"))
        if not os.path.exists(rt): results[sid] = {"error": "no reference text dump"}; continue
        # Text inside an SVG (league emblems, icons) is artwork: Unity shows those as images (docs/v4/hat-d EK-404).
        ref_texts = [t for t in load(rt) if not (len(t) > 10 and t[10])]
        res = compare(ref_texts, load(os.path.join(a.unity, f)))
        res["missing"] = [m for m in res["missing"] if m not in ARTWORK]
        with open(rl, encoding="utf-8") as h: ref_layout = json.load(h)
        ul = os.path.join(a.unity, sid + ".layout.json")
        uni_layout = json.load(open(ul, encoding="utf-8")) if os.path.exists(ul) else None
        rb, ub = boxes(ref_layout), boxes(uni_layout)
        common = sorted(set(rb) & set(ub))
        off = [k for k in common if max(abs(rb[k][c] - ub[k][c]) for c in ("x", "y", "w", "h")) > 3]
        results[sid] = {
            "texts": res["total"], "textsMatched": res["matched"], "textsOff": len(res["off"]), "textsMissing": len(res["missing"]), "textsExtra": len(res["extra"]),
            "missing": res["missing"][:30], "extra": res["extra"][:30], "off": res["off"][:30],
            "elements": len(rb), "elementsNamedInUnity": len(common), "elementsOff": off[:40],
            "uiPixelDiffPct": pixel(rp, os.path.join(a.unity, sid + ".png"), ref_layout) if os.path.exists(os.path.join(a.unity, sid + ".png")) else None,
        }
        r = results[sid]
        print(f"{sid:4s} texts {r['textsMatched']:3d}/{r['texts']:3d} off {r['textsOff']:3d} miss {r['textsMissing']:3d} extra {r['textsExtra']:3d} · named {len(common):3d}/{len(rb):3d} off {len(off):3d} · ui px {r['uiPixelDiffPct']}%")
    out = a.out or os.path.join(a.unity, "compare.json")
    with open(out, "w", encoding="utf-8") as h: json.dump(results, h, ensure_ascii=False, indent=1)
    print("wrote", out)


if __name__ == "__main__":
    main()
