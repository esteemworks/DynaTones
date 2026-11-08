# The gallery's packages

DynaTones' gallery lists packages to add with one click. These tools make the files it reads: one `.zip` per package
with its pictures and a `dynatones.json` (names in English and German, credits, and when each picture begins), a
preview `.jpg`, and `packages.json`, the list.

| File | Does |
| --- | --- |
| `galerie.json` | the gallery's packages: id, source folder, style, made with AI or drawn, names and descriptions in English and German |
| `galerie.py <out> --bilder <folder of day series> --farbe <folder of color packages>` | one `.zip` per package (pictures stored as they are, and a `dynatones.json` with names, credits and anchors), one preview `.jpg` (a slice of every picture side by side) and `packages.json` with size and SHA-256 |
| `farbpakete.py <out>` | draws the color packages Ember, Deep sea and Pastel without any image model: twelve pictures each at 3840 × 2160, the same waves in every picture and the colors of the hour, with a `dynatones.json` giving each a clock time two hours apart |

Requirements: Python 3 with Pillow and NumPy.

## How the places were made

The ten places of the gallery were made with AI and are marked as such in the app:

| Step | Tool | License |
| --- | --- | --- |
| A base picture at noon, a few seeds to choose from | SDXL 1.0 | CreativeML OpenRAIL++-M |
| A depth map of the chosen base | Depth Anything V2 small | Apache 2.0 |
| Each time of day: the base graded into that light, then painted again with its shapes held in place by the depth | SDXL 1.0, ControlNet Depth SDXL | CreativeML OpenRAIL++-M |
| ×4 and 16:9 to 5120 × 2880 | Real-ESRGAN x4plus | BSD-3 |

Only the light changes between the pictures of a place, so the flowing transition blends cleanly from one into the
next. The scenes are invented places, without real buildings, brands or people.

## Anchors

A day series: night from -10° setting, blue hour from -9° rising, sunrise from -2°, morning from 7°, noon from 45° on
the reference arc (late morning), afternoon from 40° setting, golden hour from 10°, sunset from 3°, the evening blue
hour from -3°. Lakeside keeps the elevations it was drawn for; `assets\packages\lakeside\` holds its pictures and the
same `dynatones.json` that `galerie.py` writes, and `build.ps1` puts it into the setup from there.

## Publishing

Upload everything in `<out>` to the release with the tag `packages`, marked as a pre-release so the update check
never takes it for a version of the app:

```powershell
gh release create packages --prerelease --title "Image packages" --notes "Packages for the gallery in DynaTones." <out>\*
gh release upload packages <out>\* --clobber   # later changes
```

An organization puts the same folder on a share and sets the policy `PackageGallery` to it.
