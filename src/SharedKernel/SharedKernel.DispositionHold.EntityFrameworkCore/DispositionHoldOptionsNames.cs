using Microsoft.EntityFrameworkCore;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>Builds the named-options key under which per-context hold storage options are registered.</summary>
internal static class DispositionHoldOptionsNames
{
    /// <summary>Returns the named-options key for <typeparamref name="TContext" />, so each context keeps its own settings.</summary>
    public static string Storage<TContext>()
        where TContext : DbContext => $"{typeof(TContext).AssemblyQualifiedName}:disposition-hold-storage";
}
