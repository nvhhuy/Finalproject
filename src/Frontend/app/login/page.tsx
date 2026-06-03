"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { useTranslation } from "react-i18next";
import { LazyGoogleSignInButton } from "@/lib/ui/lazy-components";
import { Skeleton } from "@/components/ui/skeleton";
import { AuthLayout } from "@/components/layout/AuthLayout";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardBody } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { TurnstileWidget } from "@/components/ui/TurnstileWidget";
import { useAuth } from "@/lib/auth/auth-context";
import { ApiClientError } from "@/lib/errors";

function LoginForm() {
  const { t } = useTranslation();
  const auth = useAuth();
  const router = useRouter();
  const params = useSearchParams();
  const next = params.get("next") ?? "/app";

  const [email, setEmail] = useState("admin@localhost.com");
  const [password, setPassword] = useState("Temp!ChangeMe123");
  const [pendingLoginId, setPendingLoginId] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);

  async function onLogin() {
    setError(null);
    setLoading(true);
    try {
      const result = await auth.login(email, password, turnstileToken);
      if (result.kind === "ok") {
        router.push(next);
        return;
      }
      if (result.kind === "2fa_required") {
        setPendingLoginId(result.pendingLoginId);
        return;
      }
      if (result.kind === "email_not_verified") {
        router.push(`/awaiting-verification?email=${encodeURIComponent(email)}`);
        return;
      }
      if (result.kind === "error") {
        setError(result.message);
        setTurnstileToken(null);
        return;
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : t("login.signInFailed");
      setError(
        msg.includes("fetch") || msg.includes("Failed to fetch") ? t("login.apiUnreachable") : msg,
      );
      setTurnstileToken(null);
    } finally {
      setLoading(false);
    }
  }

  async function onComplete2fa() {
    if (!pendingLoginId) return;
    setError(null);
    setLoading(true);
    try {
      await auth.completeTwoFactor(pendingLoginId, code);
      router.push(next);
    } catch (e) {
      setError(e instanceof ApiClientError ? e.message : t("login.signInFailed"));
    } finally {
      setLoading(false);
    }
  }

  return (
    <AuthLayout title={t("login.title")} subtitle={t("login.subtitle")}>
      <Card>
        <CardBody className="grid gap-4">
          {pendingLoginId ? (
            <>
              <div>
                <p className="font-medium text-[var(--color-foreground)]">{t("login.twoFaTitle")}</p>
                <p className="mt-1 text-sm text-[var(--color-muted)]">{t("login.twoFaHint")}</p>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="twofa-code">{t("login.twoFaTitle")}</Label>
                <Input id="twofa-code" value={code} onChange={(e) => setCode(e.target.value)} placeholder="123456" />
              </div>
              <Button onClick={onComplete2fa} loading={loading} disabled={code.length < 6} className="w-full">
                {t("login.verify")}
              </Button>
              <Button variant="ghost" onClick={() => setPendingLoginId(null)} disabled={loading}>
                {t("common.back")}
              </Button>
            </>
          ) : (
            <>
              <div className="grid gap-2">
                <Label htmlFor="email">{t("common.email")}</Label>
                <Input id="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="email" />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="password">{t("common.password")}</Label>
                <Input
                  id="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  type="password"
                  autoComplete="current-password"
                />
              </div>
              <TurnstileWidget
                onVerify={setTurnstileToken}
                onExpire={() => setTurnstileToken(null)}
                onError={() => setTurnstileToken(null)}
              />
              <Button onClick={onLogin} loading={loading} className="w-full">
                {t("login.signIn")}
              </Button>
              <div className="relative py-2">
                <div className="absolute inset-0 flex items-center">
                  <div className="w-full border-t border-[var(--color-border)]" />
                </div>
                <div className="relative flex justify-center text-xs uppercase">
                  <span className="bg-[var(--color-surface)] px-2 text-[var(--color-muted)]">or</span>
                </div>
              </div>
              <Suspense fallback={<Skeleton className="h-10 w-full rounded-lg" />}>
                <LazyGoogleSignInButton />
              </Suspense>
              <div className="flex flex-wrap justify-between gap-2 text-sm text-[var(--color-muted)]">
                <span>
                  {t("login.needAccount")}{" "}
                  <Link className="link" href="/register">
                    {t("common.register")}
                  </Link>
                </span>
                <Link className="link" href="/forgot-password">
                  {t("login.forgotPassword")}
                </Link>
              </div>
            </>
          )}
          {error ? <Alert variant="error">{error}</Alert> : null}
        </CardBody>
      </Card>
    </AuthLayout>
  );
}

export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginForm />
    </Suspense>
  );
}
