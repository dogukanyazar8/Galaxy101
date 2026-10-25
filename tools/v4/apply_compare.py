"""Fold a compare_states.py result into docs/v4/durumlar.json, then regenerate DURUMLAR.md.

    python tools/v4/apply_compare.py <unity capture dir>/compare.json [--run <label>]

A state becomes "eşleşti" only when every reference text is matched within ±3 px (none off, missing or extra) and
the UI-region pixel difference is at most 5 % (font rasterisation and antialiasing differ between Chrome and Unity).
A state already marked "bilinçli fark", "eksik" or "kaldırıldı" by hand keeps its mark; the measurement is added
to its note. Everything else stays "bekliyor" with the measurement in the note.
"""
import argparse, json, os, subprocess, sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
STATUS = os.path.join(REPO, "docs", "v4", "durumlar.json")
PIXEL_LIMIT = 5.0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("compare")
    ap.add_argument("--run", default=None)
    a = ap.parse_args()
    with open(a.compare, encoding="utf-8") as f: results = json.load(f)
    with open(STATUS, encoding="utf-8") as f: status = json.load(f)
    run = a.run or os.path.basename(os.path.dirname(os.path.abspath(a.compare)))
    changed = 0
    for sid, r in results.items():
        st = status["states"].setdefault(sid, {})
        if "error" in r: continue
        px = r.get("uiPixelDiffPct")
        measure = (f"[{run}] metin {r['textsMatched']}/{r['texts']} (kayık {r['textsOff']}, eksik {r['textsMissing']}, fazla {r['textsExtra']})"
                   f" · ad {r['elementsNamedInUnity']}/{r['elements']} · UI piksel %{px}")
        ok = r["textsOff"] == 0 and r["textsMissing"] == 0 and r["textsExtra"] == 0 and px is not None and px <= PIXEL_LIMIT
        keep = st.get("durum") in ("bilinçli fark", "eksik", "kaldırıldı")
        base = st.get("not", "")
        base = base.split(" [")[0] if base.startswith("[") is False and " [" in base else ("" if base.startswith("[") else base)
        st["not"] = (base + " " if base else "") + measure
        if not keep:
            st["durum"] = "eşleşti" if ok else "bekliyor"
        changed += 1
    with open(STATUS, "w", encoding="utf-8") as f: json.dump(status, f, ensure_ascii=False, indent=1)
    print(f"updated {changed} states from {a.compare}")
    subprocess.run([sys.executable, os.path.join(REPO, "tools", "v4", "states_table.py")], check=True)


if __name__ == "__main__":
    main()
