"use client";

import Link from "next/link";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardDescription, CardTitle } from "@/components/ui/card";
import { PageContent, PageHeader } from "@/components/ui/page-header";
import { useAuth } from "@/lib/auth/auth-context";

export default function DashboardPage() {
  const { t } = useTranslation();
  const auth = useAuth();

  const quickLinks = [
    { href: "/app/profile", label: t("dashboard.quickProfile") },
    { href: "/app/security/2fa", label: t("dashboard.quick2fa") },
    { href: "/app/media", label: t("dashboard.quickMedia") },
  ];

  return (
    <PageContent>
      <PageHeader title={t("dashboard.title")} description={t("dashboard.subtitle")} />

      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardBody>
            <CardTitle>{t("dashboard.signedInAs")}</CardTitle>
            <CardDescription className="mt-3">
              <span className="text-base font-semibold text-[var(--color-foreground)]">
                {auth.user?.firstName} {auth.user?.lastName}
              </span>
              <span> ({auth.user?.userName})</span>
            </CardDescription>
            <p className="mt-2 text-sm text-[var(--color-muted)]">{auth.user?.email}</p>
          </CardBody>
        </Card>

        <Card>
          <CardBody>
            <CardTitle>{t("landing.featuresTitle")}</CardTitle>
            <CardDescription className="mt-3">{t("dashboard.tip")}</CardDescription>
            <div className="mt-4 grid gap-2">
              {quickLinks.map((link) => (
                <Link key={link.href} href={link.href}>
                  <Button variant="secondary" className="w-full justify-start">
                    {link.label} →
                  </Button>
                </Link>
              ))}
            </div>
          </CardBody>
        </Card>
      </div>
    </PageContent>
  );
}
