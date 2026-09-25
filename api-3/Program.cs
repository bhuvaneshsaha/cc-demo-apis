const string ServiceName = "api-3";
const string SampleKey = "API_C_SAMPLE_VALUE";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = ServiceName,
        Version = "v1",
        Description = "Private API. Reachable from api-a (net-a3) and api-b (net-b34). No published host port."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", ServiceName);
    options.RoutePrefix = "swagger";
});

var sampleValue = ReadSample(app.Configuration[SampleKey]);

app.MapGet("/", () => Results.Json(new { name = ServiceName, role = "private" }))
    .WithName("Identity")
    .WithTags("meta")
    .WithSummary("Service identity");

app.MapGet("/health", () => Results.Json(new { service = ServiceName, status = "ok" }))
    .WithName("Health")
    .WithTags("meta")
    .WithSummary("Liveness");

app.MapGet("/env/sample", () => Results.Json(new
{
    service = ServiceName,
    key = SampleKey,
    set = sampleValue is not null,
    value = sampleValue
}))
    .WithName("SampleEnv")
    .WithTags("meta")
    .WithSummary("Optional sample value from configuration");

app.Run();

static string? ReadSample(string? configured) =>
    string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
