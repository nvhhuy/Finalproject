"use client";

import { useEffect } from "react";
import { detectInitialThemePreference, type ThemePreference } from "@/lib/theme/theme";
import { useTheme } from "@/lib/theme/ThemeProvider";

/** After hydration, apply localStorage preference if it differs from the server cookie snapshot. */
export function ThemeSync({ serverPreference }: { serverPreference: ThemePreference }) {
  const { setPreference } = useTheme();

  useEffect(() => {
    const preferred = detectInitialThemePreference();
    if (preferred !== serverPreference) {
      setPreference(preferred);
    }
  }, [serverPreference, setPreference]);

  return null;
}
