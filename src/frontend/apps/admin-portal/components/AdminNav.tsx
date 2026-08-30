"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useSyncExternalStore } from "react";

import { api } from "@/lib/api";
import { clearSession, readAccessToken, readRefreshToken, subscribeToSession } from "@/lib/session";

function getSnapshot(): boolean {
  return Boolean(readAccessToken());
}

/** No session is knowable during server rendering; the client corrects this on mount. */
function getServerSnapshot(): boolean {
  return false;
}

/**
 * Shows the staff-only links and Sign out only to an authenticated visitor, and Sign in only to
 * an anonymous one. Mirrors the user-portal's SiteNav so both apps stay consistent.
 */
export function AdminNav() {
  const router = useRouter();
  const isAuthenticated = useSyncExternalStore(subscribeToSession, getSnapshot, getServerSnapshot);
  const [signingOut, setSigningOut] = useState(false);

  async function handleSignOut() {
    setSigningOut(true);

    try {
      await api.auth.logout({ refreshToken: readRefreshToken() ?? null });
    } catch {
      // Server-side revocation is best-effort; the client still forgets the token below.
    } finally {
      clearSession();
      setSigningOut(false);
      router.replace("/login");
    }
  }

  return (
    <header className="flex flex-wrap items-center gap-4 border-b pb-3 text-sm">
      <Link href="/dashboard">Dashboard</Link>
      <Link href="/upgrade-requests">Upgrade requests</Link>
      {isAuthenticated ? (
        <button type="button" onClick={handleSignOut} disabled={signingOut} className="ml-auto">
          {signingOut ? "Signing out..." : "Sign out"}
        </button>
      ) : (
        <Link href="/login" className="ml-auto">
          Sign in
        </Link>
      )}
    </header>
  );
}
