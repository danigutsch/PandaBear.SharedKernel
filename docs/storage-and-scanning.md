# Object storage and malware scanning

This map separates the reusable library packages from the application policy a consuming repository keeps. A capability lives in
SharedKernel only when it is host-agnostic and carries no application business rule.

## Package responsibilities

| Concern | Package | Owns | Does not own |
| --- | --- | --- | --- |
| Bounded seekable content for multi-pass intake | `SharedKernel.IO` | `SeekableContent` lease with memory threshold and spill-to-disk | Request admission, size policy, storage |
| Object-key validation | `SharedKernel.InputNormalization` | `ObjectStorageKeyValidator`: relative, slash-delimited keys without dot segments, bounded length | Key layout, variants, naming rules |
| Object-store contract | `SharedKernel.ObjectStorage` | `IObjectStore`, write, read, list, delete, direct-upload tickets, checksum and metadata carriers | Provider behavior, key policy, publishing |
| S3-compatible provider | `SharedKernel.ObjectStorage.S3` | `AddS3ObjectStore`, options validation, presigned PUT tickets, traces without bucket or key tags, `AddS3ObjectStoreHealthCheck` | Hosting a server, access policy, application metadata |
| Scanner contract | `SharedKernel.MalwareScanning` | `IMalwareScanner` over a stream and length, a verdict without daemon text, names, or bytes | Which uploads are scanned, what a rejection means |
| ClamAV provider | `SharedKernel.MalwareScanning.ClamAv` | `zINSTREAM` client, fail-closed `Failed` status, scan telemetry, `AddClamAvHealthCheck` | Container hosting, upload policy |
| Local ClamAV hosting | `SharedKernel.Aspire.Hosting.ClamAv` | Aspire resource, image tag, `PING` readiness check | Runtime health of a deployed app |
| Local SeaweedFS hosting | `SharedKernel.Aspire.Hosting.SeaweedFs` | Aspire resource, endpoints, credentials parameters, master readiness check | S3 client behavior |

Application policy stays in the consuming repository: media metadata, upload size and type rules, which uploads are scanned,
tour or content links, publishing and public-visibility rules, accessibility workflows, and processing pipelines.

## Health checks

Both runtime checks register on `IHealthChecksBuilder` and add no host dependency beyond
`Microsoft.Extensions.Diagnostics.HealthChecks`.

- `AddClamAvHealthCheck(name = "clamav", failureStatus, tags)` sends `zPING\0` to the daemon that
  `AddConfiguredClamAvMalwareScanner` configured and expects `PONG\0` within the configured scan timeout. It reports healthy
  without contacting a daemon when scanning is explicitly disabled for a development, test, or testing host.
- `AddS3ObjectStoreHealthCheck(name = "object-storage", failureStatus, tags)` lists at most one key of the configured bucket. It
  never creates the bucket, so a missing bucket is unhealthy even when `AutoProvisionBucket` is set.

Failure descriptions are fixed strings. They never include a host, port, endpoint, bucket, object key, daemon response, or
provider message, and the result carries no exception. Caller cancellation propagates; only the scan timeout is a health failure.

## Consumer prerequisites

- Register the scanner and store first: `AddConfiguredClamAvMalwareScanner` and `AddS3ObjectStore`. Each health check resolves
  their registrations and fails at first use when they are missing.
- Pre-provision the private bucket in production and supply `AccessKey` and `SecretKey` through a secret store.
- Treat `MalwareScanStatus.Failed` as a rejection (fail closed).
- Consume a validated candidate from a configured local or private feed; registry publication remains deferred.

## Validation evidence

- `tests/SharedKernel.ObjectStorage.S3.Tests` covers the store, upload tickets, options, telemetry, and the health check against
  a fake S3 client.
- `tests/SharedKernel.MalwareScanning.ClamAv.Tests` covers verdicts, fail-closed behavior, configuration, and the health check
  against a loopback daemon double.
- `tests/SharedKernel.InputNormalization.Tests` covers the object-key validator.
