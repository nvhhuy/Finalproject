"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  applyThemeToDocument,
  resolveTheme,
  setThemeCookie,
  THEME_STORAGE_KEY,
  type ResolvedTheme,
  type ThemePreference,
} from "./theme";

type ThemeContextValue = {
  preference: ThemePreference;
  resolved: ResolvedTheme;
  setPreference: (preference: ThemePreference) => void;
};

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({
  children,
  serverPreference,
}: {
  children: ReactNode;
  serverPreference: ThemePreference;
}) {
  const [preference, setPreferenceState] = useState<ThemePreference>(serverPreference);
  const [resolved, setResolved] = useState<ResolvedTheme>(() => {
    if (typeof window === "undefined") {
      return serverPreference === "dark" ? "dark" : "light";
    }
    return resolveTheme(serverPreference);
  });

  const apply = useCallback((pref: ThemePreference) => {
    const next = resolveTheme(pref);
    setResolved(next);
    applyThemeToDocument(next);
  }, []);

  const setPreference = useCallback(
    (pref: ThemePreference) => {
      setPreferenceState(pref);
      if (typeof window !== "undefined") {
        localStorage.setItem(THEME_STORAGE_KEY, pref);
        setThemeCookie(pref);
      }
      apply(pref);
    },
    [apply],
  );

  // Sync React state with DOM after ThemeScript (avoid fighting pre-hydration class on <html>)
  useEffect(() => {
    const next = resolveTheme(preference);
    const domDark = document.documentElement.classList.contains("dark");
    if ((next === "dark") !== domDark) {
      apply(preference);
    } else {
      setResolved(next);
    }
  }, [preference, apply]);

  useEffect(() => {
    if (preference !== "auto") return;
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => apply("auto");
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, [preference, apply]);

  const value = useMemo(
    () => ({ preference, resolved, setPreference }),
    [preference, resolved, setPreference],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
