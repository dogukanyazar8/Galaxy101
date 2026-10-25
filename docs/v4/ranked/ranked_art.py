"""Rasterise the Dereceli screen's two vector pieces that UI Toolkit's GalaxyIcon cannot draw (gradients, <defs>, <g>):

  * the header badge (G101-Dereceli.dc.html:36-58, the 44 px laurel/tile SVG = package icon `icon-50`), and
  * the season reward frames (g101-rank.js ring(key, 64) — G101FramedAvatar's notched metal ring), one per league.

Both come straight from the prototype's own source (no redrawing): the badge from the package's `icons/icon-50.svg`, the
rings by running g101-rank.js in node. Output: @2x PNGs in Assets/Galaxy101/Scripts/Screens/Ranked/Resources/Galaxy101/Ranked
(loaded with Resources.Load). The ring keeps its halo (r 99 × 1.12, overflow: visible) by rendering the view box
-12.5 … 212.5: a 64 px ring is a 72 px image placed at -4 px.

    uv run --no-project --with resvg-py python docs/v4/ranked/ranked_art.py
"""
import json, os, re, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
PROTO = r"C:/GD/Okey 101/Design/Galaxy101_ClaudeCode/prototip"
OUT = os.path.join(REPO, "Assets", "Galaxy101", "Scripts", "Screens", "Ranked", "Resources", "Galaxy101", "Ranked")
FONTS = [os.path.join(PROTO, "galaxy101_handoff_v4", "assets", "fonts", f) for f in ("Montserrat-ExtraBold.ttf", "Montserrat-Black.ttf")]
META = os.path.join(REPO, "Assets", "Galaxy101", "Resources", "Galaxy101", "Rank", "gold_II_160@2x.png.meta")
LEAGUES = ["bronze", "silver", "gold", "diamond", "master", "galaxy"]

NODE = r"""
const fs = require('fs'), vm = require('vm');
const ctx = { window: {} }; vm.createContext(ctx);
vm.runInContext(fs.readFileSync(process.argv[2], 'utf8'), ctx);
const out = {}; for (const k of JSON.parse(process.argv[3])) out[k] = ctx.window.G101Rank.ring(k, 64);
process.stdout.write(JSON.stringify(out));
"""


def strip_metadata(svg):
    return re.sub(r"<metadata>.*?</metadata>", "", svg, flags=re.S).replace(' xmlns:c2pa="http://c2pa.org/manifest"', "")


def rasterize(svg, size):
    import resvg_py
    return bytes(resvg_py.svg_to_bytes(svg_string=svg, width=size, height=size, skip_system_fonts=True, font_files=FONTS, font_family="Montserrat"))


def write(name, data):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    with open(path, "wb") as f: f.write(data)
    if not os.path.exists(path + ".meta"):
        import uuid
        meta = open(META, encoding="utf-8").read()
        meta = re.sub(r"guid: [0-9a-f]+", "guid: " + uuid.uuid4().hex, meta, count=1)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f: f.write(meta)
    print("wrote", path, len(data))


def main():
    badge = strip_metadata(open(os.path.join(PROTO, "galaxy101_handoff_v4", "assets", "icons", "icon-50.svg"), encoding="utf-8").read())
    write("badge@2x.png", rasterize(badge, 88))  # 44 px in the 52 px tile (G101-Dereceli.dc.html:36)
    with tempfile.NamedTemporaryFile("w", suffix=".js", delete=False, encoding="utf-8") as js: js.write(NODE)
    try:
        rings = json.loads(subprocess.check_output(["node", js.name, os.path.join(PROTO, "g101-rank.js"), json.dumps(LEAGUES)], text=True, encoding="utf-8"))
    finally: os.unlink(js.name)
    for key, svg in rings.items():
        svg = svg.replace('width="64" height="64" viewBox="0 0 200 200"', 'width="144" height="144" viewBox="-12.5 -12.5 225 225"', 1)
        svg = re.sub(r' style="[^"]*"', "", svg, count=1)
        write("frame_" + key + "_64@2x.png", rasterize(svg, 144))


if __name__ == "__main__":
    sys.exit(main())
