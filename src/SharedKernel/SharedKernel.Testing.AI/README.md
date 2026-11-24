# SharedKernel.Testing.AI

`PandaBear.SharedKernel.Testing.AI` provides `PandaBear.Testing.AI.TestImageTextGenerator` for
tests that depend on the SharedKernel `IImageTextGenerator` contract.

Supply a fixed `ImageTextGenerationResult` to the constructor. Set `Exception` in the object
initializer to simulate a provider failure. `LastRequest` records the last accepted request before
the generator returns the fixed result or failure. A request cancelled before the call leaves
`LastRequest` intact. The test owns the request image stream and controls its contents and lifetime.

Set `BeforeResult` to an asynchronous callback to pause or cancel an accepted call. The callback
receives the caller's token and runs after the request is recorded. The generator returns a
cancelled result if the token is cancelled while the callback runs. A pre-cancelled request or
a configured exception bypasses the callback.
