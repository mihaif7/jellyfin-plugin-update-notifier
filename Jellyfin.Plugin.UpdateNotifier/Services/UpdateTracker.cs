using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.UpdateNotifier.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UpdateNotifier.Services;

/// <summary>
/// Tracks plugin updates and persists them to the plugin data folder.
/// </summary>
public class UpdateTracker
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly ILogger<UpdateTracker> _logger;
    private readonly ConcurrentDictionary<Guid, string?> _preInstallVersions = new();
    private readonly object _stateLock = new();

    private TrackerState _state = new();
    private bool _loaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTracker"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public UpdateTracker(ILogger<UpdateTracker> logger)
    {
        _logger = logger;
    }

    private static string? StatePath
    {
        get
        {
            var dataFolder = UpdateNotifierPlugin.Instance?.DataFolderPath;
            return dataFolder is null ? null : Path.Combine(dataFolder, "updates.json");
        }
    }

    /// <summary>
    /// Records the version installed before an update overwrites it.
    /// </summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="oldVersion">The currently installed version, if any.</param>
    public void RecordPreInstall(Guid id, string? oldVersion)
    {
        _preInstallVersions[id] = oldVersion;
    }

    /// <summary>
    /// Records a completed install or update.
    /// </summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="name">The plugin name.</param>
    /// <param name="newVersion">The newly installed version.</param>
    /// <param name="changelog">The changelog, if supplied by the repository.</param>
    public void RecordUpdate(Guid id, string name, string newVersion, string? changelog)
    {
        _preInstallVersions.TryRemove(id, out var oldVersion);

        lock (_stateLock)
        {
            EnsureLoaded();
            _state.Updates.RemoveAll(r => r.Id.Equals(id));
            _state.Updates.Add(new PluginUpdateRecord
            {
                Id = id,
                Name = name,
                OldVersion = oldVersion,
                NewVersion = newVersion,
                Changelog = changelog,
                // Jellyfin's own isUpdate flag is only true when the same version
                // is reinstalled, so derive it from what was installed before.
                IsUpdate = oldVersion is not null
                    && !string.Equals(oldVersion, newVersion, StringComparison.Ordinal),
                TimestampUtc = DateTime.UtcNow,
            });

            // A new change re-arms a notice that was previously dismissed.
            _state.Dismissed = false;

            Save();
        }
    }

    /// <summary>
    /// Gets the recorded updates, newest first.
    /// </summary>
    /// <returns>The recorded updates.</returns>
    public IReadOnlyList<PluginUpdateRecord> GetUpdates()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            return _state.Updates.OrderByDescending(r => r.TimestampUtc).ToList();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the notification has been dismissed.
    /// </summary>
    /// <returns><c>true</c> if dismissed.</returns>
    public bool IsDismissed()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            return _state.Dismissed;
        }
    }

    /// <summary>
    /// Marks the current set of updates as dismissed.
    /// </summary>
    public void Dismiss()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            _state.Dismissed = true;
            Save();
        }
    }

    /// <summary>
    /// Clears all recorded updates. Called once the server has restarted.
    /// </summary>
    public void Clear()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            _state.Updates.Clear();
            _state.Dismissed = false;
            Save();
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var path = StatePath;
        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            _state = JsonSerializer.Deserialize<TrackerState>(json) ?? new TrackerState();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to read update state from {Path}", path);
            _state = new TrackerState();
        }
    }

    private void Save()
    {
        var path = StatePath;
        if (path is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_state, _jsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to write update state to {Path}", path);
        }
    }

    private sealed class TrackerState
    {
        public List<PluginUpdateRecord> Updates { get; set; } = new();

        public bool Dismissed { get; set; }
    }
}
