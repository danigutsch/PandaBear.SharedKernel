# SharedKernel.Messaging.Protobuf

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Protobuf support for integration-event messaging, at the two layers CloudEvents defines.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Messaging.Protobuf`. C# namespace: `PandaBear.Messaging.Protobuf`. Assembly/project identity: `SharedKernel.Messaging.Protobuf`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.

Dependencies: `CloudNative.CloudEvents`, `CloudNative.CloudEvents.Protobuf` (Apache-2.0), and `Google.Protobuf` (BSD-3-Clause). On `net10.0` they bring no further packages. The package makes no NativeAOT or trimming claim; no packed NativeAOT consumer validates it.

## Payload format: `application/protobuf`

`ProtobufIntegrationEventSerializer` serializes an integration event that implements `Google.Protobuf.IMessage` (an event type generated from a `.proto` file that also implements `IIntegrationEvent`). The payload is base64 text with `EventPayloadEncoding.Base64` and `datacontenttype` `application/protobuf` (`ProtobufContentTypes.Payload`). This is how CloudEvents carries binary data in the JSON event format: `data_base64` plus `datacontenttype`. Envelope handling stays format-agnostic; the content type and encoding identify the format.

Choose the format by registering the serializer for a context. Register it globally or keyed by context, like any `IIntegrationEventSerializer`:

```csharp
services.AddSingleton<IIntegrationEventSerializer>(new ProtobufIntegrationEventSerializer());

// A context that mixes formats: Protobuf messages go binary, every other event uses the fallback.
services.AddSingleton<IIntegrationEventSerializer>(
    new ProtobufIntegrationEventSerializer(fallback: generatedJsonSerializer));
```

The outbox records `SerializePayload`'s payload, encoding, and content type on each row. JSON stays the default: a serializer that only implements `Serialize` yields `application/json` with `EventPayloadEncoding.Json`. Without a fallback, an event that is not a Protobuf message fails with a `NotSupportedException` naming the event type. `ProtobufIntegrationEventSerializer.Deserialize<TIntegrationEvent>` reads a payload back; it throws `ArgumentException` for invalid base64 and `InvalidProtocolBufferException` for bytes that are not a valid message.

### Producing and consuming with the source generator

The integration event source generator (`SharedKernel.Messaging.IntegrationEvents.SourceGenerator`) understands Protobuf messages, detected by name as `Google.Protobuf.IMessage`, so no `JsonTypeInfo<T>` is needed for them:

- **Producing.** An `[IntegrationEventMapping]` that returns a Protobuf message is serialized by the generated `IIntegrationEventSerializer` as base64 with `EventPayloadEncoding.Base64` and `application/protobuf`; its `SerializePayload` reports that, and JSON events keep `application/json`. An explicit `AddIntegrationEventContract<T>` (JSON) registration of the same type keeps it on JSON.
- **Consuming.** Register the consumer with `AddProtobufIntegrationEventConsumer`. The generator recognizes the call and emits a delivery branch in the generated `IEventEnvelopePublisher`: it matches the event type, `EventPayloadEncoding.Base64` and the `application/protobuf` content type, builds the message with `new T()` and `MessageExtensions.MergeFrom`, and runs the single discovered `IIntegrationEventHandler<T>` (SKMSG001/SKMSG002 apply as for JSON consumers). The event type must implement `IMessage` and have a public parameterless constructor, which a protoc-generated message does; otherwise the generator reports SKMSG006. SKMSG003 compares consumer event types per format, so a JSON and a Protobuf consumer of one event type can coexist.

```csharp
services.AddGeneratedIntegrationEvents();
services.AddProtobufIntegrationEventConsumer<TourCreated, TourCreatedHandler>();
```

The generated `AddGeneratedIntegrationEvents` registers a `ProtobufGeneratedConsumer` marker for each generated Protobuf consumer, so the runtime registration below hands those envelopes to the generated publisher instead of handling them a second time. Call `AddProtobufIntegrationEventConsumer` after `AddGeneratedIntegrationEvents`; called earlier, a JSON envelope or a generator-covered Protobuf envelope fails with a `NotSupportedException` that names this requirement.

### Consuming without the generator

`AddProtobufIntegrationEventConsumer<TEvent, THandler>()` also works in hosts that do not run the generator. The event type must implement `IIntegrationEvent` and `IMessage<T>` and have a public parameterless constructor. The first call replaces the unkeyed `IEventEnvelopePublisher` with one that delivers envelopes with `EventPayloadEncoding.Base64` and `datacontenttype` `application/protobuf` to the handler registered for their event type, and passes every other envelope to the publisher registered before it (with its original lifetime, as a keyed service). A Protobuf envelope whose event type has no runtime consumer goes to that earlier publisher, or fails with an actionable error when there is none; a second consumer for one event type fails at registration. The deserialization is typed and uses no reflection; the replacement is a plain `ServiceDescriptor` swap.

## Event format: `application/cloudevents+protobuf`

`ProtobufEventEnvelopeFormat.Encode` writes an `IEventEnvelope` as one CloudEvents Protobuf message (`ProtobufContentTypes.EventFormat`), and `Decode` reads it back into an `EventEnvelope`:

| Envelope | CloudEvents Protobuf |
| --- | --- |
| `EventId`, `Source`, `EventType`, `Time`, `Subject`, `DataContentType`, `DataSchema` | `id`, `source`, `type`, `time`, `subject`, `datacontenttype`, `dataschema` |
| `EventVersion` | integer extension attribute `eventversion` |
| each property of `ExtensionAttributesJson` (string values) | string extension attribute of the same name, such as `traceparent` |
| `Payload` with `Json` | `text_data` |
| `Payload` with `Base64` | `binary_data` (decoded bytes) |

Only `cloudevents` 1.0 envelopes encode; other specs throw `NotSupportedException`. A `proto_data` payload is not supported on decode and throws `NotSupportedException`: re-publish it as `binary_data` with `datacontenttype` `application/protobuf`.

## Schema evolution

Follow the Protobuf compatibility rules for every message used as an integration event:

- Never reuse or renumber a field number. Mark removed fields and their numbers `reserved`.
- Add new fields as optional with a default; consumers on the previous schema skip unknown fields.
- Do not change a field's type, except between wire-compatible integer types. Do not change `repeated` to a singular field or the reverse.
- Do not rename a message or change its package in a way that alters the event's `EventType`. Increment `IIntegrationEvent.EventVersion` for a change that readers must distinguish, and use `dataschema` to point to the `.proto` of that version.
- Ship a producer's new field before any consumer relies on it. Treat an enum's unknown values as the default value.
