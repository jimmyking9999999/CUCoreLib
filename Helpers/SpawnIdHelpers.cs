namespace CUCoreLib.Helpers;

internal static class SpawnIdHelpers
{
    internal static string NormalizeSpawnId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;

        var normalized = id.Trim();
        var separator = normalized.IndexOf('$');
        if (separator > 0) normalized = normalized.Substring(0, separator);

        return normalized;
    }
}