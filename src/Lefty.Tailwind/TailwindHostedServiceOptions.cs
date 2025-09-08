namespace Lefty.Tailwind;

/// <summary />
public class TailwindHostedServiceOptions
{
    /// <summary>
    /// Github authentication token, in order to avoid (low) rate limiting.
    /// </summary>
    public string? GithubToken { get; set; }

    /// <summary>
    /// Where to download binary to.
    /// </summary>
    /// <remarks>
    /// If starts with ~, is relative path to solution root.
    /// Otherwise, is relative path to project root.
    /// </remarks>
    public string DownloadTo { get; set; } = "~/bin";

    /// <summary>
    /// How old, in minutes, the 'check for latest' can be.
    /// </summary>
    public int MaxLatestCheckAgeMins { get; set; } = 24 * 60;

    /// <summary>
    /// Allow updating to the latest version (if internet is available).
    /// </summary>
    public bool AllowUpdates { get; set; } = true;

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
    public string OutputFile { get; set; } = "./wwwroot/css/site.css";

    /// <summary />
    public bool OutputMinify { get; set; } = false;

    /// <summary />
    public bool OutputOptimize { get; set; } = false;
}