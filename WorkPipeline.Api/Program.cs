using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WorkPipeline.Api.BackgroundServices;
using WorkPipeline.Core.Interfaces;
using WorkPipeline.Infrastructure.Data;
using WorkPipeline.Infrastructure.Hubs;
using WorkPipeline.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── EF Core / SQLite ───────────────────────────────────────────────────────────
var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "workpipeline.db");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

// ── JWT Auth ───────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? throw new InvalidOperationException("Auth:JwtSecret is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
        // Allow JWT via query string for SignalR WebSocket connections
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ── CORS ───────────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "https://localhost:5173")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// ── SignalR ────────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── HTTP Clients ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("jira");
builder.Services.AddHttpClient("github");
builder.Services.AddHttpClient("anthropic");

// ── Application Services ───────────────────────────────────────────────────────
builder.Services.AddSingleton<ITicketQueue, TicketQueue>();
builder.Services.AddScoped<IJiraService, JiraService>();
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<IClaudeCodeService, ClaudeCodeService>();
builder.Services.Configure<WorkflowOptions>(builder.Configuration.GetSection("Workflow"));
builder.Services.AddScoped<WorkflowOrchestrator>();
builder.Services.AddHostedService<TicketQueueProcessor>();

builder.Services.AddControllers();

var app = builder.Build();

// ── Auto-migrate on startup ────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var dbCtx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbCtx.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<WorkflowHub>("/hubs/workflow");

// Serve built frontend in production
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
