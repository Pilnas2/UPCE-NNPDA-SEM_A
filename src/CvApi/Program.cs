using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using CvApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
// add limit for input data(10MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = DocumentService.MaxBytes + 65536);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = DocumentService.MaxBytes + 65536);
builder.Services.AddDbContext<CvDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("CvDatabase") ?? "Data Source=cv.db",
    sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
builder.Services.AddSingleton<IPasswordHasher<ApiUser>, PasswordHasher<ApiUser>>();
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddHttpClient<ICvAi, OpenAiService>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IConfiguration>((o, config) =>
{
    var key = config["Jwt:Key"] ?? "";
    if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Nastavte Jwt__Key na náhodný klíč o délce nejméně 32 bajtů.");
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidIssuer = config["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = config["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.Zero,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = ClaimTypes.NameIdentifier
    };
});
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "CV REST API", Version = "v1", Description = "SEM_A" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Add token" });
    o.OperationFilter<SecurityDocumentation>();
});
var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var status = error switch { ApiException e => e.Status, BadHttpRequestException e => e.StatusCode, _ => 500 };
    await Results.Problem(statusCode: status, title: status == 500 ? "Neočekávaná chyba serveru." : error?.Message).ExecuteAsync(context);
}));
app.UseStatusCodePages(async ctx => await Results.Problem(statusCode: ctx.HttpContext.Response.StatusCode).ExecuteAsync(ctx.HttpContext));
// Documentation is public so the login operation can be discovered. Business endpoints require JWT.
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    _ = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme); // check JWT settings(short key, etc..)
    var db = scope.ServiceProvider.GetRequiredService<CvDb>();
    await db.Database.EnsureCreatedAsync();
    var username = app.Configuration["SeedUser:Username"];
    var password = app.Configuration["SeedUser:Password"];
    if (!string.IsNullOrWhiteSpace(username) && !await db.Users.AnyAsync(x => x.Username == username))
    {
        var user = new ApiUser { Username = username };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApiUser>>().HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
app.Run();
public partial class Program;

namespace CvApi
{
    public sealed class SecurityDocumentation : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
    {
        public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
        {
            operation.Summary = context.MethodInfo.Name switch
            {
                "Login" => "Ověří heslo a vydá JWT",
                "Extract" => "Extrahuje celý text",
                "Check" => "Zkontroluje skrytý, drobný a podezřelý text",
                "Parse" => "Převede text CV na strukturované JSON pomocí OpenAI.",
                "Create" => "Uloží strukturované CV",
                "List" => "Vrátí seznam CV",
                "Get" => "Vrátí CV.",
                "Update" => "Nahradí celé strukturované CV (PUT).",
                "Delete" => "Odstraní CV.",
                "Analyze" => "Porovná profesní údaje CV s nabídkou pomocí OpenAI.",
                _ => operation.Summary
            };
            operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Neplatný vstup" });
            operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Neočekávaná chyba serveru." });
            if (context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any()) return;
            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() }];
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Chybějící, neplatný nebo expirovaný JWT." });
        }
    }
}
