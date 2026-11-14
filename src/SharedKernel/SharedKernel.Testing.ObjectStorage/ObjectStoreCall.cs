namespace PandaBear.Testing.ObjectStorage;

/// <summary>One call that <see cref="InMemoryObjectStore" /> received with valid arguments, whether or not it succeeded.</summary>
/// <param name="Operation">The store member that was called.</param>
/// <param name="Target">The object key the call named, or the listing prefix for <see cref="ObjectStoreOperation.ListKeys" /> and <see cref="ObjectStoreOperation.ListObjects" />.</param>
public sealed record ObjectStoreCall(ObjectStoreOperation Operation, string Target);
