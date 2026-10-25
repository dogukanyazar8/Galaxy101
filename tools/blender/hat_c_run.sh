#!/usr/bin/env sh
# Galaxy 101 · v4 Hat C: regenerate every report in docs/v4/hat-c from the GLBs (no Unity, no Blender needed).
# Run from the repository root:  sh tools/blender/hat_c_run.sh
set -e
NEW=Design/Galaxy101_ClaudeCode/galaxy101_3d_tum_temalar
PKG=Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4
OUT=docs/v4/hat-c
TMP=${TMPDIR:-/tmp}/g101_hat_c
mkdir -p "$OUT" "$TMP"
export PYTHONIOENCODING=utf-8
uv run --with numpy python tools/blender/gltf_inventory.py "$NEW" "$OUT/glb_inventory.json" --label v4_tum_temalar
uv run --with numpy python tools/blender/gltf_inventory.py Assets/Galaxy101/Table/Models "$OUT/glb_inventory_proje.json" --label proje_Models --flat
uv run --with numpy python tools/blender/gltf_inventory.py Design/old/assets_src/glb "$TMP/old_raw.json" --label old_assets_src_glb
python tools/blender/gltf_compare.py "$OUT/glb_inventory.json" "$OUT/glb_inventory_proje.json" "$TMP/old_raw.json" "$OUT/compare.json"
python tools/blender/material_light_map.py "$OUT/glb_inventory.json" "$OUT/glb_inventory_proje.json" "$PKG" "$OUT"
uv run --with numpy python tools/blender/unity_contract_check.py "$OUT/glb_inventory.json" "$NEW" "$OUT/unity_contract.json"
python tools/blender/place_v4_models.py "$NEW" . "$OUT/placement.json"
