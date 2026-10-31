### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
SKSTYLE001 | Style | Warning | Method name should not end with Async unless an inherited contract requires it
SKSTYLE002 | Style | Warning | CancellationToken parameters should be named ct
SKSTYLE003 | Style | Warning | CancellationToken parameters should not declare default values
SKSTYLE004 | Style | Warning | Source files should not declare more than one top-level type
SKSTYLE005 | Style | Warning | Generic type names should not include suffixes that repeat generic arity
SKSTYLE006 | Style | Warning | Catch filters should not suppress every OperationCanceledException without checking the operation token
SKSTYLE007 | Style | Warning | Production logging should use source-generated LoggerMessage methods
SKSTYLE008 | Style | Warning | Domain event types implementing IDomainEvent should end with DomainEvent
SKSTYLE009 | Style | Warning | Result-returning methods should be able to return a failure Result
SKSTYLE010 | Style | Warning | Result-returning methods should use Option for not-found outcomes
SKSTYLE011 | IO | Disabled | Review whole-input reads in size-sensitive code; enable warning severity for selected production scopes
SKSTYLE012 | Style | Warning | Replace registered direct C# string literals with their canonical constant
SKSTYLE013 | Style | Warning | Governed literal catalog must be valid and unambiguous
SKSTYLE014 | Style | Warning | Use TimeProvider for current time
SKSTYLE015 | Style | Disabled | Require case-insensitive property names on API JSON contexts; enable for API payload scopes
SKSTYLE016 | Style | Warning | File name should match its single top-level type
SKSTYLE017 | Style | Warning | Internal types and members, which CS1591 does not cover, should have XML documentation
SKSTYLE018 | Style | Warning | Documented members should list each exception they throw in an exception tag
