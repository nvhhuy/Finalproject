import { z } from "zod";

export function firstZodError(error: z.ZodError): string {
  return error.issues[0]?.message ?? "Invalid input";
}

export function createAuthSchemas(t: (key: string) => string) {
  return {
    login: z.object({
      email: z.string().email(t("validation.invalidEmail")),
      password: z.string().min(1, t("validation.required")),
    }),
    register: z.object({
      email: z.string().email(t("validation.invalidEmail")),
      userName: z.string().min(2, t("validation.required")),
      password: z.string().min(8, t("errors.passwordMin")),
      firstName: z.string().min(1, t("validation.required")),
      lastName: z.string().min(1, t("validation.required")),
    }),
    twoFaCode: z.object({
      code: z.string().min(6, t("validation.required")).max(8),
    }),
    resetPassword: z
      .object({
        password: z.string().min(8, t("errors.passwordMin")),
        confirm: z.string(),
      })
      .refine((d) => d.password === d.confirm, {
        message: t("errors.passwordMismatch"),
        path: ["confirm"],
      }),
  };
}
