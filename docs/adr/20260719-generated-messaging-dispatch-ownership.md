# ADR-039: Generated Messaging Dispatch Ownership

**Status**: Accepted - 2026-07-19

**Supersedes**: [ADR-023: Separate Domain and Integration Event Dispatch](20260621-separate-domain-and-integration-event-dispatch.md)

## Context

The mediator, domain-event, and integration-event stacks accumulated adapters, registration objects,
dictionaries, and service-provider lookup after closed generic types had already been known at compile
time. That duplicated dispatch ownership and made composition order determine which
`IDomainEventDispatcher` implementation won.

A bounded comparison of Mediator 3.0.2, Immediate.Handlers 3.11.1, Foundatio.Mediator 1.3.3, and
FastEndpoints showed that direct generated methods and exhaustive cases keep consumer glue smaller and
more explicit than runtime type recovery. The research supports generation of mechanical binding, not
adoption of another mediator framework or generation of business logic.

## Decision

- The mediator generator emits typed request, stream, notification, handler, and pipeline paths.
  Generic sender and publisher APIs remain for real interface-typed and object-dispatch callers, and
  forward through exhaustive generated cases.
- `AppMediator` receives closed typed `Func<T>` handler and pipeline dependencies. The factories defer
  scoped resolution until dispatch, avoiding constructor cycles without a general runtime registry.
- Domain-event provider generators emit closed `IDomainEventDispatchHandler` implementations. The
  scoped `CompositeDomainEventDispatcher` composes outbox and audit handlers without registration-order
  ownership of `IDomainEventDispatcher`. These APIs belong to `PandaBear.Domain`; the EF integration
  belongs to `PandaBear.Domain.EntityFrameworkCore`.
- Each host supplies explicit `JsonTypeInfo<T>` metadata. Generated serializers and envelope publishers
  use closed typed cases instead of contract registration objects or dictionaries.
- Background consumers retain the genuine scope boundaries. One consumer scope owns each claimed
  batch and its generated publisher. Each envelope delivery creates and asynchronously disposes a child
  scope, then resolves the closed `IIntegrationEventHandler<T>` from that scope. The transport consumer
  passes the batch's messages to the publisher sequentially and forwards cancellation without
  generating business logic.
- Outbox and inbox registration remain separate. Producer contexts do not acquire inbox schema or
  idempotency services implicitly.

### Contract ownership

Domain dispatch contracts live in the `PandaBear.Domain` namespace, with EF integration in `PandaBear.Domain.EntityFrameworkCore`. The corresponding package IDs are `PandaBear.SharedKernel.Domain` and `PandaBear.SharedKernel.Domain.EntityFrameworkCore`; namespaces and package identities are distinct. Superseded dispatch adapters are not retained through forwarding packages or compatibility namespaces. Review deliberate source breaks using the existing API gates and [namespace decision](20261020-pandabear-namespaces.md).

## Measured shape

Representative tests measure structure rather than enforce arbitrary line quotas:

| Path | Before | After |
| --- | --- | --- |
| Mediator ordinary dispatch | Typed switch plus dispatch-time `IServiceProvider` recovery in request, stream, pipeline, and notification paths | Same exhaustive generic forwarding with zero service-provider sites in ordinary dispatch |
| Domain event mapping | Competing mediator adapter and integration-event mapper registrations | One exhaustive generated mapper and one dispatcher registration |
| Integration envelopes | Five top-level generated types, including one handler forwarder; one registration method; no delivery scope or service-provider site | Four top-level generated types, no forwarder or other runtime-recovery type; one registration method; one async delivery scope and one closed handler-resolution site |

The external comparison above remains rationale rather than a line-count quota. Counts guide review;
preserving validation, transactions, cancellation, outbox retry, serialization compatibility, and
idempotency is the acceptance boundary.

For the representative one-consumer combined-generator composition, nested generated types remain `0`
and registration methods remain `1`. The forwarder count changes from `1` to `0`, while async
delivery-scope creation changes from `0` to `1`. The single `GetRequiredService` site is intentional and
bounded to an envelope's child scope; ordinary mediator dispatch retains zero service-provider sites.

In the separate two-event registration case, one handler implementation that consumes both event types
changes from one concrete registration plus two generated forwarders to two direct closed
`IIntegrationEventHandler<T>` registrations. That generated publisher has one delivery-scope and one
closed handler-resolution site in each event case.

Generated delivery resolves the closed handler interface rather than bypassing it for the concrete
implementation. Consumers compose idempotency at that closed interface, so concrete-type resolution would
skip the decorator and weaken duplicate-delivery protection.

## Consequences

- Missing and duplicate integration-event consumer handlers fail compilation with `SKMSG001` and
  `SKMSG002`; duplicate consumer event types, in either payload format, fail compilation with
  `SKMSG003`; invalid registered contract types fail compilation with `SKMSG006`.
- Protobuf messages (`Google.Protobuf.IMessage`) need no JSON metadata: the generated serializer writes
  mapped ones as base64 with `application/protobuf`, and the generated publisher delivers consumers
  registered with `AddProtobufIntegrationEventConsumer` from a Base64 `application/protobuf` envelope.
- The generated publisher constructor holds the scope factory and closed JSON metadata for JSON
  consumers, not scoped handlers. Closed handler registrations remain visible to DI validation and are resolved only inside
  an envelope's asynchronously disposed child scope.
- Adding a contract requires explicit metadata and generated host composition.
- Dynamic runtime registries are not retained without a demonstrated dynamic caller.

## Links

- [Back to ADR Index](../README.md)
- [Separate Domain and Integration Event Dispatch](20260621-separate-domain-and-integration-event-dispatch.md)


Reviewed for the standalone library: 2026-10-20. Namespaces above use `PandaBear.*`; project/assembly identities remain `SharedKernel.*` and package IDs remain `PandaBear.SharedKernel.*`.
