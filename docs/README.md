# Documentation

- [Development and quality](development.md)
- [Security baseline](security-baseline.md)
- [Test assertion conventions](TEST_GUIDELINES.md)
- [File and stream intake](stream-intake.md)
- [Payment identifiers and payment epic handoff](plans/payment-identifiers.md)
- [Command-line tool guidance](command-line-tools.md)
- [Vault manager tool design](plans/vault-manager.md)
- [Vault migration: Keeper and Bitwarden into Vaultwarden](vault-migration.md)
- [Architecture and generated diagrams](architecture/README.md)
- [Release policy](release-policy.md)
- [Package development and validation](package-development.md)
- [API contract versioning](API_VERSIONING.md)
- [Exception documentation](EXCEPTION_DOCUMENTATION.md)
- [Warning policy](ci/warning-policy.md)
- [Action and image pin review](ci/pin-update-review.md)

Module README files beside source own consumer usage and examples. Samples and benchmarks remain under `samples/` and `benchmarks/`. The architecture diagrams are generated from this repository's code and workflow.

## Architecture decisions

- [Result pattern over exceptions](adr/20251108-result-pattern-over-exceptions.md)
- [Domain and building-block split](adr/20260621-split-sharedkernel-domain-and-building-blocks.md)
- [Domain materialization and persistence boundaries](adr/20260621-domain-materialization-and-sharedkernel-persistence-boundaries.md)
- [CloudEvents integration adapter](adr/20260621-cloudevents-as-integration-event-adapter.md)
- [Separate event dispatch (superseded)](adr/20260621-separate-domain-and-integration-event-dispatch.md)
- [Provider-specific modules](adr/20260624-provider-specific-sharedkernel-infrastructure-modules.md)
- [Messaging terminology and composition](adr/20260705-messaging-event-abstractions.md)
- [Generated messaging dispatch ownership](adr/20260719-generated-messaging-dispatch-ownership.md)
- [Breaking namespace decision](adr/20261020-pandabear-namespaces.md)
- [Public Git hosting decision](adr/20261020-public-git-hosting.md)
- [Classic extension methods](adr/20261026-classic-extension-methods.md)
