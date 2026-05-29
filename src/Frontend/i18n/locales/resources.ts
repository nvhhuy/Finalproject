/**
 * Single import boundary for locale JSON — reduces Turbopack HMR glitches when editing *.json files.
 */
import en from "./en.json";
import vi from "./vi.json";
import zh from "./zh.json";

export const i18nResources = {
  en: { translation: en },
  vi: { translation: vi },
  zh: { translation: zh },
} as const;
