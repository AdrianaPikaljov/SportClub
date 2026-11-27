using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SportClub.Api.Infrastructure;

/// <summary>Swaggeri grupid (rollide kaupa) selles järjekorras, nagu neid näidatakse.</summary>
public static class ApiTags
{
    public const string Auth = "1. Registreerimine ja sisselogimine";
    public const string Guest = "2. Külaline – tunniplaan, treenerid, tüübid, tariifid";
    public const string Client = "3. Klient – kuukaart ja broneeringud";
    public const string Trainer = "4. Treener – treeningud, kohalolu, statistika";
    public const string Admin = "5. Admin – haldus";
    public const string Worker = "6. Taustateenus (Worker)";

    public static readonly (string Name, string Description)[] All =
    {
        (Auth, "Alusta siit: logi sisse, kopeeri vastusest **token** ja vajuta üleval **Authorize**."),
        (Guest, "Vaatamiseks ei pea sisse logima. Peaaegu kõikjal piisab: *Try it out* → *Execute*."),
        (Client, "Kuukaardi ost, registreerimine treeningule (täis treeningul automaatselt ootejärjekorda), tühistamine."),
        (Trainer, "Oma treeningud, osalejate nimekiri ja kohalolu märkimine, statistika oma treeningute kohta."),
        (Admin, "Treenerid, tüübid, tunniplaan, tariifid, kõik broneeringud, kuukaardid ja kasutajad. " +
                "Muutmiseks: GET {id} → kopeeri vastus PUT-i → muuda → Execute."),
        (Worker, "Neid kutsub eraldi Worker-programm rolliga **Service** (service@sportclub.ee).")
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
            operation.Extensions["x-roles"] = new OpenApiString("*"); // kõigile, ka külalisele
            return;
        }

        // The most specific [Authorize(Roles=...)] wins: method first, then controller.
        var roles = method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).FirstOrDefault(r => r is not null)
                    ?? method.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).FirstOrDefault(r => r is not null);
        var who = roles is null
            ? "iga sisseloginud kasutaja"
            : string.Join(", ", roles.Split(',').Select(r => RoleNames.GetValueOrDefault(r.Trim(), r.Trim())));

        operation.Description = Prepend($"🔒 **Kes saab kasutada:** {who}", operation.Description);
        // Swaggeri lehel peidetakse otspunktid, mida sisseloginud roll kasutada ei saa (vt SwaggerRoleFilter).
        operation.Extensions["x-roles"] = new OpenApiString(roles is null ? "any" : string.Join(",", roles.Split(',').Select(r => r.Trim())));
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

/// <summary>
/// Teeb parameetrist "type" (GET /api/sessions) rippmenüü treeningutüüpide nimedega andmebaasist.
/// Swagger ehitab dokumendi igal laadimisel uuesti, nii et uus tüüp ilmub menüüsse kohe.
/// </summary>
public class DropdownOperationFilter : IOperationFilter
{
    private readonly IServiceScopeFactory _scopes;

    public DropdownOperationFilter(IServiceScopeFactory scopes) => _scopes = scopes;

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.HttpMethod != "GET" || context.ApiDescription.RelativePath != "api/sessions") return;
        var parameter = operation.Parameters?.FirstOrDefault(p => p.Name == "type");
        if (parameter?.Schema is null) return;

        using var scope = _scopes.CreateScope();
        var types = scope.ServiceProvider.GetRequiredService<SportClub.Core.Services.ITrainingTypeService>()
            .GetAllAsync().GetAwaiter().GetResult();
        parameter.Schema.Enum = types.Select(t => (IOpenApiAny)new OpenApiString(t.Name)).ToList();
    }
}

/// <summary>Puts the groups in a fixed order and adds their descriptions.</summary>
public class TagOrderDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument doc, DocumentFilterContext context)
    {
        doc.Tags = ApiTags.All.Select(t => new OpenApiTag { Name = t.Name, Description = t.Description }).ToList();
    }
}

/// <summary>
/// Swaggeri lehele lisatav skript: näitab ainult neid otspunkte, mida praegune kasutaja saab kasutada.
/// Külaline (pole Authorize vajutanud) näeb ainult avatud otspunkte, Klient lisaks kliendi omi jne.
/// Roll võetakse Authorize all olevast JWT-st, otspunkti rollid swagger.json-i väljast "x-roles".
/// </summary>
public static class SwaggerRoleFilter
{
    public const string Path = "/swagger-roles.js";

