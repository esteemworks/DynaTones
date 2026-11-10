using System.Diagnostics;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;

namespace DynaTones.Core;

/// <summary>
/// The screen before anyone signs in, which shows the device's lock screen picture. Windows lets only the device set
/// it (the Personalization CSP under HKLM), so this belongs to the installation with admin rights:
/// <list type="bullet">
/// <item>The admin tier sets up two folders under %ProgramData%\EsteemWorks\DynaTones: <c>signin</c>, written only by
/// SYSTEM and administrators, with the script <c>apply.ps1</c>, the image in use and <c>status.json</c>; and
/// <c>signin-request</c>, where DynaTones running for the user leaves the pictures of the next 36 hours and a small
/// plan.</item>
/// <item>Two tasks run the script as SYSTEM: at start, before anyone signs in, and on request, which DynaTones starts
/// whenever the lock screen picture changes. The script picks the plan's picture for now, checks it (a plain JPEG or
/// PNG, one name only, no link), copies it under a new name and sets the CSP values.</item>
/// </list>
/// The task never runs DynaTones itself: installed per user, its program folder is the user's to change, and SYSTEM
/// must not run what a user can replace. Windows takes the picture over the next time the screen is locked, the user
/// signs out or the PC starts (sandbox, 07.11.2026); the script also writes what Windows last made of it, so
/// <c>--status</c> shows whether it worked on this edition.
/// </summary>
public static class SignIn
{
    public const string InstallCommand = "--signin-install";
    public const string UninstallCommand = "--signin-uninstall";
    const string TaskFolder = "EsteemWorks";
    const string TaskStart = "DynaTones sign-in screen at start";
    const string TaskNow = "DynaTones sign-in screen now";
    const string CspKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP";
    static readonly string[] CspValues = ["LockScreenImagePath", "LockScreenImageUrl", "LockScreenImageStatus"];

    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EsteemWorks", "DynaTones");
    public static string Protected => Path.Combine(Root, "signin");
    public static string Requests => Path.Combine(Root, "signin-request");
    static string Script => Path.Combine(Protected, "apply.ps1");
    static string Before => Path.Combine(Protected, "before.json");
    public static string StatusFile => Path.Combine(Protected, "status.json");

    static DateTime checkedAt;
    static bool installed;

    /// <summary>Whether the admin tier set it up; looked at again at most every five minutes.</summary>
    public static bool IsInstalled
    {
        get
        {
            if (DateTime.Now - checkedAt < TimeSpan.FromMinutes(5)) return installed;
            checkedAt = DateTime.Now;
            installed = File.Exists(Script) && Directory.Exists(Requests) && TaskExists(TaskNow);
            return installed;
        }
    }

    /// <summary>Looks again at the next <see cref="IsInstalled"/>, after setting it up.</summary>
    public static void Recheck() => checkedAt = default;

    /// <summary>Whether a device lock screen picture is the one DynaTones' task set.</summary>
    public static bool IsOurs(string path) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(Protected) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the screen before sign-in shows DynaTones' picture now.</summary>
    public static bool Showing
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(CspKey);
                return key?.GetValue("LockScreenImagePath") is string path && path.Length > 0 && IsOurs(path);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Whether the script the task runs is the one this version brings; an older one (set up by an earlier version)
    /// is set up again with one UAC prompt, from the lock screen page.
    /// </summary>
    public static bool ScriptCurrent
    {
        get
        {
            try
            {
                return File.Exists(Script) && File.ReadAllText(Script).Trim() == ApplyScript.Trim();
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Switched off: the task gives the device's own values back, as they were before DynaTones, and the screen
    /// before sign-in shows Windows' picture again. Only when DynaTones' picture is there; returns a problem or null.
    /// </summary>
    public static string? Withdraw()
    {
        if (!Showing) return null;
        try
        {
            const string plan = "{\"off\":true}";
            if (plan == lastPlan) return null;
            File.WriteAllText(Path.Combine(Requests, "plan.json"), plan);
            Service().GetFolder("\\" + TaskFolder).GetTask(TaskNow).Run(null);
            lastPlan = plan;
            Log.Info("Sign-in screen: given back to Windows");
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning($"Sign-in screen: giving back failed: {ex.Message}");
            return ex.Message;
        }
    }

    public static bool IsElevated => KitApp.IsElevated;

    // ---------- Setup (admin) ----------

    /// <summary>From the setup's admin tier: sets it up here when elevated, else through one elevated helper run.</summary>
    public static void Setup()
    {
        if (IsElevated) Install();
        else RunElevated(InstallCommand);
    }

    /// <summary>Something of it is set up (folders or the task), so uninstalling has to take it away with admin rights.</summary>
    public static bool NeedsRemoval() => Directory.Exists(Protected) || TaskExists(TaskNow);

    /// <summary>Takes it away again, elevated, but only when it was set up.</summary>
    public static void Remove()
    {
        if (!NeedsRemoval()) return;
        if (IsElevated) Uninstall();
        else RunElevated(UninstallCommand);
    }

    /// <summary>Through the kit, so each prompt is counted in the log ("Elevated start"); declined, the rest works on.</summary>
    static void RunElevated(string command) => KitElevation.Run(command);

    public static void Install()
    {
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        const InheritanceFlags both = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        // signin: SYSTEM and administrators write, users only read.
        var safe = new DirectorySecurity();
        safe.SetAccessRuleProtection(true, false);
        safe.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, both, PropagationFlags.None, AccessControlType.Allow));
        safe.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, both, PropagationFlags.None, AccessControlType.Allow));
        safe.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute, both, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(Root);
        new DirectoryInfo(Protected).Create(safe);
        new DirectoryInfo(Protected).SetAccessControl(safe);

