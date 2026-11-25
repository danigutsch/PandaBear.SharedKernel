namespace SharedKernel.Domain;

/// <summary>
/// Requests generated support code for the annotated model.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateModelSupportAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether identity/equality support should be generated.
    /// </summary>
    public bool Identity { get; set; }
}
