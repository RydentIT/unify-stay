"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { resetPasswordSchema, type ResetPasswordFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Field } from "@unify/ui";
import Link from "next/link";
import { use, useState } from "react";
import { useForm } from "react-hook-form";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";
import { clearSession } from "@/lib/session";

export default function ResetPasswordPage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = use(params);

  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [done, setDone] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });

  async function onSubmit(values: ResetPasswordFormValues) {
    setFailure(null);

    try {
      await api.auth.resetPassword({ token, newPassword: values.newPassword });

      // BR-FPW-004: the server revoked every session, so any token held here is already dead.
      clearSession();
      setDone(true);
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  if (done) {
    return (
      <section className="flex flex-col gap-3">
        <h1 className="text-xl font-semibold">Password updated</h1>
        <Alert>
          Your password has been changed and you have been signed out everywhere. Sign in with
          your new password.
        </Alert>
        <p className="text-sm">
          <Link href="/login">Go to sign in</Link>
        </p>
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Choose a new password</h1>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <Field
          label="New password"
          type="password"
          autoComplete="new-password"
          errors={errors.newPassword?.message ? [errors.newPassword.message] : []}
          {...register("newPassword")}
        />
        <Field
          label="Confirm new password"
          type="password"
          autoComplete="new-password"
          errors={errors.confirmPassword?.message ? [errors.confirmPassword.message] : []}
          {...register("confirmPassword")}
        />
        <Button type="submit" pending={isSubmitting}>
          Set new password
        </Button>
      </form>

      {failure ? (
        <div className="flex flex-col gap-2">
          <Alert tone="error">{failure.message}</Alert>
          {/* An expired or spent link is recoverable, so offer the way back. */}
          {failure.code === "auth.password.expired_reset_token" ||
          failure.code === "auth.password.used_reset_token" ||
          failure.code === "auth.password.invalid_reset_token" ? (
            <p className="text-sm">
              <Link href="/forgot-password">Request a new reset link</Link>
            </p>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
