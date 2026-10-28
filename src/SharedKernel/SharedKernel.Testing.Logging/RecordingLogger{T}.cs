using Microsoft.Extensions.Logging;

namespace PandaBear.Testing.Logging;

/// <summary>A <see cref="RecordingLogger" /> that also satisfies <see cref="ILogger{TCategoryName}" />.</summary>
/// <typeparam name="T">The logger category.</typeparam>
public sealed class RecordingLogger<T> : RecordingLogger, ILogger<T>;
