using Serilog;

namespace Lefty.Tailwind.Host;

/// <summary />
public class Program
{
    /// <summary />
    public static void Main( string[] args )
    {
        var builder = WebApplication.CreateBuilder( args );

        builder.Services.AddSerilog( ( svc, lc ) =>
        {
            lc
                .ReadFrom.Configuration( builder.Configuration )
                .ReadFrom.Services( svc )
                .Enrich.FromLogContext();
        } );

        // Add services to the container.
        builder.Services.AddRazorPages();

        builder.Services.AddOptions();
        builder.Services.AddHttpClient();

        builder.Services.AddOptions<TailwindHostedServiceOptions>()
            .Bind( builder.Configuration.GetSection( "Tailwind" ) );

        builder.Services.AddHostedService<TailwindHostedService>();


        /*
         * 
         */
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if ( app.Environment.IsDevelopment() == false )
        {
            app.UseExceptionHandler( "/Error" );
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();

        app.Run();
    }
}