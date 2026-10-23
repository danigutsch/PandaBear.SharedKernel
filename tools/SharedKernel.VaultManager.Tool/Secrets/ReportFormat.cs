namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Findings report output format.</summary>
internal enum ReportFormat
{
    /// <summary>Markdown table.</summary>
    Markdown = 0,

    /// <summary>Plain text, one finding per line.</summary>
    Text,
}
