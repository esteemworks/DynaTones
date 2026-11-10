using System.Text;
using System.Windows;
using DynaTones.Core;
using DynaTones.UI;

namespace DynaTones;

public partial class App : Application
{
    TrayHost? host;
    SingleInstance? instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        KitApp.Init("DynaTones", Palette.Sunset);
        KitUpdate.Configure("esteemworks/DynaTones");
        // The gallery is the app's one other way online; About says so.
        EsteemWorks.Kit.UI.AboutPage.OtherConnection = ("for the gallery, when you open it", "für die Galerie, wenn du sie öffnest");
        // Where the pictures come from: a line under About; each package says its own origin on its page.
        EsteemWorks.Kit.UI.AboutPage.Notices.Add((
            "Pictures: \"Lakeside\" and the color packages are drawn. Gallery packages marked as made with AI were generated with SDXL 1.0 and show invented places.",
            "Bilder: „Lakeside“ und die Farbpakete sind gezeichnet. Galerie-Pakete mit KI-Kennzeichen entstanden mit SDXL 1.0 und zeigen erfundene Orte."));
        EsteemWorks.Kit.UI.AboutPage.Notices.Add((
            "Place names: GeoNames (geonames.org), licensed CC BY 4.0.",
            "Ortsnamen: GeoNames (geonames.org), Lizenz CC BY 4.0."));
        EsteemWorks.Kit.UI.AboutPage.Notices.Add((
            "Map: Natural Earth (naturalearthdata.com), public domain.",
            "Karte: Natural Earth (naturalearthdata.com), gemeinfrei."));
        Policies.Define();
        // Restorable settings (kit STANDARD.md, section 3): takes over settings of an earlier version before anything
        // reads them. Only config.json; the image packages and the blend steps are not settings. When Config.Schema
        // goes up, add a step from the version before ([1] = doc => { ... } turns a version 1 file into version 2) and a
        // sample file of the released version to tests\fixtures.
        KitSettings.Configure(Config.Schema, ["config.json"]);
        AppIcons.SetMark(Mark.Paint);
        // The kit logs what nobody caught; DynaTones goes on, the next tick puts things right.
        KitDiagnostics.CatchUnhandled();
        DispatcherUnhandledException += (_, ex) => ex.Handled = true;
        KitDiagnostics.AddToPackage("status.txt", Diagnostics.StatusReport);
        KitDiagnostics.AddToPackage("settings.json", Diagnostics.SettingsForPackage);

