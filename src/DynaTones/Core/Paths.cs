namespace DynaTones.Core;

public static class Paths
{
    public static string Data => KitApp.DataFolder;
    public static string Config => Path.Combine(Data, "config.json");
    public static string Packages => Path.Combine(Data, "packages");
    public static string Cache => Path.Combine(Data, "cache");
}
