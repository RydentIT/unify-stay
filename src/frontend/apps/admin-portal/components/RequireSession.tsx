"use client";

import { useRouter } from "next/navigation";
import { useEffect, useSyncExternalStore, type ReactNode } from "react";

import { isPasswordChangeRequired, readAccessToken } from "@/lib/session";

/** Where a user holding a limited-scope token is sent, and the one route they may stay on. */
export const FORCED_PASSWORD_CHANGE_ROUTE = "/change-password-required";

export interface RequireSessionProps {
  children: ReactNode;
  /**
   * Set on the mandatory change-password screen itself, which must remain reachable while the
   * limited-scope token is the only credential the user holds.
   */
  allowPasswordChangeScope?: boolean;
}

/**
 * Client-side route guard.
 *
 * LOG-013 / BR-LOG-008 are enforced server-side by the authorization policy - that is what
 * actually secures the application. This guard exists so the user is not shown a page that
 * would only fail, and so typing a URL directly does not appear to work. It is a
 * user-experience mechanism, never the security boundary.
 *
 * The token is read through useSyncExternalStore rather than an effect: the status is then a
 * value derived during render, and the effect is left doing only what an effect should - the
 * navigation. The server snapshot is undefined, so nothing protected renders until hydration.
 */
export function RequireSession({ children, allowPasswordChangeScope = false }: RequireSessionProps) {
  const router = useRouter();
  const token = useSyncExternalStore(subscribeToSession, readAccessToken, () => undefined);

  const status = !token
    ? "unauthenticated"
    : isPasswordChangeRequired(token) && !allowPasswordChangeScope
      ? "password-change-required"
      : "allowed";

  useEffect(() => {
    if (status === "unauthenticated") {
      router.replace("/login");
    } else if (status === "password-change-required") {
      router.replace(FORCED_PASSWORD_CHANGE_ROUTE);
    }
  }, [status, router]);

  // Nothing protected is rendered until the check passes, so it never flashes on screen.
  if (status !== "allowed") {
    return <p className="text-sm">Loading...</p>;
  }

  return <>{children}</>;
}

/**
 * Session changes originate in this tab (sign-in writes then navigates) and, for a sign-out in
 * another tab, through the storage event. Subscribing to both keeps the guard honest without
 * polling.
 */
function subscribeToSession(onStoreChange: () => void): () => void {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  window.addEventListener("storage", onStoreChange);
  return () => window.removeEventListener("storage", onStoreChange);
}

/** Re-exported so callers can build their own guards on the same primitive if needed. */
export function useSessionToken(): string | undefined {
  return useSyncExternalStore(subscribeToSession, readAccessToken, () => undefined);
}
