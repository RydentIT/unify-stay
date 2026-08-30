"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { type UserDetail } from "@unify/api-client";
import { suspendUserSchema, type SuspendUserFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Field, useAsyncData } from "@unify/ui";
import { use, useCallback, useState } from "react";
import { useForm } from "react-hook-form";

import { RequireSession } from "@/components/RequireSession";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

export default function UserDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);

  return (
    <RequireSession>
      <UserDetailView userId={id} />
    </RequireSession>
  );
}

/**
 * Part 3 + Part 4: profile, current suspension, and property-owner-request history in one call,
 * with the Suspend/Lift Suspension action wired directly to this page - the single entry point
 * for admin user-management actions, per the spec's explicit "not a separate disconnected screen".
 */
function UserDetailView({ userId }: { userId: string }) {
  const fetchDetail = useCallback(() => api.adminUsers.getUserDetail(userId), [userId]);
  const { data: user, error, loading, reload } = useAsyncData<UserDetail>(fetchDetail);

  const [notice, setNotice] = useState<string | null>(null);
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [suspending, setSuspending] = useState(false);
  const [liftPending, setLiftPending] = useState(false);

  const loadFailure = error ? toFormFailure(error) : null;

  async function liftSuspension() {
    setFailure(null);
    setNotice(null);
    setLiftPending(true);

    try {
      await api.adminUsers.liftSuspension(userId);
      setNotice("Suspension lifted. The user can sign in again.");
      await reload();
    } catch (caught) {
      setFailure(toFormFailure(caught));
    } finally {
      setLiftPending(false);
    }
  }

  if (loading) {
    return <p className="text-sm">Loading user...</p>;
  }

  if (loadFailure || !user) {
    return <Alert tone="error">{loadFailure?.message ?? "User not found."}</Alert>;
  }

  const isSuspended = user.status === "Suspended";

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">
        {user.firstName} {user.lastName}
      </h1>

      <div className="flex flex-col gap-1 text-sm">
        <p>Email: {user.email}</p>
        <p>Contact number: {user.contactNumber ?? "-"}</p>
        <p>Roles: {user.roles.join(", ") || "-"}</p>
        <p>Status: {user.status}</p>
        <p>Email verified: {user.emailVerified ? "Yes" : "No"}</p>
        <p>Joined: {new Date(user.createdAt).toLocaleString()}</p>
      </div>

      {notice ? <Alert>{notice}</Alert> : null}
      {failure ? <Alert tone="error">{failure.message}</Alert> : null}

      <div className="flex flex-col gap-3 border-t pt-4">
        <h2 className="text-lg font-medium">Suspension</h2>

        {user.activeSuspension ? (
          <div className="flex flex-col gap-2 text-sm">
            <p>
              Suspended {new Date(user.activeSuspension.suspendedAt).toLocaleString()} - reason:{" "}
              {user.activeSuspension.reason}
            </p>
            <Button type="button" onClick={() => void liftSuspension()} pending={liftPending}>
              Lift suspension
            </Button>
          </div>
        ) : suspending ? (
          <SuspendForm
            userId={userId}
            onCancel={() => setSuspending(false)}
            onSuspended={async () => {
              setSuspending(false);
              setNotice("User suspended. All of their active sessions have been revoked.");
              await reload();
            }}
            onFailure={setFailure}
          />
        ) : (
          <Button type="button" onClick={() => setSuspending(true)} disabled={isSuspended}>
            Suspend user
          </Button>
        )}
      </div>

      <div className="flex flex-col gap-2 border-t pt-4">
        <h2 className="text-lg font-medium">Property Owner request history</h2>

        {user.upgradeRequests.length === 0 ? (
          <p className="text-sm">No requests submitted.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {user.upgradeRequests.map((request) => (
              <li key={request.id} className="border p-2">
                <p>
                  {request.status} - submitted {new Date(request.submittedAt).toLocaleDateString()}
                </p>
                <p>NIC: {request.nicNumber}</p>
                <p>Address: {request.address}</p>
                {request.rejectionReason ? <p>Rejection reason: {request.rejectionReason}</p> : null}
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

/** Part 4: suspending requires a reason before it can be confirmed. */
function SuspendForm({
  userId,
  onCancel,
  onSuspended,
  onFailure,
}: {
  userId: string;
  onCancel: () => void;
  onSuspended: () => Promise<void>;
  onFailure: (failure: FormFailure) => void;
}) {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SuspendUserFormValues>({
    resolver: zodResolver(suspendUserSchema),
    defaultValues: { reason: "" },
  });

  async function onSubmit(values: SuspendUserFormValues) {
    try {
      await api.adminUsers.suspendUser(userId, { reason: values.reason });
      await onSuspended();
    } catch (caught) {
      onFailure(toFormFailure(caught));
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-2">
      <Field
        label="Reason for suspension"
        errors={errors.reason?.message ? [errors.reason.message] : []}
        {...register("reason")}
      />
      <div className="flex gap-2">
        <Button type="submit" pending={isSubmitting}>
          Confirm suspension
        </Button>
        <Button type="button" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
