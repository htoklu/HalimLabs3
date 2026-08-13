using HalimLabs.Models;

namespace HalimLabs.Services.Abstractions;

public interface IChatHistoryRepository
{
    Task<IReadOnlyList<ChatSession>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ChatSession?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(ChatSession session, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
