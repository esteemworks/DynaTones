"""Builds src/DynaTones/Resources/worldmap.gz, the land and the borders between countries for the small map under
Settings > Location.

From Natural Earth 1:50m, which is in the public domain (https://www.naturalearthdata.com): ne_50m_land and
ne_50m_admin_0_boundary_lines_land, as shapefiles. Download and unpack them yourself, then:

    python -I tools/map.py <folder with ne_50m_land.shp> <folder with ne_50m_admin_0_boundary_lines_land.shp>

Format, little endian: a list of lines, each a byte for the kind (1 the coast of a piece of land, 2 a border), an
unsigned 32 bit count, then that many points as two signed 16 bit numbers, longitude and latitude in hundredths of a
degree (about a kilometre). Points closer than that to the one before are left out.
"""
import gzip
import pathlib
import struct
import sys


def shapes(path):
    data = pathlib.Path(path).read_bytes()
    at = 100
    while at + 8 <= len(data):
        _, length = struct.unpack(">ii", data[at:at + 8])
        content = data[at + 8:at + 8 + length * 2]
        at += 8 + length * 2
        kind = struct.unpack("<i", content[:4])[0]
        if kind not in (3, 5):
            continue
        num_parts, num_points = struct.unpack("<ii", content[36:44])
        parts = list(struct.unpack(f"<{num_parts}i", content[44:44 + 4 * num_parts])) + [num_points]
        base = 44 + 4 * num_parts
        points = [struct.unpack("<dd", content[base + 16 * i:base + 16 * i + 16]) for i in range(num_points)]
        for a, b in zip(parts, parts[1:]):
            yield points[a:b]


def pack(kind, line):
    out = []
    for lon, lat in line:
        p = (round(lon * 100), round(lat * 100))
        if not out or out[-1] != p:
            out.append(p)
    if len(out) < 2:
        return b""
    return struct.pack("<BI", kind, len(out)) + b"".join(struct.pack("<hh", *p) for p in out)


land = pathlib.Path(sys.argv[1]) / "ne_50m_land.shp"
borders = pathlib.Path(sys.argv[2]) / "ne_50m_admin_0_boundary_lines_land.shp"
blob = b"".join(pack(1, line) for line in shapes(land)) + b"".join(pack(2, line) for line in shapes(borders))
target = pathlib.Path(__file__).resolve().parent.parent / "src" / "DynaTones" / "Resources" / "worldmap.gz"
target.write_bytes(gzip.compress(blob, 9))
print(f"{len(blob) // 1024} KB, packed {target.stat().st_size // 1024} KB: {target}")
