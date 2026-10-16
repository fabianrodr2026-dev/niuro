using System.Text;
using System.Text.Json;

namespace Fundo.Api;

public sealed class OutboxWorker(LoanStore store, HttpClient client, ILogger<OutboxWorker> logger) : BackgroundService
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
                    using var document = JsonDocument.Parse(pending.Payload);
                    var root = document.RootElement;
                    var id = root.GetProperty("customerId").GetInt64();
                    var returning = root.GetProperty("returningCustomer").GetBoolean();
                    using var content = new StringContent(pending.Payload, Encoding.UTF8, "application/json");
                    using var response = await client.SendAsync(new HttpRequestMessage(returning ? HttpMethod.Put : HttpMethod.Post,
                        returning ? $"/customers/{id}" : "/customers") { Content = content }, stoppingToken);
                    response.EnsureSuccessStatusCode();
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