        // signin-request: users add and change files inside, but cannot remove or swap the folder itself.
        var open = new DirectorySecurity();
        open.SetAccessRuleProtection(true, false);
        open.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, both, PropagationFlags.None, AccessControlType.Allow));
        open.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, both, PropagationFlags.None, AccessControlType.Allow));
        open.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute | FileSystemRights.CreateFiles, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
        open.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.Modify, both, PropagationFlags.InheritOnly, AccessControlType.Allow));
        new DirectoryInfo(Requests).Create(open);
        new DirectoryInfo(Requests).SetAccessControl(open);

        // What was there before, to give it back on uninstall; only the first time.
        if (!File.Exists(Before))
        {
            using var key = Registry.LocalMachine.OpenSubKey(CspKey);
            var before = new Dictionary<string, object?> { ["existed"] = key != null };
            foreach (var name in CspValues) before[name] = key?.GetValue(name)?.ToString();
            File.WriteAllText(Before, JsonSerializer.Serialize(before));
        }
        File.WriteAllText(Script, ApplyScript);

        var action = $"""
            <Actions Context="Author">
              <Exec>
                <Command>%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe</Command>
                <Arguments>-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "{SecurityElement.Escape(Script)}"</Arguments>
              </Exec>
            </Actions>
            """;
        RegisterTask(TaskStart, TaskXml("<Triggers><BootTrigger><Enabled>true</Enabled></BootTrigger></Triggers>", action,
            "DynaTones: sets the picture of the screen before sign-in for this time of day."), null);
        // Users may start this one (read and run), so DynaTones can ask for a new picture without admin rights.
        RegisterTask(TaskNow, TaskXml("", action, "DynaTones: sets the picture of the screen before sign-in, on request."),
            "D:(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;AU)");
        Log.Event(KitEvents.SetUp, LogLevel.Info, "Sign-in screen set up (admin tier).");
        checkedAt = default;
    }

    public static void Uninstall()
    {
        DeleteTask(TaskStart);
        DeleteTask(TaskNow);
        try
        {
            var before = File.Exists(Before) ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(Before)) : null;
            if (before == null || !before.TryGetValue("existed", out var existed) || !existed.GetBoolean())
                Registry.LocalMachine.DeleteSubKeyTree(CspKey, false);
            else
            {
                using var key = Registry.LocalMachine.CreateSubKey(CspKey);
                foreach (var name in CspValues)
                {
                    var value = before.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    if (value == null) key.DeleteValue(name, false);
                    else if (name == "LockScreenImageStatus") key.SetValue(name, int.Parse(value), RegistryValueKind.DWord);
                    else key.SetValue(name, value);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Giving back the device lock screen values failed: {ex.Message}");
        }
        foreach (var folder in new[] { Protected, Requests })
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                Log.Warning($"Removing {folder} failed: {ex.Message}");
            }
        }
        Log.Info("Sign-in screen removed");
    }

    // ---------- Asking for a picture (user) ----------

    static string? lastPlan;
    static readonly Dictionary<(string, DateTime), string> hashes = [];

    /// <summary>
    /// Leaves the pictures of <paramref name="timeline"/> (from when each shows) and the plan in the request folder and
    /// starts the task, when anything changed since the last time. Returns a problem in a few words, or null.
    /// </summary>
    public static string? Push(IReadOnlyList<(DateTime From, string Image)> timeline)
    {
        if (timeline.Count == 0) return null;
        try
        {
            var entries = new List<Dictionary<string, string>>();
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (from, image) in timeline)
            {
                var ext = Path.GetExtension(image).ToLowerInvariant() is ".png" ? ".png" : ".jpg";
                var name = HashOf(image) + ext;
                var target = Path.Combine(Requests, name);
                if (!File.Exists(target)) File.Copy(image, target);
                keep.Add(name);
                entries.Add(new() { ["from"] = from.ToString("yyyy-MM-ddTHH:mm:ss"), ["file"] = name });
            }
            var plan = JsonSerializer.Serialize(new { entries });
            if (plan == lastPlan) return null;
            File.WriteAllText(Path.Combine(Requests, "plan.json"), plan);
            foreach (var old in Directory.GetFiles(Requests).Where(f => !keep.Contains(Path.GetFileName(f)) && !f.EndsWith("plan.json")))
            {
                try
                {
                    File.Delete(old);
                }
                catch
                {
                    // Left for the next time.
                }
            }
            Service().GetFolder("\\" + TaskFolder).GetTask(TaskNow).Run(null);
            lastPlan = plan;
            Log.Debug($"Sign-in screen: plan of {entries.Count} pictures handed over", "SignIn");
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning($"Sign-in screen: handing over failed: {ex.Message}");
            return ex.Message;
        }
    }

    static string HashOf(string path)
    {
        var stamp = File.GetLastWriteTimeUtc(path);
        if (hashes.TryGetValue((path, stamp), out var known)) return known;
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()[..32];
        hashes[(path, stamp)] = hash;
        return hash;
    }

    /// <summary>What the script last wrote: when, which picture, on which edition, and what Windows made of it.</summary>
    public static string? StatusText()
    {
        try
        {
            return File.Exists(StatusFile) ? File.ReadAllText(StatusFile).Trim() : null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// True when the task has set a picture but Windows has never made its own copy of a device picture. On an edition
    /// that leaves the device picture out (seen on Windows 11 Pro, 07.11.2026: nothing taken over at locking), this
    /// stays true; elsewhere it clears at the next lock or start.
    /// </summary>
    public static bool NotShownYet()
    {
        try
        {
            if (!File.Exists(StatusFile)) return false;
            using var doc = JsonDocument.Parse(File.ReadAllText(StatusFile));
            var r = doc.RootElement;
            var set = r.TryGetProperty("set", out var s) && s.ValueKind == JsonValueKind.String;
            var taken = r.TryGetProperty("windows", out var w) && w.ValueKind == JsonValueKind.Object;
            return set && !taken;
        }
        catch
        {
            return false;
        }
    }

    // ---------- Task Scheduler ----------

    static string TaskXml(string triggers, string action, string description) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo><Description>{SecurityElement.Escape(description)}</Description></RegistrationInfo>
          {triggers}
          <Principals>
            <Principal id="Author">
              <UserId>S-1-5-18</UserId>
              <RunLevel>HighestAvailable</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>Queue</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <ExecutionTimeLimit>PT5M</ExecutionTimeLimit>
            <Hidden>true</Hidden>
          </Settings>
          {action}
        </Task>
        """;

    const int CreateOrUpdate = 6;
    const int ServiceAccount = 5;

    static void RegisterTask(string name, string xml, string? sddl)
    {
        dynamic service = Service();
        dynamic folder;
        try
        {
            folder = service.GetFolder("\\" + TaskFolder);
        }
        catch
        {
            folder = service.GetFolder("\\").CreateFolder(TaskFolder, null);
        }
        dynamic task = folder.RegisterTask(name, xml, CreateOrUpdate, "SYSTEM", null, ServiceAccount, sddl);
        // Replacing a task keeps its old security descriptor (kit lesson, 06.11.2026): set it again.
        if (sddl != null) task.SetSecurityDescriptor(sddl, 0);
    }

    static bool TaskExists(string name)
    {
        try
        {
            Service().GetFolder("\\" + TaskFolder).GetTask(name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void DeleteTask(string name)
    {
        try
        {
            Service().GetFolder("\\" + TaskFolder).DeleteTask(name, 0);
        }
        catch
        {
            // Not there.
        }
    }

    static dynamic Service()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new InvalidOperationException("Task Scheduler is not available.");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    // ---------- The script SYSTEM runs ----------

    /// <summary>
    /// Runs as SYSTEM with Windows PowerShell 5.1. Trusts nothing from the request folder: a plain file name from the
    /// plan, the file itself not a link and with one name only, a JPEG or PNG by its first bytes, at most 40 MB.
    /// </summary>
    const string ApplyScript = """
        # DynaTones: the picture of the screen before sign-in. Written by DynaTones' admin tier; runs as SYSTEM.
        $ErrorActionPreference = 'Stop'
        $root = Split-Path $PSScriptRoot
        $req = Join-Path $root 'signin-request'
        $images = Join-Path $PSScriptRoot 'images'
        $key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP'
        $nt = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
        $status = [ordered]@{ at = (Get-Date).ToString('s'); edition = $nt.EditionID; build = $nt.CurrentBuild; result = ''; image = $null; from = $null; set = $null; windows = $null }
        try { $status.set = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'status.json') -Raw | ConvertFrom-Json).set } catch { }
        try {
            $planFile = Join-Path $req 'plan.json'
            $pf = Get-Item -LiteralPath $planFile -Force
            if ($pf.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'the plan is a link' }
            if ($pf.Length -gt 65536) { throw 'the plan is too large' }
            $plan = Get-Content -LiteralPath $planFile -Raw | ConvertFrom-Json
            if ($plan.off -eq $true) {
                # Switched off in DynaTones: the device's own values as they were before, and no picture of ours left.
                $before = $null
                try { $before = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'before.json') -Raw | ConvertFrom-Json } catch { }
                if (-not $before -or -not $before.existed) { Remove-Item $key -Recurse -Force -ErrorAction SilentlyContinue }
                else {
                    foreach ($name in 'LockScreenImagePath', 'LockScreenImageUrl', 'LockScreenImageStatus') {
                        $value = $before.$name
                        if ($null -eq $value) { Remove-ItemProperty $key $name -ErrorAction SilentlyContinue }
                        elseif ($name -eq 'LockScreenImageStatus') { Set-ItemProperty $key $name ([int]$value) -Type DWord }
                        else { Set-ItemProperty $key $name ([string]$value) }
                    }
                }
                Get-ChildItem $images -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                $status.result = 'off'
                $status.image = $null
                $status | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'status.json') -Encoding UTF8
                return
            }
            $now = Get-Date
            $sorted = @($plan.entries | Sort-Object { [datetime]$_.from })
            $entry = @($sorted | Where-Object { [datetime]$_.from -le $now }) | Select-Object -Last 1
            if (-not $entry) { $entry = $sorted | Select-Object -First 1 }
            $name = [string]$entry.file
            if ($name -notmatch '^[0-9a-f]{32}\.(jpg|png)$') { throw "not a picture name: $name" }
            $src = Join-Path $req $name
            $sf = Get-Item -LiteralPath $src -Force
            if ($sf.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'the picture is a link' }
            if ($sf.Length -lt 1000 -or $sf.Length -gt 40MB) { throw 'the picture has an odd size' }
            if (@(fsutil hardlink list $src 2>$null).Count -gt 1) { throw 'the picture has more than one name' }
            $head = New-Object byte[] 4
            $fs = [IO.File]::OpenRead($src); [void]$fs.Read($head, 0, 4); $fs.Close()
            $jpg = $head[0] -eq 0xFF -and $head[1] -eq 0xD8 -and $head[2] -eq 0xFF
            $png = $head[0] -eq 0x89 -and $head[1] -eq 0x50 -and $head[2] -eq 0x4E -and $head[3] -eq 0x47
            if (-not ($jpg -or $png)) { throw 'not a JPEG or PNG' }
            New-Item -ItemType Directory -Force $images | Out-Null
            $dest = Join-Path $images $name
            $current = (Get-ItemProperty $key -ErrorAction SilentlyContinue).LockScreenImagePath
            if ($current -ne $dest -or -not (Test-Path -LiteralPath $dest)) {
                Copy-Item -LiteralPath $src -Destination $dest -Force
                if (-not (Test-Path $key)) { New-Item $key -Force | Out-Null }
                Set-ItemProperty $key LockScreenImagePath $dest
                Set-ItemProperty $key LockScreenImageUrl $dest
                Set-ItemProperty $key LockScreenImageStatus 1 -Type DWord
                Get-ChildItem $images -File | Where-Object { $_.FullName -ne $dest } | Remove-Item -Force -ErrorAction SilentlyContinue
                $status.result = 'set'
                $status.set = (Get-Date).ToString('s')
            } else { $status.result = 'unchanged' }
            $status.image = $name
            $status.from = [string]$entry.from
        } catch { $status.result = 'error: ' + $_.Exception.Message }
        # Windows' own copy of the device lock screen picture, made the next time the screen is locked: proof that this
        # edition took it.
        $cache = Get-ChildItem 'C:\ProgramData\Microsoft\Windows\SystemData\S-1-5-18\ReadOnly' -Recurse -Filter 'LockScreen___*notdimmed.jpg' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($cache) { $status.windows = [ordered]@{ written = $cache.LastWriteTime.ToString('s'); size = $cache.Length; folder = $cache.Directory.Name } }
        $status | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'status.json') -Encoding UTF8
        """;
}
