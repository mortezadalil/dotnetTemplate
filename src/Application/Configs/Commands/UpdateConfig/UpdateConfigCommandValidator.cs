using FluentValidation;

namespace Application.Configs.Commands.UpdateConfig;

public class UpdateConfigCommandValidator : AbstractValidator<UpdateConfigCommand>
{
    public UpdateConfigCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("Config ID is required");

        When(x => x.Value != null, () =>
        {
            RuleFor(x => x.Value)
                .MaximumLength(2000).WithMessage("Value must not exceed 2000 characters");
        });

        When(x => x.Description != null, () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters");
        });

        When(x => x.Category != null, () =>
        {
            RuleFor(x => x.Category)
                .NotEmpty().WithMessage("Category cannot be empty")
                .MaximumLength(100).WithMessage("Category must not exceed 100 characters");
        });
    }
}
