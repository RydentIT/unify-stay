"use client";

const STORAGE_KEY = "unify.accessToken";

/**
 * SCAFFOLD token storage.
 *
 * localStorage is readable by any script on the origin, so this is not where the access token
 * should live once auth is real - the intended end state is an httpOnly, SameSite cookie set
 * by the backend. This exists so the pages have somewhere to put a token today, and is
 * deliberately confined to this one file so replacing it touches nothing else.
 */
export function readAccessToken(): string | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }

  return window.localStorage.getItem(STORAGE_KEY) ?? undefined;
}

export function writeAccessToken(token: string): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(STORAGE_KEY, token);
}

export function clearAccessToken(): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(STORAGE_KEY);
}
