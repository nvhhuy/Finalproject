using Application.Modules.Users.Authentication.Commands.CompleteTwoFactorLogin;
using Application.Modules.Users.Authentication.Commands.GoogleSignIn;
using Application.Modules.Users.Authentication.Commands.RefreshSession;
using Application.Modules.Users.Authentication.Commands.ResendVerification;
using Application.Modules.Users.Authentication.Commands.VerifyEmail;
using Application.Modules.Users.Profile.Commands;
using Application.Modules.Users.Profile.Commands.TwoFactor;
using Application.Modules.SystemSettings.Admin.Commands;
using FluentValidation;

namespace Application.Common.Validators;

/// <summary>
/// Validators for commands that were previously letting invalid input bubble all the way to the
/// handler (which would throw <see cref="ArgumentException"/> from the aggregate constructor and
/// surface as a 500 via the global exception handler). Each validator is intentionally narrow:
/// just enough to reject obvious garbage at the boundary. Deeper invariants (e.g. token
/// hash matching, TOTP code validity) still live in the handler because they require service
/// dependencies.
/// </summary>

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator()
    {
        RuleFor(c => c.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.")
            .MaximumLength(2048).WithMessage("Refresh token is too long.");
    }
}

public sealed class GoogleSignInCommandValidator : AbstractValidator<GoogleSignInCommand>
{
    public GoogleSignInCommandValidator()
    {
        RuleFor(c => c.IdToken)
            .NotEmpty().WithMessage("Google id token is required.")
            // Google id tokens are well under 4 KB but we leave headroom for future audience-bound
            // formats. Without an upper bound a malicious client could DOS the validator path with
            // a multi-MB string.
            .MaximumLength(8192).WithMessage("Google id token is malformed (too long).");
    }
}

public sealed class ResendEmailVerificationCommandValidator : AbstractValidator<ResendEmailVerificationCommand>
{
    public ResendEmailVerificationCommandValidator()
    {
        // We deliberately do not require non-empty: the handler quietly returns success when the
        // email is empty to limit enumeration. The validator only catches obviously oversized
        // input that would otherwise cause an index-corrupting INSERT in a downstream log table.
        RuleFor(c => c.Email)
            .MaximumLength(256).WithMessage("Email is too long.");
    }
}

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(c => c.TokenId).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Token).NotEmpty().MaximumLength(2048);
    }
}

public sealed class CompleteTwoFactorLoginCommandValidator : AbstractValidator<CompleteTwoFactorLoginCommand>
{
    public CompleteTwoFactorLoginCommandValidator()
    {
        RuleFor(c => c.PendingLoginId)
            .NotEmpty()
            .MaximumLength(128);

        // TOTP codes are 6 digits in our setup. We allow up to 10 chars so recovery codes (longer)
        // can route through the same endpoint in the future without a contract change.
        RuleFor(c => c.Code)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(16);
    }
}

public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(128);
    }
}

public sealed class BeginTwoFactorSetupCommandValidator : AbstractValidator<BeginTwoFactorSetupCommand>
{
    public BeginTwoFactorSetupCommandValidator() => RuleFor(c => c.UserId).NotEmpty();
}

public sealed class ConfirmTwoFactorSetupCommandValidator : AbstractValidator<ConfirmTwoFactorSetupCommand>
{
    public ConfirmTwoFactorSetupCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Code).NotEmpty().MinimumLength(6).MaximumLength(16);
    }
}

public sealed class SetBooleanAppSettingCommandValidator : AbstractValidator<SetBooleanAppSettingCommand>
{
    public SetBooleanAppSettingCommandValidator()
    {
        // Keys are namespaced dot-paths like "Auth.AllowPublicRegister". Whitelist character set
        // so a typo can't accidentally create a near-duplicate row in AppSettings.
        RuleFor(c => c.Key)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[A-Za-z][A-Za-z0-9._-]*$")
            .WithMessage("Setting key must be a dotted identifier.");
    }
}
