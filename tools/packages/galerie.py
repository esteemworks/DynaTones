"""Builds the files of the DynaTones gallery from galerie.json: one .zip per package with its pictures and a
dynatones.json (names, description, anchors), one preview .jpg, and packages.json, the list the app reads.

    python galerie.py <out folder> --bilder <day series root> --farbe <color packages root>

The out folder is then uploaded as the release "packages" (a pre-release, so the app's update check never takes it
for a version), or put on a share for an organization's own gallery.
"""
import argparse
import hashlib
import json
import os
import zipfile

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))

# Where in the day each picture of a day series starts: the sun's elevation and whether it is rising.
# The night runs from the end of the evening blue hour to the morning one; noon from high in the morning to the
# afternoon. Above 10 degrees the elevations are on DynaTones' reference arc, whose top (60) is the sun at noon.
TAGESSERIE = {
    "1-night": (-10, False),
    "2-blue-hour-morning": (-9, True),
    "3-sunrise": (-2, True),
    "4-morning": (7, True),
    "5-noon": (45, True),
    "6-afternoon": (40, False),
    "7-golden-hour": (10, False),
    "8-sunset": (3, False),
    "9-blue-hour-evening": (-3, False),
}

# Lakeside was drawn for these elevations.
LAKESIDE = {
    "1-night": (-30, False),
    "2-blue-hour": (-6, True),
    "3-sunrise": (0, True),
    "4-morning": (12, True),
    "5-noon": (25, True),
    "6-afternoon": (14, False),
    "7-sunset": (0, False),
    "8-dusk": (-6, False),
}

# Names of the pictures in English and German, by file name.
TITLES = {
    "1-night": ("Night", "Nacht"),
    "2-blue-hour-morning": ("Blue hour", "Blaue Stunde"),
    "2-blue-hour": ("Blue hour", "Blaue Stunde"),
    "3-sunrise": ("Sunrise", "Sonnenaufgang"),
    "4-morning": ("Morning", "Morgen"),
    "5-noon": ("Noon", "Mittag"),
    "6-afternoon": ("Afternoon", "Nachmittag"),
    "7-golden-hour": ("Golden hour", "Goldene Stunde"),
    "7-sunset": ("Sunset", "Sonnenuntergang"),
    "8-sunset": ("Sunset", "Sonnenuntergang"),
    "8-dusk": ("Dusk", "Dämmerung"),
    "9-blue-hour-evening": ("Evening blue hour", "Abendliche blaue Stunde"),
}

AI_CREDITS = ("Made with AI by EsteemWorks: SDXL 1.0 with ControlNet Depth, enlarged with Real-ESRGAN",
              "Mit KI erstellt von EsteemWorks: SDXL 1.0 mit ControlNet Depth, vergrößert mit Real-ESRGAN")
DRAWN_CREDITS = ("Drawn by EsteemWorks", "Gezeichnet von EsteemWorks")


def source_folder(spec, args):
    kind, path = spec["source"].split(":", 1)
    root = {"bilder": args.bilder, "farbe": args.farbe, "repo": REPO}[kind]
    return os.path.join(root, path)


def manifest_for(spec, folder):
    if spec["anchors"] == "manifest":
        with open(os.path.join(folder, "dynatones.json"), encoding="utf-8") as f:
            manifest = json.load(f)
        files = [i["file"] for i in manifest["images"]]
    else:
        table = TAGESSERIE if spec["anchors"] == "tagesserie" else LAKESIDE
        files = sorted(f for f in os.listdir(folder) if f.endswith(".jpg") and os.path.splitext(f)[0] in table)
        if len(files) != len(table):
            raise SystemExit(f"{spec['id']}: {len(files)} pictures found in {folder}, {len(table)} expected")
        images = []
        for f in files:
            elevation, rising = table[os.path.splitext(f)[0]]
            title, title_de = TITLES[os.path.splitext(f)[0]]
            images.append({"file": f, "title": title, "titleDe": title_de,
                           "anchor": {"mode": "sun", "elevation": elevation, "rising": rising}})
        manifest = {
            "name": spec["name"], "nameDe": spec["nameDe"],
            "description": spec["description"], "descriptionDe": spec["descriptionDe"],
            "credits": (AI_CREDITS if spec["aiMade"] else DRAWN_CREDITS)[0],
            "creditsDe": (AI_CREDITS if spec["aiMade"] else DRAWN_CREDITS)[1],
            "aiMade": spec["aiMade"], "galleryId": spec["id"], "mirrorEvening": False,
            "images": images,
        }
    return manifest, files


def preview(folder, files, target):
    """The day in one picture: a slice of every picture side by side, from night to night."""
    w, h = 960, 540
    out = Image.new("RGB", (w, h))
    n = len(files)
    for k, f in enumerate(files):
        img = Image.open(os.path.join(folder, f)).convert("RGB").resize((w, h), Image.LANCZOS)
        x0, x1 = round(k * w / n), round((k + 1) * w / n)
        out.paste(img.crop((x0, 0, x1, h)), (x0, 0))
    out.save(target, quality=88, optimize=True)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    p = argparse.ArgumentParser()
    p.add_argument("out")
    p.add_argument("--bilder", required=True)
    p.add_argument("--farbe", required=True)
    p.add_argument("--nur", help="only these ids, comma-separated (e.g. lakeside for the setup)")
    args = p.parse_args()
    with open(os.path.join(HERE, "galerie.json"), encoding="utf-8") as f:
        specs = json.load(f)["packages"]
    if args.nur:
        wanted = set(args.nur.split(","))
        specs = [s for s in specs if s["id"] in wanted]
    os.makedirs(args.out, exist_ok=True)

    entries = []
    for spec in specs:
        folder = source_folder(spec, args)
        manifest, files = manifest_for(spec, folder)
        zip_name, preview_name = spec["id"] + ".zip", spec["id"] + ".jpg"
        zip_path = os.path.join(args.out, zip_name)
        # Pictures are JPEGs already; stored, not compressed again.
        with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_STORED) as z:
            for f in files:
                z.write(os.path.join(folder, f), f)
            z.writestr("dynatones.json", json.dumps(manifest, ensure_ascii=False, indent=2))
        preview(folder, files, os.path.join(args.out, preview_name))
        entries.append({
            "id": spec["id"], "name": manifest["name"], "nameDe": manifest["nameDe"],
            "description": manifest["description"], "descriptionDe": manifest["descriptionDe"],
            "style": spec["style"], "aiMade": manifest["aiMade"], "images": len(files),
            "size": os.path.getsize(zip_path), "sha256": sha256(zip_path), "file": zip_name, "preview": preview_name,
        })
        print(f"{spec['id']:12} {len(files):2} pictures {os.path.getsize(zip_path) / 1048576:6.1f} MB")

    with open(os.path.join(args.out, "packages.json"), "w", encoding="utf-8") as f:
        json.dump({"format": 1, "packages": entries}, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
