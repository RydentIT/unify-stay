"use client";

import { Alert, Button, Field } from "@unify/ui";
import { useState } from "react";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

export default function ForgotPasswordPage() {
  const [pending, setPending] = useState(false);
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [submitted, setSubmitted] = useState(false);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setFailure(null);

    const form = new FormData(event.currentTarget);

    try {
      await api.auth.forgotPassword({ email: String(form.get("email") ?? "") });
      setSubmitted(true);
    } catch (error) {
      setFailure(toFormFailure(error));
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Reset your password</h1>

      <form onSubmit={onSubmit} className="flex flex-col gap-3">
        <Field label="Email" name="email" type="email" autoComplete="email" errors={failure?.fieldErrors.Email} />
        <Button type="submit" pending={pending}>
          Send reset link
        </Button>
      </form>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
      {/* Deliberately says the same thing whether or not the address exists. */}
      {submitted ? <Alert>If that address has an account, a reset link is on its way.</Alert> : null}
    </section>
  );
}
