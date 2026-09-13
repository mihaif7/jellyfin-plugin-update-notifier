using System;
using System.IO;
using System.Reflection;

namespace Jellyfin.Plugin.UpdateNotifier;

/// <summary>
/// Reads embedded resources shipped with the plugin.
/// </summary>
public static class EmbeddedResource
{
    /// <summary>
    /// Reads an embedded resource as text.
    /// </summary>
    /// <param name="relativeName">The resource name relative to the root namespace.</param>
    /// <returns>The resource text, or <c>null</c> when missing.</returns>
    public static string? Read(string relativeName)
    {
        var assembly = typeof(EmbeddedResource).Assembly;
        var name = $"{typeof(EmbeddedResource).Namespace}.{relativeName}";
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
