using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Unix;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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
        /*
         *
         */
        var cts = new CancellationTokenSource();

        _om.OnChange( ( x, _ ) =>
        {
            _logger.LogWarning( "Tailwind: Configuration reloaded" );

            cts.Cancel();
        } );


        /*
         * 
         */
        while ( stoppingToken.IsCancellationRequested == false )
        {
            var lts = CancellationTokenSource.CreateLinkedTokenSource( stoppingToken, cts.Token );

            var opt = _om.CurrentValue;
            var tw = await EnsureTailwind( opt, stoppingToken );
            var cmd = ExpandCommand( opt, tw );
            FileSystemWatcher? fsw = null;


            /*
             * 
             */
            if ( cmd == null )
            {
                _logger.LogWarning( "** Nope" );

                await Task.Delay( Timeout.Infinite, lts.Token )
                    .ContinueWith( ( _ ) => { } );
            }
            else
            {
                fsw = MonitorFile( cmd.InputFile, ( ea ) =>
                {
                    _logger.LogWarning( "Tailwind: Input file changed" );

                    cts.Cancel();
                } );


                /*
                 * 
                 */
                await File.WriteAllTextAsync( cmd.OutputFile, "/* empty */" );
                await RunTailwind( cmd.Tailwind, cmd.AsArguments(), lts.Token );
            }


            /*
             * 
             */
            if ( cts.IsCancellationRequested == true )
            {
                fsw?.Dispose();
                cts.Dispose();

                cts = new CancellationTokenSource();
            }
        }


        /*
         * 
         */
        cts.Dispose();
    }


    /// <summary>
    /// Ensures that the Tailwind binary is available.
    /// </summary>
    internal async Task<string?> EnsureTailwind( TailwindHostedServiceOptions opt, CancellationToken cancellationToken )
    {
        /*
         * Whe
         */
        var targetDir = ResolvePath( opt.DownloadTo );
        _logger.LogDebug( "Target {TargetDir}", targetDir );

        var bin = GetBestTailwindCliBinary( cancellationToken );
        var targetExe = Path.Combine( targetDir, bin );


        /*
         * 
         */
        string? currentVersion = null;

        if ( File.Exists( targetExe ) == true )
        {
            if ( await IsLatestCheckYoung( targetExe, opt, cancellationToken ) == true )
            {
                _logger.LogInformation( "Tailwind available, and latest check is recent: using as is" );
                return targetExe;
            }

            if ( opt.AllowUpdates == false )
            {
                _logger.LogInformation( "Tailwind available, but will not check for updates: using as is" );
                return targetExe;
            }

            if ( await HasInternet() == false )
            {
                _logger.LogInformation( "Tailwind available, but have no internet atm: using as is" );
                return targetExe;
            }

            currentVersion = await GetCurrentVersion( targetExe, cancellationToken );
            _logger.LogDebug( "Current: {Version}", currentVersion );
        }


        /*
         * Happy path: latest version is the version we need :)
         */
        var latest = await GetLatestGithubRelease( opt.GithubToken, cancellationToken );

        if ( latest == null )
        {
            if ( File.Exists( targetExe ) == true )
            {
                _logger.LogInformation( "Tailwind available but failed to check latest version, using as is" );
                return targetExe;
            }
            else
            {
                return null;
            }
        }

        await WriteLatestCheck( targetExe, cancellationToken );


        /*
         * 
         */
        if ( latest.IsDraft == true || latest.IsPreRelease == true )
        {
            var useLatest = false;

            if ( latest.IsDraft == true && opt.UseDraft == true )
                useLatest = true;

            if ( latest.IsPreRelease == true && opt.UsePreRelease == true )
                useLatest = true;

            if ( useLatest == false )
            {
                var all = await GetAllGithubReleases( opt.GithubToken, cancellationToken );
                _logger.LogDebug( "Tailwind: Has {NrReleases} releases", all.Count );

                var allq = all.AsQueryable();

                if ( opt.UseDraft == false )
                    allq = allq.Where( x => x.IsDraft == false );

                if ( opt.UsePreRelease == false )
                    allq = allq.Where( x => x.IsPreRelease == false );

                var candidate = allq.FirstOrDefault();

                if ( candidate == null )
                {
                    _logger.LogWarning( "No matching release found: use draft={UseDraft}, use pre-release={UsePreRelease}", opt.UseDraft, opt.UsePreRelease );
                    return null;
                }

                latest = candidate;
            }
        }


        /*
         * 
         */
        var asset = latest.Assets?.SingleOrDefault( x => x.Name == bin );

        if ( asset == null )
        {
            _logger.LogWarning( "No tailwind for arch/OS: no asset with name {AssetName} found", bin );
            return null;
        }

        _logger.LogDebug( "Latest {Version} - Binary {Binary}, Size {Size}", latest.TagName, asset.Name, asset.Size );

        if ( latest.TagName == currentVersion )
        {
            _logger.LogInformation( "Tailwind already up-to-date" );
            return targetExe;
        }


        /*
         * 
         */
        _logger.LogInformation( "Downloading from {Url}...", asset.DownloadUrl );
        await DownloadTo( asset.DownloadUrl, targetExe, cancellationToken );


        /*
         * Ensure +x
         */
        if ( RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) == true ||
             RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) == true )
        {
            var info = new UnixFileInfo( targetExe );
            info.FileAccessPermissions |= FileAccessPermissions.UserExecute;
        }

        return targetExe;
    }


    /// <summary />
    internal TailwindCommand? ExpandCommand( TailwindHostedServiceOptions opt, string? binary )
    {
        if ( binary == null )
            return null;

        var input = ResolvePath( opt.InputFile );
        var output = ResolvePath( opt.OutputFile );

        return new TailwindCommand()
        {
            Tailwind = binary,
            InputFile = input,
            OutputFile = output,
            OutputMinify = opt.OutputMinify,
            OutputOptimize = opt.OutputOptimize,
        };
    }


    /// <summary>
    /// Starts the Tailwind CLI binary and streams output to the logger.
    /// </summary>
    /// <param name="binaryPath">Path to tailwind binary (e.g. "tailwindcss").</param>
    /// <param name="arguments">Arguments to pass to Tailwind CLI.</param>
    /// <param name="cancellationToken">Cancellation token to kill the process.</param>
    internal async Task<int> RunTailwind( string binaryPath, string arguments, CancellationToken cancellationToken )
    {
        var tcs = new TaskCompletionSource<int>( TaskCreationOptions.RunContinuationsAsynchronously );

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },

            EnableRaisingEvents = true,
        };


        /*
         * Hook up output events
         */
        process.OutputDataReceived += ( s, e ) =>
        {
            if ( string.IsNullOrEmpty( e.Data ) == true )
                return;

            _logger.LogDebug( "Tailwind: {Line}", e.Data );
        };
        process.ErrorDataReceived += ( s, e ) =>
        {
            if ( string.IsNullOrEmpty( e.Data ) == true )
                return;

            _logger.LogDebug( "Tailwind: {Line}", e.Data );
        };

        process.Exited += ( s, e ) =>
        {
            tcs.TrySetResult( process.ExitCode );
            process.Dispose();
        };

        try
        {
            if ( process.Start() == false )
                throw new InvalidOperationException( $"Failed to start {binaryPath}" );

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Cancel process if token triggers
            using ( cancellationToken.Register( () =>
            {
                try
                {
                    if ( process.HasExited == false )
                        process.Kill( true );
                }
                catch
                {
                }
            } ) )
            {
                return await tcs.Task.ConfigureAwait( false );
            }
        }
        catch ( TaskCanceledException )
        {
            // ?
            return -2;
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex, "Failed to run Tailwind binary" );
            return -1;
        }
    }


    /// <summary />
    internal async Task WriteLatestCheck( string targetExe, CancellationToken cancellationToken )
    {
        var path = Path.Combine( Path.GetDirectoryName( targetExe )!, "lefty-tailwind.txt" );
        var contents = DateTime.UtcNow.ToString( "o" );

        await File.WriteAllTextAsync( path, contents, cancellationToken );
    }


    /// <summary />
    internal async Task<bool> IsLatestCheckYoung( string targetExe, TailwindHostedServiceOptions opt, CancellationToken cancellationToken )
    {
        if ( opt.MaxLatestCheckAgeMins == 0 )
            return false;


        /*
         * 
         */
        var path = Path.Combine( Path.GetDirectoryName( targetExe )!, "lefty-tailwind.txt" );

        if ( File.Exists( path ) == false )
            return false;

        var contents = await File.ReadAllTextAsync( path, cancellationToken );

        if ( DateTime.TryParseExact( contents, "o", null, System.Globalization.DateTimeStyles.None, out var age ) == false )
            return false;


        /*
         * 
         */
        var now = DateTime.UtcNow;
        var delta = now - age;

        if ( delta.TotalMinutes < opt.MaxLatestCheckAgeMins )
            return true;

        return false;
    }


    /// <summary />
    internal FileSystemWatcher MonitorFile( string filePath, Action<FileSystemEventArgs> onChange )
    {
        var directory = Path.GetDirectoryName( filePath );
        var fileName = Path.GetFileName( filePath );

        if ( directory == null )
            throw new ArgumentOutOfRangeException( nameof( filePath ), "Empty directory name" );

        if ( fileName == null )
            throw new ArgumentOutOfRangeException( nameof( fileName ), "Empty file name" );

        var watcher = new FileSystemWatcher( directory, fileName )
        {
            NotifyFilter = NotifyFilters.LastWrite
                         | NotifyFilters.FileName
                         | NotifyFilters.Size,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
        };


        /*
         * 
         */
        watcher.Changed += ( s, e ) => onChange( e );
        watcher.Created += ( s, e ) => onChange( e );
        watcher.Deleted += ( s, e ) => onChange( e );
        watcher.Renamed += ( s, e ) => onChange( e );


        /*
         * 
         */
        watcher.Error += ( s, e ) =>
        {
            _logger.LogWarning( "FileSystemWatcher buffer overflowed. Monitoring restarted." );
            watcher.EnableRaisingEvents = false;
            watcher.EnableRaisingEvents = true;
        };

        return watcher;
    }


    /// <summary />
    internal async Task<bool> HasInternet()
    {
        try
        {
            using var cts = new CancellationTokenSource( TimeSpan.FromSeconds( 2 ) );

            using var response = await _http.GetAsync( "https://www.google.com/generate_204",
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token );

            return response.StatusCode == System.Net.HttpStatusCode.NoContent;
        }
        catch
        {
            return false;
        }
    }


    /// <summary />
    internal async Task<string?> GetCurrentVersion( string targetExe, CancellationToken cancellationToken )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = targetExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = false };

        process.Start();


        /*
         * 
         */
        string? firstLine = await process.StandardOutput.ReadLineAsync().WaitAsync( cancellationToken );

        await process.WaitForExitAsync( cancellationToken );

        if ( string.IsNullOrWhiteSpace( firstLine ) == true )
            return null;


        /*
         * 
         */
        var match = Regex.Match( firstLine, @"v\d+\.\d+\.\d+" );

        if ( match.Success == false )
            return null;

        return match.Value;
    }


    /// <summary>
    /// Fetches the latest release from GitHub for the tailwindcss repository.
    /// </summary>
    /// <returns>
    /// Latest Github release.
    /// </returns>
    internal async Task<GithubRelease> GetLatestGithubRelease( string? githubToken, CancellationToken cancellationToken )
    {
        const string ApiUrl = "https://api.github.com/repos/tailwindlabs/tailwindcss/releases/latest";

        using var request = new HttpRequestMessage( HttpMethod.Get, ApiUrl );
        request.Headers.Add( "X-GitHub-Api-Version", "2022-11-28" );
        request.Headers.Add( "User-Agent", "lefty-tailwind" );

        if ( githubToken != null )
            request.Headers.Authorization = new AuthenticationHeaderValue( "Bearer", githubToken );


        /*
         * 
         */
        using var response = await _http.SendAsync( request, cancellationToken );
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GithubRelease>( cancellationToken );

        return release!;
    }


    /// <summary>
    /// Fetches all releases from GitHub for the tailwindcss repository.
    /// </summary>
    /// <returns>
    /// All Github releases.
    /// </returns>
    internal async Task<List<GithubRelease>> GetAllGithubReleases( string? githubToken, CancellationToken cancellationToken )
    {
        const string ApiUrl = "https://api.github.com/repos/tailwindlabs/tailwindcss/releases";

        using var request = new HttpRequestMessage( HttpMethod.Get, ApiUrl );
        request.Headers.Add( "X-GitHub-Api-Version", "2022-11-28" );
        request.Headers.Add( "User-Agent", "lefty-tailwind" );

        if ( githubToken != null )
            request.Headers.Authorization = new AuthenticationHeaderValue( "Bearer", githubToken );


        /*
         * 
         */
        using var response = await _http.SendAsync( request, cancellationToken );
        response.EnsureSuccessStatusCode();

        var all = await response.Content.ReadFromJsonAsync<List<GithubRelease>>( cancellationToken );

        return all!;
    }


    /// <summary>
    /// Determines the best Tailwind CLI binary name for the current platform.
    /// </summary>
    /// <returns>Binary file name (e.g. "tailwindcss-windows-x64.exe")</returns>
    internal static string GetBestTailwindCliBinary( CancellationToken cancellationToken )
    {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => throw new PlatformNotSupportedException( $"Arch {RuntimeInformation.ProcessArchitecture} is not supported." )
        };


        /*
         * 
         */
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
        // Check path
        var fileName = "/usr/bin/ldd";

        if ( File.Exists( fileName ) == false )
            return false;

        try
        {
            // Look for "musl" in ldd version output (works on Alpine)
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var proc = System.Diagnostics.Process.Start( psi );
            string output = proc!.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
            proc.WaitForExit( 1_000 );

            return output.ToLowerInvariant().Contains( "musl" );
        }
        catch
        {
            // If ldd isn't available or fails, assume glibc
            return false;
        }
    }


    /// <summary />
    internal static string ResolvePath( string path )
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
    internal static DirectoryInfo? WalkUpTo( DirectoryInfo start, Func<DirectoryInfo, bool> predicate )
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
    internal async Task DownloadTo( string url, string filePath, CancellationToken cancellationToken )
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