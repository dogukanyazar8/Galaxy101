"""Galaxy 101 · v4 Hat C: copy the 103 v4 GLBs into the Unity project with .meta files.

  <src>/<theme>/*.glb  -> Assets/Galaxy101/Table/Models/v4/<theme>/*.glb
  <src>/*.glb (shared) -> Assets/Galaxy101/Table/Models/v4/shared/*.glb

The GLBs are copied byte for byte (no Blender pass). Every new file and folder gets a .meta: GLBs reuse the glTFast
importer block of an existing project GLB .meta (Assets/Galaxy101/Table/Models/galaxy101_zar.glb.meta, report items
cleared: Unity rewrites them on import), folders the DefaultImporter block of Models.meta. GUIDs are new random 32-hex
values, checked against every .meta in the repository; an existing .meta keeps its GUID (re-runs are idempotent).
The old Models/*.glb stay where they are: the Unity phase switches references.

Usage:
  python tools/blender/place_v4_models.py <src dir> <repo root> <manifest.json>
"""
import hashlib
import json
import os
import re
import shutil
import sys
import uuid

TEMPLATE_GLB_META = "Assets/Galaxy101/Table/Models/galaxy101_zar.glb.meta"
TEMPLATE_DIR_META = "Assets/Galaxy101/Table/Models.meta"
DEST = "Assets/Galaxy101/Table/Models/v4"


def all_guids(repo):
    guids = set()
    for top in ("Assets", "Packages"):
        for dirpath, _, files in os.walk(os.path.join(repo, top)):
            for f in files:
                if f.endswith(".meta"):
                    m = re.search(r"^guid: ([0-9a-f]{32})", open(os.path.join(dirpath, f), encoding="utf-8", errors="replace").read(), re.M)
                    if m:
                        guids.add(m.group(1))
    return guids


def new_guid(taken):
    while True:
        g = uuid.uuid4().hex
        if g not in taken:
            taken.add(g); return g


def template(repo, rel):
    text = open(os.path.join(repo, rel), encoding="utf-8").read().replace("\r\n", "\n")
    body = text.split("\n", 2)[2]  # drop "fileFormatVersion" and "guid" lines
    # Import reports belong to the imported file; Unity regenerates them.
    body = re.sub(r"  reportItems:\n(    .*\n|  - .*\n)*", "  reportItems: []\n", body)
    return body


def write_meta(path, body, taken):
    meta = path + ".meta"
    if os.path.exists(meta):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(meta, encoding="utf-8").read(), re.M)
        if m:
            return m.group(1), False
    g = new_guid(taken)
    with open(meta, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f"fileFormatVersion: 2\nguid: {g}\n{body}")
    return g, True


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    src, repo, manifest = sys.argv[1:4]
    glb_body, dir_body = template(repo, TEMPLATE_GLB_META), template(repo, TEMPLATE_DIR_META)
    taken = all_guids(repo)
    entries, created = [], 0
    dest_root = os.path.join(repo, DEST)
    os.makedirs(dest_root, exist_ok=True)
    g, c = write_meta(dest_root, dir_body, taken); created += c
    folders = {DEST: g}
    for dirpath, _, files in os.walk(src):
        rel_dir = os.path.relpath(dirpath, src).replace("\\", "/")
        folder = "shared" if rel_dir == "." else rel_dir
        for f in sorted(files):
            if not f.lower().endswith(".glb"):
                continue
            out_dir = os.path.join(dest_root, folder)
            if folder not in folders:
                os.makedirs(out_dir, exist_ok=True)
                folders[f"{DEST}/{folder}"], c = write_meta(out_dir, dir_body, taken); created += c
            s, d = os.path.join(dirpath, f), os.path.join(out_dir, f)
            if not os.path.exists(d) or sha256(d) != sha256(s):
                shutil.copyfile(s, d)
            guid, c = write_meta(d, glb_body, taken); created += c
            digest = sha256(d)
            assert digest == sha256(s), d
            entries.append({"source": os.path.relpath(s, repo).replace("\\", "/"), "dest": f"{DEST}/{folder}/{f}", "guid": guid,
                            "bytes": os.path.getsize(d), "sha256": digest})
    entries.sort(key=lambda e: e["dest"])
    doc = {"tool": "tools/blender/place_v4_models.py", "glb_meta_template": TEMPLATE_GLB_META, "folder_meta_template": TEMPLATE_DIR_META,
           "folders": dict(sorted(folders.items())), "files": entries}
    with open(manifest, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, ensure_ascii=False, indent=1)
    print("PLACED", len(entries), "files,", len(folders), "folders, new metas", created)


if __name__ == "__main__":
    main()
