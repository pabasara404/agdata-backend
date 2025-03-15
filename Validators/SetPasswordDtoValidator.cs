using FluentValidation;
using AgData.DTOs;

namespace AgData.Validators
{
    public class SetPasswordDtoValidator : AbstractValidator<SetPasswordDto>
    {
        public SetPasswordDtoValidator()
        {
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters")
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
                .Matches("[0-9]").WithMessage("Password must contain at least one number")
                .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.Password).WithMessage("Passwords must match");

            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Token is required");
        }
    }
}