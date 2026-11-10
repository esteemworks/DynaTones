# Security

DynaTones changes the wallpaper, the color mode and the mouse pointer for your account and reads policies of your
organization. Security issues are taken seriously.

## Supported versions

Only the [latest release](../../releases/latest) receives fixes.

## Reporting a vulnerability

Please do not open a public issue for a vulnerability. Report it privately through **Security > Report a
vulnerability** in this repository (GitHub private vulnerability reporting), with the version, what you did and what
happened.

You will get an answer as soon as possible. Once a fix is released, the report can be published.

## Design notes

- DynaTones always runs with normal rights and installs per user. Admin rights are asked for only once, when "Full" is
  chosen in the setup or with **Set up fully** under Settings, General: one elevated pass registers the event log
  source and the sign-in screen tasks
- Images are read with Windows' own decoders. Packages are copied into the data folder before they are read, and a
  `.zip` or `.ddw` is unpacked into a folder of its own
- Policies are read, never written. A fixed policy value always wins over the user's own setting, and the user's
  setting is never overwritten by it
- One instance per user; other programs talk to it only through a named pipe limited to the same user
- No network access of its own except the update check, when it is on: the latest release from the GitHub API, and
  the setup from GitHub when the user installs it. The setup must match the SHA-256 digest GitHub gives for it, and
  once DynaTones is signed, carry the same signature; otherwise it is deleted, not run. The position comes from the
  Windows location service, only where Windows allows it
