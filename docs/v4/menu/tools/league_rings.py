"""League frame rings (G101Ring, g101-rank.js ring()) rasterised for the menu's avatars.

The package ships the league emblems and medals as SVG/PNG (lane D, Res/Rank), but not the avatar frame ring that
G101Ring draws at run time. This script runs the prototype's own ring() (node), pads the viewBox so the ring's halo
(r 99 × 1.12, drawn outside the 200-unit box) is kept, and rasterises it with resvg at 2× for every size the menu uses:
40 (edit-profile chips), 44 (Profilim tiles), 62 (top bar, Hesap card), 104 (share card), 110 (edit-profile preview),
124 (Profilim). Output: Assets/Galaxy101/Resources/Galaxy101/UI/Rank/ring_<league>_<size>@2x.png + .meta; the image
is (size + 2·pad) CSS px, pad = ceil(size · 10.88 / 200), placed at -pad (MenuView.LeagueRing).

    uv run --no-project --with resvg-py python docs/v4/menu/tools/league_rings.py
"""
import json, math, subprocess, uuid
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
PROTO = Path(r"C:/GD/Okey 101/Design/Galaxy101_ClaudeCode/prototip/g101-rank.js")
OUT = REPO / "Assets/Galaxy101/Resources/Galaxy101/UI/Rank"
META = REPO / "Assets/Galaxy101/Resources/Galaxy101/Rank/gold_II_24.png.meta"
FOLDER_META = REPO / "Assets/Galaxy101/Resources/Galaxy101/Rank.meta"
LEAGUES = ["bronze", "silver", "gold", "diamond", "master", "galaxy"]
SIZES = [40, 44, 62, 104, 110, 124]
HALO = 99 * 1.12 - 100  # units the halo reaches past the 200-unit box

JS = r"""
const fs=require('fs');global.window={};global.React=null;eval(fs.readFileSync(process.argv[1],"utf8"));
const out={};for(const k of JSON.parse(process.argv[2]))out[k]=window.G101Rank.ring(k,200);process.stdout.write(JSON.stringify(out));
"""


def main():
    svgs = json.loads(subprocess.run(["node", "-e", JS, str(PROTO), json.dumps(LEAGUES)], capture_output=True, text=True, check=True, encoding="utf-8").stdout)
    import resvg_py
    OUT.mkdir(parents=True, exist_ok=True)
    folder_meta = OUT.parent / "Rank.meta"
    if not folder_meta.exists():
        folder_meta.write_text(FOLDER_META.read_text(encoding="utf-8").replace(FOLDER_META.read_text(encoding="utf-8").split("guid: ")[1].split("\n")[0], uuid.uuid4().hex), encoding="utf-8")
    meta_template = META.read_text(encoding="utf-8")
    old_guid = meta_template.split("guid: ")[1].split("\n")[0]
    pads = {}
    for size in SIZES:
        pad = math.ceil(size * HALO / 200)  # CSS px around the ring box
        pads[str(size)] = pad
        units = pad * 200 / size
        for key in LEAGUES:
            svg = svgs[key]
            svg = svg.replace('width="200" height="200" viewBox="0 0 200 200"', f'width="{200 + 2 * units:.4f}" height="{200 + 2 * units:.4f}" viewBox="{-units:.4f} {-units:.4f} {200 + 2 * units:.4f} {200 + 2 * units:.4f}"', 1)
            assert 'viewBox="-' in svg, key
            px = (size + 2 * pad) * 2
            data = bytes(resvg_py.svg_to_bytes(svg_string=svg, width=px, height=px, skip_system_fonts=True))
            name = f"ring_{key}_{size}@2x.png"
            (OUT / name).write_bytes(data)
            meta = OUT / (name + ".meta")
            if not meta.exists(): meta.write_text(meta_template.replace(old_guid, uuid.uuid4().hex), encoding="utf-8")
    (OUT / "ring_pad.json").write_text(json.dumps({"halo": "g101-rank.js ring(): circle r 99 scaled 1.12", "padCssPx": pads}, indent=1), encoding="utf-8")
    jm = OUT / "ring_pad.json.meta"
    if not jm.exists():
        jm.write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    print("rings:", len(SIZES) * len(LEAGUES), "pads:", pads)


if __name__ == "__main__":
    main()
