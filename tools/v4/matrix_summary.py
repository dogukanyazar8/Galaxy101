"""Count the "Durum" column of every difference matrix (docs/v4/matris/*.md) and every gap file (docs/v4/**/EKSIKLER.md)
and print a markdown summary (FARK_MATRISI "Özet sayılar", EKSIKLER index).

    python tools/v4/matrix_summary.py [--write]

--write replaces the "## Özet sayılar" section of docs/v4/FARK_MATRISI.md and the "## Hatların eksik kayıtları"
section of docs/v4/EKSIKLER.md.
"""
import glob, os, re, sys
from collections import Counter

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(REPO, "docs", "v4")
STATES = ("var", "farklı", "eksik", "pakette yok")


def cells(line):
    return [c.strip() for c in line.strip().strip("|").split("|")]


def matrix_counts(path):
    counts, col = Counter(), None
    for line in open(path, encoding="utf-8"):
        if not line.startswith("|"): col = None if not line.strip() else col; continue
        c = cells(line)
        if col is None:
            for i, h in enumerate(c):
                if h.lower().startswith("durum"): col = i
            continue
        if set(line.strip()) <= set("|-: "): continue
        if col is not None and col < len(c):
            v = re.sub(r"[*`]", "", c[col]).lower()
            hit = next((s for s in STATES if v.startswith(s)), None)
            counts[hit or "diğer"] += 1
    return counts


def gap_ids(path):
    ids = []
    for line in open(path, encoding="utf-8"):
        m = re.match(r"\|\s*(EK-\d+)\s*\|\s*([A-Z])?", line)
        if m: ids.append((m.group(1), m.group(2) or "?"))
    return ids


def main():
    rows = ["| Matris | Satır | var | farklı | eksik | pakette yok | diğer |", "|---|---|---|---|---|---|---|"]
    total = Counter()
    for path in sorted(glob.glob(os.path.join(DOCS, "matris", "*.md"))):
        c = matrix_counts(path); total.update(c)
        rows.append(f"| `matris/{os.path.basename(path)}` | {sum(c.values())} | " + " | ".join(str(c.get(s, 0)) for s in STATES + ("diğer",)) + " |")
    rows.append(f"| **toplam** | {sum(total.values())} | " + " | ".join(str(total.get(s, 0)) for s in STATES + ("diğer",)) + " |")
    summary = "\n".join(["## Özet sayılar", "", "Üretildi: `python tools/v4/matrix_summary.py --write`. \"var\" = proje paketle zaten aynıydı; \"farklı\"/\"eksik\" = v4 öncesi durum (her satırın \"v4 sonrası\" sütunu ne yapıldığını söyler).", ""] + rows + [""])
    gaps = ["## Hatların eksik kayıtları", "", "Her hat kendi kayıtlarını tutar; kimlik aralıkları: 0xx ana, 1xx kural, 2xx sunucu, 3xx 3D, 4xx veri, 5xx masa, 6xx menü, 7xx dereceli, 8xx lobi, 9xx ses. Grup: T tasarımda yok · K kullanıcı kararı · D dış hizmet · U Unity kısıtı · P paket tutarsızlığı.", "",
            "| Dosya | Kayıt | T | K | D | U | P |", "|---|---|---|---|---|---|---|"]
    for path in sorted(glob.glob(os.path.join(DOCS, "**", "EKSIKLER.md"), recursive=True)):
        ids = gap_ids(path); g = Counter(x[1] for x in ids)
        rel = os.path.relpath(path, DOCS).replace("\\", "/")
        gaps.append(f"| `{rel}` | {len(ids)} ({ids[0][0]}…{ids[-1][0]}) | " + " | ".join(str(g.get(k, 0)) for k in "TKDUP") + " |" if ids else f"| `{rel}` | 0 | | | | | |")
    gaps_text = "\n".join(gaps + [""])
    print(summary); print(gaps_text)
    if "--write" in sys.argv:
        for name, section, head in (("FARK_MATRISI.md", summary, "## Özet sayılar"), ("EKSIKLER.md", gaps_text, "## Hatların eksik kayıtları")):
            p = os.path.join(DOCS, name); text = open(p, encoding="utf-8").read()
            if head in text: text = text[:text.index(head)].rstrip() + "\n\n" + section
            else: text = text.rstrip() + "\n\n" + section
            open(p, "w", encoding="utf-8", newline="\n").write(text)


if __name__ == "__main__":
    main()
