using ShormanServicesBackend.Api;

var builder = WebApplication.CreateBuilder(args);

var dbProvider = builder.Configuration["Database:Provider"];
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
var apiConn = builder.Configuration.GetConnectionString("ApiConnection");

Console.WriteLine($"Database Provider: {dbProvider ?? "<null>"}");
Console.WriteLine($"DefaultConnection exists: {!string.IsNullOrWhiteSpace(defaultConn)}");
Console.WriteLine($"ApiConnection exists: {!string.IsNullOrWhiteSpace(apiConn)}");

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true);

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
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing connection string: ConnectionStrings__DefaultConnection"
    );
}

builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ShormanServices API v1");
    options.RoutePrefix = "swagger";
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