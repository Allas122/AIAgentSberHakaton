using ChatNode.Api.Rest.Messages.Chat;
using ChatNode.Infrastructure.Validation;
using FluentValidation;

namespace ChatNode.Api.Rest.Validators;

public class UploadFileRequestValidator : AbstractValidator<UploadFileRequest>
{
    public UploadFileRequestValidator()
    {
        RuleFor(x => x.Content)
            .MaximumLength(5000).WithMessage("Промпт слишком длинный (макс. 5000 символов).");

        RuleFor(x => x.File)
            .NotNull().WithMessage("Файл обязателен")
            .MaxFileSize(50 * 1024 * 1024)
            .AllowedExtensions([".pdf", ".doc", ".docx"]);
    }
}