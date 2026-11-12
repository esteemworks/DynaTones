# Changelog

All notable changes to DynaTones. Downloads will be on the [releases page](../../releases) from the first release on.

## Not released yet

- **Profiles work as in every EsteemWorks app.** The profiles are chips in their order of precedence, with
  "No profile" last; a ring marks the one that applies now, a pause sign the paused ones. Drag a chip to change
  the order, or right-click it to edit, use it by hand (until the end of its time or until you switch back), pause
  it, give it higher or lower precedence or remove it. The line under the chips says when the profile applies and
  offers Back to the schedule, Order, Pause and Edit. Editing, the pause and the order open as pages of the tab;
  Back or Esc returns to the same place. New: **pausing a profile** (for a time or until you resume it) and **the
  order of the profiles** as a list with arrows. "For all EsteemWorks apps" is set in the editor, as in the other
  apps. What a profile changes in DynaTones stays on the tab, under the chips
- **Explorer no longer flickers during a flowing transition.** With Windows' "accent color from the background" on,
  Windows worked out a new accent for every picture in between, once a minute, and told every window; Explorer in
  dark mode then flashed its scroll bars. While pictures in between show, DynaTones now switches that automatic off
  and on again right before the target picture, so Windows works out the accent once, from the picture it is meant
  for. Only when it was on; it goes back on with the target picture, on pausing, on quitting and at the next start
  if DynaTones ended in the middle. Each change is a line in the log
- **The settings as in every EsteemWorks app:** one calm column, a name above each group (General, Location, About
  DynaTones, More from EsteemWorks), and every row with an icon, a short line saying what it does and one control.
  The accent color is a choice field. Updates, Back up settings, Troubleshooting and Privacy open pages of their own;
  Back or Esc returns to the same place. Under Color mode the way it switches has its own line below the switch
- **Buttons that open a menu behave the same everywhere** (Add package, its tile, Pause, Switch to dark, Add profile
  and every choice field in the settings): a second click closes the menu instead of opening it again, the menu opens
  below its button and is at least as wide, the arrow points the way the menu opens, and Enter, Space and Down open it
  with the first entry, Up with the last, Esc closes it and leaves the focus on the button. While paused, **Resume**
  is a button of its own next to where Pause was: a button either does something or opens a menu, never both
- **Sharing a profile on and off, without copies piling up.** Switching "Offer to all EsteemWorks apps" off while
  another app of the family uses the profile asks first, then DynaTones keeps a copy of its own; the other apps keep
  theirs, and what the profile changes in DynaTones (package, transition, lock screen, color mode, pointer) stays
  with DynaTones' copy. Switching it on again while a shared profile of the same name exists joins that one: with the
  same days and times without asking, otherwise DynaTones asks whether to use the existing profile or overwrite it,
  and shows both versions. Switching back and forth never makes a new profile. The remove button says "Remove from
  DynaTones" while other apps use the profile and "Delete profile" only when it is gone afterwards
- Screen readers: the switch "Offer to all EsteemWorks apps" has its name, and every row in the settings is read
  with its name and the line below it, also rows without a control
- The arrow of a choice field in the settings stays the same after Windows switches between light and dark
- **A new tray menu, as the family's:** a head with DynaTones' tile, its name and what holds now ("Light mode, next
  change at 14:50"), a click on it opens the window; then the day's actions with icons (Update now, Next image,
  Switch to dark, Jump to the next dark phase), Pause with its state at the right ("Running", "until 15:00"), and Quit.
  Rounded, light and dark, and by keyboard: Win+B, the arrow keys to DynaTones, then Enter or Shift+F10; the arrow
  keys, Home, End and a letter move, Right opens Pause, Esc closes
- **One main button per view, quieter everywhere else.** Add package in the header is the window's one main button;
  in the gallery every Add, on a package's page Use for the desktop, and in the profile editor the header's button
  step back, so Save is the editor's one. Next image and Switch to dark under The day are quiet text buttons with an
  icon, Add profile is the one text action of the Profiles page, Delete profile is neutral
- **Light and dark while the window is open:** every part of a page now changes with the window. Before, parts built
  in light stayed light when the window turned dark (white lines and dark titles in the introduction, the frame of
  the map)
- **Chosen is one look:** a chosen card (transition, lock screen, anchor, the pictures of the introduction) and a
  package in use take the chosen surface with a thin line, as everywhere in the family, instead of a thick colored
  border; nothing shifts when the choice moves
- **Menus speak in verbs and show icons:** Choose from the gallery, Add a folder of pictures, Open a package file,
  Make a package of your own; Switch only the color mode, Jump to the next dark phase; Create a new profile, Take
  over a profile from another app. Remove in a picture's menu is red, as a dangerous entry is
