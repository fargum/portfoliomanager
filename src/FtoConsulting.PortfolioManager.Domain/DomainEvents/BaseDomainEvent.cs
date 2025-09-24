namespace FtoConsulting.PortfolioManager.Domain.DomainEvents;

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