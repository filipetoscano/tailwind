using System.Text.Json.Serialization;

namespace Lefty.Tailwind;

/// <summary />
public class GithubRelease
{
    /// <summary />
    [JsonPropertyName( "name" )]
    public required string Name { get; set; }

    /// <summary />
    [JsonPropertyName( "tag_name" )]
    public required string TagName { get; set; }

    /// <summary />
    [JsonPropertyName( "draft" )]
    public required bool IsDraft { get; set; }

    /// <summary />
    [JsonPropertyName( "prerelease" )]
    public required bool IsPreRelease { get; set; }

    /// <summary />
    [JsonPropertyName( "updated_at" )]
    public required DateTimeOffset MomentUpdated { get; set; }

    /// <summary />
    [JsonPropertyName( "published_at" )]
    public required DateTimeOffset MomentPublished { get; set; }

    /// <summary />
    [JsonPropertyName( "assets" )]
    public List<GithubAsset>? Assets { get; set; }
}


/// <summary />
public class GithubAsset
{
    /// <summary />
    [JsonPropertyName( "name" )]
    public required string Name { get; set; }

    /// <summary />
    [JsonPropertyName( "digest" )]
    public required string Digest { get; set; }

    /// <summary />
    [JsonPropertyName( "size" )]
    public required long Size { get; set; }

    /// <summary />
    [JsonPropertyName( "browser_download_url" )]
    public required string DownloadUrl { get; set; }
}