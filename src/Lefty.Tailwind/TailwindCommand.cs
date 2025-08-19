namespace Lefty.Tailwind;

/// <summary />
public class TailwindCommand
{
    /// <summary />
    public required string Tailwind { get; set; }

    /// <summary />
    public required string InputFile { get; set; }

    /// <summary />
    public required string OutputFile { get; set; }

    /// <summary />
    public required bool OutputMinify { get; set; }

    /// <summary />
    public required bool OutputOptimize { get; set; }


    /// <summary />
    public string AsArguments()
    {
        var s = $"--watch --input {InputFile} --output {OutputFile}";

        if ( this.OutputMinify == true )
            s += " --minify";
        else if ( this.OutputOptimize == true )
            s += " --optimize";

        return s;
    }
}