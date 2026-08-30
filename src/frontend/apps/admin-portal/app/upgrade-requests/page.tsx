"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { type UpgradeRequestResponse } from "@unify/api-client";
import { rejectUpgradeSchema, type RejectUpgradeFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Field, useAsyncData } from "@unify/ui";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { RequireSession } from "@/components/RequireSession";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

export default function UpgradeRequestsPage() {
  return (
    <RequireSession>
      <UpgradeRequestsList />
    </RequireSession>
  );
}

/** Defined outside the component so the reference is stable across renders. */
const fetchPendingRequests = () => api.settings.listUpgradeRequests("pending");

function UpgradeRequestsList() {
  // fetchPendingRequests is module-level, so the reference is already stable and needs no
  // useCallback wrapper.
  const { data, error, loading, reload } =
    useAsyncData<UpgradeRequestResponse[]>(fetchPendingRequests);

  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState<string | null>(null);

  const requests = data ?? [];
  const loadFailure = error ? toFormFailure(error) : null;

  async function approve(requestId: string) {
    setFailure(null);
    setNotice(null);

    try {
      await api.settings.approveUpgradeRequest(requestId);
      setNotice("Request approved. The Property Owner role has been granted.");
      await reload();
    } catch (caught) {
      setFailure(toFormFailure(caught));
    }
  }

  if (loading) {
    return <p className="text-sm">Loading pending requests...</p>;
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Pending upgrade requests</h1>

      {notice ? <Alert>{notice}</Alert> : null}
      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
      {loadFailure ? <Alert tone="error">{loadFailure.message}</Alert> : null}

      {requests.length === 0 ? (
        <p className="text-sm">There are no pending requests.</p>
      ) : (
        <ul className="flex flex-col gap-4">
          {requests.map((request) => (
            <li key={request.id} className="flex flex-col gap-2 border p-3">
              <div className="text-sm">
                <p>
                  <strong>{request.userName ?? "Unknown"}</strong> ({request.userEmail})
                </p>
                <p>Submitted {new Date(request.submittedAt).toLocaleString()}</p>
                <p>NIC: {request.nicNumber}</p>
                <p>Address: {request.address}</p>
                <p>Second phone: {request.phoneNumber2}</p>
                <p>Property info: {request.propertyInfo}</p>
              </div>

              {rejecting === request.id ? (
                <RejectForm
                  requestId={request.id}
                  onCancel={() => setRejecting(null)}
                  onRejected={async () => {
                    setRejecting(null);
                    setNotice("Request rejected. The applicant has been told why.");
                    await reload();
                  }}
                  onFailure={setFailure}
                />
              ) : (
                <div className="flex gap-2">
                  <Button type="button" onClick={() => void approve(request.id)}>
                    Approve
                  </Button>
                  <Button type="button" onClick={() => setRejecting(request.id)}>
                    Reject
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** SET-007: a reason is mandatory, so rejecting is a form rather than a single button. */
function RejectForm({
  requestId,
  onCancel,
  onRejected,
  onFailure,
}: {
  requestId: string;
  onCancel: () => void;
  onRejected: () => Promise<void>;
  onFailure: (failure: FormFailure) => void;
}) {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RejectUpgradeFormValues>({
    resolver: zodResolver(rejectUpgradeSchema),
    defaultValues: { reason: "" },
  });

  async function onSubmit(values: RejectUpgradeFormValues) {
    try {
      await api.settings.rejectUpgradeRequest(requestId, { reason: values.reason });
      await onRejected();
    } catch (caught) {
      onFailure(toFormFailure(caught));
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-2">
      <Field
        label="Reason for rejection"
        errors={errors.reason?.message ? [errors.reason.message] : []}
        {...register("reason")}
      />
      <div className="flex gap-2">
        <Button type="submit" pending={isSubmitting}>
          Confirm rejection
        </Button>
        <Button type="button" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
