namespace PandaBear.Secrets;

/// <summary>The document formats whose SOPS metadata this library reads.</summary>
public enum SopsFormat
{
    /// <summary>YAML, read by a purpose-built parser of the subset that SOPS writes.</summary>
    Yaml,

    /// <summary>JSON.</summary>
    Json,
}
