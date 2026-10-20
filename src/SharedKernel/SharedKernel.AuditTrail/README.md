# SharedKernel.AuditTrail

Metadata-only audit-trail mapping contracts for application-owned persistence boundaries.

The package does not own persistence, transport, identity extraction, retention, or CloudEvents
serialization. Consumers must keep audit entries free of personal data and persist them atomically
with the audited state change.

## Usage

Implement `IAuditTrailEntry` for an audit record and `IAuditTrailSink<TAuditTrailEntry>.Append` for storage. `AuditTrailMappingAttribute` declares mappings consumed by the optional audit generator. Applications own retention, access control, event selection, and exclusion of secrets or personal data from audit payloads.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AuditTrail`. C# namespace: `PandaBear.AuditTrail`. Assembly/project identity: `SharedKernel.AuditTrail`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
