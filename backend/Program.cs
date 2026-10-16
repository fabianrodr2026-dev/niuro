using Fundo.Api;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5050");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<RuleEngine>(_ => new RuleEngine([
    new NewYorkRule(),
    new BlacklistRule(new HashSet<string> { "000-00-0000", "999-99-9999" })
]));
builder.Services.AddSingleton<LoanStore>();
builder.Services.AddHttpClient<OutboxWorker>(client => client.BaseAddress = new Uri(builder.Configuration["ExternalServiceUrl"] ?? "http://localhost:5051"));
builder.Services.AddHostedService(provider => provider.GetRequiredService<OutboxWorker>());

var app = builder.Build();
app.UseCors();
app.Services.GetRequiredService<LoanStore>().Initialize();

app.MapPost("/api/applications", (LoanRequest request, RuleEngine rules, LoanStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
        string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.CompanyName) ||
        string.IsNullOrWhiteSpace(request.State) || string.IsNullOrWhiteSpace(request.Ssn) || request.RequestedAmount <= 0)
        return Results.BadRequest(new { error = "Complete every field and enter a positive amount." });

    var decision = rules.Decide(request);
    if (!decision.Approved) return Results.Ok(new { approved = false, reason = decision.Reason });

    var result = store.SaveApproved(request);
    return Results.Ok(new { approved = true, result.ApplicationId, result.ReturningCustomer });
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program { }
