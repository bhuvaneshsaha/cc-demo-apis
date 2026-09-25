using System.Text.Json.Nodes;

const string ServiceName = "api-b";
const string SampleKey = "API_B_SAMPLE_VALUE";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = ServiceName,
        Version = "v1",
        Description = "Public edge API. Shares net-b34 with api-3 and api-4, so both upstream calls succeed."
    });
});
builder.Services.AddHttpClient("upstream", client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    ConnectTimeout = TimeSpan.FromSeconds(5)
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", ServiceName);
    options.RoutePrefix = "swagger";
});

var api3Url = ConfigUrl(app.Configuration["API3_URL"], "http://api-3:8080");
var api4Url = ConfigUrl(app.Configuration["API4_URL"], "http://api-4:8080");
var sampleValue = ReadSample(app.Configuration[SampleKey]);

app.MapGet("/", () => Results.Json(new { name = ServiceName, role = "public" }))
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

app.MapGet("/call/api3", async (IHttpClientFactory factory, ILogger<Program> logger) =>
        await CallHealth(factory, api3Url, logger))
    .WithName("CallApi3")
    .WithTags("calls")
    .WithSummary("GET api-3 /health over net-b34");

app.MapGet("/call/api4", async (IHttpClientFactory factory, ILogger<Program> logger) =>
        await CallHealth(factory, api4Url, logger))
    .WithName("CallApi4")
    .WithTags("calls")
    .WithSummary("GET api-4 /health over net-b34");

app.Run();

static string ConfigUrl(string? configured, string fallback) =>
    string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();

static string? ReadSample(string? configured) =>
    string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();

static async Task<IResult> CallHealth(IHttpClientFactory factory, string baseUrl, ILogger logger)
{
    var target = $"{baseUrl.TrimEnd('/')}/health";
    var client = factory.CreateClient("upstream");

    try
    {
        using var response = await client.GetAsync(target);
        var text = await response.Content.ReadAsStringAsync();
        return Results.Json(new
        {
            ok = response.IsSuccessStatusCode,
            status = (int)response.StatusCode,
            body = ParseBody(text)
        });
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Upstream call to {Target} failed", target);
        var reason = ex.InnerException is null
            ? ex.Message
            : $"{ex.Message} ({ex.InnerException.Message})";
        return Results.Json(new { ok = false, reason });
    }
}

static object ParseBody(string text)
{
    try
    {
        var node = JsonNode.Parse(text);
        if (node is not null)
        {
            return node;
        }
    }
    catch (System.Text.Json.JsonException)
    {
        // Upstream returned non-JSON; surface the raw text.
    }

    return text;
}
