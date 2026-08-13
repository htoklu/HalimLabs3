using System.IO;
using System.Text.Json;
using HalimLabs.Models;
using HalimLabs.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace HalimLabs.Services.Persistence;

public sealed class ChatHistoryRepository : IChatHistoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _folder;
    private readonly ILogger<ChatHistoryRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ChatHistoryRepository(ILogger<ChatHistoryRepository> logger)
    {
        _logger = logger;
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HalimLabs",
            "chats");
        Directory.CreateDirectory(_folder);
    }

    public async Task<IReadOnlyList<ChatSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sessions = new List<ChatSession>();
            foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
            {
                try
                {
                    await using var stream = File.OpenRead(file);
                    var session = await JsonSerializer.DeserializeAsync<ChatSession>(stream, JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                    if (session is not null)
                        sessions.Add(session);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipping corrupt chat file {File}", file);
                }
            }

            return sessions
                .OrderByDescending(s => s.UpdatedAt)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChatSession?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
            return null;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ChatSession>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat {Id}", id);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        session.UpdatedAt = DateTimeOffset.Now;
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stream = File.Create(GetPath(session.Id));
            await JsonSerializer.SerializeAsync(stream, session, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = GetPath(id);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(file);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetPath(Guid id) => Path.Combine(_folder, $"{id:N}.json");
}
