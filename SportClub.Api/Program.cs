using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SportClub.Api.Infrastructure;
using SportClub.Api.Realtime;
using SportClub.Core.Interfaces;
using SportClub.Core.Services;
using SportClub.Infrastructure;
using SportClub.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------- Data + services (DI) ----------
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default") ?? "Data Source=sportclub.db");

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<WebSocketSessionNotifier>();
builder.Services.AddSingleton<ISessionNotifier>(sp => sp.GetRequiredService<WebSocketSessionNotifier>());

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITrainerService, TrainerService>();
builder.Services.AddScoped<ITrainingTypeService, TrainingTypeService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IWaitlistService, WaitlistService>();
builder.Services.AddScoped<IMonthlyPassService, MonthlyPassService>();
builder.Services.AddScoped<IPassPlanService, PassPlanService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IStatsService, StatsService>();

// ---------- JWT ----------
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters (appsettings.json).");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        // WebSocket clients cannot set headers, so the token comes as ?access_token=...
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/ws"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ---------- Errors + logging ----------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- MVC + JSON ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- CORS (React admin) ----------
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o => o.AddPolicy("Admin", p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SwaggerSetup.Configure);

var app = builder.Build();

// ---------- Database: migrations + initial data ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.GetMigrations().Any())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync(); // fallback if migrations were not generated yet
    await SeedData.SeedRuntimeAsync(db, DateTime.UtcNow);
}

// ---------- Pipeline ----------
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SportClub API v1");
    c.DocumentTitle = "SportClub API";
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List); // groups open, endpoints closed
    c.DefaultModelsExpandDepth(-1);      // hide the long "Schemas" list at the bottom
    c.EnableTryItOutByDefault();         // fields are editable right away
    c.DisplayRequestDuration();
    c.EnablePersistAuthorization();      // token stays after page refresh
});

app.UseCors("Admin");
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Realtime: ws://host/ws/sessions?access_token=<JWT>  →  {"type":"FreeSpotsChanged","sessionId":1,"free":3}
app.Map("/ws/sessions", async (HttpContext context, WebSocketSessionNotifier notifier) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket request expected.");
        return;
    }
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await notifier.HandleConnectionAsync(socket, context.User.Identity?.Name ?? "?", context.RequestAborted);
}).RequireAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

/// <summary>Needed for integration tests (WebApplicationFactory).</summary>
public partial class Program { }
