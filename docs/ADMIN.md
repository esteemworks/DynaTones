# DynaTones for administrators

How to deploy, steer and support DynaTones in an organization. DynaTones installs per user without admin rights, or
per device for all users with admin rights; the app itself always runs with the user's normal rights.

**Legend:** HKLM, HKCU = registry of the device, of the signed-in user. ADMX, ADML = Group Policy template and its
language file. `<setup>` = `DynaTones-Setup-<version>.exe`. Fixed = a policy users cannot change. Recommended = a
starting value users can change.

> [!IMPORTANT]
> The setup is not signed with a public code signing certificate yet. SmartScreen, Smart App Control, AppLocker, WDAC
> or ASR rules may block it; allow it by hash or path until it is signed (roadmap 4). The build already signs once a
> certificate is set up; then a publisher rule in AppLocker or WDAC covers every later version.

## What needs admin rights

| Task | Admin rights |
| --- | --- |
| Install, update, uninstall per user | no; to `%LOCALAPPDATA%\Programs\DynaTones` |
| Install, update, uninstall per device | yes; to `%ProgramFiles%\DynaTones`, for all users |
| Wallpaper, color mode, mouse pointer, lock screen | no |
| The screen before signing in follows the day | yes, once per device: the admin option or the per-device installation sets up two SYSTEM tasks; switching it on is then up to each user, or the policy `SignInScreen` |
| Register the event log source `DynaTones` | yes, once per device; included in the per-device installation |
| Fixed or recommended policies for the device (HKLM) | yes, as with any policy |
| Policies for a user (HKCU `Software\Policies`) | yes; users cannot write that key themselves |

## Install and uninstall

One setup for both ways. It brings its own .NET.

| | Per device (recommended for organizations) | Per user |
| --- | --- | --- |
| Who it is for | every user of the device | the user who runs the setup |
| Context | system, or an administrator | the user; in the system context it would install for the system account |
| Folder | `%ProgramFiles%\DynaTones` | `%LOCALAPPDATA%\Programs\DynaTones` |
| Start at sign-in | device Run key, for all users; only an administrator switches it off | the user's Run key; the user switches it |
| Event log source | set up with the installation | with the admin option, or later under Troubleshooting |
| Install silently | `<setup> /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /ALLUSERS` | `<setup> /VERYSILENT /SUPPRESSMSGBOXES /NORESTART` |
| Uninstall silently | `"%ProgramFiles%\DynaTones\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART` | `"%LOCALAPPDATA%\Programs\DynaTones\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART` |

An update is the same command with the newer setup and keeps the way it was installed; settings and packages stay.
Each user's settings and packages live in their own `%APPDATA%\DynaTones` either way, and DynaTones registers itself
for the app family when a user starts it the first time. After a silent installation per device, DynaTones starts at
the users' next sign-in.

Per user, `/MERGETASKS="adminfeatures"` adds the event log when the user running the setup is an administrator. Where
users are not, register the event log source once per device in the system context, for example as an Intune platform
script:

```powershell
if (-not [System.Diagnostics.EventLog]::SourceExists('DynaTones')) { New-EventLog -LogName Application -Source DynaTones }
```

**Intune, Win32 app:**

