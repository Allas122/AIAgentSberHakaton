using System.Security.Claims;
using ChatNode.Api.Rest.Messages.Manual;
using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Validation;
using Domain.ValueTypes;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace ChatNode.Api.Rest.Validators;

public class UploadManualRequestValidator : AbstractValidator<UploadManualRequest>
{
    private static readonly UserRole[] StaffRoles = [UserRole.Rector, UserRole.Coordinator];

    public UploadManualRequestValidator(
        IOptions<ManualUploadOptions> options,
        IHttpContextAccessor httpContextAccessor)
    {
        var limits = options.Value;

        long Limit() => IsStaff(httpContextAccessor.HttpContext?.User)
            ? limits.StaffMaxFileSizeBytes
            : limits.MaxFileSizeBytes;

        RuleFor(x => x.Title)
            .Length(4, 500).WithMessage("Название методички может быть от 4 до 500 символов");

        RuleFor(x => x.File)
            .NotNull().WithMessage("Файл обязателен")
            .AllowedExtensions([".MD", ".md"]).WithMessage("Файл должен быть в .md формате.")
            .Must(file => file is null || file.Length <= Limit())
            .WithMessage(_ => IsStaff(httpContextAccessor.HttpContext?.User)
                ? $"Методичка не должна быть больше {Human(limits.StaffMaxFileSizeBytes)}."
                : $"Методичка не должна быть больше {Human(limits.MaxFileSizeBytes)}. " +
                  "Разбор большого документа занимает минуты и расходует лимиты модели — " +
                  $"для положений до {Human(limits.StaffMaxFileSizeBytes)} войдите под служебным аккаунтом.");
    }

    private static bool IsStaff(ClaimsPrincipal? user) =>
        user is not null && StaffRoles.Any(role => user.IsInRole(role.ToString()));

    private static string Human(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / (1024 * 1024)} МБ"
            : $"{bytes / 1024} КБ";
}
