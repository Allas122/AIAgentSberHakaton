using System.Net.Sockets;

namespace ChatNode.Infrastructure.AI.Policy;

public sealed class TransientHttpRetryHandler(
    int maxRetries,
    double backoffFactor,
    ILogger<TransientHttpRetryHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await base.SendAsync(request, ct);
            }
            catch (Exception ex) when (GigaChatRetry.IsHandshakeFailure(ex))
            {
                logger.LogError(
                    ex,
                    "GigaChat: рукопожатие TLS с {RequestUri} не состоялось ({Failure}), повторы не выполняются",
                    request.RequestUri,
                    Describe(ex));

                throw;
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetryable(ex, request, ct))
            {
                var delay = TimeSpan.FromSeconds(backoffFactor * Math.Pow(2, attempt));

                logger.LogWarning(
                    ex,
                    "GigaChat: транспортная ошибка на {RequestUri} ({Failure}), повтор {Attempt}/{MaxRetries} через {Delay:0.#} с",
                    request.RequestUri,
                    Describe(ex),
                    attempt + 1,
                    maxRetries,
                    delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
        }
    }
    
    private static string Describe(Exception ex)
    {
        var parts = new List<string>();

        for (var current = ex; current is not null; current = current.InnerException)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join(" <- ", parts);
    }

    private static bool IsRetryable(Exception ex, HttpRequestMessage request, CancellationToken ct) =>
        !ct.IsCancellationRequested
        && request.Content is null or ByteArrayContent
        && ex is HttpRequestException or IOException or SocketException;
}
