"use client";

import { Alert } from "@unify/ui";
import Link from "next/link";
import { use, useEffect, useState } from "react";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

type State = { kind: "verifying" } | { kind: "verified" } | { kind: "failed"; failure: FormFailure };

/**
 * Admin/Staff counterpart of the user-portal verify-email page. Exists because bootstrap-admin
 * and any staff account created by an admin send a link here rather than to the user portal -
 * see AppUrlOptions on the backend, which picks the portal by the account's role.
 *
 * EVR-010: expired, already-used and unrecognised are surfaced as three different outcomes,
 * because each needs a different next step from the user.
 */
export default function VerifyEmailTokenPage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = use(params);
  const [state, setState] = useState<State>({ kind: "verifying" });

  useEffect(() => {
    let cancelled = false;

    async function verify() {
      try {
        await api.auth.verifyEmail({ token });

        if (!cancelled) {
          setState({ kind: "verified" });
        }
      } catch (error) {
        if (!cancelled) {
          setState({ kind: "failed", failure: toFormFailure(error) });
        }
      }
    }

    void verify();

    return () => {
      cancelled = true;
    };
  }, [token]);

  if (state.kind === "verifying") {
    return <p className="text-sm">Verifying your email address...</p>;
  }

  if (state.kind === "verified") {
    return (
      <section className="flex flex-col gap-3">
        <h1 className="text-xl font-semibold">Email verified</h1>
        <Alert>Your email address is confirmed. You can now sign in.</Alert>
        <p className="text-sm">
          <Link href="/login">Go to sign in</Link>
        </p>
      </section>
    );
  }

  const alreadyUsed = state.failure.code === "auth.email.used_token";

  return (
    <section className="flex flex-col gap-3">
      <h1 className="text-xl font-semibold">
        {alreadyUsed ? "Already verified" : "Verification failed"}
      </h1>

      <Alert tone={alreadyUsed ? "info" : "error"}>{state.failure.message}</Alert>

      <p className="text-sm">
        <Link href="/login">Go to sign in</Link>
      </p>
    </section>
  );
}
