"use client";

import { Button } from "@unify/ui";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { obtainGoogleIdToken } from "@/lib/google-identity";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";
import { writeSession } from "@/lib/session";

export interface GoogleAuthButtonProps {
  mode: "register" | "login";
  onFailure: (failure: FormFailure | null) => void;
  rememberMe?: boolean;
}

/**
 * Google sign-in entry point.
 *
 * The browser's only job is obtaining an ID token from Google Identity Services and handing it
 * to the backend untouched - the email and its verified flag are read from the signature-checked
 * token on the server, never asserted from here (REG-004 / REG-005 depend on that).
 *
 * Renders disabled with an explanation if NEXT_PUBLIC_GOOGLE_CLIENT_ID is unset, so a missing
 * config fails obviously rather than as a confusing click-and-nothing-happens.
 */
export function GoogleAuthButton({ mode, onFailure, rememberMe = false }: GoogleAuthButtonProps) {
  const router = useRouter();
  const [pending, setPending] = useState(false);

  const clientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "";
  const configured = clientId.length > 0;

  async function onClick() {
    onFailure(null);
    setPending(true);

    try {
      const idToken = await obtainGoogleIdToken(clientId);

      const result =
        mode === "register"
          ? await api.auth
              .registerWithGoogle({ idToken, acceptedTerms: true })
              .then(() => api.auth.loginWithGoogle({ idToken, rememberMe }))
          : await api.auth.loginWithGoogle({ idToken, rememberMe });

      writeSession({
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        mustChangePassword: result.mustChangePassword,
        mustCompleteProfile: result.mustCompleteProfile,
      });

      // Only a Google account can owe a phone number, and only an email account can owe a
      // password reset, so these two checks never both apply to the same result. A fully
      // resolved registration lands on the home page signed in; a plain login goes to the
      // profile screen, since the user already knows their own account.
      router.replace(
        result.mustChangePassword
          ? "/change-password-required"
          : result.mustCompleteProfile
            ? "/complete-profile"
            : mode === "register"
              ? "/"
              : "/profile",
      );
    } catch (error) {
      onFailure(toFormFailure(error));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="flex flex-col gap-1">
      <Button type="button" onClick={onClick} pending={pending} disabled={!configured}>
        {mode === "register" ? "Sign up with Google" : "Continue with Google"}
      </Button>
      {!configured ? (
        <p className="text-sm text-gray-600">
          Google sign-in is not configured. Set NEXT_PUBLIC_GOOGLE_CLIENT_ID and the API&apos;s
          Google__ClientId to enable it.
        </p>
      ) : null}
    </div>
  );
}
