using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TechMove.Web.Data;
using TechMove.Web.Data.Repositories;
using TechMove.Web.Services;
using TechMove.Web.Services.Currency;
using TechMove.Web.Services.Files;
using TechMove.Web.Services.Workflow;

public partial class Program {
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllersWithViews();


        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found in appsettings.json.");
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));


        builder.Services.Configure<ExchangeRateApiOptions>(builder.Configuration.GetSection(ExchangeRateApiOptions.SectionName));
        builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));


        builder.Services.AddScoped<IClientRepository, ClientRepository>();
        builder.Services.AddScoped<IContractRepository, ContractRepository>();
        builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();


        builder.Services.AddScoped<IClientService, ClientService>();
        builder.Services.AddScoped<IContractService, ContractService>();
        builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();

        builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        builder.Services.AddSingleton<ICurrencyConverter, CurrencyConverter>();
        builder.Services.AddSingleton<IContractWorkflow, ContractWorkflow>();
        builder.Services.AddSingleton<IServiceRequestFactory, ServiceRequestFactory>();
        builder.Services.AddSingleton<IFileValidator, PdfFileValidator>();
        builder.Services.AddSingleton<IFileStorageService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            var root = Path.IsPathRooted(options.RootPath)
                ? options.RootPath
                : Path.Combine(env.ContentRootPath, options.RootPath);
            return new LocalFileStorageService(root);
        });


        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient<IExchangeRateProvider, ExchangeRateApiProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ExchangeRateApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}

public partial class Program { }
