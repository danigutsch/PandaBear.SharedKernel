### New Rules

| Rule ID | Category | Severity | Notes |
| ------- | -------- | -------- | ----- |
| SKMED001 | Usage | Warning | Request has no handler |
| SKMED002 | Usage | Warning | Request has multiple handlers |
| SKMED003 | Usage | Warning | Handler has invalid signature |
| SKMED004 | Usage | Warning | Handler is missing CancellationToken ct |
| SKMED005 | Usage | Warning | Handler return type does not match request response type |
| SKMED006 | Usage | Warning | Mediator call does not pass available CancellationToken ct |
| SKMED007 | Usage | Warning | Async stream Handle method is missing `[EnumeratorCancellation]` |
| SKMED008 | Usage | Info | CancellationToken parameter has no effect in non-iterator stream handler |
| SKMED010 | Usage | Warning | Mediator registration type is inaccessible |
| SKMED011 | Usage | Warning | Handler module is not marked with `[assembly: MediatorModule]` |
| SKMED012 | Usage | Warning | Generated mediator registration is duplicated |
| SKMED013 | Usage | Warning | Generated object dispatch coverage cannot be proven |
| SKMED020 | Usage | Warning | Pipeline behavior has invalid generic arity |
| SKMED021 | Usage | Warning | Duplicate pipeline order |
| SKMED022 | Usage | Warning | Pipeline behavior is registered but never applies |
| SKMED023 | Usage | Warning | Pipeline behavior constraints cannot bind to any request |
| SKMED200 | Usage | Warning | Notification handlers require explicit order |
| SKMED201 | Usage | Warning | Duplicate notification handler order |
| SKMED500 | Architecture | Info | Handler should not call mediator send APIs directly |
