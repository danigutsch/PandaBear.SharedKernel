### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
SKDOM001 | Design | Warning | Domain-model properties should not expose non-private setters
SKDOM002 | Design | Warning | Domain-model properties should not expose mutable collections outside their assembly
SKDOM003 | Design | Warning | Domain code should not read the ambient clock (DateTime.Now, UtcNow, Today, DateTimeOffset.Now, UtcNow)
