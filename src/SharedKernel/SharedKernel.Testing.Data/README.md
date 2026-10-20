# SharedKernel.Testing.Data

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable deterministic test data helpers for SharedKernel test suites.

## Usage

Call `TestUniqueId.Create(prefix)` when a test needs an isolated identifier. Choose a prefix that identifies the test resource and keep cleanup in the owning fixture. This utility does not allocate external resources or replace durable application identifiers.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Data`. C# namespace: `PandaBear.Testing.Data`. Assembly/project identity: `SharedKernel.Testing.Data`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
