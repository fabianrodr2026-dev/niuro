using System.Text;
using System.Text.Json;

namespace Fundo.Api;

public interface IOutboxStore
{
    PendingEvent? NextEvent();
    void MarkDelivered(long id);
}

public interface IExternalCustomerClient
{
    Task Send(PendingEvent pending, CancellationToken cancellationToken);
}

public sealed class HttpExternalCustomerClient(HttpClient client) : IExternalCustomerClient
{
    public async Task Send(PendingEvent pending, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(pending.Payload);
        var root = document.RootElement;
        var id = root.GetProperty("customerId").GetInt64();
        var returning = root.GetProperty("returningCustomer").GetBoolean();
        using var content = new StringContent(pending.Payload, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(new HttpRequestMessage(returning ? HttpMethod.Put : HttpMethod.Post,
            returning ? $"/customers/{id}" : "/customers") { Content = content }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class OutboxWorker(IOutboxStore store, IExternalCustomerClient client, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = store.NextEvent();
                if (pending is not null)
                {
                    await client.Send(pending, stoppingToken);
                    store.MarkDelivered(pending.Id);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning(error, "External delivery failed; retrying");
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
