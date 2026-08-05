using ChatNode.Api.Rest.Messages.Manual;
using ChatNode.Infrastructure.Validation;
using FluentValidation;

namespace ChatNode.Api.Rest.Validators;

public class UploadManualRequestValidator : AbstractValidator<UploadManualRequest>
{
    public UploadManualRequestValidator()
    {
        RuleFor(x => x.Title)
            .Length(4,500).WithMessage("Название методички может быть от 4 до 500 символов");

        RuleFor(x => x.File)
            .NotNull().WithMessage("Файл обязателен")
            .MaxFileSize(50 * 1024 * 1024)
            .AllowedExtensions([".MD",".md"]).WithMessage("Файл должен быть в .md формате.");
    }
}