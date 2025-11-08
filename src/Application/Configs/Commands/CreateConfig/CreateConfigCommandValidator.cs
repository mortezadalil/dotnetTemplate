using FluentValidation;

namespace Application.Configs.Commands.CreateConfig;

public class CreateConfigCommandValidator : AbstractValidator<CreateConfigCommand>
{
    public CreateConfigCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Config key is required")
            .MaximumLength(200).WithMessage("Key must not exceed 200 characters");

        RuleFor(x => x.Value)
            .NotNull().WithMessage("Config value cannot be null")
            .MaximumLength(2000).WithMessage("Value must not exceed 2000 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required")
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters");
    }
}
