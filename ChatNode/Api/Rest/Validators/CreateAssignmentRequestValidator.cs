using ChatNode.Api.Rest.Messages.Assignments;
using ChatNode.Infrastructure.Database.Configurations;
using FluentValidation;

namespace ChatNode.Api.Rest.Validators;

public class CreateAssignmentRequestValidator : AbstractValidator<CreateAssignmentRequest>
{
    public CreateAssignmentRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Сформулируйте поручение.")
            .MaximumLength(AssignmentConfiguration.MaxTitleLength)
            .WithMessage($"Формулировка не должна быть длиннее {AssignmentConfiguration.MaxTitleLength} символов.");

        RuleFor(x => x.Description)
            .MaximumLength(AssignmentConfiguration.MaxDescriptionLength)
            .WithMessage($"Описание не должно быть длиннее {AssignmentConfiguration.MaxDescriptionLength} символов.");

        RuleFor(x => x.Assignee)
            .MaximumLength(AssignmentConfiguration.MaxAssigneeLength)
            .WithMessage($"Имя исполнителя не должно быть длиннее {AssignmentConfiguration.MaxAssigneeLength} символов.");
    }
}
