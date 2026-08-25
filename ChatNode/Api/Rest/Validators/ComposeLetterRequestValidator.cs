using ChatNode.Api.Rest.Messages.Letters;
using ChatNode.Infrastructure.Validation;
using FluentValidation;

namespace ChatNode.Api.Rest.Validators;

public class ComposeLetterRequestValidator : AbstractValidator<ComposeLetterRequest>
{
    public ComposeLetterRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.File is not null || !string.IsNullOrWhiteSpace(x.Text))
            .WithMessage("Приложите файл письма или вставьте его текст.");

        RuleFor(x => x.Text)
            .MaximumLength(20000).WithMessage("Текст письма слишком длинный (макс. 20000 символов).");

        RuleFor(x => x.Intent)
            .MaximumLength(1000).WithMessage("Указание слишком длинное (макс. 1000 символов).");

        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File!)
                .MaxFileSize(UploadLimits.MaxDocxBytes)
                .AllowedExtensions([".docx"]).WithMessage("Письмо принимается в формате .docx");
        });
    }
}
