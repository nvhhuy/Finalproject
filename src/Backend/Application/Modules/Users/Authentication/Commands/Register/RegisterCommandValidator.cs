using Application.Common.Validators;
using FluentValidation;

namespace Application.Modules.Users.Authentication.Commands.Register
{
    /// <summary>
    /// Public-registration validator. Mirrors the rules used by <c>AdminResetPasswordCommandValidator</c>
    /// so the self-service path cannot accept a weaker password than the admin-controlled reset path.
    /// </summary>
    public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(x => x.UserName).NotEmpty().WithMessage("User name is required").MaximumLength(256);
            RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required").MaximumLength(200);
            RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required").MaximumLength(200);
            RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required").EmailAddress().MaximumLength(320);
            RuleFor(x => x.Password).PasswordComplexity();
        }
    }
}
