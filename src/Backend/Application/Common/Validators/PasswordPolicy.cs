using FluentValidation;

namespace Application.Common.Validators;

/// <summary>
/// Shared password-strength policy applied to every code path that accepts a new password
/// (public registration, admin reset, forgot-password completion). Centralising the rule here
/// prevents the policy from drifting between flows — historically the public path enforced an
/// 8-char minimum while admin reset enforced 10, which let attackers exploit the cheaper path.
/// </summary>
/// <remarks>
/// <para><b>Policy:</b> minimum 10 characters, with at least one uppercase letter, one lowercase
/// letter, and one digit. We deliberately do not require a symbol — NIST 800-63B recommends
/// against composition rules in favour of length, but a small character-class floor is still
/// useful to bounce the obviously-weak entries ("password", "123456789") that breach lists
/// constantly highlight as the top 20.</para>
///
/// <para><b>Maximum length 200:</b> guards against PBKDF2 work-bomb attacks where a multi-MB
/// password forces the server to spend seconds hashing every login attempt.</para>
/// </remarks>
public static class PasswordPolicy
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 200;

    public static IRuleBuilderOptions<T, string> PasswordComplexity<T>(this IRuleBuilder<T, string> rule)
    {
        return rule
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinimumLength).WithMessage($"Password must be at least {MinimumLength} characters.")
            .MaximumLength(MaximumLength).WithMessage($"Password must not exceed {MaximumLength} characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