- **Calmer type and spacing:** headings are heavier, not bigger (the introduction's headings, the place under
  Location and a profile's name now follow the family's sizes), and every gap sits on the 4 px grid. Background fits
  the window at its normal size without scrolling, and the gallery's Back lines up with its cards
- A pointer package that was added says so in a notification instead of a message to click away
- The timeline's tooltip opens after the family's usual rest of the pointer, no longer at once
- **Calmer colors:** switches and radio buttons in one solid tone; the gradient stays in the bar below the header
  and on the main button. Menus grow out of the button they open from
- **The keyboard, as in every EsteemWorks app:** the tabs and the segments in the settings are one Tab stop each, the
  arrow keys move and choose, Home and End jump; menus opened from the keyboard highlight their first entry and take
  Home, End and a letter; a question about deleting puts the focus on Cancel first, Tab stays inside it, and the
  focus goes back where it was. A setting's explanation also shows when its control gets the keyboard focus
- **Notifications:** Undo after deleting is one of several that can show at once; the time stops while the pointer is
  on them or the window is in the background, and F6 moves to them and back
- **The setup asks one question:** Basic, without admin rights, or Full, with admin rights, each with what it means
  here. Without admin rights Full is greyed out and says why; while Full is chosen, Install shows the shield of
  Windows. Full asks Windows once for the event log and the screen before signing in together, and DynaTones never
  asks again afterwards. Basic can become Full later under Settings, General, **Set up fully**. The window is called
  "Set up DynaTones", the box "Look for updates" says what it does, and "for all users" is only `/ALLUSERS` now
- **DynaTones changes nothing until you switch it on.** A new installation starts with a short introduction (Welcome,
  Place and pictures, Switch on); background and color mode stay as they are until **Switch DynaTones on**. **Look
  around first** opens DynaTones as usual with a note and **Switch on** in the header. Installations from before are
  switched on already: an update shows no introduction and changes nothing. `--switch-on` does the same from the
  command line
- **Every change of picture fades.** DynaTones mixes three frames between the two pictures and shows them over about a
  second, also for Next image, the jump to the next light or dark phase and Back to the plan. Windows' own fade could
  not be relied on: it depends on Windows' animation settings and did not show at all in Windows Sandbox. The color
  mode changes halfway through the fade, not before it
- **At once** is gone: where it was set (the desktop, a package, a profile), Soft takes its place
- **Accent color** in Settings, General: DynaTones, Profile (the default: the color of the profile that applies, or of
  the one being edited), Windows, or Sand, the family's quiet beige
- The window's frame (title bar, header, tabs and the settings gear) comes from the family's kit, as in every
  EsteemWorks app; it looks and works as before
- Screen readers: the Add package tile, the choices for apps and taskbar and the Earlier state button have names; Add
  pointer package sits below the two pointer choices, so Tab no longer jumps up the page

## 0.5.0

- **Switch to dark** or **light** in the tray and next to Next image: only the color mode, the picture stays, until
  the plan comes to that mode by itself; from then on the day goes on as planned. Without DynaTones' color mode it
  switches Windows' own, as Settings would
- **Next dark** (or **light**) **phase, with its picture**: color mode and picture as they will be when the other mode
  next begins, at once; at that moment the plan has caught up and goes on. **Back to the plan** undoes both
- Next image and both of these show while paused too, and the pause keeps them; `--switch-mode` and `--jump-mode`
  do the same from the command line
- When a Next image ran out, a profile's settings stayed out until the next change; now they apply at once

## 0.4.0

### A new layout
- Two tabs, **Background** and **Color mode**, instead of Today, Images, Color mode and Location. Background shows on
  top what the desktop and the lock screen show now and next, with the day below it (the sun, light and dark, the
  pictures in their order), then the way into **Desktop background** and **Lock and sign-in screen**, then your
  packages. Location moved to the settings, where it is set up once
- **Next image** on the desktop, in the tray menu and with `--next`: the coming picture now, and the day keeps one
  step ahead until that picture's own time, so neither the next check nor the flowing transition brings the skipped
  one back; **Back to the plan** undoes it. Kept over a restart of DynaTones
- One picture at a time: a click on a picture opens when it begins (automatic, with the sun, at a clock time, or not
  at all), with its hours today, in June and in December. The list of pictures no longer shows every switch at once
- Lock screen as one choice: like the desktop, a package of its own, or like Windows. Windows spotlight goes off by
  itself when DynaTones shows the pictures and comes back with "Like Windows"
- **Pause** asks how long: an hour, until tomorrow morning, until you resume. **Update now** is a small icon (F5)
- **Add package** has the gallery, a folder and a file in one menu; Esc goes one page back
- With a package of its own for the lock screen, the day shows two strips: the desktop's pictures and below them the
  lock screen's, each marked with its symbol; pointing at the day names both pictures
