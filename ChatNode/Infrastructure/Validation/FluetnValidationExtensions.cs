using FluentValidation;

namespace ChatNode.Infrastructure.Validation;

public static class FluentValidationExtensions
{
    public static IRuleBuilderOptions<T, IFormFile> MaxFileSize<T>(this IRuleBuilder<T, IFormFile> ruleBuilder, long maxSizeBytes)
    {
        return ruleBuilder.Must(file => file == null || file.Length <= maxSizeBytes)
            .WithMessage($"Размер файла не должен превышать {maxSizeBytes / 1024 / 1024} МБ.");
    }

    public static IRuleBuilderOptions<T, IFormFile> AllowedExtensions<T>(this IRuleBuilder<T, IFormFile> ruleBuilder, string[] extensions)
    {
        return ruleBuilder.Must(file =>
        {
            if (file == null) return true;
            var extension = Path.GetExtension(file.FileName).ToLower();
            return extensions.Contains(extension);
        }).WithMessage($"Разрешены только следующие форматы: {string.Join(", ", extensions)}");
    }
}