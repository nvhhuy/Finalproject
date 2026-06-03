/** Stored on the ticket; labels come from i18n (`support.categories.*`). */
export const SUPPORT_CATEGORY_VALUES = [
  "account",
  "billing",
  "technical",
  "feature",
  "other",
] as const;

export type SupportCategory = (typeof SUPPORT_CATEGORY_VALUES)[number];
