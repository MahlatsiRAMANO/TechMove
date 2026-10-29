using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Options;
using TechMove.Web.Infrastructure;
using TechMove.Web.Services.Api;

var builder = WebApplication.CreateBuilder(args);

// Every page requires a signed-in user unless marked [AllowAnonymous] (the login page).
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
    options.Filters.Add<ApiExceptionFilter>();
});

// ---- Authentication: cookie for the browser, holding the JWT issued by the API ----
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "TechMove.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false;
    });

// ---- The frontend talks to the TechMove API over HTTP - there is NO database here ----
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();

void ConfigureApiClient(IServiceProvider sp, HttpClient client)
{
    var options = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
    var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
}

builder.Services.AddHttpClient<IAuthApi, AuthApi>(ConfigureApiClient);
builder.Services.AddHttpClient<IClientsApi, ClientsApi>(ConfigureApiClient).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IContractsApi, ContractsApi>(ConfigureApiClient).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IServiceRequestsApi, ServiceRequestsApi>(ConfigureApiClient).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IDashboardApi, DashboardApi>(ConfigureApiClient).AddHttpMessageHandler<BearerTokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Inside Docker the site is served over plain HTTP on port 8080.
if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
