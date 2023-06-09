// Copyright (c) 2022 Quetzal Rivera.
// Licensed under the GNU General Public License v3.0, See LICENCE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Ngrok.AgentAPI;
using SauceNAO.Core;
using SauceNAO.Infrastructure;
using SauceNAO.Infrastructure.Data;
using SauceNAO.Webhook.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure database context
var connectionString = builder.Configuration.GetConnectionString("Default");

switch (builder.Configuration["DbProvider"])
{
    case "SQLite":
    case "sqlite":
    case "lite":
    case "Lite":
    default:
        // Use SQLite Server. Default.
        builder.Services.AddDbContext<SauceNaoContext>(options => options.UseSqlite(connectionString));
        break;
    case "SqlServer":
    case "sqlserver":
    case "mssql":
    case "sql":
        // Use SQL Server.
        builder.Services.AddDbContext<SauceNaoContext>(options => options.UseSqlServer(connectionString));
        break;
}

// Configure cache context
var cacheConnection = $"Data Source={Path.GetTempFileName()}"; // Get connection string for cache
builder.Services.AddDbContext<CacheDbContext>(options => options.UseSqlite(cacheConnection));

// Add temp repository
builder.Services.AddScoped<TemporalFileRepository>();

builder.Services.AddScoped<ISauceDatabase, BotDb>(); // Bot data class

// Ensure start ngrok tunnel
string appUrl;
{
    var ngrok = builder.Configuration.GetSection("Ngrok");
    var agentapiurl = ngrok["ApiUrl"];

    var agent = string.IsNullOrEmpty(agentapiurl) ? new NgrokAgentClient() : new NgrokAgentClient(agentapiurl);
    var tunnelName = ngrok["TunnelName"] ?? "SnaoTunnel";
    var tunnel = agent.ListTunnels().Tunnels.FirstOrDefault(t => t.Name == tunnelName);

    if (tunnel != null)
    {
        agent.StopTunnel(tunnelName);
    }

    var port = ngrok["Port"];
    var hostheader = string.Format("localhost:{0}", port);
    var address = string.Format("https://{0}", hostheader);

    var tunnelConfig = new HttpTunnelConfiguration(tunnelName, address)
    {
        HostHeader = hostheader,
        Schemes = new string[] { "https" }
    };
    tunnel = agent.StartTunnel(tunnelConfig);

    appUrl = tunnel.PublicUrl;
}

// Add Telegram Bot Configurtaion
builder.Services.AddSingleton<SnaoBotProperties>();

// Add Telegram Bot
builder.Services.AddScoped<SauceNaoBot>();

// Add Data Cleaner service
builder.Services.AddHostedService<CleanerService>();

var app = builder.Build();

// Create database if not exists
using (var scope = app.Services.CreateScope())
{
    using var context = scope.ServiceProvider.GetRequiredService<SauceNaoContext>();
#if DEBUG
    context.Database.EnsureDeleted(); // Delete database
    context.Database.EnsureCreated(); // Create database without migrations
#else
    context.Database.EnsureCreated(); // Create database without migrations
    // context.Database.Migrate(); // Create database using migrations
#endif
}

using (var scope = app.Services.CreateScope())
{
    using var context = scope.ServiceProvider.GetRequiredService<CacheDbContext>();
    // Create cache file
    context.Database.EnsureCreated();
    // Initialize bot
    _ = scope.ServiceProvider.GetRequiredService<SnaoBotProperties>();
}

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
