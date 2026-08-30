"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { forgotPasswordSchema, type ForgotPasswordFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Field } from "@unify/ui";
import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

/**
 * "Check your email" plus the resend form (EVR-006).
 *
 * Reuses forgotPasswordSchema because the shape is identical - a single email field with the
 * same rules. Sharing it avoids two definitions drifting apart.
 */
export default function VerifyEmailPage() {
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [sent, setSent] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: "" },
  });

  async function onSubmit(values: ForgotPasswordFormValues) {
    setFailure(null);

    try {
      await api.auth.resendVerification({ email: values.email });
      setSent(true);
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Verify your email</h1>
      <p className="text-sm">
        Open the link we emailed you to confirm your address. If it has expired or never
        arrived, request a new one below.
      </p>

      {/* Deliberately non-committal: this endpoint must not reveal whether an account exists. */}
      {sent ? (
        <Alert>
          If that address has an unverified account, a new verification link is on its way.
        </Alert>
      ) : (
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
          <Field
            label="Email"
            type="email"
            autoComplete="email"
            errors={errors.email?.message ? [errors.email.message] : []}
            {...register("email")}
          />
          <Button type="submit" pending={isSubmitting}>
            Resend verification email
          </Button>
        </form>
      )}

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}

      <p className="text-sm">
        <Link href="/login">Back to sign in</Link>
      </p>
    </section>
  );
}
