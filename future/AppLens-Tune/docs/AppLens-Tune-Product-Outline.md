# Tune implementation

`TuneAuditService` collects diagnostics; `TunePlanBuilder` generates proposed actions.
`TuneActionExecutor` supports selected startup, service, and cache actions through
the shared approval loop. Reports include findings and plan details.

Actions require explicit approval and recorded outcomes. Tune is separate from
the core inventory client; see [entry points](../README.md).
