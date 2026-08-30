"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  completeGoogleProfileSchema,
  type CompleteGoogleProfileFormValues,
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
 * The mandatory step for a Google account with no phone number on file. Google's ID token
 * cannot supply one, so this mirrors the forced-password-change screen: the one route reachable
 * while holding the limited-scope profile_completion_required token.
 */
export default function CompleteProfilePage() {
  return (
    <RequireSession allowProfileCompletionScope>
      <CompleteProfileForm />
    </RequireSession>
  );
}

function CompleteProfileForm() {
  const router = useRouter();
  const [failure, setFailure] = useState<FormFailure | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<CompleteGoogleProfileFormValues>({
    resolver: zodResolver(completeGoogleProfileSchema),
    defaultValues: { contactNumber: "" },
  });

  async function onSubmit(values: CompleteGoogleProfileFormValues) {
    setFailure(null);

    try {
      const result = await api.auth.completeGoogleProfile({ contactNumber: values.contactNumber });

      // The response carries a full-access token, so the user continues without a second
      // sign-in. Storing it is what lifts the client-side restriction too.
      writeSession({
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        mustChangePassword: result.mustChangePassword,
        mustCompleteProfile: result.mustCompleteProfile,
      });

      // This is the true end of the Google registration journey: land on the home page signed
      // in, the same place a returning user's plain login goes, rather than straight into the
      // profile screen.
      router.replace("/");
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Add your phone number</h1>
      <Alert>
        Google does not share a phone number, so we need yours before you can use your account.
        Everything else is unavailable until you add it.
      </Alert>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <Field
          label="Phone number"
          type="tel"
          autoComplete="tel"
          errors={errors.contactNumber?.message ? [errors.contactNumber.message] : []}
          {...register("contactNumber")}
        />
        <Button type="submit" pending={isSubmitting}>
          Continue
        </Button>
      </form>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
    </section>
  );
}
