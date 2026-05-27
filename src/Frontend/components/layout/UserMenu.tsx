"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/auth/auth-context";
import { cn } from "@/lib/ui/cn";

function ChevronIcon({ open }: { open: boolean }) {
  return (
    <svg
      viewBox="0 0 20 20"
      fill="currentColor"
      aria-hidden
      className={cn("h-4 w-4 shrink-0 text-[var(--color-muted)] transition-transform duration-200", open && "rotate-180")}
    >
      <path
        fillRule="evenodd"
        d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.25a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
        clipRule="evenodd"
      />
    </svg>
  );
}

export function UserMenu() {
  const { t } = useTranslation();
  const auth = useAuth();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  const initials =
    [auth.user?.firstName?.[0], auth.user?.lastName?.[0]].filter(Boolean).join("") ||
    auth.user?.userName?.slice(0, 2).toUpperCase() ||
    "?";

  const displayName =
    auth.user?.firstName && auth.user?.lastName
      ? `${auth.user.firstName} ${auth.user.lastName}`
      : (auth.user?.userName ?? "—");

  useEffect(() => {
    if (!open) return;
    function onPointerDown(e: MouseEvent) {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className={cn(
          "flex items-center gap-2 rounded-full border border-[var(--color-border)] bg-[var(--color-surface)] py-1 pl-1 pr-2.5 shadow-sm transition",
          "hover:border-[var(--color-border-strong)]",
          open && "border-[var(--color-primary)] ring-2 ring-[var(--color-primary)]/25",
        )}
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label={t("userMenu.openMenu")}
      >
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[var(--color-primary)] text-xs font-semibold text-white">
          {initials}
        </span>
        <span className="hidden max-w-[6rem] truncate text-sm font-medium text-[var(--color-foreground)] md:inline lg:max-w-[8rem]">
          {auth.user?.userName}
        </span>
        <ChevronIcon open={open} />
      </button>

      {open ? (
        <div
          role="menu"
          className="absolute right-0 z-50 mt-2 w-[min(17rem,calc(100vw-1.5rem))] origin-top-right overflow-hidden rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] shadow-[var(--shadow-elevated)] sm:w-64"
        >
          <div className="border-b border-[var(--color-border)] bg-[var(--color-surface-muted)] px-4 py-3.5">
            <p className="truncate text-sm font-semibold text-[var(--color-foreground)]">{displayName}</p>
            <p className="truncate text-sm text-[var(--color-muted)]">{auth.user?.email}</p>
            <p className="mt-0.5 truncate text-xs text-[var(--color-muted)]">@{auth.user?.userName}</p>
          </div>

          <div className="px-2 py-2">
            <p className="menu-section-label">{t("userMenu.account")}</p>
            <MenuLink href="/app/profile" onClick={() => setOpen(false)}>
              {t("userMenu.profile")}
            </MenuLink>
            <MenuLink href="/app/security/2fa" onClick={() => setOpen(false)}>
              {t("userMenu.security")}
            </MenuLink>
          </div>

          <div className="mx-3 border-t border-[var(--color-border)]" />

          <div className="px-3 py-2.5">
            <p className="menu-section-label mb-2">{t("userMenu.appearance")}</p>
            <ThemeSwitcher variant="menu" />
          </div>

          <div className="px-3 py-2.5">
            <p className="menu-section-label mb-2">{t("userMenu.language")}</p>
            <LanguageSwitcher variant="menu" />
          </div>

          <div className="mx-3 border-t border-[var(--color-border)]" />

          <div className="p-2">
            <Button
              variant="ghost"
              className="w-full justify-start text-[var(--color-danger-text)] hover:bg-[var(--color-danger-soft)]"
              onClick={() => {
                setOpen(false);
                auth.logout();
              }}
            >
              {t("common.signOut")}
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function MenuLink({
  href,
  children,
  onClick,
}: {
  href: string;
  children: React.ReactNode;
  onClick?: () => void;
}) {
  return (
    <Link
      href={href}
      role="menuitem"
      onClick={onClick}
      className="block rounded-lg px-3 py-2 text-sm text-[var(--color-foreground)] transition hover:bg-[var(--color-surface-muted)]"
    >
      {children}
    </Link>
  );
}
