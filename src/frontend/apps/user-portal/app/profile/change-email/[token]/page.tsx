"use client";

import { Alert } from "@unify/ui";
import Link from "next/link";
import { use, useEffect, useState } from "react";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

type State = { kind: "confirming" } | { kind: "confirmed" } | { kind: "failed"; failure: FormFailure };

/**
 * Promotes the pending address. Deliberately outside RequireSession: the user follows this link
 * from their NEW inbox, quite possibly in a browser with no session. The token is the credential.
 */
export default function ConfirmEmailChangePage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = use(params);
  const [state, setState] = useState<State>({ kind: "confirming" });

  useEffect(() => {
    let cancelled = false;

    async function confirm() {
      try {
        await api.profile.confirmEmailChange({ token });

        if (!cancelled) {
          setState({ kind: "confirmed" });
        }
      } catch (error) {
        if (!cancelled) {
          setState({ kind: "failed", failure: toFormFailure(error) });
        }
      }
    }

    void confirm();

    return () => {
      cancelled = true;
    };
  }, [token]);

  if (state.kind === "confirming") {
    return <p className="text-sm">Confirming your new email address...</p>;
  }

  if (state.kind === "confirmed") {
    return (
      <section className="flex flex-col gap-3">
        <h1 className="text-xl font-semibold">Email address updated</h1>
        <Alert>Your account now uses this address. Sign in with it from now on.</Alert>
        <p className="text-sm">
          <Link href="/login">Go to sign in</Link>
        </p>
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-3">
      <h1 className="text-xl font-semibold">Could not confirm</h1>
      <Alert tone="error">{state.failure.message}</Alert>
      <p className="text-sm">
        <Link href="/profile/change-email">Request a new confirmation link</Link>
      </p>
    </section>
  );
}
