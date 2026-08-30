import { type ApiClient } from "../client";
import {
  type ChangePasswordForcedRequest,
  type CompleteGoogleProfileRequest,
  type ForgotPasswordRequest,
  type GoogleLoginRequest,
  type GoogleRegisterRequest,
  type GoogleRegisterResponse,
  type LoginRequest,
  type LoginResponse,
  type LogoutRequest,
  type RegisterRequest,
  type RegisterResponse,
  type ResendVerificationRequest,
  type ResetPasswordRequest,
  type VerifyEmailRequest,
} from "../types";

type Options = { signal?: AbortSignal };

/** Calls under /api/auth. */
export function createAuthEndpoints(client: ApiClient) {
  return {
    register(request: RegisterRequest, options?: Options) {
      return client.post<RegisterResponse>("/api/auth/register", request, options);
    },

    registerWithGoogle(request: GoogleRegisterRequest, options?: Options) {
      return client.post<GoogleRegisterResponse>("/api/auth/google/register", request, options);
    },

    login(request: LoginRequest, options?: Options) {
      return client.post<LoginResponse>("/api/auth/login", request, options);
    },

    loginWithGoogle(request: GoogleLoginRequest, options?: Options) {
      return client.post<LoginResponse>("/api/auth/google/login", request, options);
    },

    logout(request: LogoutRequest = {}, options?: Options) {
      return client.post<void>("/api/auth/logout", request, options);
    },

    /**
     * The mandatory reset. Callable with a limited-scope token, and the only route that is.
     * Returns a full-access token so the user continues without signing in again.
     */
    changePasswordRequired(request: ChangePasswordForcedRequest, options?: Options) {
      return client.post<LoginResponse>("/api/auth/change-password-required", request, options);
    },

    /**
     * Mirrors changePasswordRequired for a Google account missing a phone number. Callable
     * with a limited-scope token, and the only route that is. Returns a full-access token.
     */
    completeGoogleProfile(request: CompleteGoogleProfileRequest, options?: Options) {
      return client.post<LoginResponse>("/api/auth/complete-profile", request, options);
    },

    /** Always resolves for a well-formed address, whether or not an account exists. */
    forgotPassword(request: ForgotPasswordRequest, options?: Options) {
      return client.post<void>("/api/auth/forgot-password", request, options);
    },

    resetPassword(request: ResetPasswordRequest, options?: Options) {
      return client.post<void>("/api/auth/reset-password", request, options);
    },

    verifyEmail(request: VerifyEmailRequest, options?: Options) {
      return client.post<void>("/api/auth/verify-email", request, options);
    },

    resendVerification(request: ResendVerificationRequest, options?: Options) {
      return client.post<void>("/api/auth/resend-verification", request, options);
    },
  };
}

export type AuthEndpoints = ReturnType<typeof createAuthEndpoints>;
