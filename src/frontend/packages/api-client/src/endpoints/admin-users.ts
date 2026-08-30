import { type ApiClient } from "../client";
import { type SuspendUserRequest, type UserDetail, type UserSummaryPage } from "../types";

type Options = { signal?: AbortSignal };

export interface UserDirectoryQuery {
  search?: string;
  role?: string;
  status?: string;
  page?: number;
  pageSize?: number;
  sortBy?: "newest" | "oldest";
}

/** Calls under /api/admin/users - the admin user directory (Part 3) and suspend/lift (Part 4). */
export function createAdminUserEndpoints(client: ApiClient) {
  return {
    getUsers(query: UserDirectoryQuery = {}, options?: Options) {
      const params = new URLSearchParams();

      if (query.search) params.set("search", query.search);
      if (query.role) params.set("role", query.role);
      if (query.status) params.set("status", query.status);
      if (query.page) params.set("page", String(query.page));
      if (query.pageSize) params.set("pageSize", String(query.pageSize));
      if (query.sortBy) params.set("sortBy", query.sortBy);

      const queryString = params.toString();

      return client.get<UserSummaryPage>(
        `/api/admin/users${queryString ? `?${queryString}` : ""}`,
        options,
      );
    },

    getUserDetail(userId: string, options?: Options) {
      return client.get<UserDetail>(`/api/admin/users/${userId}`, options);
    },

    suspendUser(userId: string, request: SuspendUserRequest, options?: Options) {
      return client.post<void>(`/api/admin/users/${userId}/suspend`, request, options);
    },

    liftSuspension(userId: string, options?: Options) {
      return client.post<void>(`/api/admin/users/${userId}/lift-suspension`, undefined, options);
    },
  };
}

export type AdminUserEndpoints = ReturnType<typeof createAdminUserEndpoints>;
