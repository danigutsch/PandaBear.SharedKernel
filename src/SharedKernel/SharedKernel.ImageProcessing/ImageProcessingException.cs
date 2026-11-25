namespace SharedKernel.ImageProcessing;

/// <summary>
/// Represents an image processing failure caused by invalid input or unsupported image data.
/// </summary>
public sealed class ImageProcessingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageProcessingException" /> class.
    /// </summary>
    public ImageProcessingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageProcessingException" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ImageProcessingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageProcessingException" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying processing exception.</param>
    public ImageProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
