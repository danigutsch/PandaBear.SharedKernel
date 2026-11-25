using System.Runtime.InteropServices;

namespace SharedKernel.Secrets;

/// <summary>A Unix file descriptor that <see cref="Libc.open"/> returned, closed when the handle is released.</summary>
internal sealed class FileDescriptorHandle : SafeHandle
{
    /// <summary>Creates an invalid handle; the marshaller sets the descriptor.</summary>
    public FileDescriptorHandle()
        : base(invalidHandleValue: -1, ownsHandle: true)
    {
    }

    /// <inheritdoc />
    public override bool IsInvalid => handle == -1;

    /// <inheritdoc />
    protected override bool ReleaseHandle() => Libc.close(handle) == 0;
}
