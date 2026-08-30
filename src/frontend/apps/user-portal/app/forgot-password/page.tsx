"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { forgotPasswordSchema, type ForgotPasswordFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Field } from "@unify/ui";
import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

export default function ForgotPasswordPage() {
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [submitted, setSubmitted] = useState(false);

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
      await api.auth.forgotPassword({ email: values.email });
      setSubmitted(true);
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  // FPW-003 / BR-FPW-002: the confirmation is identical whether or not the address exists, so
  // this screen must not hint at which case applied.
  if (submitted) {
    return (
      <section className="flex flex-col gap-3">
        <h1 className="text-xl font-semibold">Check your email</h1>
        <Alert>
          If that address has an account, a password reset link is on its way. The link can only
          be used once and expires shortly.
        </Alert>
        <p className="text-sm">
          <Link href="/login">Back to sign in</Link>
        </p>
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Reset your password</h1>
      <p className="text-sm">Enter your email address and we will send you a reset link.</p>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <Field
          label="Email"
          type="email"
          autoComplete="email"
          errors={errors.email?.message ? [errors.email.message] : []}
          {...register("email")}
        />
        <Button type="submit" pending={isSubmitting}>
          Send reset link
        </Button>
      </form>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}

      <p className="text-sm">
        <Link href="/login">Back to sign in</Link>
      </p>
    </section>
  );
}
