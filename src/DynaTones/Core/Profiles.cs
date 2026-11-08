namespace DynaTones.Core;

/// <summary>The family's profiles as DynaTones uses them (EsteemWorks.Kit FamilyProfiles, app name "DynaTones").</summary>
public static class Profiles
{
    public const string App = ProfileOverrides.App;

    /// <summary>The profiles DynaTones uses, in order of precedence.</summary>
    public static List<FamilyProfile> All => FamilyProfiles.Visible(App);

    /// <summary>The profile that applies at a moment; null: none.</summary>
    public static FamilyProfile? Active(DateTime at)
    {
        try
        {
            return FamilyProfiles.Active(App, at);
        }
        catch (Exception ex)
        {
            Log.Debug($"Profiles could not be read: {ex.Message}", "Profiles");
            return null;
        }
    }
}
