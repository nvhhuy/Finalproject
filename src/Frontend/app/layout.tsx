import "./globals.css";
import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { cookies } from "next/headers";
import { ThemeScript } from "@/components/theme/ThemeScript";
import { APP_NAME } from "@/lib/config/branding";
import { Providers } from "./providers";
import { LOCALE_STORAGE_KEY, localeFromCookieValue, localeToHtmlLang } from "@/i18n/locales";
import { THEME_STORAGE_KEY, themeFromCookieValue } from "@/lib/theme/theme";

const inter = Inter({
  subsets: ["latin", "vietnamese"],
  variable: "--font-inter",
  display: "swap",
});

export const metadata: Metadata = {
  title: {
    default: APP_NAME,
    template: `%s · ${APP_NAME}`,
  },
  description: "Reusable frontend base aligned with the .NET backend.",
  manifest: "/manifest.webmanifest",
  appleWebApp: {
    capable: true,
    title: APP_NAME,
    statusBarStyle: "default",
  },
  icons: {
    icon: "/favicon.svg",
    apple: "/favicon.svg",
  },
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const cookieStore = await cookies();
  const serverLocale = localeFromCookieValue(cookieStore.get(LOCALE_STORAGE_KEY)?.value);
  const serverTheme = themeFromCookieValue(cookieStore.get(THEME_STORAGE_KEY)?.value);

  return (
    <html
      lang={localeToHtmlLang(serverLocale)}
      className={serverTheme === "dark" ? "dark" : undefined}
      suppressHydrationWarning
    >
      <head>
        <ThemeScript />
      </head>
      {/* suppressHydrationWarning: browser extensions (e.g. Grammarly) inject body attributes before React hydrates */}
      <body className={`${inter.variable} font-sans antialiased`} suppressHydrationWarning>
        <Providers serverLocale={serverLocale} serverTheme={serverTheme}>
          {children}
        </Providers>
      </body>
    </html>
  );
}