| Setting | Per device | Per user |
| --- | --- | --- |
| Install command | `DynaTones-Setup-<version>.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /ALLUSERS` | the same without `/ALLUSERS` |
| Uninstall command | `"%ProgramFiles%\DynaTones\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART` | `"%LOCALAPPDATA%\Programs\DynaTones\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART` |
| Install behavior | System | User |
| Detection rule | file `%ProgramFiles%\DynaTones\DynaTones.exe` exists | file `%LOCALAPPDATA%\Programs\DynaTones\DynaTones.exe` exists |
| Registry detection, instead | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A5553E1F-0B58-4C60-A11B-E013BB7F8CC9}_is1`, value `DisplayVersion` | the same under `HKCU` |

Do not mix both ways for the same user: a per-user installation and a per-device one are two separate installations.
To move, uninstall the per-user one first.

Uninstalling removes start at sign-in (per device: the device's entry), the event log source, the program and
DynaTones' entry of the app family, and gives Windows spotlight back if DynaTones switched it off; per device, the
family entry and Spotlight of the user who uninstalls. With the admin tier it also removes the two sign-in screen tasks
and their folders and gives the device's lock screen values back as they were before. It leaves the wallpaper, the lock screen picture, the color mode
and the data folders `%APPDATA%\DynaTones`.

With StartQueue installed, StartQueue starts DynaTones at sign-in in its startup order, and the switch Start with
Windows turns StartQueue's entry on and off. A per-device entry stays with Windows, so it starts for every user.

## Policies

### Templates

```powershell
DynaTones.exe --write-admx C:\temp\admx
```

writes `EsteemWorks.admx` (the family's category), `DynaTones.admx`, and their ADML files in `en-US` and `de-DE`.
`DynaTones.admx` builds on `EsteemWorks.admx`, so the family template always goes in first.

**Group Policy:** copy both ADMX files to `%windir%\PolicyDefinitions` or the central store
(`\\<domain>\SYSVOL\<domain>\Policies\PolicyDefinitions`), and the ADML files to the language folders next to them.
The settings appear under Computer and User Configuration > Administrative Templates > EsteemWorks > DynaTones, and
DynaTones: defaults users can change.

**Intune:**
1. Devices > Configuration > Import ADMX: upload `EsteemWorks.admx` with `en-US\EsteemWorks.adml`, wait until it shows
   as available
2. The same with `DynaTones.admx` and `en-US\DynaTones.adml`
3. Create a profile: Windows 10 and later, Templates, Imported Administrative templates; the settings are under
   EsteemWorks > DynaTones

**Other tools** (Matrix42, baramundi, scripts): write the registry values below directly, or use `policy.json`.

### Where the values live

| Kind | Registry |
| --- | --- |
| Fixed | `HKLM\Software\Policies\EsteemWorks\DynaTones` or `HKCU\Software\Policies\EsteemWorks\DynaTones` |
| Recommended | the same keys with `\Recommended` |

The device (HKLM) wins over the user (HKCU), the registry over `policy.json`, fixed over recommended. Fixed values are
laid over the user's settings when DynaTones reads them and are never saved into them: when a policy goes, the user's
own choice is back. A recommended value is applied once, when it is set or changed. Users see a fixed setting greyed
out with "Set by your organization". DynaTones reads policies, it never writes them.

### The policies

The value name is the policy's name. Toggles are `REG_DWORD` 1 (on) or 0 (off); text and choices are `REG_SZ`.

| Value name | Type | Values | What it sets |
| --- | --- | --- | --- |
| `WallpaperEnabled` | toggle | 1, 0 | whether DynaTones changes the desktop background |
| `ManagedPackageFolder` | text | a folder, `.zip` or `.ddw`; environment variables are expanded | the organization's image package, imported again when its files change and shown as the wallpaper |
| `AllowUserImport` | toggle | 1, 0 | 0 locks "Add package" and dropping files, and hides the gallery |
| `PackageGallery` | text | a folder, share or web address with a `packages.json`, or `off`; environment variables are expanded | where the gallery (Background, Add package) takes its packages from; not set: the release "packages" of DynaTones on GitHub. See [The gallery](#the-gallery) |
| `LockScreenEnabled` | toggle | 1, 0 | whether the user's lock screen follows the day |
| `SignInScreen` | toggle | 1, 0 | whether the screen before signing in follows too; works only where DynaTones was installed with admin rights (two SYSTEM tasks under `\EsteemWorks`, folders `%ProgramData%\EsteemWorks\DynaTones\signin` and `signin-request`, the device's `PersonalizationCSP` values, given back on uninstall). Windows applies them at the next lock or start;
some editions of Windows leave that screen out, and `--status` then shows `SignInShown not yet` |
| `ColorModeEnabled` | toggle | 1, 0 | whether DynaTones switches the color mode |
| `AppsColorMode` | choice | `switch`, `light`, `dark`, `leave` | the color mode of apps |
| `WindowsColorMode` | choice | `switch`, `light`, `dark`, `leave` | the color mode of Windows: taskbar and Start |
| `LocationMode` | choice | `auto`, `windows`, `coordinates`, `timezone`, `none` | where the sun is calculated for; `none` uses own times only |
| `Coordinates` | text | `52.2111, 8.3557` (latitude, longitude, dot as decimal separator) | the position for `coordinates` |
| `Language` | choice | `system`, `en`, `de` | the language of DynaTones |
| `StartWithWindows` | toggle | 1, 0 | start at sign-in, through the user's own Run key; no effect when installed per device |
| `Updates` | choice | `off`, `notify`, `install` | the update check on GitHub: never, once a day with a notice, or once a day installing by itself. Installations per device never update themselves; deploy new versions with the install command above |

A value that does not fit is ignored and logged (event 2003).

### policy.json

For tools that cannot write the registry: `%ProgramData%\EsteemWorks\DynaTones\policy.json`, the same names and values.

```json
{
  "fixed": { "WallpaperEnabled": true, "ManagedPackageFolder": "%ProgramData%\\Contoso\\Wallpapers" },
  "recommended": { "AppsColorMode": "switch", "LockScreenEnabled": false }
}
```

### Managed image package

Put the organization's images in a folder all users can read, for example `%ProgramData%\<Company>\Wallpapers`, and
set `ManagedPackageFolder`. DynaTones imports it into each user's data folder when its files change (names, sizes,
times), places the images through the day like any package, and uses it as the wallpaper. File names that sort in
the order of the day help Auto; a WinDynamicDesktop `.ddw` brings its own times. Import results go to the event log
(1100, 2100, 3101).

A package can also bring its own names and anchors in a `dynatones.json` next to the images, the file DynaTones keeps
in each package folder: export a package set up by hand from `%APPDATA%\DynaTones\packages\<id>` and every user gets
it with the same times.

### The gallery

The gallery (Background, Add package, From the gallery) lists packages to add with one click. By default it reads `packages.json` from the release
"packages" of DynaTones on GitHub, only when the user opens it. For an organization's own gallery, put a folder with
`packages.json`, the packages as `.zip` (pictures and `dynatones.json`) and a preview `.jpg` each on a share, and set
`PackageGallery` to it; `tools\packages\galerie.py` in the repository makes all three from folders of pictures.
`PackageGallery` set to `off`, or `AllowUserImport` set to 0, hides the gallery. A package from the internet is added
only when its SHA-256 matches the list; from a folder or share the hash may be left out.

The setup brings one package, "Lakeside" (drawn, `packages\lakeside.zip` in the program folder). DynaTones adds it
once per user at the first start and shows it, unless a package is chosen already or `ManagedPackageFolder` is set;
a user who deletes it does not get it back. `%APPDATA%\DynaTones\bundled-packages.txt` records it.

## Support

### Event log

Application log, source `DynaTones`, once the source is registered (see above).

| Id | Event |
| --- | --- |
| 1000, 1001 | Started, stopped |
| 1010, 1011 | Set up, uninstalled |
| 1020 | Policies in effect, listed |
| 1030, 1031 | Diagnostics mode on, off |
| 1100 | Managed image package imported |
| 2002 | Settings could not be read, started with defaults |
| 2003 | A policy value is not valid and is ignored |
| 2100 | Managed image package not found |
| 3000, 3001 | An error, an error nothing else caught |
| 3100 | Setting the wallpaper failed |
| 3101 | Importing the managed image package failed |
| 3102 | Working out the plan failed |
| 9000 | Detail, only in diagnostics mode |

### Log and diagnostics

The log is `%APPDATA%\DynaTones\DynaTones.log`, at most five files of 1 MB, none older than 30 days. In the user's
context, for example as an Intune remediation or a script:

| Command | Does |
| --- | --- |
| `DynaTones.exe --diagnostics-mode on` | writes every detail to the log for 24 hours, then turns itself off; also `off` or a number of hours |
| `DynaTones.exe --diagnostics [folder]` | writes a diagnostics package (zip): the log, the policies in effect, the event log entries, what is on screen, the settings with the position rounded to about 10 km. Default folder: the desktop |
| `DynaTones.exe --status <file>` | what is on screen now: wallpaper per monitor, color mode, pointer, lock screen, and what blocks them |
| `DynaTones.exe --plan <file>` | today's plan as text: position, sun, images, color mode, lock screen, and a skip by "Next image" |
| `DynaTones.exe --next` | "Next image" in the running DynaTones, as the button and the tray menu do |
| `DynaTones.exe --gallery <file>` | where the gallery reads from (`off` when switched off) and every package in it, with whether it is added |

Nothing is sent anywhere; the package stays where it is written.

### When something is in the way

DynaTones says why instead of failing quietly, in its window and the log:

| Situation | What DynaTones does |
| --- | --- |
| `NoChangingWallPaper`, a wallpaper policy or the Personalization CSP sets the desktop | leaves the wallpaper alone, keeps the color mode |
| `NoChangingLockScreen`, `LockScreenImage` or the Personalization CSP sets the lock screen | leaves the lock screen alone |
| Windows location switched off | uses the place entered, then the time zone |
| Another tool changes the wallpaper (slideshow, Spotlight) | puts its own back and names the other tool in the settings |
