using Fundo.Api;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5050");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<RuleEngine>(_ => new RuleEngine([
    new NewYorkRule(),
    new BlacklistRule(new HashSet<string> { "000-00-0000", "999-99-9999" })
]));
builder.Services.AddSingleton<LoanStore>();
builder.Services.AddSingleton<ILoanStore>(provider => provider.GetRequiredService<LoanStore>());
builder.Services.AddSingleton<IOutboxStore>(provider => provider.GetRequiredService<LoanStore>());
builder.Services.AddSingleton<SubmitApplication>();
builder.Services.AddHttpClient<IExternalCustomerClient, HttpExternalCustomerClient>(client => client.BaseAddress = new Uri(builder.Configuration["ExternalServiceUrl"] ?? "http://localhost:5051"));
builder.Services.AddHostedService<OutboxWorker>();

var app = builder.Build();
app.UseCors();
app.Services.GetRequiredService<LoanStore>().Initialize();

app.MapPost("/api/applications", (LoanRequest request, SubmitApplication submit) =>
{
    try { return Results.Ok(submit.Execute(request)); }
    catch (ArgumentException error) { return Results.BadRequest(new { error = error.Message }); }
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program { }
