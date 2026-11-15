using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SportClub.Api.Infrastructure;

/// <summary>Swagger groups in the order they are shown, with a short explanation for each.</summary>
public static class ApiTags
{
    public const string Auth = "1. Sisselogimine";
    public const string Schedule = "2. Tunniplaan";
    public const string Booking = "3. Broneerimine";
    public const string Waitlist = "4. Ootejärjekord";
    public const string Passes = "5. Kuukaardid";
    public const string Trainer = "6. Treener";
    public const string Admin = "7. Admin – haldus";
    public const string Worker = "8. Taustateenus (Worker)";

    public static readonly (string Name, string Description)[] All =
    {
        (Auth, "Alusta siit: logi sisse, kopeeri vastusest **token** ja vajuta üleval **Authorize**."),
        (Schedule, "Treeningute tunniplaan. Vaatamiseks ei pea sisse logima."),
        (Booking, "Klient broneerib koha, vaatab oma broneeringuid ja tühistab. Täis treening → **409**."),
        (Waitlist, "Täis treeningu ootejärjekord. Kui koht vabaneb, viib Worker järjekorras esimese treeningule."),
        (Passes, "Kuukaardi paketid ja ostmine. Iga broneering kasutab ühe külastuse."),
        (Trainer, "Treeneri vaade: oma treeningud, osalejad, kohalolu ja statistika."),
        (Admin, "Klubi haldus: treenerid, treeningutüübid, tunniplaan, paketid, kuukaardid ja kasutajad."),
        (Worker, "Need otspunktid kutsub välja eraldi Worker-programm rolliga **Service**.")
    };
}

/// <summary>
/// Adds the lock and the "who can call this" line only to endpoints that need login.
/// Login, registration and the timetable stay open (no lock).
/// </summary>
public class RoleInfoOperationFilter : IOperationFilter
{
    private static readonly Dictionary<string, string> RoleNames = new()
    {
        ["Admin"] = "Admin", ["Trainer"] = "Treener", ["Client"] = "Klient", ["Service"] = "Taustateenus"
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var attrs = method.GetCustomAttributes(true).Concat(method.DeclaringType!.GetCustomAttributes(true)).ToList();

        if (method.GetCustomAttribute<AllowAnonymousAttribute>() is not null || !attrs.OfType<AuthorizeAttribute>().Any())
        {
            operation.Description = Prepend("🔓 **Sisselogimine pole vajalik.**", operation.Description);
            return;
        }

        // The most specific [Authorize(Roles=...)] wins: method first, then controller.
        var roles = method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).FirstOrDefault(r => r is not null)
                    ?? method.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).FirstOrDefault(r => r is not null);
        var who = roles is null
            ? "iga sisseloginud kasutaja"
            : string.Join(", ", roles.Split(',').Select(r => RoleNames.GetValueOrDefault(r.Trim(), r.Trim())));

        operation.Description = Prepend($"🔒 **Kes saab kasutada:** {who}", operation.Description);
        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
            }
        };
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Pole sisse logitud (token puudub või on aegunud)" });
        if (roles is not null)
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Sinu rollil pole selleks õigust" });
    }

    private static string Prepend(string line, string? text) => string.IsNullOrWhiteSpace(text) ? line : $"{line}\n\n{text}";
}

/// <summary>Puts the groups in a fixed order and adds their descriptions.</summary>
public class TagOrderDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument doc, DocumentFilterContext context)
    {
        doc.Tags = ApiTags.All.Select(t => new OpenApiTag { Name = t.Name, Description = t.Description }).ToList();
    }
}

public static class SwaggerSetup
{
    public static void Configure(SwaggerGenOptions c)
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "SportClub API",
            Version = "v1",
            Description = """
                Spordiklubi grupitreeningute tunniplaan ja broneerimine.

                **Kuidas proovida:**
                1. Ava **1. Sisselogimine → POST /api/auth/login**, vajuta *Try it out* ja *Execute* (näidisandmed on juba täidetud).
                2. Kopeeri vastusest `token`.
                3. Vajuta paremal üleval **Authorize**, kleebi token ja vajuta *Authorize*.
                4. Nüüd töötavad ka lukuga otspunktid.

                **Testkontod:** klient@sportclub.ee / Klient123! · treener@sportclub.ee / Treener123! · admin@sportclub.ee / Admin123! · worker@sportclub.ee / Worker123!

                **Vastuse koodid:** 200 OK · 201 loodud · 204 tehtud (sisu pole) · 400 vigased andmed · 401 pole sisse logitud · 403 pole õigust · 404 ei leitud · 409 konflikt (nt treening on täis).
                Vead tulevad alati ühesuguse ProblemDetails kujul: `status`, `title`, `detail`.
                """
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Kleebi siia token, mille said POST /api/auth/login vastusest (ilma sõnata Bearer)."
        });


        // Order: by group (as listed in ApiTags), then by path, then GET before POST/PUT/PATCH/DELETE.
        var order = ApiTags.All.Select((t, i) => (t.Name, i)).ToDictionary(x => x.Name, x => x.i);
        string[] methods = { "GET", "POST", "PUT", "PATCH", "DELETE" };
        c.OrderActionsBy(api =>
        {
            var tag = api.ActionDescriptor.EndpointMetadata.OfType<TagsAttribute>().LastOrDefault()?.Tags.FirstOrDefault() ?? "";
            var m = Array.IndexOf(methods, api.HttpMethod ?? "GET");
            return $"{order.GetValueOrDefault(tag, 99):D2}_{api.RelativePath}_{m}";
        });

        // Show TimeSpan-free, readable examples for DateTime query params.
        c.MapType<DateTime>(() => new OpenApiSchema { Type = "string", Format = "date-time", Example = new OpenApiString("2026-11-16T00:00:00Z") });

        foreach (var xml in new[] { Assembly.GetExecutingAssembly().GetName().Name, "SportClub.Core" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"{xml}.xml");
            if (File.Exists(path)) c.IncludeXmlComments(path);
        }

        // Registered after the XML comments so the role line is added on top of the remarks, not overwritten by them.
        c.OperationFilter<RoleInfoOperationFilter>();
        c.DocumentFilter<TagOrderDocumentFilter>();
    }
}
