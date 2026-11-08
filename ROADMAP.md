# Roadmap

Where DynaTones is heading, in the order it is planned. What is already done is in the [changelog](CHANGELOG.md).
Ideas and wishes are welcome as issues.

## 1. Lock screen for organizations

The lock screen and, with the installation with admin rights, the screen before signing in follow the day since 0.2.0.
Still open:

- A lock screen package of its own chosen by policy

## 2. More packages in the gallery

The gallery has ten places and three color packages since 0.3.0. The places were made with an image model that runs
on a home PC, and it shows in the details. Next:

- The remaining themes (city in 3D and 2D, countryside, cars, cats, dogs, an EsteemWorks package in each style) with a
  stronger image model, under a license that allows them in a free app, still invented places without brands or
  people
- The ten places of 0.3.0 made again the same way, where the stronger model does better

## 3. More image formats

- HEIC images and macOS `.heic` dynamic wallpapers, read directly instead of converted first

## 4. Several monitors

- A package or an image per monitor, and one image across all monitors that follows the day

## 5. Release

- Code signing, so Windows SmartScreen no longer warns about an unknown publisher
- A winget package `EsteemWorks.DynaTones`

## Known limitations

- Tested in Windows Sandbox; locking and unlocking, sleep and several monitors have not been tested on real hardware
  yet
- Uninstalling leaves the lock screen picture as it is, like the wallpaper
- The flowing transition has been checked on one real PC with three monitors by its files and log, not yet by eye
- A position estimated from the time zone can be off by up to half an hour for sunrise and sunset
