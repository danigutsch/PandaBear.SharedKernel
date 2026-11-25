namespace SharedKernel.IO;

/// <summary>The outcome of <see cref="ContentAddressedFileStore.Store"/>.</summary>
/// <param name="Name">The file name: the content's lowercase hexadecimal SHA-256 followed by the extension.</param>
/// <param name="Created">
/// <see langword="true"/> when this call wrote the file; <see langword="false"/> when a file of that name already existed, which
/// another store, possibly a concurrent one, wrote.
/// </param>
public readonly record struct StoredFile(string Name, bool Created);
