using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Empostor.Api.Service;

/// <summary>
///     Abstract base class for JSON file-based data persistence.
///     Stores data under <c>./Data/{ClassName}Data.json</c>.
///     Subclasses only need to implement <see cref="GetSnapshot"/> and <see cref="ApplySnapshot"/>.
/// </summary>
/// <typeparam name="TData">The serializable data type.</typeparam>
public abstract class JsonDataStore<TData> : IDisposable
{
    /// <summary>
    ///     JSON serialization options used for save/load.
    ///     Override in subclass constructor to customize (e.g. add <see cref="JsonSerializerOptions.DefaultIgnoreCondition"/>).
    /// </summary>
    protected JsonSerializerOptions JsonOpts { get; } = new() { WriteIndented = true };

    /// <summary>
    ///     Full path to the data file.
    ///     Derived automatically from the subclass name: <c>./Data/{ClassName}Data.json</c>
    /// </summary>
    protected string FilePath { get; }

    /// <summary>
    ///     Semaphore used to prevent concurrent writes.
    /// </summary>
    protected SemaphoreSlim SaveLock { get; } = new(1, 1);

    /// <summary>
    ///     Logger instance for this store.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    ///     Creates a new instance.
    /// </summary>
    /// <param name="logger">Logger for this store.</param>
    /// <param name="legacyPath">
    ///     If provided and the legacy file exists but the new path does not,
    ///     the legacy file is automatically copied to the new path (one-time migration).
    /// </param>
    protected JsonDataStore(ILogger logger, string? legacyPath = null)
    {
        Logger = logger;

        // Derive file name from class name: "DiscordWebhookStore" → "DiscordWebhookData.json"
        var name = GetType().Name;
        if (name.EndsWith("Store", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - 5) + "Data";
        }

        FilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", name + ".json");

        // One-time legacy migration
        if (legacyPath != null && File.Exists(legacyPath) && !File.Exists(FilePath))
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(legacyPath, FilePath);
                Logger.LogInformation("{Name} migrated from {Legacy} to {New}", GetType().Name, legacyPath, FilePath);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "{Name} failed to migrate {Legacy}", GetType().Name, legacyPath);
            }
        }
    }

    /// <summary>
    ///     Build a serializable snapshot from the current in-memory state.
    /// </summary>
    protected abstract TData GetSnapshot();

    /// <summary>
    ///     Restore in-memory state from a previously-saved snapshot.
    /// </summary>
    protected abstract void ApplySnapshot(TData data);

    /// <summary>
    ///     Load data from the file on disk. If the file does not exist or fails to parse,
    ///     the store remains in its default (constructor-initialized) state.
    /// </summary>
    protected void Load()
    {
        if (!File.Exists(FilePath))
        {
            Logger.LogDebug("{Name} no data file at {Path}, using defaults", GetType().Name, FilePath);
            return;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var data = JsonSerializer.Deserialize<TData>(json, JsonOpts);
            if (data != null)
            {
                ApplySnapshot(data);
                Logger.LogInformation("{Name} loaded from {Path}", GetType().Name, FilePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "{Name} failed to load {Path}, using defaults", GetType().Name, FilePath);
        }
    }

    /// <summary>
    ///     Save the current state to disk asynchronously, with a non-blocking write lock.
    ///     If a save is already in progress, this call is silently skipped.
    /// </summary>
    protected async ValueTask SaveAsync()
    {
        if (!await SaveLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var snapshot = GetSnapshot();
            var json = JsonSerializer.Serialize(snapshot, JsonOpts);
            await File.WriteAllTextAsync(FilePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "{Name} failed to save {Path}", GetType().Name, FilePath);
        }
        finally
        {
            SaveLock.Release();
        }
    }

    /// <summary>
    ///     Fire-and-forget save. Schedules a background save and returns immediately.
    ///     Useful for high-frequency writes where waiting is not needed.
    /// </summary>
    protected void SaveFireAndForget()
    {
        _ = Task.Run(SaveAsyncAsTask);

        async Task SaveAsyncAsTask()
        {
            if (!await SaveLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var snapshot = GetSnapshot();
                var json = JsonSerializer.Serialize(snapshot, JsonOpts);
                await File.WriteAllTextAsync(FilePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "{Name} failed to save {Path}", GetType().Name, FilePath);
            }
            finally
            {
                SaveLock.Release();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SaveLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
