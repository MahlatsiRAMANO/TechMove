using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TechMove.Api.Auth;
using TechMove.Api.Data;
using TechMove.Api.Data.Repositories;
using TechMove.Api.Dtos;
using TechMove.Api.Infrastructure;
using TechMove.Api.Services;
using TechMove.Api.Services.Currency;
using TechMove.Api.Services.Files;
using TechMove.Api.Services.Workflow;

var builder = WebApplication.CreateBuilder(args);

// ---- Controllers + JSON (enums as strings, e.g. "status": "Active") ----
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// ---- Database: the API is the ONLY component that talks to SQL Server ----
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5)));

// ---- Options ----
builder.Services.Configure<ExchangeRateApiOptions>(builder.Configuration.GetSection(ExchangeRateApiOptions.SectionName));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

// ---- Repositories (data access) ----
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();

// ---- Service layer (business logic moved here from the Part 2 MVC app) ----
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddSingleton<ICurrencyConverter, CurrencyConverter>();
builder.Services.AddSingleton<IContractWorkflow, ContractWorkflow>();
builder.Services.AddSingleton<IServiceRequestFactory, ServiceRequestFactory>();
builder.Services.AddSingleton<IFileValidator, PdfFileValidator>();
builder.Services.AddSingleton<DtoMapper>();
builder.Services.AddSingleton<IFileStorageService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var root = Path.IsPathRooted(options.RootPath) ? options.RootPath : Path.Combine(env.ContentRootPath, options.RootPath);
    return new LocalFileStorageService(root);
});

// ---- External currency API (typed HttpClient) ----
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IExchangeRateProvider, ExchangeRateApiProvider>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ExchangeRateApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// ---- Authentication: JWT bearer tokens ----
builder.Services.AddSingleton<IUserStore, ConfigurationUserStore>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters long.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtTokenService.SigningKey(jwt.Key),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

// ---- Swagger / OpenAPI (self-documenting and testable, with an Authorize button) ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TechMove API",
        Version = "v1",
        Description = "Service layer for TechMove contracts, clients and service requests. " +
                      "Call POST /api/auth/login (admin / Admin@123), then click Authorize and paste the token."
    });

    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from POST /api/auth/login.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", bearer);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = Array.Empty<string>() });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml))
    {
        c.IncludeXmlComments(xml);
    }
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TechMove API v1");
    c.DocumentTitle = "TechMove API";
});

// Inside Docker the API listens on plain HTTP on the private network, so no HTTPS redirect there.
if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await DatabaseInitializer.InitialiseAsync(app.Services, app.Logger);

app.Run();

/// <summary>Exposed so integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program { }
