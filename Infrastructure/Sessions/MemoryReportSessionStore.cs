using Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Sessions;

public sealed class MemoryReportSessionStore : IReportSessionStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);
    private readonly IMemoryCache _cache;

    public MemoryReportSessionStore(IMemoryCache cache) => _cache = cache;

    public void Save(ReportSession session)
    {
        _cache.Set(CacheKey(session.SessionId), session, Ttl);
    }

    public ReportSession? Get(string sessionId)
        => _cache.TryGetValue(CacheKey(sessionId), out ReportSession? session) ? session : null;

    private static string CacheKey(string sessionId) => $"report-session:{sessionId}";
}
