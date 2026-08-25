using System.IO.Packaging;
using ChatNode.Application.Exceptions;
using DocumentFormat.OpenXml.Packaging;
using GigaChat.Net;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Configuration;

public static class DomainErrors
{
    public static (int Status, string Title) Describe(Exception exception) => exception switch
    {
        PermissionDenied e =>
            (StatusCodes.Status403Forbidden, e.Message),

        NotFoundException e =>
            (StatusCodes.Status404NotFound, e.Message),

        InvalidDocumentException e =>
            (StatusCodes.Status400BadRequest, e.Message),

        InvalidRequestException e =>
            (StatusCodes.Status400BadRequest, e.Message),

        FileFormatException or OpenXmlPackageException or InvalidDataException =>
            (StatusCodes.Status400BadRequest, "Файл повреждён или это не документ .docx."),

        RpcException =>
            (StatusCodes.Status503ServiceUnavailable,
                "Сервис обезличивания недоступен, а без него документ в модель не отправляется. " +
                "Попробуйте позже."),

        AuthenticationError =>
            (StatusCodes.Status502BadGateway,
                "GigaChat отклонил ключ авторизации. Проверьте GIGACHAT_AUTHORIZATION_KEY и Scope в .env — " +
                "ключ истёк, отозван или выдан на другой Scope."),

        RateLimitError =>
            (StatusCodes.Status429TooManyRequests,
                "GigaChat ограничил частоту запросов. Попробуйте через минуту."),

        ForbiddenError =>
            (StatusCodes.Status502BadGateway,
                "GigaChat отказал в доступе: у ключа нет прав на эту модель или закончились токены."),

        ServerError =>
            (StatusCodes.Status502BadGateway,
                "GigaChat временно недоступен. Попробуйте ещё раз."),

        _ =>
            (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервиса. Попробуйте ещё раз.")
    };

}
