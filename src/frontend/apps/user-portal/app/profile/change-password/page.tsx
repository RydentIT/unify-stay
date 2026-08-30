"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { changePasswordSchema, type ChangePasswordFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Field } from "@unify/ui";
import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { RequireSession } from "@/components/RequireSession";
import { api } from "@/lib/api";
import { applyFieldErrors, toFormFailure, type FormFailure } from "@/lib/problem";

const FIELDS = ["currentPassword", "newPassword", "confirmPassword"] as const;

export default function ChangePasswordPage() {
  return (
    <RequireSession>
      <ChangePasswordForm />
    </RequireSession>
  );
}

function ChangePasswordForm() {
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [done, setDone] = useState(false);

  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });

  async function onSubmit(values: ChangePasswordFormValues) {
    setFailure(null);
    setDone(false);

    try {
      // PRF-004: the current password is required, and the server verifies it.
      await api.profile.changePassword({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
      });

      reset();
      setDone(true);
    } catch (error) {
      const formFailure = toFormFailure(error);
      setFailure(formFailure);

      // Map the server's specific outcomes back onto the field they concern.
      if (formFailure.code === "auth.password.current_incorrect") {
        setError("currentPassword", { type: "server", message: formFailure.message });
      } else if (formFailure.code === "auth.password.matches_current") {
        setError("newPassword", { type: "server", message: formFailure.message });
      }

      applyFieldErrors(formFailure, setError as never, FIELDS);
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Change password</h1>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <Field
          label="Current password"
          type="password"
          autoComplete="current-password"
          errors={errors.currentPassword?.message ? [errors.currentPassword.message] : []}
          {...register("currentPassword")}
        />
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
          Change password
        </Button>
      </form>

      {/* PRF-010 / BR-PRF-003: other devices are signed out, this one is not. */}
      {done ? (
        <Alert>
          Your password has been changed. You have been signed out on your other devices.
        </Alert>
      ) : null}

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}

      <p className="text-sm">
        <Link href="/profile">Back to profile</Link>
      </p>
    </section>
  );
}
