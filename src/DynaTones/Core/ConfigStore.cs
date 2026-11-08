namespace DynaTones.Core;

public static class ConfigStore
{
    static readonly object Gate = new();

    /// <summary>
    /// The settings as DynaTones works with them: the user's own, with new recommendations of the organization
    /// saved into them once and fixed values laid over them (never saved), see <see cref="Policies"/>.
    /// </summary>
    public static Config Load()
    {
        lock (Gate)
        {
            var config = LoadOwn();
            if (Policies.ApplyRecommendations(config)) Save(config);
            Policies.ApplyFixed(config);
            return config;
        }
    }

    /// <summary>The user's own settings as saved, without fixed policies.</summary>
    static Config LoadOwn()
    {
        lock (Gate)
        {
            try
            {
                return Json.Read<Config>(Paths.Config) ?? new Config();
            }
            catch (Exception ex)
            {
                // Kept under backups, logged and shown under About with the last backup to go back to; meanwhile the
                // defaults, saved only when the user changes something.
                KitSettings.ReportUnreadable(Paths.Config, ex);
                return new Config();
            }
        }
    }

    public static void Save(Config config)
    {
        lock (Gate) Json.Write(Paths.Config, config);
    }

    public static Config Update(Action<Config> change)
    {
        lock (Gate)
        {
            // Changed and saved without the fixed policies, so they never end up in the user's own settings.
            var config = LoadOwn();
            change(config);
            Save(config);
            return Load();
        }
    }
}
