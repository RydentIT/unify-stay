"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { changeEmailSchema, type ChangeEmailFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Field } from "@unify/ui";
import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { RequireSession } from "@/components/RequireSession";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

export default function ChangeEmailPage() {
  return (
    <RequireSession>
      <ChangeEmailForm />
    </RequireSession>
  );
}

function ChangeEmailForm() {
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [requested, setRequested] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ChangeEmailFormValues>({
    resolver: zodResolver(changeEmailSchema),
    defaultValues: { newEmail: "", currentPassword: "" },
  });

  async function onSubmit(values: ChangeEmailFormValues) {
    setFailure(null);

    try {
      await api.profile.requestEmailChange({
        newEmail: values.newEmail,
        currentPassword: values.currentPassword,
      });

      setRequested(values.newEmail);
    } catch (error) {
      const formFailure = toFormFailure(error);
      setFailure(formFailure);

      if (formFailure.code === "auth.password.current_incorrect") {
        setError("currentPassword", { type: "server", message: formFailure.message });
      } else if (formFailure.code === "profile.email.address_in_use") {
        setError("newEmail", { type: "server", message: formFailure.message });
      }
    }
  }

  if (requested) {
    return (
      <section className="flex flex-col gap-3">
        <h1 className="text-xl font-semibold">Confirm your new address</h1>
        {/* BR-PRF-002: nothing changes until the new address is confirmed. */}
        <Alert>
          We have sent a confirmation link to <strong>{requested}</strong>. Your account keeps
          using its current address until you open that link.
        </Alert>
        <p className="text-sm">
          <Link href="/profile">Back to profile</Link>
        </p>
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Change email address</h1>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <Field
          label="New email address"
          type="email"
          autoComplete="email"
          errors={errors.newEmail?.message ? [errors.newEmail.message] : []}
          {...register("newEmail")}
        />
        <Field
          label="Current password"
          type="password"
          autoComplete="current-password"
          errors={errors.currentPassword?.message ? [errors.currentPassword.message] : []}
          {...register("currentPassword")}
        />
        <Button type="submit" pending={isSubmitting}>
          Send confirmation link
        </Button>
      </form>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}

      <p className="text-sm">
        <Link href="/profile">Back to profile</Link>
      </p>
    </section>
  );
}
