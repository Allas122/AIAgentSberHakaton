using ChatNode.Api.Rest.Messages.Assignments;
using ChatNode.Infrastructure.Database.Configurations;
using FluentValidation;

namespace ChatNode.Api.Rest.Validators;

public class UpdateAssignmentRequestValidator : AbstractValidator<UpdateAssignmentRequest>
{
    public UpdateAssignmentRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Title is not null
                       || x.Description is not null
                       || x.Assignee is not null
                       || x.AssigneeId is not null
                       || x.DueDate is not null
                       || x.Status is not null)
            .WithMessage("Нечего менять: не передано ни одного поля.");

        RuleFor(x => x.Title)
            .NotEmpty().When(x => x.Title is not null)
            .WithMessage("Формулировка не может быть пустой.")
            .MaximumLength(AssignmentConfiguration.MaxTitleLength)
            .WithMessage($"Формулировка не должна быть длиннее {AssignmentConfiguration.MaxTitleLength} символов.");

        RuleFor(x => x.Description)
            .MaximumLength(AssignmentConfiguration.MaxDescriptionLength)
            .WithMessage($"Описание не должно быть длиннее {AssignmentConfiguration.MaxDescriptionLength} символов.");

        RuleFor(x => x.Assignee)
            .MaximumLength(AssignmentConfiguration.MaxAssigneeLength)
            .WithMessage($"Имя исполнителя не должно быть длиннее {AssignmentConfiguration.MaxAssigneeLength} символов.");

        RuleFor(x => x.Status)
            .IsInEnum().When(x => x.Status is not null)
            .WithMessage("Неизвестный статус поручения.");
    }
}
