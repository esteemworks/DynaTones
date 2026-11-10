# Privacy

DynaTones works on your device only. It collects no usage data, needs no account and passes nothing on to the author
or anyone else. There are no ads and no analytics.

It makes two connections, both to GitHub and both only when you want them:

- **The update check**, while it is on (the setup suggests it, **Settings > About > Updates** switches it off): once a
  day DynaTones asks GitHub for the latest release of DynaTones. A new version is downloaded only when you install it.
- **The gallery**, when you open it (Background, Add package, From the gallery): DynaTones downloads the list of packages and their previews, and a
  package only when you click Add. Your organization can point the gallery to a share of its own or switch it off.

GitHub sees your IP address and the app's name and version, as with any download; nothing about you, your images or
your position is sent.

## What DynaTones reads

Only what the job needs, on the device it runs on:

| What | Why |
| --- | --- |
| Your position from Windows location, if Windows allows it | to calculate where the sun stands. Switched off in Windows, DynaTones uses a place you enter or your time zone |
| Your time zone | as the fallback for the position |
| The images you add, including the time and place stored in a photo | to place each image at the right moment of the day |
| The current wallpaper, color mode and mouse pointer scheme | to change only what differs from the plan |
| Policies of Windows and of your organization that concern the wallpaper and DynaTones | to respect them instead of overriding them |

## What DynaTones stores

In `%APPDATA%\DynaTones` on your device:

| File | Contents |
| --- | --- |
| `config.json` | your settings, a place or coordinates you entered, and the last position Windows reported |
| `packages\` | copies of the images you added, with the anchors you set and what DynaTones read from each image (brightness, and the time and place a photo was taken, if it carries them) |
| `cache\` | temporary files while a package is imported, the steps of a flowing transition, and the gallery's previews |
| `bundled-packages.txt` | which packages of the setup were added once, so one you deleted does not come back |
| `lockscreen\` | the copy of the image on the lock screen now, and which image it came from |
| `backups\` | the last five copies of `config.json`, made before an update, an import or a restore |
| `DynaTones.log` | a log of changes and errors; at most five files of 1 MB, none older than 30 days |

The position and the photo data can tell where you are or were. They never leave the device. The folder stays when
DynaTones is uninstalled, so a new installation finds your packages again; you can delete it at any time.

An export of your settings (Settings, About, Backup and restore, or `--export`) holds `config.json` with a small note
of the version: so it contains a place or coordinates you entered and the last position Windows reported, but no
images. It is saved where you choose and not sent anywhere.

A diagnostics package (Settings, About, Troubleshooting) is only created when you ask for it. It rounds the position to
about 10 km, is saved on your desktop and is not sent anywhere; you decide whom you give it to.

## What DynaTones changes in Windows

Depending on your settings: the desktop wallpaper, the color mode of apps and of Windows, the mouse pointer scheme and,
when you switch it on, the picture of your lock screen and whether Windows spotlight shows its own pictures there.
It adds itself to start at sign-in, which you can switch off. Set up as "Full" it also sets up its source
in the Windows event log. Uninstalling removes start at sign-in, the event log source and the program and gives Windows spotlight back if
DynaTones switched it off; the wallpaper, the lock screen picture and the color mode stay as they are at that moment.

## AI

DynaTones was developed with the help of AI (Claude by Anthropic). The app itself contains no AI, makes no automated
decisions about people and sends no data to AI services. The places in the gallery were made with AI image models on
the author's PC and are marked as made with AI in the app; they show invented places.

## Questions

Open an issue in this repository.
