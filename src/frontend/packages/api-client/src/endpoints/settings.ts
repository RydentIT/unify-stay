import { type ApiClient } from "../client";
import {
  type ConfirmWithPasswordRequest,
  type RejectUpgradeRequest,
  type RequestPropertyOwnerUpgradeRequest,
  type ResubmitUpgradeRequest,
  type UpgradeRequestResponse,
} from "../types";

type Options = { signal?: AbortSignal };

/** Calls under /api/settings and the admin routes under /api/admin. */
export function createSettingsEndpoints(client: ApiClient) {
  return {
    /** The latest request regardless of status, so a rejection and its reason are visible. */
    getMyUpgradeRequest(options?: Options) {
      return client.get<UpgradeRequestResponse | null>("/api/settings/upgrade-request", options);
    },

    requestUpgrade(request: RequestPropertyOwnerUpgradeRequest, options?: Options) {
      return client.post<UpgradeRequestResponse>("/api/settings/upgrade-request", request, options);
    },

    /** Permitted only after a rejection. */
    resubmitUpgrade(request: ResubmitUpgradeRequest, options?: Options) {
      return client.post<UpgradeRequestResponse>(
        "/api/settings/upgrade-request/resubmit",
        request,
        options,
      );
    },

    deactivateAccount(request: ConfirmWithPasswordRequest, options?: Options) {
      return client.post<void>("/api/settings/deactivate", request, options);
    },

    deleteAccount(request: ConfirmWithPasswordRequest, options?: Options) {
      return client.post<void>("/api/settings/delete", request, options);
    },

    // ---------- Admin ----------

    listUpgradeRequests(status: string = "pending", options?: Options) {
      return client.get<UpgradeRequestResponse[]>(
        `/api/admin/upgrade-requests?status=${encodeURIComponent(status)}`,
        options,
      );
    },

    approveUpgradeRequest(requestId: string, options?: Options) {
      return client.post<void>(`/api/admin/upgrade-requests/${requestId}/approve`, undefined, options);
    },

    rejectUpgradeRequest(requestId: string, request: RejectUpgradeRequest, options?: Options) {
      return client.post<void>(`/api/admin/upgrade-requests/${requestId}/reject`, request, options);
    },
  };
}

export type SettingsEndpoints = ReturnType<typeof createSettingsEndpoints>;
