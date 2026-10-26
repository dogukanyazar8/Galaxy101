"""Copy the prototype's two embedded 128 px tile badges verbatim; no raster redraw.

Source: table3d.js:242-261, exported in the Saturn markers GLB as
mesh/material 11 (simge_side, texture 3) and 12 (simge_islek, texture 4).
Run from any directory: python tools/v4/extract_tile_tags.py
"""
from hashlib import sha256
import json
from pathlib import Path
import struct


REPO = Path(__file__).resolve().parents[2]
SOURCE = REPO / "Assets/Galaxy101/Table/Models/v4/saturn/galaxy101_isaretler_saturn.glb"
DESTINATION = REPO / "Assets/Galaxy101/Resources/Galaxy101/Table/TileTags"


def extract():
    data = SOURCE.read_bytes()
    magic, version, length = struct.unpack_from("<4sII", data)
    if magic != b"glTF" or version != 2 or length != len(data):
        raise ValueError("Expected a complete glTF 2.0 binary: " + str(SOURCE))
    chunks = {}
    offset = 12
    while offset < len(data):
        size, kind = struct.unpack_from("<I4s", data, offset)
        start = offset + 8
        if start + size > len(data) or kind in chunks:
            raise ValueError("Invalid or duplicate GLB chunk")
        chunks[kind] = data[start:start + size]
        offset = start + size
    gltf = json.loads(chunks[b"JSON"])
    binary = chunks[b"BIN\0"]
    images = {}
    for name, mesh_index, texture_index in (("side", 11, 3), ("islek", 12, 4)):
        primitive, = gltf["meshes"][mesh_index]["primitives"]
        material = gltf["materials"][primitive["material"]]
        if primitive["material"] != mesh_index or material["name"] != "simge_" + name:
            raise ValueError("The exported badge mesh/material contract changed: " + name)
        if material["pbrMetallicRoughness"]["baseColorTexture"]["index"] != texture_index:
            raise ValueError("The exported badge texture contract changed: " + name)
        image = gltf["images"][gltf["textures"][texture_index]["source"]]
        view = gltf["bufferViews"][image["bufferView"]]
        if image["mimeType"] != "image/png" or view["buffer"] != 0:
            raise ValueError("Expected an embedded PNG in buffer 0: " + name)
        start = view.get("byteOffset", 0)
        end = start + view["byteLength"]
        if start < 0 or end > len(binary):
            raise ValueError("Badge image exceeds the binary buffer: " + name)
        png = binary[start:end]
        if png[:8] != b"\x89PNG\r\n\x1a\n" or png[12:16] != b"IHDR" or struct.unpack_from(">II", png, 16) != (128, 128):
            raise ValueError("Expected a 128 x 128 PNG: " + name)
        images[name] = png
    DESTINATION.mkdir(parents=True, exist_ok=True)
    for name, png in images.items():
        (DESTINATION / (name + ".png")).write_bytes(png)
        print(f"{name}: {len(png)} bytes, sha256 {sha256(png).hexdigest()}")


if __name__ == "__main__":
    extract()
