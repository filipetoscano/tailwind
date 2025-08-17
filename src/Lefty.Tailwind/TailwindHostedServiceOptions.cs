namespace Lefty.Tailwind;

/// <summary />
public class TailwindHostedServiceOptions
{
    /// <summary>
    /// Where to download binary to.
    /// </summary>
    /// <remarks>
    /// If starts with ~, is relative path to solution root.
    /// Otherwise, is relative path to project root.
    /// </remarks>
    public string DownloadTo { get; set; } = "~/bin";

    /// <summary>
    /// Allow use of draft releases.
    /// </summary>
    public bool UseDraft { get; set; } = false;

    /// <summary>
    /// Allow use of pre-release releases.
    /// </summary>
    public bool UsePreRelease { get; set; } = false;

    /// <summary>
    /// Input CSS file.
    /// </summary>
    public string InputFile { get; set; } = "./wwwroot/css/input.css";

    /// <summary>
    /// Output CSS file.
    /// </summary>
    public string OutputFile { get; set; } = "./wwwroot/css/output.css";
}