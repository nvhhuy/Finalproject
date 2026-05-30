"use client";

import { useTranslation } from "react-i18next";
import { PublicHeader } from "@/components/layout/PublicHeader";

export default function AboutPage() {
  const { t } = useTranslation();
  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh]">
      <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        <PublicHeader />
        <article className="mx-auto mt-10 max-w-3xl">
          <h1 className="text-3xl font-semibold">{t("about.title")}</h1>
          <p className="mt-4 leading-relaxed text-[var(--color-muted)]">{t("about.body")}</p>
        </article>
      </div>
    </div>
  );
}
