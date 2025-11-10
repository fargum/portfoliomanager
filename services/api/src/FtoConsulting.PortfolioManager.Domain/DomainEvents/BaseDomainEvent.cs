namespace FtoConsulting.PortfolioManager.Domain.DomainEvents;

/// <summary>
/// Base class for domain events. domain events are useful for decoupling different parts of the domain model
/// and triggering side effects in response to state changes within aggregates. and also to keep track of changes for auditing or integration purposes.
/// Currently, this project does not use domain events, but this base class is provided for future extensibility.   
/// </summary>
public abstract class BaseDomainEvent : IDomainEvent
{
    protected BaseDomainEvent()
    {
        Id = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public DateTime OccurredOn { get; private set; }
}