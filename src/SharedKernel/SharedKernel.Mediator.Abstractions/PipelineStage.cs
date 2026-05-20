namespace SharedKernel.Mediator;

/// <summary>
/// Defines the coarse-grained stages used to order pipeline behaviors.
/// </summary>
public enum PipelineStage
{
    /// <summary>
    /// Validation behaviors run before all other stages.
    /// </summary>
    Validation = -1000,

    /// <summary>
    /// Authorization behaviors run after validation.
    /// </summary>
    Authorization = -800,

    /// <summary>
    /// Caching behaviors run after authorization.
    /// </summary>
    Caching = -600,

    /// <summary>
    /// Transaction behaviors run before the handler.
    /// </summary>
    Transaction = -400,

    /// <summary>
    /// The handler stage represents the terminal request handler.
    /// </summary>
    Handler = 0,

    /// <summary>
    /// Post-processing behaviors run after the handler.
    /// </summary>
    PostProcessing = 400,

    /// <summary>
    /// Observability behaviors run late in the pipeline.
    /// </summary>
    Observability = 800,
}