        var args = e.Args;
        // The setup's command lines (--setup, --uninstall, --prepare-update): start at sign-in, family entry, quitting
        // a running copy. The wallpaper and light or dark stay as they are when DynaTones goes.
        // Also --diagnostics [folder] and --diagnostics-mode on|off|<hours>, the family's troubleshooting lines, and
        // --updates on|off from the setup's task "look for updates", and --export or --import <file> for the settings.
        // The admin tier ("Full") also sets up the sign-in screen (SignIn: two SYSTEM tasks and their folders), in the
        // same elevated pass as the event log, so Windows asks once (--setup-admin).
        // --setup --new marks a new installation: nothing changes in Windows until DynaTones is switched on (KitIntro).
        // Uninstalling takes the sign-in screen away in the same single elevated pass as the event log (--uninstall-admin).
        if (KitSetup.TryRun(args, uninstall: GiveBack, adminSetup: SignIn.Install, adminUninstall: SignIn.Uninstall, adminUninstallNeeded: SignIn.NeedsRemoval) ||
            KitDiagnostics.TryRun(args) ||
            KitUpdate.TryRun(args) || KitSettings.TryRun(args))
        {
            Shutdown();
            return;
        }
        var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        var path = args.Length > 1 ? args[1] : null;
        try
        {
            switch (mode)
            {
                // The Group Policy templates (ADMX and ADML, English and German) from the list in Policies.
                case "--write-admx" when path != null:
                    PolicyTemplates.Write(path);
                    Shutdown();
                    return;
                // The elevated helper of the setup's admin tier and of uninstall, for the sign-in screen.
                case SignIn.InstallCommand:
                    SignIn.Install();
                    Shutdown();
                    return;
                case SignIn.UninstallCommand:
                    SignIn.Uninstall();
                    Shutdown();
                    return;
                case "--write-assets" when path != null:
                    AppIcons.WriteAssets(path, "DynaTones");
                    Shutdown();
                    return;
                // Diagnostics: today's plan as text, without changing anything on screen.
                case "--plan" when path != null:
                    File.WriteAllText(path, Diagnostics.PlanReport(args.Length > 2 ? args[2] : null), Encoding.UTF8);
                    Shutdown();
                    return;
                // Diagnostics: what is on screen right now, read through the same interfaces DynaTones sets it with.
                case "--status" when path != null:
                    File.WriteAllText(path, Diagnostics.StatusReport(), Encoding.UTF8);
                    Shutdown();
                    return;
                // Diagnostics: draws every page of the window into PNGs, off screen and without changing anything.
                case "--snapshot" when path != null:
                    Snapshot.Run(path, this);
                    return;
                // Imports a folder, .zip or .ddw as an image package and writes what was suggested; does not switch to
                // it. (--import alone belongs to the settings, see KitSettings.)
                case "--import-package" when path != null && args.Length > 2:
                    File.WriteAllText(args[2], Diagnostics.ImportReport(path), Encoding.UTF8);
                    Shutdown();
                    return;
                // The gallery: --gallery <report> lists it, --gallery-add <id> <report> adds one package from it.
                case "--gallery" when path != null:
                    File.WriteAllText(path, Diagnostics.GalleryReport(), Encoding.UTF8);
                    Shutdown();
                    return;
                case "--gallery-add" when path != null && args.Length > 2:
                    File.WriteAllText(args[2], Diagnostics.GalleryAddReport(path), Encoding.UTF8);
                    Shutdown();
                    return;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{mode} failed", ex);
            Shutdown(1);
            return;
        }

        instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            SingleInstance.HandOver(mode switch
            {
                "--autostart" => "refresh", "--next" => "next", "--switch-mode" => "switch-mode", "--jump-mode" => "jump-mode",
                "--switch-on" => "switch-on", _ => "show",
            });
            Shutdown();
            return;
        }
        // Switched on from the command line (deployment, tests) before anything is applied.
        if (mode == "--switch-on") KitIntro.SwitchOn();

        KitDiagnostics.Start(args);
        // Only does something when the setup brought a package not added yet (the first start): then it has to come
        // before the first plan, so that plan already has a background.
        AddBundledPackages();
        System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);
        // The background and the color mode first (TrayHost resolves before the tray exists); what nobody waits for at
        // sign-in comes after, when the window's thread is idle.
        host = new TrayHost(showWindow: mode is "" or "--show");
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
        {
            if (!DayEngine.DryRun)
            {
                Family.Register("show", "refresh", "status", "next");
                Policies.EnforceAutostart();
                // Profiles changed in another app of the family arrive at once.
                FamilyProfiles.Watch();
            }
            // After the tray exists: "Updated to ..." after --updated, then the daily look on GitHub when it is on.
            KitUpdate.Start(args);
        });
    }

    /// <summary>
    /// The packages the setup brings, once each; the first becomes the background when none is chosen, so DynaTones
    /// shows something from the first start.
    /// </summary>
    static void AddBundledPackages()
    {
        try
        {
            if (Gallery.AddBundled(AppContext.BaseDirectory) is { } id && string.IsNullOrEmpty(ConfigStore.Load().Wallpaper.PackageId))
                ConfigStore.Update(c => c.Wallpaper.PackageId = id);
        }
        catch (Exception ex)
        {
            Log.Warning($"Packages from the setup could not be added: {ex.Message}");
        }
    }

    /// <summary>
    /// Uninstalling gives back what DynaTones changed beyond the pictures: Windows spotlight on the lock screen, when
    /// DynaTones switched it off at the user's request. Wallpaper, lock screen picture and color mode stay as they are.
    /// </summary>
    static void GiveBack()
    {
        var lockScreen = ConfigStore.Load().LockScreen;
        if (lockScreen.TurnOffSpotlight) LockScreen.SetSpotlight(lockScreen.SpotlightBefore);
        // The sign-in screen goes in the kit's elevated pass after this (adminUninstall): the tasks go and the device's
        // values come back as they were.
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (host != null) KitDiagnostics.Stop();
        host?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
