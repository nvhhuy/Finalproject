"use client";

import { useTranslation } from "react-i18next";
import { PublicHeader } from "@/components/layout/PublicHeader";
import { Card, CardBody } from "@/components/ui/card";

export default function ContactPage() {
  const { t } = useTranslation();
  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh]">
      <div className="mx-auto max-w-3xl px-4 py-8 sm:px-6 lg:px-8">
        <PublicHeader />
        <h1 className="mt-10 text-3xl font-semibold">{t("contact.title")}</h1>
        <Card className="mt-6">
          <CardBody className="space-y-2 text-sm text-[var(--color-muted)]">
            <p>{t("contact.body")}</p>
            <p>
              <strong>{t("contact.emailLabel")}:</strong> support@example.com
            </p>
          </CardBody>
        </Card>
      </div>
    </div>
  );
}
