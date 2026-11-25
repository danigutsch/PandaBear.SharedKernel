namespace SharedKernel.Testing.ObjectStorage;

/// <summary>Names an <see cref="SharedKernel.ObjectStorage.IObjectStore" /> member, for call recording and failure injection.</summary>
public enum ObjectStoreOperation
{
    /// <summary>A write through <see cref="SharedKernel.ObjectStorage.IObjectStore.Put" />.</summary>
    Put,

    /// <summary>A read through <see cref="SharedKernel.ObjectStorage.IObjectStore.OpenRead" />.</summary>
    OpenRead,

    /// <summary>An existence check through <see cref="SharedKernel.ObjectStorage.IObjectStore.Exists" />.</summary>
    Exists,

    /// <summary>A key listing through <see cref="SharedKernel.ObjectStorage.IObjectStore.ListKeys" />.</summary>
    ListKeys,

    /// <summary>An inventory listing through <see cref="SharedKernel.ObjectStorage.IObjectStore.ListObjects" />.</summary>
    ListObjects,

    /// <summary>A public URI lookup through <see cref="SharedKernel.ObjectStorage.IObjectStore.GetPublicUri" />.</summary>
    GetPublicUri,

    /// <summary>A direct-upload ticket request through <see cref="SharedKernel.ObjectStorage.IObjectStore.CreateUploadUrl" />.</summary>
    CreateUploadUrl,

    /// <summary>A delete through <see cref="SharedKernel.ObjectStorage.IObjectStore.Delete" />.</summary>
    Delete
}
