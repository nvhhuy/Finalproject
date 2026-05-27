"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { useState } from "react";
import { I18nextProvider } from "react-i18next";
import i18n, { ensureI18nLocale } from "@/i18n/config";
import type { AppLocale } from "@/i18n/locales";
import { LocaleSync } from "@/components/i18n/LocaleSync";
import { ThemeSync } from "@/components/theme/ThemeSync";
import { AuthProvider } from "@/lib/auth/auth-context";
import { ThemeProvider } from "@/lib/theme/ThemeProvider";
import type { ThemePreference } from "@/lib/theme/theme";

export function Providers({
  children,
  serverLocale,
  serverTheme,
}: {
  children: React.ReactNode;
  serverLocale: AppLocale;
  serverTheme: ThemePreference;
}) {
  ensureI18nLocale(serverLocale);

  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { retry: 1, refetchOnWindowFocus: false },
          mutations: { retry: 0 },
        },
      }),
  );

  return (
    <I18nextProvider i18n={i18n}>
      <ThemeProvider serverPreference={serverTheme}>
        <ThemeSync serverPreference={serverTheme} />
        <LocaleSync serverLocale={serverLocale} />
        <QueryClientProvider client={queryClient}>
          <AuthProvider>{children}</AuthProvider>
          {process.env.NODE_ENV === "development" ? <ReactQueryDevtools initialIsOpen={false} /> : null}
        </QueryClientProvider>
      </ThemeProvider>
    </I18nextProvider>
  );
}
