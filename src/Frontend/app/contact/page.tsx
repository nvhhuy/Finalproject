"use client";

import { useState } from "react";
import { useTranslation } from "react-i18next";
import { PublicHeader } from "@/components/layout/PublicHeader";
import { Card, CardBody } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Alert } from "@/components/ui/alert";
import { TurnstileWidget } from "@/components/ui/TurnstileWidget";
import { submitContactMessage } from "@/lib/features/contact/contact-api";

function isValidEmail(email: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

export default function ContactPage() {
  const { t } = useTranslation();

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const validate = () => {
    if (!name.trim()) return t("validation.required");
    if (!email.trim() || !isValidEmail(email)) return t("validation.invalidEmail");
    if (!subject.trim()) return t("validation.required");
    if (!body.trim()) return t("validation.required");
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return;
    }

    setLoading(true);
    try {
      const res = await submitContactMessage({
        name: name.trim(),
        email: email.trim(),
        subject: subject.trim(),
        body: body.trim(),
        turnstileToken,
      });

      if (res.success) {
        setSuccess(true);
        setName("");
        setEmail("");
        setSubject("");
        setBody("");
        setTurnstileToken(null);
      } else {
        setError(res.error?.message ?? t("errors.requestFailed"));
        setTurnstileToken(null);
      }
    } catch {
      setError(t("errors.requestFailed"));
      setTurnstileToken(null);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh]">
      <div className="mx-auto max-w-2xl px-4 py-8 sm:px-6 lg:px-8">
        <PublicHeader />
        <h1 className="mt-10 text-3xl font-semibold">{t("contact.title")}</h1>
        <p className="mt-2 text-sm text-[var(--color-muted)]">{t("contact.body")}</p>

        <Card className="mt-6">
          <CardBody>
            {success ? (
              <div className="space-y-3 py-4 text-center">
                <p className="text-lg font-medium text-green-400">{t("contact.successTitle")}</p>
                <p className="text-sm text-[var(--color-muted)]">{t("contact.successBody")}</p>
                <Button
                  variant="secondary"
                  className="mt-2"
                  onClick={() => setSuccess(false)}
                >
                  {t("contact.sendAnother")}
                </Button>
              </div>
            ) : (
              <form onSubmit={handleSubmit} className="space-y-4">
                {error && <Alert variant="error">{error}</Alert>}

                <div className="space-y-1.5">
                  <Label htmlFor="contact-name">{t("contact.nameLabel")}</Label>
                  <Input
                    id="contact-name"
                    type="text"
                    placeholder={t("contact.namePlaceholder")}
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    disabled={loading}
                    required
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="contact-email">{t("contact.emailLabel")}</Label>
                  <Input
                    id="contact-email"
                    type="email"
                    placeholder={t("contact.emailPlaceholder")}
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    disabled={loading}
                    required
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="contact-subject">{t("contact.subjectLabel")}</Label>
                  <Input
                    id="contact-subject"
                    type="text"
                    placeholder={t("contact.subjectPlaceholder")}
                    value={subject}
                    onChange={(e) => setSubject(e.target.value)}
                    disabled={loading}
                    required
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="contact-body">{t("contact.messageLabel")}</Label>
                  <textarea
                    id="contact-body"
                    className="min-h-32 w-full rounded-lg border border-[var(--color-border)] bg-transparent px-3 py-2 text-sm placeholder:text-[var(--color-muted)] focus:outline-none focus:ring-1 focus:ring-[var(--color-accent)] disabled:opacity-50"
                    placeholder={t("contact.messagePlaceholder")}
                    value={body}
                    onChange={(e) => setBody(e.target.value)}
                    disabled={loading}
                    required
                  />
                </div>

                <TurnstileWidget
                  onVerify={setTurnstileToken}
                  onExpire={() => setTurnstileToken(null)}
                  onError={() => setTurnstileToken(null)}
                />

                <Button type="submit" className="w-full" disabled={loading}>
                  {loading ? t("common.loading") : t("contact.submit")}
                </Button>
              </form>
            )}
          </CardBody>
        </Card>
      </div>
    </div>
  );
}
