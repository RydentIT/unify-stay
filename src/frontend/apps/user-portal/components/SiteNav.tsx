"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useSyncExternalStore } from "react";

import { api } from "@/lib/api";
import { clearSession, readAccessToken, readRefreshToken, subscribeToSessionChanges } from "@/lib/session";

function getSnapshot(): boolean {
  return Boolean(readAccessToken());
}

/** No session is knowable during server rendering; the client corrects this on mount. */
function getServerSnapshot(): boolean {
  return false;
}

/**
 * Shows Profile/Settings only to a signed-in visitor, and Register/Log in only to an
 * anonymous one. Without this an anonymous visitor could see (though not actually use, since
 * the API rejects the request) navigation into screens that belong to a session they don't
 * have - confusing, and a hint about the app's shape it doesn't need to give away.
 *
 * useSyncExternalStore rather than state-in-an-effect: the session lives in localStorage, an
 * external store React does not know about, and this is the hook built for subscribing to
 * exactly that - it reacts to sign-in/sign-out in this tab (see subscribeToSessionChanges) as
 * well as other tabs, without the cascading-render effect pattern.
 */
export function SiteNav() {
  const router = useRouter();
  const isAuthenticated = useSyncExternalStore(subscribeToSessionChanges, getSnapshot, getServerSnapshot);
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
      <Link href="/">Home</Link>
      {isAuthenticated ? (
        <>
          <Link href="/profile">Profile</Link>
          <Link href="/settings">Settings</Link>
          <button type="button" onClick={handleSignOut} disabled={signingOut} className="ml-auto">
            {signingOut ? "Signing out..." : "Sign out"}
          </button>
        </>
      ) : (
        <>
          <Link href="/register">Register</Link>
          <Link href="/login" className="ml-auto">
            Log in
          </Link>
        </>
      )}
    </header>
  );
}
