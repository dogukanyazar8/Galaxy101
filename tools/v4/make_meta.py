"""Give every file and folder under the given Unity paths a .meta (fileFormatVersion 2 + a fresh guid) if it lacks one.

    python tools/v4/make_meta.py Assets/Galaxy101/Scripts/Diagnostics Assets/Galaxy101/Diagnostics
"""
import os, sys, uuid


def ensure(path):
    meta = path + ".meta"
    if os.path.exists(meta) or os.path.basename(path).endswith(".meta"): return False
    lines = ["fileFormatVersion: 2", "guid: " + uuid.uuid4().hex]
    if os.path.isdir(path): lines += ["folderAsset: yes", "DefaultImporter:", "  externalObjects: {}", "  userData: ", "  assetBundleName: ", "  assetBundleVariant: "]
    with open(meta, "w", encoding="utf-8", newline="\n") as f: f.write("\n".join(lines) + "\n")
    print("meta", meta)
    return True


for top in sys.argv[1:]:
    top = os.path.abspath(top)
    parts = top.split(os.sep)
    # Parents inside Assets/ or Packages/<pkg>/ need metas too.
    for i in range(len(parts) - 1, 0, -1):
        if parts[i - 1] in ("Assets",) or (i >= 2 and parts[i - 2] == "Packages"):
            for j in range(i + 1, len(parts) + 1): ensure(os.sep.join(parts[:j]))
            break
    if os.path.isdir(top):
        for root, dirs, files in os.walk(top):
            for d in dirs: ensure(os.path.join(root, d))
            for f in files: ensure(os.path.join(root, f))
    else: ensure(top)
