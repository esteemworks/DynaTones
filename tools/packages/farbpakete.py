"""Draws the three color packages of DynaTones: twelve pictures each, one every two hours, no AI model involved.

Every picture of a package has the same shapes, a sky of soft light and three waves, so the flowing transition
only shifts the colors. The colors follow the day: darker and deeper at night, lightest at noon.

    python farbpakete.py <out folder>

Writes <out>/<id>/01-0000.jpg ... 12-2200.jpg at 3840 x 2160 and a dynatones.json with time anchors.
"""
import colorsys
import json
import math
import os
import sys

import numpy as np
from PIL import Image

W, H = 3840, 2160
# Drawn small and enlarged: the pictures are soft gradients, they lose nothing by it.
SW, SH = 960, 540

PACKAGES = {
    "glut": {
        "name": "Ember", "nameDe": "Glut",
        "description": "Waves in the family's warm colors: wine red at night, orange in the morning, gold at noon.",
        "descriptionDe": "Wellen in den warmen Farben der Familie: Weinrot in der Nacht, Orange am Morgen, Gold am Mittag.",
        # Hue at midnight and at noon, saturation, lightness of the sky at night and at noon.
        "hue": (345, 42), "sat": 0.82, "light": (0.20, 0.62), "waveHue": -14, "waveLight": (0.80, 0.62, 0.46),
    },
    "tiefsee": {
        "name": "Deep sea", "nameDe": "Tiefsee",
        "description": "Waves in blue and turquoise, from the deep sea at night to bright lagoon water at noon.",
        "descriptionDe": "Wellen in Blau und Türkis, von der Tiefsee in der Nacht bis zu hellem Lagunenwasser am Mittag.",
        "hue": (232, 186), "sat": 0.72, "light": (0.16, 0.60), "waveHue": 12, "waveLight": (0.80, 0.62, 0.46),
    },
    "pastell": {
        "name": "Pastel", "nameDe": "Pastell",
        "description": "Soft pastel waves that go once around the color wheel in a day: pink, peach, lemon, mint, sky, lavender.",
        "descriptionDe": "Weiche Pastellwellen, die an einem Tag einmal durch den Farbkreis gehen: Rosa, Pfirsich, Zitrone, Mint, Himmel, Lavendel.",
        "hue": None, "sat": 0.50, "light": (0.66, 0.84), "waveHue": 12, "waveLight": (0.94, 0.87, 0.80),
    },
}


def rgb(h, s, l):
    r, g, b = colorsys.hls_to_rgb((h % 360) / 360.0, max(0, min(1, l)), max(0, min(1, s)))
    return np.array([r, g, b])


def colors(spec, i):
    """Sky top, sky bottom and the three waves for picture i (0 = midnight, 6 = noon)."""
    day = 0.5 - 0.5 * math.cos(2 * math.pi * i / 12)  # 0 at midnight, 1 at noon
    lo, hi = spec["light"]
    light = lo + (hi - lo) * day
    s = spec["sat"]
    if spec["hue"] is None:
        hue = 330 + 30 * i
    else:
        night, noon = spec["hue"]
        # The short way round the color wheel from the night's hue to the noon's.
        delta = ((noon - night + 180) % 360) - 180
        hue = night + delta * day
    # The waves lean towards one side of the color wheel and get deeper towards the front, never black.
    step = spec["waveHue"]
    return {
        "top": rgb(hue - step, s * 0.9, light + 0.08),
        "bottom": rgb(hue, s, light),
        "waves": [rgb(hue + step * (k + 1), s, light * f) for k, f in enumerate(spec["waveLight"])],
        "glow": rgb(hue - step * 1.5, s * 0.8, min(0.95, light + 0.22)),
    }


def smooth(edge, x):
    """0 below the edge, 1 above it, with a soft step a few pixels wide."""
    return 1 / (1 + np.exp(-x / edge))


def draw(spec, i):
    c = colors(spec, i)
    y, x = np.mgrid[0:SH, 0:SW].astype(np.float64)
    u, v = x / SW, y / SH
    img = c["top"][None, None, :] * (1 - v[..., None]) + c["bottom"][None, None, :] * v[..., None]
    # A soft light in the upper left, the same place in every picture.
    d = np.sqrt(((u - 0.28) * 1.6) ** 2 + (v - 0.22) ** 2)
    glow = np.exp(-(d / 0.32) ** 2)[..., None] * 0.55
    img = img * (1 - glow) + c["glow"][None, None, :] * glow
    # Three waves from back to front: each a sine line, everything below it in the wave's color with a gentle shade.
    for k, (base, amp, freq, phase) in enumerate([(0.56, 0.05, 1.3, 0.4), (0.68, 0.06, 1.0, 2.1), (0.81, 0.045, 1.7, 4.0)]):
        line = base + amp * np.sin(2 * math.pi * freq * u + phase) + 0.015 * np.sin(2 * math.pi * 3.1 * u + phase * 1.7)
        below = smooth(0.0016, v - line)[..., None]
        shade = 1 - 0.18 * np.clip((v - line) / 0.25, 0, 1)[..., None]
        wave = c["waves"][k][None, None, :] * shade
        img = img * (1 - below) + wave * below
    small = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    big = np.asarray(small.resize((W, H), Image.BICUBIC)).astype(np.float64)
    # A little noise against banding in the large gradients; the same seed for every picture, so it does not flicker.
    big += np.random.default_rng(7).normal(0, 1.2, big.shape)
    return Image.fromarray(np.clip(big, 0, 255).astype(np.uint8))


def main():
    out = sys.argv[1]
    for pid, spec in PACKAGES.items():
        folder = os.path.join(out, pid)
        os.makedirs(folder, exist_ok=True)
        images = []
        for i in range(12):
            name = f"{i + 1:02d}-{2 * i:02d}00.jpg"
            draw(spec, i).save(os.path.join(folder, name), quality=92, optimize=True)
            images.append({"file": name, "title": f"{2 * i:02d}:00", "titleDe": f"{2 * i:02d}:00 Uhr",
                           "anchor": {"mode": "time", "time": f"{2 * i:02d}:00"}})
        manifest = {
            "name": spec["name"], "nameDe": spec["nameDe"],
            "description": spec["description"], "descriptionDe": spec["descriptionDe"],
            "credits": "Drawn by EsteemWorks", "creditsDe": "Gezeichnet von EsteemWorks", "aiMade": False, "galleryId": pid, "mirrorEvening": False,
            "images": images,
        }
        with open(os.path.join(folder, "dynatones.json"), "w", encoding="utf-8") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)
        print(pid, "done")


if __name__ == "__main__":
    main()