    public const string Script = """
        (function () {
          var ROLE_CLAIMS = ['role', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
          var NAMES = { Guest: 'Külaline', Client: 'Klient', Trainer: 'Treener', Admin: 'Admin', Service: 'Taustateenus' };
          var rolesByOp = null;

          fetch('/swagger/v1/swagger.json').then(function (r) { return r.json(); }).then(function (doc) {
            rolesByOp = {};
            Object.keys(doc.paths || {}).forEach(function (path) {
              Object.keys(doc.paths[path]).forEach(function (method) {
                var op = doc.paths[path][method];
                rolesByOp[method.toLowerCase() + ' ' + path] = (op && op['x-roles']) || '*';
              });
            });
          });

          function currentRole() {
            try {
              var auth = JSON.parse(localStorage.getItem('authorized') || '{}');
              var token = auth.Bearer && auth.Bearer.value;
              if (!token) return 'Guest';
              var part = token.replace(/^Bearer\s+/i, '').split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
              while (part.length % 4) part += '=';
              var payload = JSON.parse(decodeURIComponent(escape(atob(part))));
              if (payload.exp && payload.exp * 1000 < Date.now()) return 'Guest';
              for (var i = 0; i < ROLE_CLAIMS.length; i++) {
                var r = payload[ROLE_CLAIMS[i]];
                if (r) return Array.isArray(r) ? r[0] : r;
              }
              return 'Guest';
            } catch (e) { return 'Guest'; }
          }

          function allowed(roles, role) {
            if (roles === '*') return true;
            if (role === 'Guest') return false;
            if (roles === 'any') return true;
            return roles.split(',').indexOf(role) >= 0;
          }

          function banner(role) {
            var el = document.getElementById('sc-role-banner');
            if (!el) {
              var info = document.querySelector('.swagger-ui .information-container');
              if (!info) return;
              el = document.createElement('div');
              el.id = 'sc-role-banner';
              el.style.cssText = 'margin:12px 0;padding:12px 16px;border-radius:8px;background:#e8f4ff;border:1px solid #9cc9f5;font:14px sans-serif';
              info.parentNode.insertBefore(el, info.nextSibling);
            }
            var text = role === 'Guest'
              ? '👤 Vaade: <b>Külaline</b> – näed ainult avatud otspunkte. Logi sisse ja vajuta <b>Authorize</b>, et näha oma rolli otspunkte.'
              : '👤 Vaade: <b>' + (NAMES[role] || role) + '</b> – näed ainult otspunkte, mida see roll saab kasutada.';
            if (el.innerHTML !== text) el.innerHTML = text;
          }

          function apply() {
            if (!rolesByOp) return;
            var role = currentRole();
            banner(role);
            document.querySelectorAll('.swagger-ui .opblock').forEach(function (block) {
              var methodClass = Array.prototype.find.call(block.classList, function (c) { return /^opblock-(get|post|put|patch|delete)$/.test(c); });
              var pathEl = block.querySelector('.opblock-summary-path');
              if (!methodClass || !pathEl) return;
              var path = pathEl.getAttribute('data-path') || pathEl.textContent.trim();
              var roles = rolesByOp[methodClass.replace('opblock-', '') + ' ' + path] || '*';
              block.style.display = allowed(roles, role) ? '' : 'none';
            });
            // peida grupid, kus pole ühtegi nähtavat otspunkti
            document.querySelectorAll('.swagger-ui .opblock-tag-section').forEach(function (section) {
              var blocks = section.querySelectorAll('.opblock');
              if (!blocks.length) return; // suletud grupp – ei tea sisu, jätame alles
              var visible = Array.prototype.some.call(blocks, function (b) { return b.style.display !== 'none'; });
              section.style.display = visible ? '' : 'none';
            });
          }

          setInterval(apply, 400);
        })();
        """;
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
                1. Ava **1. Registreerimine ja sisselogimine → POST /api/auth/login**, vajuta *Try it out* ja *Execute* (näidisandmed on juba täidetud).
                2. Kopeeri vastusest `token`.
                3. Vajuta paremal üleval **Authorize**, kleebi token ja vajuta *Authorize*.
                4. Nüüd töötavad ka lukuga otspunktid.

                **Testkontod:** klient@sportclub.ee / Klient123! · treener@sportclub.ee / Treener123! · admin@sportclub.ee / Admin123! · service@sportclub.ee / Service123!

                Kuupäevad on kujul **2026-11-27**, kellaajad Eesti aja järgi (**2026-11-27 18:00**).

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

        foreach (var xml in new[] { Assembly.GetExecutingAssembly().GetName().Name, "SportClub.Core" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"{xml}.xml");
            if (File.Exists(path)) c.IncludeXmlComments(path);
        }

        // Registered after the XML comments so the role line is added on top of the remarks, not overwritten by them.
        c.OperationFilter<RoleInfoOperationFilter>();
        c.OperationFilter<DropdownOperationFilter>();
        c.DocumentFilter<TagOrderDocumentFilter>();
    }
}
