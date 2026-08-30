"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  forcedPasswordChangeSchema,
  type ForcedPasswordChangeFormValues,
} from "@unify/api-client/schemas";
import { Alert, Button, Field } from "@unify/ui";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { RequireSession } from "@/components/RequireSession";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";
import { writeSession } from "@/lib/session";

/**
 * LOG-013 / LOG-014 / LOG-015. The one screen reachable while holding a limited-scope token,
 * which is why RequireSession is given allowPasswordChangeScope.
 */
export default function ChangePasswordRequiredPage() {
  return (
    <RequireSession allowPasswordChangeScope>
      <ForcedPasswordChangeForm />
    </RequireSession>
  );
}

function ForcedPasswordChangeForm() {
  const router = useRouter();
  const [failure, setFailure] = useState<FormFailure | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForcedPasswordChangeFormValues>({
    resolver: zodResolver(forcedPasswordChangeSchema),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });

  async function onSubmit(values: ForcedPasswordChangeFormValues) {
    setFailure(null);

    try {
      const result = await api.auth.changePasswordRequired({ newPassword: values.newPassword });

      // LOG-014: the response carries a full-access token, so the user continues without a
      // second sign-in. Storing it is what lifts the client-side restriction too.
      writeSession({
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        mustChangePassword: result.mustChangePassword,
        mustCompleteProfile: result.mustCompleteProfile,
      });

      router.replace("/profile");
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Choose a new password</h1>
      <Alert>
        You must set a new password before you can use your account. Everything else is
        unavailable until you do.
      </Alert>

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

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
    </section>
  );
}