- **Update now** turns its icon while it works, at least once, as StartQueue's Check
- Settings hold only settings: General, Location, Updates, Backup, Troubleshooting, then About and More from
  EsteemWorks. Where the pictures come from is a line under About instead of a card of its own
- Location is one card: the source as four choices (Automatic, Windows, Fixed place, Time zone), the place in use,
  and fields only for a fixed place, filled in with where DynaTones is when you choose it
- The two cards on top of Background open the desktop's and the lock screen's pages; Next image sits with the day
- Pages come at once: pictures are read off the window's thread, so going back after a change no longer stalls
- A flowing transition no longer flashes on several monitors: each step is set for all monitors in one call, so
  Windows draws it once
- Color mode reads as a sentence: "Light from 30 minutes after sunrise", no negative numbers
- Radio buttons and links in the app's color; the gallery in three columns
- **Color mode** as one page: the day as a band with a handle at each switch, the two rules as sentences, and "What
  changes along" as three cards (apps, taskbar and Start, mouse pointer), each with a picture of day and night
- **Pointer packages**: a folder or `.zip` with `.cur` and `.ani` files (with or without an install.inf) becomes a
  pointer scheme of your user
- **Transitions** as four cards: at once, soft, gentle (new: three mixed steps in the last seconds) and flowing over
  10, 30 or 60 minutes; a package can have its own
- **Edit package**: name, description, transition, more details (other language, source, made with AI), add pictures,
  drag them into another order, remove them with Undo, export as `.zip`; "New package of your own" in Add package
- **Profiles** tab: profiles shared with the EsteemWorks apps (kit FamilyProfiles), each setting its own or taken over,
  use now and back to the schedule, the week at a glance
- **Settings**: Location with a small map (Natural Earth, public domain, built in), the place by name and today's sun;
  About as one card with updates, backup and troubleshooting as rows
- Less work for the PC during a flowing transition: the two pictures are made smaller once and kept as raw pixels
  for its 30 steps instead of decoding the originals twice per step; a step takes about a third of the time it took,
  runs on two cores at most and at low priority. The screen before signing in is worked out again only when its
  pictures change
- The screen before signing in: DynaTones' own picture there no longer reads as a rule of the organization, and
  switching it off gives the device's own picture back
- Places by name: the position in use reads "Near Melle" instead of coordinates, and a fixed place can be any of
  about 32,000 places with at least 15,000 inhabitants (GeoNames, CC BY 4.0, built in; nothing is looked up online)
- The screen before signing in can be set up from the app: **Set up** on the lock screen page asks for admin rights
  once. Installations from before 0.2.0 that were only updated since never had it set up
- "Use evening pictures in the morning" shows only where it does something
- Pictures have names ("Blue hour") where the package brings them; the gallery's packages do

### Start
- At sign-in the color mode and the background come first: the tray icon, the family entry and the update check
  wait until they are set, telling the other windows about the color mode no longer holds up the background, and a
  flowing transition starts with the plain picture instead of waiting for its first mix. The log says how long it
  took ("Applied at start in ... ms")

## 0.3.1

### Fixed
- With the window open, a switch of the color mode of Windows (DynaTones itself switches it at sunrise and sunset)
  left the page in the old mode while the drop-down fields took the new one: white drop-down fields on a dark page.
  Page and fields now change together
- The color mode chosen for the window (light or dark) was lost at every start; it is kept now

## 0.3.0

### Packages
- **Ready from the first start**: the setup brings "Lakeside", a drawn lake in eight lights, and DynaTones shows it
  when no package is chosen yet. Added once; deleted, it stays deleted
