# SharedKernel.AuditTrail

Metadata-only audit-trail mapping contracts for application-owned persistence boundaries.

The package does not own persistence, transport, identity extraction, retention, or CloudEvents
serialization. Consumers must keep audit entries free of personal data and persist them atomically
with the audited state change.
