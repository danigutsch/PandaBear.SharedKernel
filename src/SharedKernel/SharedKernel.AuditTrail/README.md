# SharedKernel.AuditTrail

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Metadata-only audit-trail mapping contracts for application-owned persistence boundaries.

The package does not own persistence, transport, identity extraction, retention, or CloudEvents
serialization. Consumers must keep audit entries free of personal data and persist them atomically
with the audited state change.

## Usage

Implement `IAuditTrailEntry` for an audit record and `IAuditTrailSink<TAuditTrailEntry>.Append` for storage. `AuditTrailMappingAttribute` declares mappings consumed by the optional audit generator. Applications own retention, access control, event selection, and exclusion of secrets or personal data from audit payloads.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AuditTrail`. C# namespace: `PandaBear.AuditTrail`. Assembly/project identity: `SharedKernel.AuditTrail`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
