"use client";

/**
 * Minimal client for Google Identity Services (GSI) - just enough to obtain an ID token from
 * the "Sign in with Google" popup flow.
 *
 * Deliberately thin: the browser's only job is to hand the raw ID token to the backend.
 * Nothing here reads the token's claims or trusts anything about the user - the email and its
 * verified flag are established server-side, from the signature-checked token, which is what
 * REG-004 / REG-005 rely on.
 */

const SCRIPT_SRC = "https://accounts.google.com/gsi/client";

let scriptPromise: Promise<void> | null = null;

interface GoogleCredentialResponse {
  credential: string;
}

interface GoogleIdentityServices {
  accounts: {
    id: {
      initialize(config: {
        client_id: string;
        callback: (response: GoogleCredentialResponse) => void;
        auto_select?: boolean;
        cancel_on_tap_outside?: boolean;
        use_fedcm_for_prompt?: boolean;
      }): void;
      prompt(momentListener?: (notification: GsiMoment) => void): void;
      cancel(): void;
    };
  };
}

interface GsiMoment {
  isNotDisplayed(): boolean;
  isSkippedMoment(): boolean;
  getNotDisplayedReason(): string;
  getSkippedReason(): string;
}

declare global {
  interface Window {
    google?: GoogleIdentityServices;
  }
}

function loadScript(): Promise<void> {
  if (typeof window === "undefined") {
    return Promise.reject(new Error("Google Identity Services requires a browser."));
  }

  if (window.google?.accounts?.id) {
    return Promise.resolve();
  }

  scriptPromise ??= new Promise<void>((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>(`script[src="${SCRIPT_SRC}"]`);

    if (existing) {
      existing.addEventListener("load", () => resolve());
      existing.addEventListener("error", () => reject(new Error("Failed to load Google Identity Services.")));
      return;
    }

    const script = document.createElement("script");
    script.src = SCRIPT_SRC;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("Failed to load Google Identity Services."));
    document.head.appendChild(script);
  });

  return scriptPromise;
}

/**
 * Opens the Google One Tap / popup flow and resolves with the raw ID token.
 *
 * Rejects if the user closes the prompt without choosing an account (GSI reports this as a
 * "not displayed" or "skipped" moment rather than an error callback, so both are treated the
 * same way here: the caller sees a plain cancellation, not a stack of provider-specific cases).
 */
export async function obtainGoogleIdToken(clientId: string): Promise<string> {
  if (!clientId) {
    throw new Error("Google sign-in is not configured.");
  }

  await loadScript();

  const google = window.google;

  if (!google?.accounts?.id) {
    throw new Error("Google Identity Services failed to initialise.");
  }

  return new Promise<string>((resolve, reject) => {
    google.accounts.id.initialize({
      client_id: clientId,
      auto_select: false,
      cancel_on_tap_outside: false,
      // Opts into the FedCM-backed prompt now rather than waiting for Google to make it
      // mandatory; the moment-listener API below is unchanged under FedCM.
      use_fedcm_for_prompt: true,
      callback: (response) => {
        if (response.credential) {
          resolve(response.credential);
        } else {
          reject(new Error("Google did not return a credential."));
        }
      },
    });

    google.accounts.id.prompt((notification) => {
      if (notification.isNotDisplayed() || notification.isSkippedMoment()) {
        reject(new Error("Google sign-in was cancelled."));
      }
    });
  });
}
