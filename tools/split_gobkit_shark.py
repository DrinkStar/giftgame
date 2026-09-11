"""Split Gobkit Free Animal Pack Shark.glb master timeline into named clips."""
from __future__ import annotations

import copy
import json
import struct
from pathlib import Path

SRC = Path(__file__).resolve().parents[1] / "assets/models/enemies/shark_king/SharkKing_gobkit_raw.glb"
DST = Path(__file__).resolve().parents[1] / "assets/models/enemies/shark_king/Shark.glb"
FPS = 24.0
# Gobkit docs: idle 0-29, attack 30-59, dead 60-89, walk 90-119 @ 24fps
CLIPS = [
    ("Idle", 0, 29),
    ("Attack", 30, 59),
    ("Death", 60, 89),
    ("Walk", 90, 119),
]


def read_glb(path: Path):
    data = path.read_bytes()
    assert data[:4] == b"glTF"
    json_len = struct.unpack_from("<I", data, 12)[0]
    json_start = 20
    gltf = json.loads(data[json_start : json_start + json_len])
    # Next chunk header: uint32 length + 4-byte type (BIN\0), then payload.
    bin_hdr = json_start + json_len
    assert data[bin_hdr + 4 : bin_hdr + 8] == b"BIN\x00"
    bin_len = struct.unpack_from("<I", data, bin_hdr)[0]
    blob = bytearray(data[bin_hdr + 8 : bin_hdr + 8 + bin_len])
    return gltf, blob


def write_glb(path: Path, gltf: dict, blob: bytearray) -> None:
    j = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    while len(j) % 4:
        j += b" "
    b = bytes(blob)
    while len(b) % 4:
        b += b"\x00"
    total = 12 + 8 + len(j) + 8 + len(b)
    out = bytearray()
    out += b"glTF"
    out += struct.pack("<II", 2, total)
    out += struct.pack("<I", len(j)) + b"JSON" + j
    out += struct.pack("<I", len(b)) + b"BIN\x00" + b
    path.write_bytes(out)


def accessor_component_size(comp: int) -> int:
    return {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}[comp]


def accessor_type_count(t: str) -> int:
    return {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[t]


def read_accessor(gltf: dict, blob: bytearray, acc_i: int):
    acc = gltf["accessors"][acc_i]
    bv = gltf["bufferViews"][acc["bufferView"]]
    offset = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = bv.get("byteStride")
    n = acc["count"]
    ctype = acc["componentType"]
    tcount = accessor_type_count(acc["type"])
    csize = accessor_component_size(ctype)
    elem = tcount * csize
    if stride is None:
        stride = elem
    fmt = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}[ctype]
    vals = []
    for i in range(n):
        o = offset + i * stride
        comps = struct.unpack_from("<" + fmt * tcount, blob, o)
        vals.append(comps if tcount > 1 else comps[0])
    return vals, acc


def append_accessor(gltf: dict, blob: bytearray, values, template_acc: dict, force_scalar_times=False):
    acc = {
        k: v
        for k, v in template_acc.items()
        if k in ("componentType", "type", "normalized")
    }
    if force_scalar_times:
        acc["componentType"] = 5126
        acc["type"] = "SCALAR"
    tcount = accessor_type_count(acc["type"])
    fmt = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}[acc["componentType"]]
    raw = bytearray()
    for v in values:
        if tcount == 1:
            raw += struct.pack("<" + fmt, float(v) if fmt == "f" else v)
        else:
            raw += struct.pack(
                "<" + fmt * tcount,
                *[float(x) if fmt == "f" else x for x in v],
            )
    while len(blob) % 4:
        blob.append(0)
    bv_i = len(gltf["bufferViews"])
    gltf["bufferViews"].append(
        {"buffer": 0, "byteOffset": len(blob), "byteLength": len(raw)}
    )
    blob.extend(raw)
    acc["bufferView"] = bv_i
    acc["byteOffset"] = 0
    acc["count"] = len(values)
    if acc["type"] == "SCALAR" and values:
        acc["min"] = [float(min(values))]
        acc["max"] = [float(max(values))]
    elif acc["type"] == "VEC3" and values:
        xs = [v[0] for v in values]
        ys = [v[1] for v in values]
        zs = [v[2] for v in values]
        acc["min"] = [min(xs), min(ys), min(zs)]
        acc["max"] = [max(xs), max(ys), max(zs)]
    elif acc["type"] == "VEC4" and values:
        comps = list(zip(*values))
        acc["min"] = [min(c) for c in comps]
        acc["max"] = [max(c) for c in comps]
    ai = len(gltf["accessors"])
    gltf["accessors"].append(acc)
    return ai


def main() -> None:
    gltf, blob = read_glb(SRC)
    assert len(gltf["animations"]) == 1
    master = gltf["animations"][0]
    new_anims = []
    for name, f0, f1 in CLIPS:
        t0 = f0 / FPS
        t1 = f1 / FPS
        samplers = []
        channels = []
        for ch in master["channels"]:
            samp = master["samplers"][ch["sampler"]]
            times, tacc = read_accessor(gltf, blob, samp["input"])
            outs, oacc = read_accessor(gltf, blob, samp["output"])
            idxs = [i for i, t in enumerate(times) if t0 - 1e-6 <= t <= t1 + 1e-6]
            if not idxs:
                continue
            st = idxs[0]
            en = idxs[-1]
            if st > 0 and times[st] > t0 + 1e-6:
                st -= 1
            if en < len(times) - 1 and times[en] < t1 - 1e-6:
                en += 1
            slice_t = [times[i] - t0 for i in range(st, en + 1)]
            if slice_t:
                slice_t[0] = 0.0
                slice_t[-1] = max(slice_t[-1], t1 - t0)
            slice_o = [outs[i] for i in range(st, en + 1)]
            in_i = append_accessor(gltf, blob, slice_t, tacc, force_scalar_times=True)
            out_i = append_accessor(gltf, blob, slice_o, oacc)
            si = len(samplers)
            samplers.append(
                {
                    "input": in_i,
                    "output": out_i,
                    "interpolation": samp.get("interpolation", "LINEAR"),
                }
            )
            channels.append({"sampler": si, "target": copy.deepcopy(ch["target"])})
        new_anims.append({"name": name, "samplers": samplers, "channels": channels})
        print(name, "channels", len(channels), "dur", f"{(f1 - f0) / FPS:.3f}s")

    gltf["animations"] = new_anims
    gltf["buffers"][0]["byteLength"] = len(blob)
    write_glb(DST, gltf, blob)
    print("wrote", DST, "size", DST.stat().st_size)
    g2, _ = read_glb(DST)
    print("verify", [a["name"] for a in g2["animations"]])


if __name__ == "__main__":
    main()
