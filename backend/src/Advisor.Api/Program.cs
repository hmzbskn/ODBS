using System.Text;
using Advisor.Api.Auth;
using Advisor.Application.Analytics;
using Advisor.Application.Auth;
using Advisor.Application.Registration;
using Advisor.Infrastructure;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Güvenli varsayılan: geliştirme anahtarıyla üretimde çalışmayı reddet. Anahtar repoda göründüğü için
// onunla imzalanmış token'ı herkes üretebilir.
var jwtKey = builder.Configuration[$"{JwtOptions.SectionName}:SigningKey"] ?? string.Empty;
var isDevOrTest = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
if (!isDevOrTest && (jwtKey.Length < 32 || jwtKey.Contains("DEV-ONLY", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException(
        "Üretim ortamında Jwt:SigningKey, en az 32 karakterlik gizli bir değer olmalıdır " +
        "(ortam değişkeni Jwt__SigningKey ile verin). Geliştirme anahtarı kullanılamaz.");
}

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "/api/auth/login ile alınan token'ı yapıştırın.",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
    });
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRegistrationRules(
    builder.Configuration.GetSection(RegistrationRuleOptions.SectionName).Get<RegistrationRuleOptions>()
    ?? new RegistrationRuleOptions());

builder.Services.AddAnalytics(
    builder.Configuration.GetSection(RiskOptions.SectionName).Get<RiskOptions>() ?? new RiskOptions());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IStudentAccessGuard, StudentAccessGuard>();

// Kimlik doğrulama: JWT. Ayarlar IOptions üzerinden okunur (testler değiştirebilsin diye
// anahtar burada değil, JwtBearerOptions yapılandırılırken çözülür).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((o, jwt) =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

// Varsayılan: her endpoint giriş ister; herkese açık olanlar [AllowAnonymous] ile işaretlenir.
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// Kaba kuvvet denemelerine karşı giriş hız sınırı (istemci IP'si başına, dakikada N deneme).
var loginPermits = builder.Configuration.GetValue("Auth:LoginAttemptsPerMinute", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Çok fazla giriş denemesi yaptınız. Lütfen bir dakika sonra tekrar deneyin." }, ct);
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
    if (db.Database.IsRelational()) db.Database.Migrate();
    else db.Database.EnsureCreated();
    // Demo hesapları (bilinen şifreli) yalnızca geliştirme/test ortamında kendiliğinden oluşur;
    // üretimde Demo:SeedAccounts=true ile açıkça istenmedikçe oluşturulmaz.
    var seedDemo = app.Configuration.GetValue<bool?>("Demo:SeedAccounts") ?? !app.Environment.IsProduction();
    if (seedDemo) await DemoDataSeeder.SeedAsync(db);
}

app.UseMiddleware<ExceptionMiddleware>();

app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

// WebApplicationFactory<Program> için.
public partial class Program;
