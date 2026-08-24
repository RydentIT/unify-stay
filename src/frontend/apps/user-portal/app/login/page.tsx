"use client";

import { Alert, Button, Field } from "@unify/ui";
import { useState } from "react";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";
import { writeAccessToken } from "@/lib/session";

export default function LoginPage() {
  const [pending, setPending] = useState(false);
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [mustChangePassword, setMustChangePassword] = useState(false);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setFailure(null);

    const form = new FormData(event.currentTarget);

    try {
      const result = await api.auth.login({
        email: String(form.get("email") ?? ""),
        password: String(form.get("password") ?? ""),
      });

      writeAccessToken(result.accessToken);

      // A limited-scope token means the only permitted next step is changing the password.
      setMustChangePassword(result.mustChangePassword);
    } catch (error) {
      setFailure(toFormFailure(error));
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Log in</h1>

      <form onSubmit={onSubmit} className="flex flex-col gap-3">
        <Field label="Email" name="email" type="email" autoComplete="email" errors={failure?.fieldErrors.Email} />
        <Field
          label="Password"
          name="password"
          type="password"
          autoComplete="current-password"
          errors={failure?.fieldErrors.Password}
        />
        <Button type="submit" pending={pending}>
          Log in
        </Button>
      </form>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
      {mustChangePassword ? <Alert>You must change your password before continuing.</Alert> : null}
    </section>
  );
}
