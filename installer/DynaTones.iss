; DynaTones setup, built on the family's template (EsteemWorks.Kit\setup\EsteemWorks.iss): one page asks "Basic,
; without admin rights" (for the signed-in user, no UAC prompt) or "Full, with admin rights", which also sets up the
; screen before signing in and the Windows event log (one UAC prompt, none later in the app). Built by build.ps1 -Setup,
; which passes SourceDir and Kit.

#define AppName "DynaTones"
#define AppId "{{A5553E1F-0B58-4C60-A11B-E013BB7F8CC9}"
#define AppIcon "..\assets\DynaTones.ico"
#define BasicEn "Background, lock screen, and light and dark change with the day."
#define BasicDe "Hintergrund, Sperrbildschirm und Hell und dunkel wechseln mit dem Tag."
#define FullEn "Also: the screen before signing in changes with the day too."
#define FullDe "Dazu kommt: Auch der Bildschirm vor der Anmeldung wechselt mit dem Tag."
#define FinishedEn "DynaTones has not changed anything yet. When you open it, you choose your place and your pictures. Until you switch it on, your background and light or dark stay as they are."
#define FinishedDe "DynaTones hat noch nichts verändert. Beim ersten Öffnen wählst du deinen Ort und deine Bilder. Bis du es einschaltest, bleiben Hintergrund und Farbmodus, wie sie sind."
; Updates from the GitHub releases (KitUpdate): the tick "look for updates" per user.
#define Updates
#ifndef Kit
  #define Kit "..\..\EsteemWorks.Kit"
#endif
#include Kit + "\setup\EsteemWorks.iss"
