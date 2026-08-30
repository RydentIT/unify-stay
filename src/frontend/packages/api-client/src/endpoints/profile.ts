import { type ApiClient } from "../client";
import {
  type ChangePasswordRequest,
  type ConfirmEmailChangeRequest,
  type ProfileResponse,
  type RequestEmailChangeRequest,
  type UpdateProfileRequest,
} from "../types";

type Options = { signal?: AbortSignal };

/** Calls under /api/profile. All require a full-scope token except confirmEmailChange. */
export function createProfileEndpoints(client: ApiClient) {
  return {
    get(options?: Options) {
      return client.get<ProfileResponse>("/api/profile", options);
    },

    update(request: UpdateProfileRequest, options?: Options) {
      return client.put<ProfileResponse>("/api/profile", request, options);
    },

    uploadAvatar(file: File, options?: Options) {
      const form = new FormData();
      form.append("file", file);

      return client.postForm<ProfileResponse>("/api/profile/avatar", form, options);
    },

    changePassword(request: ChangePasswordRequest, options?: Options) {
      return client.post<void>("/api/profile/change-password", request, options);
    },

    requestEmailChange(request: RequestEmailChangeRequest, options?: Options) {
      return client.post<void>("/api/profile/change-email", request, options);
    },

    /** Followed from the new inbox, so it does not require a session. */
    confirmEmailChange(request: ConfirmEmailChangeRequest, options?: Options) {
      return client.post<void>("/api/profile/confirm-email-change", request, options);
    },
  };
}

export type ProfileEndpoints = ReturnType<typeof createProfileEndpoints>;
