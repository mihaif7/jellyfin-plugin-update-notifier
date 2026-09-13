using System;
using System.IO;

namespace Jellyfin.Plugin.UpdateNotifier.Helpers;

/// <summary>
/// Reads embedded resources shipped with the plugin.
/// </summary>
public static class EmbeddedResource
{
    /// <summary>
    /// Reads an embedded resource as text.
    /// </summary>
    /// <param name="relativeName">
    /// The resource path relative to the assembly root namespace, using
    /// <c>.</c> as the folder separator (for example <c>Web.inject.js</c>).
    /// </param>
    /// <returns>The resource text, or <c>null</c> when missing.</returns>
    public static string? Read(string relativeName)
    {
        var assembly = typeof(EmbeddedResource).Assembly;

        // Match by suffix so the lookup does not depend on this helper's own
        // namespace: the resource is named after the root namespace and its
        // folder path, regardless of where this class lives.
        var suffix = "." + relativeName;
        var name = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith(suffix, StringComparison.Ordinal));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
