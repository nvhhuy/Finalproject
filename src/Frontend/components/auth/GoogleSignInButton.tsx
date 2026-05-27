"use client";

import { GoogleOAuthProvider, GoogleLogin, type CredentialResponse } from "@react-oauth/google";
import { useTranslation } from "react-i18next";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/auth/auth-context";
import { authApi } from "@/lib/features/auth/auth-api";

const clientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "";

export function GoogleSignInButton() {
  const { t } = useTranslation();
  const auth = useAuth();
  const router = useRouter();
  const params = useSearchParams();
  const next = params.get("next") ?? "/app";

  if (!clientId) {
    return <p className="text-xs text-zinc-500">{t("google.notConfigured")}</p>;
  }

  async function onSuccess(res: CredentialResponse) {
    if (!res.credential) return;
    const result = await authApi.googleSignIn(res.credential);
    if (result.kind === "ok") {
      auth.setSession(result.user, result.tokens);
      router.push(next);
      return;
    }
    alert(result.message ?? t("google.failed"));
  }

  return (
    <GoogleOAuthProvider clientId={clientId}>
      <GoogleLogin
        onSuccess={onSuccess}
        onError={() => alert(t("google.failed"))}
        useOneTap={false}
        theme="outline"
        size="large"
        text="signin_with"
        shape="rectangular"
      />
    </GoogleOAuthProvider>
  );
}
