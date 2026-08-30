"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  confirmWithPasswordSchema,
  upgradeRequestSchema,
  type ConfirmWithPasswordFormValues,
  type UpgradeRequestFormValues,
} from "@unify/api-client/schemas";
import { type ProfileResponse, type UpgradeRequestResponse } from "@unify/api-client";
import { Alert, Button, Field, useAsyncData } from "@unify/ui";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { RequireSession } from "@/components/RequireSession";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";
import { clearSession } from "@/lib/session";

export default function SettingsPage() {
  return (
    <RequireSession>
      <SettingsView />
    </RequireSession>
  );
}

function SettingsView() {
  return (
    <section className="flex flex-col gap-6">
      <h1 className="text-xl font-semibold">Settings</h1>
      <UpgradeRequestSection />
      <AccountLifecycleSection />
    </section>
  );
}

/** Module-level so the reference is stable across renders. */
const fetchMyUpgradeRequest = () => api.settings.getMyUpgradeRequest();
const fetchProfileForUpgrade = () => api.profile.get();

/**
 * SET-005, SET-008: submit, track status, and resubmit after a rejection. No document upload -
 * an admin reviews the structured fields (NIC, address, second phone, property info) manually.
 * Phone 1 is the account's existing contact number from registration and is never re-collected;
 * only phone 2 is entered here, and it must differ from phone 1.
 */
function UpgradeRequestSection() {
  const {
    data: loaded,
    error: loadError,
    loading,
  } = useAsyncData<UpgradeRequestResponse | null>(fetchMyUpgradeRequest);

  const { data: profile } = useAsyncData<ProfileResponse>(fetchProfileForUpgrade);

  // Derived rather than mirrored: a locally submitted request wins, otherwise show what loaded.
  const [submitted, setSubmitted] = useState<UpgradeRequestResponse | null>(null);
  const request = submitted ?? loaded ?? null;

  const [failure, setFailure] = useState<FormFailure | null>(null);

  const wasRejected = request?.status === "Rejected";
  const isActive = request?.status === "Pending" || request?.status === "Approved";

  const currentContactNumber = profile?.contactNumber ?? "";

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<UpgradeRequestFormValues>({
    resolver: zodResolver(upgradeRequestSchema(currentContactNumber)),
    defaultValues: { nicNumber: "", address: "", phoneNumber2: "", propertyInfo: "" },
  });

  async function onSubmit(values: UpgradeRequestFormValues) {
    setFailure(null);

    try {
      // SET-008: only a rejected prior request may be resubmitted; the server enforces it too.
      const result = wasRejected
        ? await api.settings.resubmitUpgrade(values)
        : await api.settings.requestUpgrade(values);

      setSubmitted(result);
      reset();
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  if (loading) {
    return <p className="text-sm">Loading your upgrade request...</p>;
  }

  return (
    <div className="flex flex-col gap-3 border-t pt-4">
      <h2 className="text-lg font-medium">Become a Property Owner</h2>

      {request ? (
        <div className="flex flex-col gap-2 text-sm">
          <p>
            Status: <strong>{request.status}</strong> (submitted{" "}
            {new Date(request.submittedAt).toLocaleDateString()})
          </p>

          {/* SET-005: the applicant keeps their current access while a request is pending. */}
          {request.status === "Pending" ? (
            <Alert>
              Your request is being reviewed. You keep your current role and access in the
              meantime.
            </Alert>
          ) : null}

          {request.status === "Approved" ? (
            <Alert>You are now a Property Owner.</Alert>
          ) : null}

          {/* SET-007: the reason is shown so the applicant knows what to fix. */}
          {wasRejected ? (
            <Alert tone="error">Not approved: {request.rejectionReason}</Alert>
          ) : null}
        </div>
      ) : (
        <p className="text-sm">
          Fill in the details below to request a Property Owner upgrade. An admin will verify
          them manually.
        </p>
      )}

      {!isActive ? (
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
          <Field
            label="NIC number"
            errors={errors.nicNumber?.message ? [errors.nicNumber.message] : []}
            {...register("nicNumber")}
          />
          <Field
            label="Address"
            errors={errors.address?.message ? [errors.address.message] : []}
            {...register("address")}
          />
          <Field
            label="Second phone number"
            type="tel"
            errors={errors.phoneNumber2?.message ? [errors.phoneNumber2.message] : []}
            {...register("phoneNumber2")}
          />
          <div className="flex flex-col gap-1">
            <label htmlFor="propertyInfo" className="text-sm">
              Property information
            </label>
            <textarea
              id="propertyInfo"
              rows={4}
              aria-invalid={Boolean(errors.propertyInfo)}
              className="rounded border border-gray-400 px-2 py-1 text-sm"
              {...register("propertyInfo")}
            />
            {errors.propertyInfo?.message ? (
              <p className="text-sm text-red-700">{errors.propertyInfo.message}</p>
            ) : null}
          </div>

          <Button type="submit" pending={isSubmitting}>
            {wasRejected ? "Resubmit request" : "Submit request"}
          </Button>
        </form>
      ) : null}

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
      {loadError ? <Alert tone="error">{toFormFailure(loadError).message}</Alert> : null}
    </div>
  );
}

/** SET-012 / SET-013: both actions require the password and end every session. */
function AccountLifecycleSection() {
  const router = useRouter();
  const [mode, setMode] = useState<"deactivate" | "delete" | null>(null);
  const [failure, setFailure] = useState<FormFailure | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ConfirmWithPasswordFormValues>({
    resolver: zodResolver(confirmWithPasswordSchema),
    defaultValues: { password: "" },
  });

  async function onSubmit(values: ConfirmWithPasswordFormValues) {
    if (!mode) {
      return;
    }

    setFailure(null);

    try {
      if (mode === "deactivate") {
        await api.settings.deactivateAccount({ password: values.password });
      } else {
        await api.settings.deleteAccount({ password: values.password });
      }

      // Every session was revoked server-side; drop the local copy and send them out.
      clearSession();
      router.replace("/login");
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  return (
    <div className="flex flex-col gap-3 border-t pt-4">
      <h2 className="text-lg font-medium">Account</h2>

      {mode === null ? (
        <div className="flex flex-col gap-2">
          <Button type="button" onClick={() => setMode("deactivate")}>
            Deactivate my account
          </Button>
          <Button type="button" onClick={() => setMode("delete")}>
            Delete my account
          </Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
          <Alert tone={mode === "delete" ? "error" : "info"}>
            {mode === "delete"
              ? "Deleting is permanent and cannot be undone. Your personal details are removed."
              : "Deactivating signs you out everywhere. You can reactivate by signing in again."}
          </Alert>

          <Field
            label="Confirm your password"
            type="password"
            autoComplete="current-password"
            errors={errors.password?.message ? [errors.password.message] : []}
            {...register("password")}
          />

          <div className="flex gap-2">
            <Button type="submit" pending={isSubmitting}>
              {mode === "delete" ? "Permanently delete" : "Deactivate"}
            </Button>
            <Button
              type="button"
              onClick={() => {
                setMode(null);
                setFailure(null);
                reset();
              }}
            >
              Cancel
            </Button>
          </div>
        </form>
      )}

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
    </div>
  );
}
