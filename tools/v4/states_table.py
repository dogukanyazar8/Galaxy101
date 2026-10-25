"""Write docs/v4/DURUMLAR.md from the package's states.json, the reference capture report and the per-state
status file docs/v4/durumlar.json (the only hand-edited input).

    python tools/v4/states_table.py

durumlar.json: {"thresholds": "...markdown...", "states": {"<ID>": {"unity": "...", "durum": "...", "not": "..."}}}
durum: eşleşti | bilinçli fark | eksik | kaldırıldı | bekliyor
"""
import json, os, sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PKG = os.path.join(REPO, "Design", "Galaxy101_ClaudeCode", "prototip", "galaxy101_handoff_v4")
STATUS = os.path.join(REPO, "docs", "v4", "durumlar.json")
REPORT = os.path.join(REPO, "artifacts", "v4", "ref", "capture_report.json")
OUT = os.path.join(REPO, "docs", "v4", "DURUMLAR.md")
ORDER = ["eşleşti", "bilinçli fark", "eksik", "kaldırıldı", "bekliyor"]


def cell(s):
    return str(s if s is not None else "").replace("|", "\\|").replace("\n", " ")


def main():
    with open(os.path.join(PKG, "states", "states.json"), encoding="utf-8") as f:
        states = json.load(f)["states"]
    status = {"thresholds": "", "states": {}}
    if os.path.exists(STATUS):
        with open(STATUS, encoding="utf-8") as f: status = json.load(f)
    report = {}
    if os.path.exists(REPORT):
        with open(REPORT, encoding="utf-8") as f: report = json.load(f)

    def ref_cell(sid):
        rec = report.get("main", {}).get(sid)
        if not rec: return "—"
        if rec.get("captureError"): return "hata: " + rec["captureError"][:60]
        out = "✓"
        if rec.get("routerError"): out += " (yol: " + rec["routerError"][:50] + ")"
        rep = report.get("repeat", {}).get(sid)
        if rep and rep.get("diffPixels") is not None: out += f" · tekrar Δ{rep['diffPixels']}px"
        return out

    counts = {k: 0 for k in ORDER}
    rows = []
    for s in states:
        st = status["states"].get(s["id"], {})
        durum = st.get("durum") or ("kaldırıldı" if s.get("removed") else "bekliyor")
        counts[durum] = counts.get(durum, 0) + 1
        rows.append(f"| {s['id']} | {s['group']} | {cell(s['title'])} | {'—' if s.get('removed') else ref_cell(s['id'])} | "
                    f"{cell(st.get('unity'))} | {durum} | {cell(st.get('not') or (s.get('note') if s.get('removed') else ''))} |")
    active = sum(1 for s in states if not s.get("removed"))
    lines = [
        "# v4 · durum tablosu", "",
        "Üretildi: `python tools/v4/states_table.py` (girdi: `states.json`, `artifacts/v4/ref/capture_report.json`, elle tutulan `docs/v4/durumlar.json`). Bu dosyayı elle düzenleme.", "",
        f"Durum kataloğu: {len(states)} kimlik, {active} etkin, {len(states) - active} kaldırılmış.", "",
        "| Durum | Sayı |", "|---|---|"] + [f"| {k} | {counts.get(k, 0)} |" for k in ORDER] + ["",
        "## Eşikler ve yöntem", "", status.get("thresholds") or "(henüz yazılmadı)", "",
        "## Tablo", "",
        "Ref: `artifacts/v4/ref/<ID>.png` + `.layout.json` (✓ çekildi; `tekrar Δ` = aynı adresin ikinci açılışıyla farklı piksel sayısı, EK-001).", "",
        "| Kimlik | Grup | Başlık | Ref | Unity karşılığı | Durum | Not |", "|---|---|---|---|---|---|---|"] + rows + [""]
    with open(OUT, "w", encoding="utf-8", newline="\n") as f: f.write("\n".join(lines))
    print("wrote", OUT, counts)


if __name__ == "__main__":
    sys.exit(main())
