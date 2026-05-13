using FluentValidation;


namespace Application.Modules.Users.Authentication.Commands.Register
{
    public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(x => x.UserName).NotEmpty().WithMessage("User name is required");
            RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required");
            RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required");
            RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required").EmailAddress();
            RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
        }
    }
}
