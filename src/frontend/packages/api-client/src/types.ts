/**
 * Request and response shapes for the backend API.
 *
 * HAND-WRITTEN FOR NOW. The API exposes an OpenAPI document at /openapi/v1.json in
 * Development, and the intent is to replace this file with generated output. That is why every
 * type mirrors the server contract exactly rather than being reshaped for convenience - the
 * swap should be a delete-and-regenerate, not a rewrite of call sites.
 */

export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy";

export interface HealthResponse {
  status: HealthStatus;
  checkedAtUtc: string;
  components: Record<string, HealthStatus>;
}

export type RoleName = "Student" | "PropertyOwner" | "Admin" | "Staff";

export type UserStatus = "Active" | "Deactivated" | "Deleted" | "Suspended";

export type UpgradeRequestStatus = "Pending" | "Approved" | "Rejected";

// ---------------------------------------------------------------- Auth

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  contactNumber: string;
  acceptedTerms: boolean;
}

export interface RegisterResponse {
  userId: string;
  email: string;
  emailVerificationRequired: boolean;
}

export interface GoogleRegisterRequest {
  idToken: string;
  acceptedTerms: boolean;
}

export interface GoogleRegisterResponse {
  userId: string;
  email: string;
  /** True when an existing account gained a Google provider rather than a new account being made. */
  linkedToExistingAccount: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
  /** Extends the refresh token lifetime only; the access token lifetime is unchanged. */
  rememberMe?: boolean;
}

export interface GoogleLoginRequest {
  idToken: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  /**
   * When true the access token is a limited-scope credential that authorises nothing except
   * the change-password call, and no refresh token is issued. The UI must route straight to
   * the mandatory change-password screen.
   */
  mustChangePassword: boolean;
  /**
   * When true the access token is a limited-scope credential that authorises nothing except
   * the complete-profile call (Google accounts have no phone number from the ID token). Mutually
   * exclusive with mustChangePassword. The UI must route straight to the complete-profile screen.
   */
  mustCompleteProfile: boolean;
  refreshToken: string | null;
  refreshTokenExpiresAt: string | null;
  roles: RoleName[];
}

export interface LogoutRequest {
  refreshToken?: string | null;
}

export interface ChangePasswordForcedRequest {
  newPassword: string;
}

/** Supplies the phone number a Google account could not give at signup. */
export interface CompleteGoogleProfileRequest {
  contactNumber: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface VerifyEmailRequest {
  token: string;
}

export interface ResendVerificationRequest {
  email: string;
}

// ---------------------------------------------------------------- Profile

export interface ProfileResponse {
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  avatarUrl: string | null;
  contactNumber: string | null;
  emailVerified: boolean;
  status: UserStatus;
  /** Address awaiting confirmation; the account still uses `email` until then. */
  pendingEmail: string | null;
  roles: RoleName[];
  hasPassword: boolean;
  linkedProviders: string[];
  createdAt: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  avatarUrl?: string | null;
  contactNumber?: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface RequestEmailChangeRequest {
  newEmail: string;
  currentPassword: string;
}

export interface ConfirmEmailChangeRequest {
  token: string;
}

// ---------------------------------------------------------------- Settings

export interface UpgradeRequestResponse {
  id: string;
  userId: string;
  userName: string | null;
  userEmail: string | null;
  nicNumber: string;
  address: string;
  phoneNumber2: string;
  propertyInfo: string;
  status: UpgradeRequestStatus;
  rejectionReason: string | null;
  submittedAt: string;
  decidedAt: string | null;
}

/**
 * SET-005 / BR-SET-001. No document upload - an admin reviews these structured fields manually.
 * phoneNumber2 must differ from the account's existing contact number (phone 1); the server is
 * the source of truth for that check, but the UI validates it too so the error surfaces inline.
 */
export interface RequestPropertyOwnerUpgradeRequest {
  nicNumber: string;
  address: string;
  phoneNumber2: string;
  propertyInfo: string;
}

export type ResubmitUpgradeRequest = RequestPropertyOwnerUpgradeRequest;

export interface RejectUpgradeRequest {
  reason: string;
}

export interface ConfirmWithPasswordRequest {
  password: string;
}

// ---------------------------------------------------------------- Admin: user directory & suspension

export interface UserSummary {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  roles: RoleName[];
  status: UserStatus;
  createdAt: string;
}

export interface UserSummaryPage {
  items: UserSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type SuspensionStatus = "Active" | "Lifted";

export interface Suspension {
  id: string;
  reason: string;
  suspendedBy: string;
  suspendedAt: string;
  status: SuspensionStatus;
  liftedBy: string | null;
  liftedAt: string | null;
}

/** Part 3: profile + current suspension (if any) + upgrade-request history in one call. */
export interface UserDetail {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  avatarUrl: string | null;
  contactNumber: string | null;
  roles: RoleName[];
  status: UserStatus;
  emailVerified: boolean;
  createdAt: string;
  activeSuspension: Suspension | null;
  upgradeRequests: UpgradeRequestResponse[];
}

export interface SuspendUserRequest {
  reason: string;
}

// ---------------------------------------------------------------- Errors

/** RFC 7807 problem document, as produced by the API's exception middleware. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Present on validation failures: field name -> messages. */
  errors?: Record<string, string[]>;
  /** Stable machine-readable code, e.g. "auth.login.invalid_credentials". */
  code?: string;
  traceId?: string;
}
