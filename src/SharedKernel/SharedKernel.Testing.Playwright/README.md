# SharedKernel.Testing.Playwright

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable Playwright assertions for SharedKernel browser test suites.

## Usage

Import `PandaBear.Testing.Playwright` and await `page.ShouldHaveTitle(expectedTitle)` in Playwright tests. Browser creation, readiness, navigation, and test authentication remain owned by the test fixture.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Playwright`. C# namespace: `PandaBear.Testing.Playwright`. Assembly/project identity: `SharedKernel.Testing.Playwright`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