- **Gallery** under Images: packages added with one click and already set up for the day. Ten places from night to
  night (mountain lake, snowy village, autumn valley, lighthouse bay, little island, dunes, forest stream, forest
  cabin, deer at the forest's edge, old town roofs) in photo style, 3D and flat 2D at 5120 x 2880, and three color
  packages, Ember, Deep sea and Pastel, with twelve colors a day, one every two hours
- The places were made with AI (SDXL 1.0, Real-ESRGAN) and are marked as such on every card, under Images and in the
  settings under Pictures; the color packages and Lakeside are drawn
- A package can bring its names in English and German, a description, its credits and its anchors in a
  `dynatones.json`; DynaTones takes them over when it is imported
- Every download from the gallery is checked against the SHA-256 in the list before it is added
- New policy `PackageGallery`: a gallery of the organization's own on a share or web address, or `off`
- `--gallery <file>` and `--gallery-add <id> <file>` from the command line

### Privacy
- About now names the gallery beside the update check as the only ways online; both only ask GitHub for files

## 0.2.0

### Background
- Flowing transitions: before the next image starts, DynaTones mixes the two in small steps, about one a minute, so
  the same place in other light changes slowly instead of jumping. **Settings > Background > Transition**: switch,
  short fade, or flowing over 10, 30 (the default) or 60 minutes, never longer than the current image is shown. The
  steps are made from the images at the size of the screens, kept only while needed, and stay on this PC

### Your settings
- A newer version takes the settings over, and the last five copies are kept under `backups` in the data folder,
  made before every update, import or restore
- **Settings > About > Backup and restore**: save the settings to a file, load them on another PC, or go back to an
  earlier copy; `--export <file.zip>` and `--import <file.zip>` do the same from the command line
- Settings that cannot be read are kept and shown under About, instead of being replaced without a word
- The command line import of an image package is now `--import-package <source> <file>`

### Updates
- Looks for a new version on GitHub once a day, when the setup's box "look for updates" stays ticked; nothing about
  you is sent. A new version shows in the tray and under **Settings > About > Updates**, installs with one click or,
  if you switch it on, by itself, and DynaTones comes back quietly in the tray
- The setup is checked against the SHA-256 digest of the release before it runs
- Installations per device never update themselves; the policy `Updates` (off, notify, install) decides for organizations

### Window
- Rounded corners on Windows 11, like other windows (from the family's shared window frame)
- Today fits the window without scrolling: the pictures of now and next take the height that is left and grow with
  the window. Only a window smaller than about 800 px high scrolls

### Lock screen
- The lock screen can follow the day, with the desktop's images or a package of its own (Settings > Lock screen). Set
  through Windows' own interface for the user's lock screen
- Also the screen before signing in (Settings > Lock screen), with the installation with admin rights: the admin tier
  sets up two tasks that set the device's lock screen picture, at start and whenever the picture changes. Windows takes
  it over the next time the PC is locked or starts. The task checks every picture it is handed, and `--status` shows
  what it set and when Windows took it over. Some editions of Windows leave that screen out; the setting then says
  that Windows has not shown it. A policy "Also the screen before signing in"
- Windows spotlight on the lock screen is switched off only when you ask, and comes back as it was when you switch the
  option off again
- A policy "Lock screen follows the day" for organizations

### For organizations
- Group Policy and Intune through ADMX templates (`--write-admx`), or any tool that writes the registry or
  `policy.json`. Each setting can be fixed, greyed out with "Set by your organization", or given as a default users
  can change
- An image package from a folder the organization manages, imported again when it changes
- Installation per device for all users: `/ALLUSERS` (silent, for the system context) or the choice in the setup;
  Program Files, start at sign-in through the device's Run key, the event log included
- With StartQueue installed, StartQueue starts DynaTones and the switch Start with Windows goes through it, so the two
  no longer undo each other
- The build signs the program, the setup and its uninstaller once a certificate is set up on the PC that builds
- [docs/ADMIN.md](docs/ADMIN.md): silent installation, the templates in Group Policy and Intune, every policy with
  its registry value, policy.json, event ids and support from the command line
- Diagnostics mode and a diagnostics package under Settings, About, Troubleshooting; important events also in the
  Windows event log once it is set up

### Setup and the family
- One setup that installs per user without admin rights and brings its own .NET; the admin option only adds the event
  log. Uninstalling through Windows Settings removes start at sign-in and the program and keeps your packages
- Tray icon with "Update now" and "Pause"; About with the version, privacy, "Made with AI" and the other EsteemWorks
  apps

### Calmer window
- Settings open with the gear at the right end of the tabs instead of a tab of their own; Esc closes them
- **Color mode**, as Windows calls it, instead of "light and dark"
- Explanations sit behind an info sign; tooltips on every button, segment and field whose label alone does not say
  what happens
- Questions and messages appear inside the window; deleting a package can be undone for eight seconds
- A scroll bar that stays visible on long pages, scrolling with the middle mouse button, F5 and Ctrl+N
- No small caps line above the title, no dots between facts

### Fixed
- The empty date field on Today showed its hint in the language of Windows instead of the one chosen in DynaTones
- What Auto found out about an image stayed in the language of the import; it is now kept in English and German and
  shown in the language chosen. Packages imported before keep their old text until they are imported again

## 0.1.0

First version: dynamic wallpapers and the color mode from one calculation of the sun.

- Every image has a place on the sun's arc, rising or setting, and stays until the next one starts
- Auto places images from the time and place in a photo, from a WinDynamicDesktop theme or from how bright and warm
  they look; anchors by hand to an elevation or a fixed time
- Color mode by sunrise and sunset with offsets, fixed times, or following the image; apps, Windows and the mouse
  pointer separately
- Location from Windows, a place or coordinates entered, or the time zone; works on devices where Windows location is
  switched off
- Puts the wallpaper back when another tool replaces it, and catches up after sleep, unlock and a changed clock
