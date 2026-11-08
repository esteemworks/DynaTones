namespace DynaTones.Core;

/// <summary>
/// DynaTones' own ids in the Windows event log, beside the family's in <see cref="KitEvents"/>:
/// 1100s what went well, 2100s warnings, 3100s errors.
/// </summary>
public static class Events
{
    public const int ManagedPackageImported = 1100;
    public const int ManagedPackageMissing = 2100;
    public const int WallpaperFailed = 3100;
    public const int ManagedPackageFailed = 3101;
    public const int ResolveFailed = 3102;
}
