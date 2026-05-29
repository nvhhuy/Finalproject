"use client";

import { GoogleOAuthProvider, GoogleLogin, type CredentialResponse } from "@react-oauth/google";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "@/lib/auth/auth-context";
import { authApi } from "@/lib/features/auth/auth-api";
import { tenancyApi, type TenantSsoPublicDto } from "@/lib/features/tenancy/tenancy-api";
import { InlineLoading } from "@/components/ui/loading";

export function TenantSsoSignInButton({ tenantSlug }: { tenantSlug: string }) {
  const { t } = useTranslation();
  const auth = useAuth();
  const router = useRouter();
  const [sso, setSso] = useState<TenantSsoPublicDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const res = await tenancyApi.getPublicSso(tenantSlug);
      if (!cancelled) {
        if (res.success && res.data) setSso(res.data);
        setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [tenantSlug]);

  if (loading) return <InlineLoading label={t("tenantPublic.ssoLoading")} />;

  if (!sso?.isEnabled || !sso.oidcClientId) {
    return <p className="text-sm text-[var(--color-muted)]">{t("tenantPublic.ssoDisabled")}</p>;
  }

  async function onSuccess(res: CredentialResponse) {
    if (!res.credential) return;
    const result = await authApi.tenantSsoSignIn(tenantSlug, res.credential);
    if (result.kind === "ok") {
      auth.setSession(result.user, result.tokens);
      router.push(`/t/${tenantSlug}/app`);
      return;
    }
    alert(result.message ?? t("tenantPublic.ssoFailed"));
  }

  return (
    <div className="space-y-2">
      <p className="text-xs text-[var(--color-muted)]">{t("tenantPublic.ssoHint")}</p>
      <GoogleOAuthProvider clientId={sso.oidcClientId}>
        <GoogleLogin
          onSuccess={onSuccess}
          onError={() => alert(t("tenantPublic.ssoFailed"))}
          useOneTap={false}
          theme="outline"
          size="large"
          text="signin_with"
          shape="rectangular"
        />
      </GoogleOAuthProvider>
    </div>
  );
}
