"use client";

/**
 * SCAFFOLD token storage.
 *
 * localStorage is readable by any script on the origin, so this is not where an access token
 * belongs long term - the intended end state is an httpOnly, SameSite cookie set by the
 * backend. It is confined to this one module so replacing it touches nothing else.
 */
const ACCESS_TOKEN_KEY = "unify.accessToken";
const REFRESH_TOKEN_KEY = "unify.refreshToken";

/**
 * Fired whenever this tab writes or clears the session. The browser's own "storage" event only
 * fires in OTHER tabs, never the one that made the change, so anything in this tab that needs
 * to react to sign-in/sign-out (SiteNav) needs this instead.
 */
const SESSION_CHANGED_EVENT = "unify:session-changed";

function notifySessionChanged(): void {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(SESSION_CHANGED_EVENT));
  }
}

/** Subscribes to both same-tab and cross-tab session changes; returns an unsubscribe function. */
export function subscribeToSessionChanges(callback: () => void): () => void {
  if (typeof window === "undefined") {
    return () => {};
  }

  window.addEventListener(SESSION_CHANGED_EVENT, callback);
  window.addEventListener("storage", callback);

  return () => {
    window.removeEventListener(SESSION_CHANGED_EVENT, callback);
    window.removeEventListener("storage", callback);
  };
}

export interface StoredSession {
  accessToken: string;
  refreshToken: string | null;
  /** True while the token is the limited-scope forced-reset credential. */
  mustChangePassword: boolean;
  /** True while the token is the limited-scope profile-completion credential (Google accounts). */
  mustCompleteProfile: boolean;
}

function safeGet(key: string): string | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }

  try {
    return window.localStorage.getItem(key) ?? undefined;
  } catch {
    // Private mode or blocked site data. Treat as signed out rather than crashing.
    return undefined;
  }
}

export function readAccessToken(): string | undefined {
  return safeGet(ACCESS_TOKEN_KEY);
}

export function readRefreshToken(): string | undefined {
  return safeGet(REFRESH_TOKEN_KEY);
}

export function writeSession(session: StoredSession): void {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.setItem(ACCESS_TOKEN_KEY, session.accessToken);

    if (session.refreshToken) {
      window.localStorage.setItem(REFRESH_TOKEN_KEY, session.refreshToken);
    } else {
      window.localStorage.removeItem(REFRESH_TOKEN_KEY);
    }

    notifySessionChanged();
  } catch {
    // Nothing useful to do; the user simply will not stay signed in.
  }
}

export function clearSession(): void {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    window.localStorage.removeItem(REFRESH_TOKEN_KEY);
    notifySessionChanged();
  } catch {
    // Ignore.
  }
}

/**
 * Reads the token_type claim without verifying the signature.
 *
 * This is a UI hint only - it decides which screen to show. The server validates the signature
 * on every request and enforces the same restriction, so a user editing localStorage gains
 * nothing but a broken-looking page (LOG-013 / BR-LOG-008 are enforced server-side).
 */
export function isPasswordChangeRequired(token: string | undefined = readAccessToken()): boolean {
  if (!token) {
    return false;
  }

  const claims = decodeJwtPayload(token);
  return claims?.token_type === "password_change_required";
}

/** Mirrors isPasswordChangeRequired for the Google profile-completion scope. */
export function isProfileCompletionRequired(token: string | undefined = readAccessToken()): boolean {
  if (!token) {
    return false;
  }

  const claims = decodeJwtPayload(token);
  return claims?.token_type === "profile_completion_required";
}

export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const segments = token.split(".");

  if (segments.length !== 3 || !segments[1]) {
    return null;
  }

  try {
    const base64 = segments[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");

    return JSON.parse(atob(padded)) as Record<string, unknown>;
  } catch {
    return null;
  }
}
