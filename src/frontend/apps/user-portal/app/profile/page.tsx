"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { updateProfileSchema, type UpdateProfileFormValues } from "@unify/api-client/schemas";
import { type ProfileResponse } from "@unify/api-client";
import { Alert, Button, Field, useAsyncData } from "@unify/ui";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";

import { RequireSession } from "@/components/RequireSession";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

export default function ProfilePage() {
  return (
    <RequireSession>
      <ProfileView />
    </RequireSession>
  );
}

/** Module-level so the reference is stable across renders. */
const fetchProfile = () => api.profile.get();

function ProfileView() {
  const { data: loaded, error: loadError, loading } = useAsyncData<ProfileResponse>(fetchProfile);

  // The fetched profile is not mirrored into state. Only a locally-applied update is held, and
  // the value shown is derived - so there is no effect copying server data into a useState,
  // which is exactly the cascade the set-state-in-effect rule guards against.
  const [updated, setUpdated] = useState<ProfileResponse | null>(null);
  const profile = updated ?? loaded ?? null;

  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [saved, setSaved] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<UpdateProfileFormValues>({
    resolver: zodResolver(updateProfileSchema),
    defaultValues: { firstName: "", lastName: "", contactNumber: "" },
  });

  // Seed the form once the profile arrives. reset() is react-hook-form's own store, not React
  // component state, so this is not a state cascade.
  useEffect(() => {
    if (loaded) {
      reset({
        firstName: loaded.firstName,
        lastName: loaded.lastName,
        contactNumber: loaded.contactNumber ?? "",
      });
    }
  }, [loaded, reset]);

  async function onSubmit(values: UpdateProfileFormValues) {
    setFailure(null);
    setSaved(false);

    try {
      // BR-PRF-005: only these fields are sent. Role and verification status are not editable.
      const savedProfile = await api.profile.update({
        firstName: values.firstName,
        lastName: values.lastName,
        contactNumber: values.contactNumber || null,
        avatarUrl: profile?.avatarUrl ?? null,
      });

      setUpdated(savedProfile);
      setSaved(true);
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  async function onAvatarSelected(file: File) {
    setFailure(null);

    try {
      setUpdated(await api.profile.uploadAvatar(file));
      setSaved(true);
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  if (loading) {
    return <p className="text-sm">Loading your profile...</p>;
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Profile</h1>

      {profile ? (
        <dl className="grid grid-cols-2 gap-1 text-sm">
          <dt className="font-medium">Email</dt>
          <dd>
            {profile.email} {profile.emailVerified ? "(verified)" : "(unverified)"}
          </dd>

          <dt className="font-medium">Roles</dt>
          <dd>{profile.roles.join(", ")}</dd>

          <dt className="font-medium">Sign-in methods</dt>
          <dd>{profile.linkedProviders.join(", ")}</dd>

          <dt className="font-medium">Status</dt>
          <dd>{profile.status}</dd>
        </dl>
      ) : null}

      {/* PRF-009: the account keeps its current address until the new one is confirmed. */}
      {profile?.pendingEmail ? (
        <Alert>
          A change to <strong>{profile.pendingEmail}</strong> is awaiting confirmation. Your
          current address stays in use until then.
        </Alert>
      ) : null}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <Field
          label="First name"
          autoComplete="given-name"
          errors={errors.firstName?.message ? [errors.firstName.message] : []}
          {...register("firstName")}
        />
        <Field
          label="Last name"
          autoComplete="family-name"
          errors={errors.lastName?.message ? [errors.lastName.message] : []}
          {...register("lastName")}
        />
        <Field
          label="Contact number"
          autoComplete="tel"
          errors={errors.contactNumber?.message ? [errors.contactNumber.message] : []}
          {...register("contactNumber")}
        />
        <Button type="submit" pending={isSubmitting}>
          Save changes
        </Button>
      </form>

      <div className="flex flex-col gap-2 border-t pt-3">
        <h2 className="text-lg font-medium">Avatar</h2>
        {profile?.avatarUrl ? <p className="text-sm">Current: {profile.avatarUrl}</p> : null}
        <input
          ref={fileInput}
          type="file"
          accept="image/png,image/jpeg,image/webp"
          aria-label="Avatar image"
          className="text-sm"
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) {
              void onAvatarSelected(file);
            }
          }}
        />
      </div>

      {saved ? <Alert>Your profile has been updated.</Alert> : null}
      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
      {loadError ? <Alert tone="error">{toFormFailure(loadError).message}</Alert> : null}

      <div className="flex flex-col gap-1 border-t pt-3 text-sm">
        <Link href="/profile/change-password">Change password</Link>
        <Link href="/profile/change-email">Change email address</Link>
        <Link href="/settings">Settings</Link>
      </div>
    </section>
  );
}
