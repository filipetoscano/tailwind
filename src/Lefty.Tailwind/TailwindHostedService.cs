using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Runtime.InteropServices;

namespace Lefty.Tailwind;

/// <summary />
public class TailwindHostedService : BackgroundService
{
    private readonly IOptionsMonitor<TailwindHostedServiceOptions> _om;
    private readonly HttpClient _http;
    private readonly ILogger<TailwindHostedService> _logger;

    /// <summary />
    public TailwindHostedService(
        IOptionsMonitor<TailwindHostedServiceOptions> optionsMonitor,
        HttpClient http,
        ILogger<TailwindHostedService> logger
        )
    {
        _om = optionsMonitor;
        _http = http;
        _logger = logger;
    }


    /// <inheritdoc />
    protected override async Task ExecuteAsync( CancellationToken stoppingToken )
    {
        _logger.LogInformation( "Tailwind" );

        var opt = _om.CurrentValue;


        /*
         * 
         */
        var bin = GetBestTailwindCliBinary();
        var latest = await GetLatestGithubRelease();

        var asset = latest.Assets?.SingleOrDefault( x => x.Name == bin );

        if ( asset == null )
            return;

        _logger.LogInformation( "Version {TagName}, Binary {Binary}, Size {Size}", latest.TagName, asset.Name, asset.Size );


        /*
         * 
         */
        var targetDir = ResolveDirectory( opt.DownloadTo );
        _logger.LogInformation( "Target {TargetDir}", targetDir );


        /*
         * 
         */
        var targetExe = Path.Combine( targetDir, asset.Name );

        if ( File.Exists( targetExe ) == false )
        {
            _logger.LogInformation( "Downloading from {Url}...", asset.DownloadUrl );
            await DownloadTo( asset.DownloadUrl, targetExe );
        }
        else
        {
            _logger.LogInformation( "Already downloaded" );
        }
    }


    /// <summary>
    /// Fetches the latest release from GitHub for the tailwindcss repository.
    /// </summary>
    /// <returns>
    /// Github release.
    /// </returns>
    internal async Task<GithubRelease> GetLatestGithubRelease()
    {
        const string apiUrl = "https://api.github.com/repos/tailwindlabs/tailwindcss/releases/latest";

        using var request = new HttpRequestMessage( HttpMethod.Get, apiUrl );
        request.Headers.Add( "User-Agent", "lefty-tailwind" );

        using var response = await _http.SendAsync( request );
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GithubRelease>();

        return release!;
    }


    /// <summary>
    /// Determines the best Tailwind CLI binary name for the current platform.
    /// </summary>
    /// <returns>Binary file name (e.g. "tailwindcss-windows-x64.exe")</returns>
    internal static string GetBestTailwindCliBinary()
    {
        // Detect architecture
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => throw new PlatformNotSupportedException( "Only x64 and arm64 are supported." )
        };

        // Detect OS
        if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) == true )
        {
            return $"tailwindcss-windows-{arch}.exe";
        }
        else if ( RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) == true )
        {
            return $"tailwindcss-macos-{arch}";
        }
        else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) == true )
        {
            // Prefer glibc build by default (no "-musl"), unless you know you need musl (Alpine Linux)
            bool isMusl = IsMuslLibc();

            return isMusl
                ? $"tailwindcss-linux-{arch}-musl"
                : $"tailwindcss-linux-{arch}";
        }

        throw new PlatformNotSupportedException( "Unsupported OS platform." );
    }


    /// <summary>
    /// Basic heuristic to detect musl-based Linux (Alpine).
    /// </summary>
    internal static bool IsMuslLibc()
    {
        try
        {
            // Look for "musl" in ldd version output (works on Alpine)
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ldd",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = System.Diagnostics.Process.Start( psi );
            string output = proc!.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
            proc.WaitForExit( 1000 );

            return output.ToLowerInvariant().Contains( "musl" );
        }
        catch
        {
            // If ldd isn't available or fails, assume glibc
            return false;
        }
    }



    /// <summary />
    internal static string ResolveDirectory( string path )
    {
        // Determine environment
        var env = Environment.GetEnvironmentVariable( "ASPNETCORE_ENVIRONMENT" )
            ?? Environment.GetEnvironmentVariable( "DOTNET_ENVIRONMENT" )
            ?? "Production";

        // If not development, always use base directory
        if ( env.Equals( "Development", StringComparison.OrdinalIgnoreCase ) == false )
            return AppContext.BaseDirectory;

        // Development mode
        var startDir = new DirectoryInfo( AppContext.BaseDirectory );
        string endDir;

        if ( path.StartsWith( "~" ) == true )
        {
            var slnRoot = WalkUpTo( startDir, d => d.GetFiles( "*.sln" ).Any() || d.GetFiles( "*.slnx" ).Any() );

            endDir = Path.Combine( slnRoot?.FullName ?? AppContext.BaseDirectory,
                                "." + path.Substring( 1 ) );
        }
        else
        {
            // Look for project root (with .csproj)
            var projRoot = WalkUpTo( startDir, d => d.GetFiles( "*.csproj" ).Any() );

            endDir = Path.Combine( projRoot?.FullName ?? AppContext.BaseDirectory, path );
        }

        return Path.GetFullPath( endDir );
    }


    /// <summary />
    private static DirectoryInfo? WalkUpTo( DirectoryInfo start, Func<DirectoryInfo, bool> predicate )
    {
        var dir = start;

        while ( dir != null )
        {
            if ( predicate( dir ) == true )
                return dir;

            dir = dir.Parent;
        }

        return null;
    }


    /// <summary>
    /// Downloads a file from the specified URL and saves it to the given path, overwriting if it exists.
    /// </summary>
    public async Task DownloadTo( string url, string filePath )
    {
        if ( string.IsNullOrWhiteSpace( url ) )
            throw new ArgumentException( "URL cannot be null or empty.", nameof( url ) );
        if ( string.IsNullOrWhiteSpace( filePath ) )
            throw new ArgumentException( "File path cannot be null or empty.", nameof( filePath ) );

        using var response = await _http.GetAsync( url, HttpCompletionOption.ResponseHeadersRead );
        response.EnsureSuccessStatusCode();

        // Ensure directory exists
        Directory.CreateDirectory( Path.GetDirectoryName( filePath )! );

        // Open file for writing (overwrite)
        await using var fs = new FileStream( filePath, FileMode.Create, FileAccess.Write, FileShare.None );
        await response.Content.CopyToAsync( fs );
    }
}