"use client";

import Link from "next/link";
import { useTranslation } from "react-i18next";
import { PublicHeader } from "@/components/layout/PublicHeader";
import { Button } from "@/components/ui/button";
import { Card, CardBody } from "@/components/ui/card";

const featureKeys = [
  "landing.featureAuth",
  "landing.featureProfile",
  "landing.feature2fa",
  "landing.featureMedia",
  "landing.featureApi",
] as const;

export function PublicShell() {
  const { t } = useTranslation();

  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh]">
      <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10 lg:px-8 lg:py-16">
        <PublicHeader />

        <section className="mt-8 grid gap-8 sm:mt-12 lg:mt-14 lg:grid-cols-[1.1fr_0.9fr] lg:items-center lg:gap-10">
          <div className="min-w-0">
            <p className="inline-flex rounded-full bg-[var(--color-primary-soft)] px-3 py-1 text-xs font-semibold uppercase tracking-wide text-[var(--color-accent-foreground)]">
              {t("landing.tag")}
            </p>
            <h1 className="mt-4 text-3xl font-semibold tracking-tight text-[var(--color-foreground)] sm:text-4xl lg:text-5xl">
              {t("landing.title")}
            </h1>
            <p className="mt-4 max-w-xl text-base leading-relaxed text-[var(--color-muted)] sm:text-lg">
              {t("landing.heroSubtitle")}
            </p>
            <div className="mt-6 flex flex-col gap-3 sm:mt-8 sm:flex-row sm:flex-wrap">
              <Link href="/register" className="w-full sm:w-auto">
                <Button size="lg" className="w-full sm:w-auto">
                  {t("landing.getStarted")}
                </Button>
              </Link>
              <Link href="/login" className="w-full sm:w-auto">
                <Button variant="secondary" size="lg" className="w-full sm:w-auto">
                  {t("common.login")}
                </Button>
              </Link>
            </div>
          </div>

          <Card className="min-w-0 overflow-hidden shadow-[var(--shadow-elevated)]">
            <div className="h-2 bg-gradient-to-r from-indigo-500 via-violet-500 to-sky-500" />
            <CardBody className="p-4 sm:p-6">
              <h2 className="text-sm font-semibold text-[var(--color-foreground)]">{t("landing.featuresTitle")}</h2>
              <ul className="mt-4 grid gap-2.5 sm:gap-3">
                {featureKeys.map((key) => (
                  <li
                    key={key}
                    className="flex gap-3 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)] px-3 py-2.5 text-sm text-[var(--color-foreground)] sm:px-4 sm:py-3"
                  >
                    <span className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-[var(--color-primary)] text-[10px] text-white">
                      ✓
                    </span>
                    <span className="min-w-0">{t(key)}</span>
                  </li>
                ))}
              </ul>
              <div className="mt-5 flex flex-wrap gap-3 text-sm sm:mt-6 sm:gap-4">
                <Link className="link" href="/register">
                  {t("common.register")}
                </Link>
                <Link className="link" href="/forgot-password">
                  {t("login.forgotPassword")}
                </Link>
              </div>
            </CardBody>
          </Card>
        </section>
      </div>
    </div>
  );
}
