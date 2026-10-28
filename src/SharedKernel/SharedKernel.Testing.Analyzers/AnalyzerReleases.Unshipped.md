### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
SKTEST001 | Testing | Warning | xUnit test methods should not use pragma warning suppressions
SKTEST002 | Testing | Warning | xUnit test methods should follow the underscore naming convention
SKTEST003 | Testing | Warning | xUnit test methods should include configured required trait metadata
SKTEST004 | Testing | Warning | xUnit tests should keep helper methods, nested helper types, and local helper functions out of test classes and test methods
SKTEST005 | Testing | Warning | Serial xUnit collection definitions should declare a justification
SKTEST006 | Testing | Warning | Test code should use repository assertion wrappers instead of direct xUnit assertions
SKTEST007 | Testing | Warning | Complete explicit Arrange/Act/Assert marker sets in xUnit test methods should stay ordered
SKTEST008 | Testing | Warning | xUnit test methods should not use manual try/catch/finally control flow
SKTEST009 | Testing | Warning | xUnit trait metadata should use canonical constants when they exist
SKTEST011 | Testing | Info | Inline adjacent one-use assertion lambdas only when overload binding is preserved
SKTEST012 | Testing | Warning | Use ShouldHaveSingleItem for assertion-like LINQ Single calls
SKTEST013 | Testing | Warning | Use semantic assertions or named assertion targets instead of parenthesized receivers
SKTEST014 | Testing | Warning | Prevent conditional access from skipping ShouldBe assertions
SKTEST015 | Testing | Warning | Use ShouldHaveTitle for supported Playwright title assertions
SKTEST010 | Testing | Warning | Test activity listeners should filter by source identity
SKTEST016 | Testing | Warning | Construct the system under test in Arrange
SKTEST017 | Testing | Disabled | Public assertions should have a userMessage overload; enable warning severity in assertion libraries
SKTEST018 | Testing | Info | xUnit test methods should declare exactly one valid test scope
