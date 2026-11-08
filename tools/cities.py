"""Builds src/DynaTones/Resources/cities.tsv.gz, the places DynaTones names a position by ("Near Bielefeld") and
offers under Settings > Location > Fixed place.

From GeoNames cities15000 (every place with at least 15,000 inhabitants), licensed CC BY 4.0:
https://download.geonames.org/export/dump/cities15000.zip. Download it yourself, then:

    python -I tools/cities.py <path to cities15000.zip>

Kept per place: name, country (ISO 3166), latitude, longitude with three decimals (about 100 m), inhabitants. Parts
of a city (feature code PPLX, such as "Mitte") are left out: "Near Mitte" says nothing. Sorted by inhabitants, so of
two places at the same distance the larger one wins.
"""
import gzip
import io
import pathlib
import sys
import zipfile

source = pathlib.Path(sys.argv[1])
target = pathlib.Path(__file__).resolve().parent.parent / "src" / "DynaTones" / "Resources" / "cities.tsv.gz"

rows = []
with zipfile.ZipFile(source) as z:
    with z.open("cities15000.txt") as f:
        for line in io.TextIOWrapper(f, encoding="utf-8"):
            c = line.rstrip("\n").split("\t")
            if len(c) < 15 or c[7] == "PPLX":
                continue
            name = c[1].replace("\t", " ").strip()
            if not name:
                continue
            rows.append((name, c[8], float(c[4]), float(c[5]), int(c[14] or 0)))

rows.sort(key=lambda r: -r[4])
text = "# Places with at least 15,000 inhabitants: name, country, latitude, longitude, inhabitants.\n" \
       "# From GeoNames (https://www.geonames.org), licensed CC BY 4.0. Built by tools/cities.py.\n" + \
       "".join(f"{n}\t{cc}\t{lat:.3f}\t{lon:.3f}\t{pop}\n" for n, cc, lat, lon, pop in rows)
with gzip.open(target, "wt", encoding="utf-8", compresslevel=9) as out:
    out.write(text)
print(f"{len(rows)} places, {target.stat().st_size // 1024} KB: {target}")
