# Infrastructure Test Coverage

← [Test Coverage](README.md)

> Tests for shared infrastructure components — message broker integration.

---

## `MassTransitEventPublisherTests` (2 tests)

Tests `MassTransitEventPublisher`, the `IEventPublisher` implementation that wraps MassTransit's `IPublishEndpoint`.

| Test                                     | What It Verifies                                                  | Expected Outcome                               |
| ---------------------------------------- | ----------------------------------------------------------------- | ---------------------------------------------- |
| `PublishAsync_CallsPublishEndpoint`      | The publisher delegates to `IPublishEndpoint.Publish()` correctly | `IPublishEndpoint.Publish()` called with event |
| `PublishAsync_ForwardsCancellationToken` | The `CancellationToken` is forwarded to MassTransit               | Token passed through to `Publish()`            |

**Why:** `MassTransitEventPublisher` is the single exit point for all integration events leaving the monolith. If it fails to forward events to the broker, the AI Interview Service never receives candidate readiness notifications.
