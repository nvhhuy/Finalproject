import i18n from "@/i18n/config";
import type { ApiErrorPayload } from "@/lib/api/types";

/** Maps API error codes to i18n keys when the server omits a user-facing message. */
const ERROR_CODE_I18N_KEYS: Record<string, string> = {
  "Authentication.EmailIsAlreadyInUse": "errors.emailAlreadyInUse",
  "Authentication.UserNameAlreadyTaken": "errors.userNameAlreadyTaken",
  "Authentication.InvalidCredentials": "errors.invalidCredentials",
  ForbiddenError: "errors.registrationDisabled",
  TurnstileFailed: "errors.turnstileFailed",
  ConcurrencyConflict: "errors.concurrencyConflict",
};

const FIELD_LABEL_KEYS: Record<string, string> = {
  Email: "common.email",
  Password: "common.password",
  UserName: "register.userName",
  FirstName: "register.firstName",
  LastName: "register.lastName",
  Code: "twoFa.code",
  ConfirmPassword: "register.confirmPassword",
};

/** Maps common FluentValidation / API English messages to i18n keys. */
const VALIDATION_MESSAGE_KEYS: Record<string, string> = {
  "User name is required": "validation.userNameRequired",
  "First name is required": "validation.firstNameRequired",
  "Last name is required": "validation.lastNameRequired",
  "Email is required": "validation.emailRequired",
  "Email is already in use.": "errors.emailAlreadyInUse",
  "Username is already taken.": "errors.userNameAlreadyTaken",
  "Invalid authenticator code.": "errors.invalidAuthenticatorCode",
  "Two-factor authentication is not enabled.": "errors.twoFactorNotEnabled",
  "Human verification failed. Please refresh the page and try again.": "errors.turnstileFailed",
  "Public registration is disabled by application setting.": "errors.registrationDisabled",
};

function localizeValidationMessage(message: string): string {
  const trimmed = message.trim();
  const exact = VALIDATION_MESSAGE_KEYS[trimmed];
  if (exact) return i18n.t(exact);

  const lower = trimmed.toLowerCase();
  if (lower.includes("email") && lower.includes("valid")) return i18n.t("validation.invalidEmail");
  if (lower.includes("required")) return i18n.t("validation.required");
  if (lower.includes("password") && (lower.includes("uppercase") || lower.includes("digit") || lower.includes("special"))) {
    return i18n.t("validation.passwordComplexity");
  }
  if (lower.includes("minimum length") && lower.includes("8")) return i18n.t("errors.passwordMin");

  return trimmed;
}

function fieldLabel(field: string): string {
  const key = FIELD_LABEL_KEYS[field];
  return key ? i18n.t(key) : field;
}

export function formatValidationErrors(
  errors?: Record<string, string[]> | null,
): string {
  if (!errors) return "";
  return Object.entries(errors)
    .flatMap(([field, messages]) =>
      messages
        .filter(Boolean)
        .map((m) => {
          const text = localizeValidationMessage(m);
          return field ? `${fieldLabel(field)}: ${text}` : text;
        }),
    )
    .join("\n");
}

export function getApiErrorMessageFromPayload(
  error: ApiErrorPayload | null | undefined,
  fallbackKey = "errors.requestFailed",
): string {
  if (!error) return i18n.t(fallbackKey);

  const validation = formatValidationErrors(error.validationErrors);
  if (validation) return validation;

  const message = error.message?.trim();
  if (message) {
    const localized = localizeValidationMessage(message);
    if (localized !== message) return localized;
    const i18nFromMessage = VALIDATION_MESSAGE_KEYS[message];
    if (i18nFromMessage) return i18n.t(i18nFromMessage);
    return message;
  }

  const i18nKey = ERROR_CODE_I18N_KEYS[error.code];
  if (i18nKey) return i18n.t(i18nKey);

  if (error.code?.trim()) return error.code;

  return i18n.t(fallbackKey);
}

export function getNetworkErrorMessage(err: unknown, unreachableKey = "login.apiUnreachable"): string {
  const msg = err instanceof Error ? err.message : "";
  if (msg.includes("fetch") || msg.includes("Failed to fetch")) {
    return i18n.t(unreachableKey);
  }
  return err instanceof Error ? msg : i18n.t("errors.requestFailed");
}

export class ApiClientError extends Error {
  readonly code: string;
  readonly httpStatus?: number;
  readonly validationErrors?: Record<string, string[]>;

  constructor(error: ApiErrorPayload) {
    super(getApiErrorMessageFromPayload(error));
    this.name = "ApiClientError";
    this.code = error.code;
    this.httpStatus = error.httpStatus ?? undefined;
    this.validationErrors = error.validationErrors ?? undefined;
  }
}

export function isConcurrencyError(err: unknown): boolean {
  if (!(err instanceof ApiClientError)) return false;
  const msg = (err.message ?? "").toLowerCase();
  return err.httpStatus === 409 || err.code === "ConcurrencyConflict" || msg.includes("concurrency") || msg.includes("row version");
}
