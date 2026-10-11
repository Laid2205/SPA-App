using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommentsApp.Api.Events;

public interface IDomainEvent { }

public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
	Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}

public interface IEventPublisher
{
	Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent;
}

public sealed class EventPublisher : IEventPublisher
{
	private readonly IServiceProvider _services;
	private readonly ILogger<EventPublisher> _logger;

	public EventPublisher(IServiceProvider services, ILogger<EventPublisher> logger)
	{
		_services = services;
		_logger = logger;
	}

	public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
		where TEvent : IDomainEvent
	{
		foreach (var handler in _services.GetServices<IEventHandler<TEvent>>())
		{
			try
			{
				await handler.HandleAsync(domainEvent, ct);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Handler {Handler} failed for event {Event}",
					handler.GetType().Name, typeof(TEvent).Name);
			}
		}
	}
}