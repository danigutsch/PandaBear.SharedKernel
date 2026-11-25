# SharedKernel.ObjectStorage.FileSystem

A host-configured filesystem adapter for the shared object storage contracts.

Pass a storage root and application mappings for public addresses and read metadata to
`FileSystemObjectStoreOptions`. Apply app key restrictions through the key predicate and
key length option. Addresses can be absolute or relative. The adapter stores bytes and delegates metadata to its host. The input stream stays open.

Writes rewind seekable streams and verify their exact declared length in one pass.
An optional `sha256:` checksum followed by 64 hexadecimal digits is verified before publication.
Replacement publishes atomically after the write and leaves a previous object intact on failure.
Reads are opened with delete sharing and keep their original content if the object is replaced.
Consumers dispose the returned `ObjectReadResult`.

The root is controlled by the application and must be writable only by trusted processes.
The adapter refuses symbolic links below that root. Listings keep ordinal prefix semantics and
skip links and reserved temporary files. Listings include pre-existing files within scope so they
can support reconciliation. Give each store a consistent temporary prefix across restarts.
The filesystem's name and case rules apply to storage. Direct-upload ticket requests raise `NotSupportedException`.

Package ID `PandaBear.SharedKernel.ObjectStorage.FileSystem`. Namespace
`SharedKernel.ObjectStorage.FileSystem`.
