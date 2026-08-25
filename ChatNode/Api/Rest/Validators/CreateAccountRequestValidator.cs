using ChatNode.Api.Rest.Messages.Accounts;
using ChatNode.Application.Services;
using ChatNode.Infrastructure.Database.Configurations;
using FluentValidation;

namespace ChatNode.Api.Rest.Validators;

public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Login)
            .NotEmpty().WithMessage("Логин обязателен.")
            .MinimumLength(AccountService.MinLoginLength)
            .WithMessage($"Логин короче {AccountService.MinLoginLength} символов.")
            .MaximumLength(UserAccountConfiguration.MaxLoginLength)
            .WithMessage($"Логин длиннее {UserAccountConfiguration.MaxLoginLength} символов.")
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("В логине допустимы латиница, цифры, точка, дефис и подчёркивание.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен.")
            .MinimumLength(AccountService.MinPasswordLength)
            .WithMessage($"Пароль короче {AccountService.MinPasswordLength} символов.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя обязательно.")
            .MaximumLength(UserAccountConfiguration.MaxNameLength)
            .WithMessage($"Имя длиннее {UserAccountConfiguration.MaxNameLength} символов.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Почта указана неверно.")
            .MaximumLength(UserAccountConfiguration.MaxEmailLength);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Неизвестная роль.");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Login)
            .NotEmpty().WithMessage("Логин обязателен.")
            .MinimumLength(AccountService.MinLoginLength)
            .WithMessage($"Логин короче {AccountService.MinLoginLength} символов.")
            .MaximumLength(UserAccountConfiguration.MaxLoginLength)
            .WithMessage($"Логин длиннее {UserAccountConfiguration.MaxLoginLength} символов.")
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("В логине допустимы латиница, цифры, точка, дефис и подчёркивание.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен.")
            .MinimumLength(AccountService.MinPasswordLength)
            .WithMessage($"Пароль короче {AccountService.MinPasswordLength} символов.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя обязательно.")
            .MaximumLength(UserAccountConfiguration.MaxNameLength)
            .WithMessage($"Имя длиннее {UserAccountConfiguration.MaxNameLength} символов.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Почта указана неверно.")
            .MaximumLength(UserAccountConfiguration.MaxEmailLength);
    }
}

public class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Name is not null || x.Email is not null || x.Role is not null || x.Password is not null)
            .WithMessage("Нечего менять: не передано ни одного поля.");

        RuleFor(x => x.Password)
            .MinimumLength(AccountService.MinPasswordLength)
            .When(x => x.Password is not null)
            .WithMessage($"Пароль короче {AccountService.MinPasswordLength} символов.");

        RuleFor(x => x.Name)
            .NotEmpty().When(x => x.Name is not null).WithMessage("Имя не может быть пустым.")
            .MaximumLength(UserAccountConfiguration.MaxNameLength);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Почта указана неверно.");

        RuleFor(x => x.Role)
            .IsInEnum().When(x => x.Role is not null).WithMessage("Неизвестная роль.");
    }
}
