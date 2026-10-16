using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fundo.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fundo.Api.Tests;

public sealed class ApplicationTests
{
    private static LoanRequest Request(string state = "CA", string ssn = "123-45-6789", string lastName = "Lee", decimal amount = 10000) =>
        new("Ana", lastName, "123 Main St", state, "Acme", amount, ssn);

    private static RuleEngine Rules() => new([new NewYorkRule(), new BlacklistRule(new HashSet<string> { "000-00-0000" })]);

    private static (LoanStore Store, string Database) CreateStore()
    {
        var database = Path.Combine(Path.GetTempPath(), $"fundo-test-{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DatabasePath"] = database }).Build();
        var store = new LoanStore(configuration);
        store.Initialize();
        return (store, database);
    }

    private static long Count(string database, string table)
    {
        using var connection = new SqliteConnection($"Data Source={database}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void RulesDenyNyAndBlacklistedSsn()
    {
        Assert.False(Rules().Decide(Request(state: "ny")).Approved);
        Assert.False(Rules().Decide(Request(ssn: "000-00-0000")).Approved);
        Assert.True(Rules().Decide(Request()).Approved);
    }

    [Fact]
    public void ReturningCustomerUpdatesOneCustomerAndOneApplication()
    {
        var (store, database) = CreateStore();
        var first = store.SaveApproved(Request());
        var second = store.SaveApproved(Request(lastName: "Lopez", amount: 12000));

        Assert.False(first.ReturningCustomer);
        Assert.True(second.ReturningCustomer);
        Assert.Equal(first.ApplicationId, second.ApplicationId);
        Assert.Equal(1, Count(database, "customers"));
        Assert.Equal(1, Count(database, "applications"));
        Assert.Equal(2, Count(database, "outbox"));

        using var connection = new SqliteConnection($"Data Source={database}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT c.last_name, a.requested_amount FROM customers c JOIN applications a ON a.customer_id = c.id";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Lopez", reader.GetString(0));
        Assert.Equal("12000", reader.GetString(1));
    }

    [Fact]
    public void OutboxFailureRollsBackCustomerAndApplication()
    {
        var (store, database) = CreateStore();
        using (var connection = new SqliteConnection($"Data Source={database}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER fail_outbox BEFORE INSERT ON outbox BEGIN SELECT RAISE(ABORT, 'failed'); END";
            command.ExecuteNonQuery();
        }

        Assert.Throws<SqliteException>(() => store.SaveApproved(Request()));
        Assert.Equal(0, Count(database, "customers"));
        Assert.Equal(0, Count(database, "applications"));
        Assert.Equal(0, Count(database, "outbox"));
    }

    [Fact]
    public void OutboxFailureRollsBackReturningCustomerUpdate()
    {
        var (store, database) = CreateStore();
        store.SaveApproved(Request());
        using (var connection = new SqliteConnection($"Data Source={database}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER fail_outbox BEFORE INSERT ON outbox BEGIN SELECT RAISE(ABORT, 'failed'); END";
            command.ExecuteNonQuery();
        }

        Assert.Throws<SqliteException>(() => store.SaveApproved(Request(lastName: "Lopez", amount: 12000)));
        Assert.Equal(1, Count(database, "customers"));
        Assert.Equal(1, Count(database, "applications"));
        Assert.Equal(1, Count(database, "outbox"));
        using var check = new SqliteConnection($"Data Source={database}");
        check.Open();
        using var statement = check.CreateCommand();
        statement.CommandText = "SELECT c.last_name, a.requested_amount FROM customers c JOIN applications a ON a.customer_id = c.id";
        using var reader = statement.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Lee", reader.GetString(0));
        Assert.Equal("10000", reader.GetString(1));
    }

    [Fact]
    public async Task EndpointReturnsApprovalDenialAndValidationErrors()
    {
        var (_, database) = CreateStore();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabasePath"] = database,
                ["ExternalServiceUrl"] = "http://localhost:59999"
            })));
        using var client = factory.CreateClient();

        using var approved = await client.PostAsJsonAsync("/api/applications", Request());
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using var approvedJson = JsonDocument.Parse(await approved.Content.ReadAsStringAsync());
        Assert.True(approvedJson.RootElement.GetProperty("approved").GetBoolean());

        using var returning = await client.PostAsJsonAsync("/api/applications", Request(lastName: "Lopez", amount: 12000));
        using var returningJson = JsonDocument.Parse(await returning.Content.ReadAsStringAsync());
        Assert.True(returningJson.RootElement.GetProperty("returningCustomer").GetBoolean());
        Assert.Equal(approvedJson.RootElement.GetProperty("applicationId").GetInt64(), returningJson.RootElement.GetProperty("applicationId").GetInt64());

        using var denied = await client.PostAsJsonAsync("/api/applications", Request(state: "NY"));
        using var deniedJson = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
        Assert.False(deniedJson.RootElement.GetProperty("approved").GetBoolean());

        using var invalid = await client.PostAsJsonAsync("/api/applications", Request(amount: 0));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(1, Count(database, "customers"));
        Assert.Equal(1, Count(database, "applications"));
    }

    [Fact]
    public async Task WorkerRetriesFailedDeliveryAndMarksEventAfterSuccess()
    {
        var store = new RetryStore();
        var client = new RetryClient();
        using var worker = new OutboxWorker(store, client, NullLogger<OutboxWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!store.Delivered)
            {
                await Task.Delay(50, timeout.Token);
            }
            Assert.Equal(2, client.Attempts);
            Assert.True(store.Delivered);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExternalClientCreatesThenUpdatesByCustomerId()
    {
        var handler = new RecordingHandler();
        var client = new HttpExternalCustomerClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5051") });
        await client.Send(new PendingEvent(1, "{\"customerId\":7,\"returningCustomer\":false}"), CancellationToken.None);
        await client.Send(new PendingEvent(2, "{\"customerId\":7,\"returningCustomer\":true}"), CancellationToken.None);
        Assert.Equal([(HttpMethod.Post, "/customers"), (HttpMethod.Put, "/customers/7")], handler.Requests);
    }

    private sealed class RetryStore : IOutboxStore
    {
        public bool Delivered { get; private set; }
        public PendingEvent? NextEvent() => Delivered ? null : new PendingEvent(1, "{}");
        public void MarkDelivered(long id) => Delivered = true;
    }

    private sealed class RetryClient : IExternalCustomerClient
    {
        public int Attempts { get; private set; }
        public Task Send(PendingEvent pending, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts == 1) throw new HttpRequestException("mock unavailable");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
