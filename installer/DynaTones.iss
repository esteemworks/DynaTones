; DynaTones setup, built on the family's template (EsteemWorks.Kit\setup\EsteemWorks.iss): installs for the signed-in
; user without admin rights. The admin tier, offered to administrators only, sets up the screen before signing in and
; the Windows event log (one UAC prompt). Built by build.ps1 -Setup, which passes SourceDir and Kit.

#define AppName "DynaTones"
#define AppId "{{A5553E1F-0B58-4C60-A11B-E013BB7F8CC9}"
#define AppIcon "..\assets\DynaTones.ico"
#define AdminFeaturesEn "Let the screen before signing in follow the day, and write important events to the Windows event log"
#define AdminFeaturesDe "Den Bildschirm vor der Anmeldung mit dem Tag wechseln und wichtige Ereignisse ins Windows-Ereignisprotokoll schreiben"
; Updates from the GitHub releases (KitUpdate): a ticked task "look for updates" per user.
#define Updates
#ifndef Kit
  #define Kit "..\..\EsteemWorks.Kit"
#endif
#include Kit + "\setup\EsteemWorks.iss"