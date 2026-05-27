export const THEME_STORAGE_KEY = "frontend-theme";

export const THEME_PREFERENCES = ["light", "dark", "auto"] as const;
export type ThemePreference = (typeof THEME_PREFERENCES)[number];
export type ResolvedTheme = "light" | "dark";

export function isThemePreference(value: string): value is ThemePreference {
  return (THEME_PREFERENCES as readonly string[]).includes(value);
}

export function themeFromCookieValue(value: string | undefined): ThemePreference {
  if (value && isThemePreference(value)) return value;
  return "auto";
}

export function setThemeCookie(preference: ThemePreference) {
  if (typeof document === "undefined") return;
  document.cookie = `${THEME_STORAGE_KEY}=${preference};path=/;max-age=31536000;SameSite=Lax`;
}

export function systemPrefersDark(): boolean {
  if (typeof window === "undefined") return false;
  return window.matchMedia("(prefers-color-scheme: dark)").matches;
}

export function resolveTheme(preference: ThemePreference): ResolvedTheme {
  if (preference === "dark") return "dark";
  if (preference === "light") return "light";
  return systemPrefersDark() ? "dark" : "light";
}

export function detectInitialThemePreference(): ThemePreference {
  if (typeof window === "undefined") return "auto";
  const stored = localStorage.getItem(THEME_STORAGE_KEY);
  if (stored && isThemePreference(stored)) return stored;
  const match = document.cookie.match(new RegExp(`(?:^|; )${THEME_STORAGE_KEY}=([^;]*)`));
  if (match?.[1] && isThemePreference(match[1])) return match[1];
  return "auto";
}

export function applyThemeToDocument(resolved: ResolvedTheme) {
  const root = document.documentElement;
  root.classList.toggle("dark", resolved === "dark");
  root.style.colorScheme = resolved;
}
