using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using ShormanServicesBackend.Api;
using ShormanServicesBackend.Api.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true);

var keyVaultUri = builder.Configuration["KeyVault:VaultUri"]
    ?? Environment.GetEnvironmentVariable("KEYVAULT_URI");

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    ConfigureKeyVault(builder.Configuration, keyVaultUri);
}

var dbProvider = builder.Configuration["Database:Provider"];
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
var apiConn = builder.Configuration.GetConnectionString("ApiConnection");

Console.WriteLine($"Database Provider: {dbProvider ?? "<null>"}");
Console.WriteLine($"DefaultConnection exists: {!string.IsNullOrWhiteSpace(defaultConn)}");
Console.WriteLine($"ApiConnection exists: {!string.IsNullOrWhiteSpace(apiConn)}");

var allowedOrigins = builder.Configuration
                         .GetSection("Cors:AllowedOrigins")
                         .Get<string[]>()
                     ?? ["http://localhost:4200", "https://localhost:4200", "http://localhost:4201", "https://localhost:4201"];

builder.Services.AddProblemDetails();
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy.SetIsOriginAllowed(origin => IsAllowedOrigin(origin, allowedOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var otel = builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("MySqlConnector"));

if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    otel.UseAzureMonitor();   // only in Azure, where the env var is set
}
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "ShormanServices API",
        Version = "v1",
        Description = "Grocery ordering platform API"
    });
});

var connectionString = !string.IsNullOrWhiteSpace(apiConn)
    ? apiConn
    : defaultConn;

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing connection string: configure ConnectionStrings__ApiConnection or ConnectionStrings__DefaultConnection"
    );
}

builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

var productManagementOptions = app.Services.GetRequiredService<IOptions<ProductManagementOptions>>().Value;
var storageRoot = Path.IsPathRooted(productManagementOptions.StorageRootPath)
    ? productManagementOptions.StorageRootPath
    : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, productManagementOptions.StorageRootPath));
Directory.CreateDirectory(storageRoot);

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ShormanServices API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRoot),
    RequestPath = GetProductManagementRequestPath(productManagementOptions.PublicBasePath)
});

app.UseCors("AngularClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    environment = app.Environment.EnvironmentName,
    utc = DateTimeOffset.UtcNow
}));

await app.Services.InitializeApiAsync();

app.Run();

static string GetProductManagementRequestPath(string requestPath)
{
    if (string.IsNullOrWhiteSpace(requestPath))
    {
        return "/product-management-assets";
    }

    return requestPath.StartsWith('/') ? requestPath.TrimEnd('/') : "/" + requestPath.Trim('/');
}

static bool IsAllowedOrigin(string origin, IReadOnlyCollection<string> allowedOrigins)
{
    if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    return uri.Host.EndsWith(".azurestaticapps.net", StringComparison.OrdinalIgnoreCase);
}

static void ConfigureKeyVault(IConfigurationBuilder configuration, string keyVaultUri)
{
    var credential = CreateDefaultAzureCredential();
    var addAzureKeyVault = typeof(Microsoft.Extensions.Configuration.AzureKeyVaultConfigurationExtensions)
        .GetMethods()
        .Single(method =>
        {
            if (method.Name != nameof(Microsoft.Extensions.Configuration.AzureKeyVaultConfigurationExtensions.AddAzureKeyVault))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 3
                   && parameters[0].ParameterType == typeof(IConfigurationBuilder)
                   && parameters[1].ParameterType == typeof(Uri)
                   && parameters[2].ParameterType.FullName == "Azure.Core.TokenCredential";
        });

    addAzureKeyVault.Invoke(null, [configuration, new Uri(keyVaultUri), credential]);
}

static object CreateDefaultAzureCredential()
{
    var credentialType = Type.GetType("Azure.Identity.DefaultAzureCredential, Azure.Core")
        ?? Type.GetType("Azure.Identity.DefaultAzureCredential, Azure.Identity");

    if (credentialType is null)
    {
        throw new InvalidOperationException("DefaultAzureCredential type was not found in loaded Azure assemblies.");
    }

    return Activator.CreateInstance(credentialType)
           ?? throw new InvalidOperationException("Failed to create DefaultAzureCredential instance.");
}
