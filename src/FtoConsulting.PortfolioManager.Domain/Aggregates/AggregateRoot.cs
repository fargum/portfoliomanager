using FtoConsulting.PortfolioManager.Domain.DomainEvents;

namespace FtoConsulting.PortfolioManager.Domain.Aggregates;

public abstract class AggregateRoot : Entities.BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}