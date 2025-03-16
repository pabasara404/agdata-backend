using FluentValidation;
using AgData.DTOs;

namespace AgData.Validators
{
    public class CreatePostDtoValidator : AbstractValidator<CreatePostDto>
    {
        public CreatePostDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required")
                .MinimumLength(3).WithMessage("Title must be at least 3 characters")
                .MaximumLength(100).WithMessage("Title cannot exceed 100 characters");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Content is required")
                .MinimumLength(10).WithMessage("Content must be at least 10 characters");
        }
    }

    public class UpdatePostDtoValidator : AbstractValidator<UpdatePostDto>
    {
        public UpdatePostDtoValidator()
        {
            RuleFor(x => x.Title)
                .MinimumLength(3).WithMessage("Title must be at least 3 characters")
                .MaximumLength(100).WithMessage("Title cannot exceed 100 characters")
                .When(x => x.Title != null);

            RuleFor(x => x.Content)
                .MinimumLength(10).WithMessage("Content must be at least 10 characters")
                .When(x => x.Content != null);
        }
    }
}