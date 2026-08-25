using System.IO.Packaging;
using ChatNode.Application.Exceptions;
using DocumentFormat.OpenXml.Packaging;
using GigaChat.Net;
using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Configuration;

public class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var (status, title) = DomainErrors.Describe(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Необработанная ошибка при запросе {Path}", httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("{Status} на {Path}: {Message}", status, httpContext.Request.Path, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Instance = httpContext.Request.Path
            },
            cancellationToken);

        return true;
    }
}
