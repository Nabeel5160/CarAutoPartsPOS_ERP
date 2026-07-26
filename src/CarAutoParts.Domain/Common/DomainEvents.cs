namespace CarAutoParts.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}

public abstract class DomainEventBase : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

/// <summary>Aggregate root that collects domain events.</summary>
public abstract class AggregateRoot : CompanyEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
