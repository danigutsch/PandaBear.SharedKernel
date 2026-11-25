namespace SharedKernel.AI;

/// <summary>
/// Represents an AI image text generation failure.
/// </summary>
public sealed class ImageTextGenerationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTextGenerationException" /> class.
    /// </summary>
    public ImageTextGenerationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTextGenerationException" /> class.
    /// </summary>
    /// <param name="message">The failure message.</param>
    public ImageTextGenerationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTextGenerationException" /> class.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ImageTextGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
