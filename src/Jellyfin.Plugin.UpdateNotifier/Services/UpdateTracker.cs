using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.UpdateNotifier.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UpdateNotifier.Services;

/// <summary>
/// Tracks plugin updates and persists them to the plugin data folder.
/// </summary>
public class UpdateTracker
{
    /// <summary>
    /// The number of archived records kept. Old enough to answer "what changed
    /// before this broke", small enough that the file stays trivial to read.
    /// </summary>
    private const int HistoryLimit = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new PluginChangeKindConverter() },
    };

    private readonly ILogger<UpdateTracker> _logger;
    private readonly ConcurrentDictionary<Guid, string?> _preInstallVersions = new();
    private readonly object _stateLock = new();

    private TrackerState _state = new();
    private bool _loaded;
    private bool _archived;

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
    /// <param name="sourceUrl">The URL the package was installed from, if known.</param>
    public void RecordUpdate(Guid id, string name, string newVersion, string? changelog, string? sourceUrl)
    {
        _preInstallVersions.TryRemove(id, out var oldVersion);

        lock (_stateLock)
        {
            EnsureLoaded();
            Relabel(id, name);

            // A record is the net change since the last restart, so an install
            // replacing one from this same window keeps the older version.
            var pending = _state.Updates.Find(r => r.Id.Equals(id));
            if (pending is not null)
            {
                oldVersion = pending.OldVersion;
            }

            _state.Updates.RemoveAll(r => r.Id.Equals(id));
            _state.Updates.Add(new PluginUpdateRecord
            {
                Id = id,
                Name = name,
                OldVersion = oldVersion,
                NewVersion = newVersion,
                Changelog = changelog,
                SourceUrl = sourceUrl,
                // Jellyfin's own isUpdate flag is only true when the same version
                // is reinstalled, so derive it from what was installed before.
                Kind = oldVersion is not null
                    && !string.Equals(oldVersion, newVersion, StringComparison.Ordinal)
                    ? PluginChangeKind.Updated
                    : PluginChangeKind.Installed,
                TimestampUtc = DateTime.UtcNow,
            });

            // A new change re-arms a notice that was previously dismissed.
            _state.Dismissed = false;

            Save();
        }
    }

    /// <summary>
    /// Records a plugin removal, which leaves a restart pending just as an
    /// install does.
    /// </summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="name">The plugin name.</param>
    /// <param name="version">The version that was removed.</param>
    public void RecordUninstall(Guid id, string name, string version)
    {
        _preInstallVersions.TryRemove(id, out _);

        lock (_stateLock)
        {
            EnsureLoaded();

            var displayName = RecordedName(id) ?? name;
            Relabel(id, displayName);

            // A plugin that arrived and left inside one window was never live,
            // so the net effect is nothing and the record goes with it.
            var pending = _state.Updates.Find(r => r.Id.Equals(id));
            var wasLive = pending is null || pending.OldVersion is not null;

            // What is gone is the version the server was running, not the one
            // just deleted from disk if it was also updated inside this window.
            var liveVersion = pending?.OldVersion ?? version;

            _state.Updates.RemoveAll(r => r.Id.Equals(id));

            if (wasLive)
            {
                _state.Updates.Add(new PluginUpdateRecord
                {
                    Id = id,
                    Name = displayName,
                    OldVersion = liveVersion,
                    NewVersion = liveVersion,
                    Kind = PluginChangeKind.Removed,
                    TimestampUtc = DateTime.UtcNow,
                });

                _state.Dismissed = false;
            }

            Save();
        }
    }

    /// <summary>
    /// Gets the recorded updates, newest first.
    /// </summary>
    /// <remarks>
    /// Copies, because the caller serialises the result after the lock is gone
    /// and a concurrent install relabels the records held here.
    /// </remarks>
    /// <returns>The recorded updates.</returns>
    public IReadOnlyList<PluginUpdateRecord> GetUpdates()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            return _state.Updates.OrderByDescending(r => r.TimestampUtc).Select(r => r.Copy()).ToList();
        }
    }

    /// <summary>
    /// Gets the number of recorded updates.
    /// </summary>
    /// <returns>The number of recorded updates.</returns>
    public int GetUpdateCount()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            return _state.Updates.Count;
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
    /// Gets the archived records, newest first.
    /// </summary>
    /// <remarks>Copies, for the reason <see cref="GetUpdates"/> gives.</remarks>
    /// <returns>The archived records.</returns>
    public IReadOnlyList<PluginUpdateRecord> GetHistory()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            return _state.History
                .OrderByDescending(r => r.RestartedAtUtc ?? r.TimestampUtc)
                .Select(r => r.Copy())
                .ToList();
        }
    }

    /// <summary>
    /// Gets the number of archived records.
    /// </summary>
    /// <returns>The number of archived records.</returns>
    public int GetHistoryCount()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            return _state.History.Count;
        }
    }

    /// <summary>
    /// Discards the archived records.
    /// </summary>
    public void ClearHistory()
    {
        lock (_stateLock)
        {
            EnsureLoaded();
            _state.History.Clear();
            Save();
        }
    }

    /// <summary>
    /// Moves the pending records into the history. Called once the server has
    /// restarted, which is the moment those changes became live.
    /// </summary>
    /// <remarks>
    /// Runs once per server start: the startup task can also be run by hand from
    /// Scheduled Tasks, and nothing pending then has gone live.
    /// </remarks>
    public void ArchiveCurrent()
    {
        lock (_stateLock)
        {
            if (_archived)
            {
                return;
            }

            _archived = true;
            EnsureLoaded();

            if (_state.Updates.Count > 0)
            {
                var restartedAt = DateTime.UtcNow;
                foreach (var record in _state.Updates)
                {
                    record.RestartedAtUtc = restartedAt;
                }

                // Newest first, so trimming the tail drops the oldest.
                _state.History.InsertRange(0, _state.Updates.OrderByDescending(r => r.TimestampUtc));
                if (_state.History.Count > HistoryLimit)
                {
                    _state.History.RemoveRange(HistoryLimit, _state.History.Count - HistoryLimit);
                }

                _state.Updates.Clear();
            }

            _state.Dismissed = false;
            Save();
        }
    }

    /// <summary>
    /// Gets every record held for one plugin, pending before archived.
    /// </summary>
    /// <remarks>Callers hold <see cref="_stateLock"/>.</remarks>
    /// <param name="id">The plugin id.</param>
    /// <returns>The records for that plugin.</returns>
    private IEnumerable<PluginUpdateRecord> RecordsFor(Guid id)
        => _state.Updates.Concat(_state.History).Where(r => r.Id.Equals(id));

    /// <summary>
    /// Gets the name this plugin is already recorded under, if any.
    /// </summary>
    /// <remarks>Callers hold <see cref="_stateLock"/>.</remarks>
    /// <param name="id">The plugin id.</param>
    /// <returns>The recorded name, or <c>null</c>.</returns>
    private string? RecordedName(Guid id) => RecordsFor(id).FirstOrDefault()?.Name;

    /// <summary>
    /// Points every record for a plugin at one name.
    /// </summary>
    /// <remarks>
    /// A plugin can arrive under two names: the manifest names it on install,
    /// while an uninstall reports the name it compiled into itself, and a plugin
    /// may rename itself between versions. The newest manifest name wins and the
    /// older records follow it, so a rename carries through the whole history.
    /// Callers hold <see cref="_stateLock"/>.
    /// </remarks>
    /// <param name="id">The plugin id.</param>
    /// <param name="name">The name to apply.</param>
    private void Relabel(Guid id, string name)
    {
        foreach (var record in RecordsFor(id))
        {
            record.Name = name;
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
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            _state = new TrackerState
            {
                Updates = ReadRecords(root, "Updates"),
                History = ReadRecords(root, "History"),
                Dismissed = root.TryGetProperty("Dismissed", out var dismissed)
                    && dismissed.ValueKind == JsonValueKind.True,
            };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to read update state from {Path}", path);
            _state = new TrackerState();
            SetAside(path);
        }
    }

    /// <summary>
    /// Reads one array of records, skipping any the current version cannot map.
    /// </summary>
    /// <param name="root">The root object of the state file.</param>
    /// <param name="property">The property holding the array.</param>
    /// <returns>The records that could be read.</returns>
    private List<PluginUpdateRecord> ReadRecords(JsonElement root, string property)
    {
        var records = new List<PluginUpdateRecord>();
        if (!root.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return records;
        }

        foreach (var element in array.EnumerateArray())
        {
            try
            {
                var record = element.Deserialize<PluginUpdateRecord>(_jsonOptions);
                if (record is not null)
                {
                    records.Add(record);
                }
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Skipped an unreadable record in {Property}", property);
            }
        }

        return records;
    }

    /// <summary>
    /// Keeps an unreadable state file instead of letting the next write replace it.
    /// </summary>
    /// <param name="path">The path of the unreadable file.</param>
    private void SetAside(string path)
    {
        try
        {
            var kept = path + ".unreadable";
            File.Move(path, kept, overwrite: true);
            _logger.LogInformation("Kept the unreadable state file as {Path}", kept);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not set the unreadable state file aside");
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

    /// <summary>
    /// Reads a <see cref="PluginChangeKind"/> without throwing on a value this
    /// version does not know, which is what a file written by a newer one holds
    /// after a downgrade.
    /// </summary>
    private sealed class PluginChangeKindConverter : JsonConverter<PluginChangeKind>
    {
        /// <inheritdoc />
        public override PluginChangeKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<PluginChangeKind>(reader.GetString(), true, out var named)
                // TryParse also accepts a numeric string, so it can hand back
                // a value no member is named after.
                && Enum.IsDefined(named))
            {
                return named;
            }

            if (reader.TokenType == JsonTokenType.Number
                && reader.TryGetInt32(out var number)
                && Enum.IsDefined(typeof(PluginChangeKind), number))
            {
                return (PluginChangeKind)number;
            }

            // A composite token has to be consumed whole, or the serializer
            // faults the whole record rather than just the kind.
            reader.Skip();
            return PluginChangeKind.Installed;
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, PluginChangeKind value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(value.ToString());
        }
    }

    private sealed class TrackerState
    {
        public List<PluginUpdateRecord> Updates { get; set; } = new();

        public List<PluginUpdateRecord> History { get; set; } = new();

        public bool Dismissed { get; set; }
    }
}
