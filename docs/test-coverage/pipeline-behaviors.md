# Pipeline Behaviors Test Coverage

← [Test Coverage](README.md)

> Tests for MediatR pipeline behaviors — cross-cutting concerns that wrap every command and query.

---

## `LoggingPipelineBehaviorTests` (3 tests)

Tests the MediatR pipeline behavior that logs request start/finish with elapsed time.

| Test                                        | What It Verifies                                                | Expected Outcome                          |
| ------------------------------------------- | --------------------------------------------------------------- | ----------------------------------------- |
| `Handle_LogsStartAndCompletion`             | Logs "Handling {RequestName}..." and "Handled {RequestName} in" | Logger receives both log entries          |
| `Handle_ReturnsHandlerResult`               | The behavior passes through the handler's return value          | Response matches what the handler returns |
| `Handle_WhenNextThrows_PropagatesException` | Exceptions from the handler are not swallowed                   | Exception propagates to caller            |

**Why:** The logging behavior wraps every MediatR request. If it swallows exceptions or fails to pass through results, every command and query in the system breaks silently.

---

## `ValidationPipelineBehaviorTests` (4 tests)

Tests the MediatR pipeline behavior that runs FluentValidation validators before the handler.

| Test                                                  | What It Verifies                                                           | Expected Outcome                                             |
| ----------------------------------------------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------ |
| `Handle_NoValidators_PassesThrough`                   | When no `IValidator<TRequest>` is registered, the handler executes         | Handler result returned                                      |
| `Handle_ValidRequest_PassesThrough`                   | When validators pass, the handler executes                                 | Handler result returned                                      |
| `Handle_ValidationFails_ThrowsValidationError`        | When any validator fails, throws `AppErrors.Validation` with field details | Throws `AppError` with code `VALIDATION_ERROR` and `Details` |
| `Handle_MultipleValidationFailures_AggregatesDetails` | Multiple validator failures are aggregated into one `Details` dictionary   | All failing fields present in `Details`                      |

**Why:** Validation runs before every handler. If it fails to throw on invalid input, business logic processes garbage data. If it throws on valid input, every request fails. The aggregation test ensures multiple validation rules don't lose errors.
