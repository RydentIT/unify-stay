"use client";

import { Alert, Button, Field } from "@unify/ui";
import { useState } from "react";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

export default function RegisterPage() {
  const [pending, setPending] = useState(false);
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setFailure(null);
    setRegisteredEmail(null);

    const form = new FormData(event.currentTarget);

    try {
      const result = await api.auth.register({
        email: String(form.get("email") ?? ""),
        password: String(form.get("password") ?? ""),
        displayName: String(form.get("displayName") ?? ""),
        acceptedTerms: form.get("acceptedTerms") === "on",
      });

      setRegisteredEmail(result.email);
    } catch (error) {
      setFailure(toFormFailure(error));
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Create an account</h1>

      <form onSubmit={onSubmit} className="flex flex-col gap-3">
        <Field label="Email" name="email" type="email" autoComplete="email" errors={failure?.fieldErrors.Email} />
        <Field label="Display name" name="displayName" errors={failure?.fieldErrors.DisplayName} />
        <Field
          label="Password"
          name="password"
          type="password"
          autoComplete="new-password"
          errors={failure?.fieldErrors.Password}
        />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" name="acceptedTerms" />
          I accept the terms of service
        </label>
        {failure?.fieldErrors.AcceptedTerms?.map((message) => (
          <Alert key={message} tone="error">
            {message}
          </Alert>
        ))}

        <Button type="submit" pending={pending}>
          Register
        </Button>
      </form>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
      {registeredEmail ? <Alert>Check {registeredEmail} for a verification link.</Alert> : null}
    </section>
  );
}
