namespace SharedKernel.Mediator;

/// <summary>
/// Assigns an explicit stage and optional order to a pipeline behavior.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PipelineOrderAttribute(PipelineStage stage) : Attribute
{
    /// <summary>
    /// Gets the coarse-grained pipeline stage.
    /// </summary>
    public PipelineStage Stage { get; } = stage;

    /// <summary>
    /// Gets or initializes the explicit order within the stage.
    /// </summary>
    public int Order { get; init; }
}
