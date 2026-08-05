using System.Security.Authentication;
using GigaChat.Net;

namespace ChatNode.Infrastructure.AI.Policy;

public static class GigaChatRetry
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        string operation,
        int attempts,
        double firstDelaySeconds,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action(ct);
            }
            catch (Exception ex) when (attempt < attempts
                                       && !ct.IsCancellationRequested
                                       && IsTransient(ex)
                                       && !IsHandshakeFailure(ex))
            {
                var delay = TimeSpan.FromSeconds(firstDelaySeconds * Math.Pow(2, attempt - 1));

                Console.WriteLine(
                    $"[GIGACHAT RETRY]: {operation} -> {Describe(ex)}. " +
                    $"Попытка {attempt + 1}/{attempts} через {delay.TotalSeconds:0.#} с");

                await Task.Delay(delay, ct);
            }
        }
    }
    
    public static bool IsTransient(Exception ex) => ex switch
    {
        ServerError => true,
        RateLimitError => true,
        HttpRequestException => true,
        IOException => true,
        TaskCanceledException canceled => canceled.InnerException is TimeoutException,
        _ => false
    };

    public static bool IsHandshakeFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is AuthenticationException) return true;
            if (current is HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError }) return true;
        }

        return false;
    }

    public static string Describe(Exception ex) => ex switch
    {
        ResponseError response => $"HTTP {(int)response.StatusCode} ({response.GetType().Name})",
        _ => $"{ex.GetType().Name}: {ex.Message}"
    };
}
