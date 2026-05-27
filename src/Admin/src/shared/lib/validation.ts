import { z } from 'zod'
import type { TFunction } from 'i18next'
import { USER_ROLES } from '@/shared/types/api'

export function createValidators(t: TFunction) {
  return {
    requiredString: () => z.string().min(1, t('validation.required')),
    email: () => z.string().email(t('validation.invalidEmail')),
    password: () => z.string().min(1, t('validation.passwordRequired')),
    minUsername: () => z.string().min(2, t('validation.minUsername')),
    minPassword: () => z.string().min(8, t('validation.minPassword')),
    twoFaCode: () => z.string().min(6, t('validation.invalidCode')).max(8),
    role: () => z.enum(USER_ROLES),
  }
}

export function createLoginSchemas(t: TFunction) {
  const v = createValidators(t)
  return {
    login: z.object({
      email: v.email(),
      password: v.password(),
    }),
    twoFactor: z.object({
      code: v.twoFaCode(),
    }),
  }
}

export function createUserFormSchemas(t: TFunction) {
  const v = createValidators(t)
  const base = z.object({
    firstName: v.requiredString(),
    lastName: v.requiredString(),
    userName: v.minUsername(),
    email: v.email(),
    role: v.role(),
  })
  return {
    base,
    create: base.extend({
      password: v.minPassword(),
    }),
  }
}

/** First Zod error message or generic invalid input. */
export function firstZodError(
  result: z.SafeParseReturnType<unknown, unknown>,
  t: TFunction,
): string | null {
  if (result.success) return null
  return result.error.errors[0]?.message ?? t('validation.invalidInput')
}
