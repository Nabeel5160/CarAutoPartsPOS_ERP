using System.Text.Json;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;

namespace CarAutoParts.Infrastructure.Services;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly ApplicationDbContext _db;

    public OutboxWriter(ApplicationDbContext db) => _db = db;

    public void Enqueue(string type, object payload)
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = type,
            Payload = JsonSerializer.Serialize(payload),
            OccurredAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        });
    }
}
