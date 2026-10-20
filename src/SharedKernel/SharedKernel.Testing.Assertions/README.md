# SharedKernel.Testing.Assertions

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable xUnit assertion extensions for SharedKernel test suites.

## Usage

Import `PandaBear.Testing.Assertions` for `ShouldBe` and collection assertions. Use `ExceptionAssertions.ThrowsInner<TException>` when validating wrapped failures. Keep assertions tied to observable behavior and use the comparer/precision overloads when equality semantics require them.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Assertions`. C# namespace: `PandaBear.Testing.Assertions`. Assembly/project identity: `SharedKernel.Testing.Assertions`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
