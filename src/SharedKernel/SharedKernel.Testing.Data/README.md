# SharedKernel.Testing.Data

Reusable deterministic test data helpers for SharedKernel test suites.

## Usage

Call `TestUniqueId.Create(prefix)` when a test needs an isolated identifier. Choose a prefix that identifies the test resource and keep cleanup in the owning fixture. This utility does not allocate external resources or replace durable application identifiers.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Data`. C# namespace: `PandaBear.Testing.Data`. Assembly/project identity: `SharedKernel.Testing.Data`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
