Lefty.Tailwind
=====================================================================

[![CI](https://github.com/filipetoscano/tailwind/workflows/CI/badge.svg)](https://github.com/filipetoscano/tailwind/actions)
[![NuGet](https://img.shields.io/nuget/vpre/lefty.tailwind.svg?label=NuGet)](https://www.nuget.org/packages/Lefty.Tailwind/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

Run Tailwind CLI automatically as part of ASP.NET host.


Features
---------------------------------------------------------------------

* Runs Tailwind CLI as part of .NET host
* Automatically downloads Tailwind CLI from Github
* Checks for updates (may be turned off)
* Skips update checks if internet is not available
* Monitors input file: restarts Tailwind CLI on changes
* Monitors configuration files: restarts Tailwind CLI on changes


Installing
---------------------------------------------------------------------

```
dotnet package add Lefty.Tailwind
```


Setup
---------------------------------------------------------------------

Add to program/entry class:

```
builder.Services.AddOptions();
builder.Services.AddHttpClient();

builder.Services.AddOptions<TailwindHostedServiceOptions>()
	.Bind( builder.Configuration.GetSection( "Tailwind" ) );

if ( builder.Environment.IsDevelopment() == true )
	builder.Services.AddHostedService<TailwindHostedService>();
```


Configuration
---------------------------------------------------------------------

The following configuration keys are available.

| Key             | Type | Default | Description
|-----------------|------|---------|------------------------------------
| `DownloadTo`    | Path | `~/bin` | Relative path where the Tailwind CLI binary shall be downloaded to.
| `AllowUpdates`  | bool | `true`  | Whether to check for updates.
| `MaxLatestCheckAgeMins` | int | 24x60 | How often should check for updates.
| `UseDraft`      | bool | `false` | Whether to use draft releases of CLI
| `UsePreRelease` | bool | `false` | Whether to use pre-release versions of CLI
| `InputFile`     | path | `./wwwroot/css/input.css`  | Source file.
| `OutputFile`    | path | `./wwwroot/css/output.css` | Output file.
| `OutputMinify`  | bool | `false` | Whether to optimize and minify output file.
| `OutputOptimize`| bool | `false` | Whether to optimize output file.


License
--------------------------------------------------------------------------

MIT License