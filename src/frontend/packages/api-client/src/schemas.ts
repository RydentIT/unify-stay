import { z } from "zod";

import type {
  ChangePasswordRequest,
  CompleteGoogleProfileRequest,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  RequestEmailChangeRequest,
  RequestPropertyOwnerUpgradeRequest,
  ResetPasswordRequest,
  SuspendUserRequest,
  UpdateProfileRequest,
} from "./types";

/**
 * Client-side validation schemas.
 *
 * These live in the api-client, next to the DTOs they validate, precisely so the rules are
 * declared once rather than re-typed in each form. They mirror the backend's FluentValidation
 * rules - the server remains the authority, and every one of these checks is enforced again
 * server-side; this layer exists to give the user immediate feedback, not to be trusted.
 *
 * Where a rule is duplicated (minimum password length, for instance) the two must be kept in
 * step deliberately: MIN_PASSWORD_LENGTH matches Auth:MinimumPasswordLength in appsettings.
 */
export const MIN_PASSWORD_LENGTH = 12;
export const MAX_PASSWORD_LENGTH = 256;
export const MAX_EMAIL_LENGTH = 320;
export const MAX_NAME_LENGTH = 120;

const emailField = z
  .string()
  .min(1, "Email is required.")
  .max(MAX_EMAIL_LENGTH, `Email must be at most ${MAX_EMAIL_LENGTH} characters.`)
  .email("Enter a valid email address.");

const newPasswordField = z
  .string()
  .min(MIN_PASSWORD_LENGTH, `Password must be at least ${MIN_PASSWORD_LENGTH} characters long.`)
  .max(MAX_PASSWORD_LENGTH, `Password must be at most ${MAX_PASSWORD_LENGTH} characters.`);

/**
 * Existing passwords are only checked for presence. Applying the new-password rules here would
 * lock out anyone whose password predates a rule change, and the server decides correctness.
 */
const existingPasswordField = z.string().min(1, "Password is required.");

export const MAX_CONTACT_NUMBER_LENGTH = 32;

/**
 * Mirrors the backend's ContactNumberPattern: an optional leading +, then digits, spaces,
 * hyphens or parentheses, 7-32 characters overall. Deliberately permissive - international
 * formats vary too much for anything stricter to be worth the false rejections it would cause.
 */
const CONTACT_NUMBER_PATTERN = /^\+?[0-9()\-\s]{7,32}$/;

const requiredContactNumberField = z
  .string()
  .min(1, "A phone number is required.")
  .max(MAX_CONTACT_NUMBER_LENGTH, `Phone number must be at most ${MAX_CONTACT_NUMBER_LENGTH} characters.`)
  .regex(CONTACT_NUMBER_PATTERN, "Enter a valid phone number.");

const nameFieldFactory = (label: string) => z.string().min(1, `${label} is required.`).max(MAX_NAME_LENGTH);

export const registerSchema = z
  .object({
    firstName: nameFieldFactory("First name"),
    lastName: nameFieldFactory("Last name"),
    email: emailField,
    password: newPasswordField,
    confirmPassword: z.string().min(1, "Please confirm your password."),
    contactNumber: requiredContactNumberField,
    // REG-006: consent is required before the account is created. Expressed as a refine
    // rather than z.literal so the message survives zod's version-to-version API changes.
    acceptedTerms: z.boolean().refine((accepted) => accepted, {
      message: "You must accept the terms of service.",
    }),
  })
  // A UI-only rule: the backend never sees confirmPassword.
  .refine((values) => values.password === values.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

export type RegisterFormValues = z.infer<typeof registerSchema>;

export const loginSchema = z.object({
  email: emailField,
  password: existingPasswordField,
  rememberMe: z.boolean().optional(),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

export const forgotPasswordSchema = z.object({ email: emailField });

export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z
  .object({
    newPassword: newPasswordField,
    confirmPassword: z.string().min(1, "Please confirm your password."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

export const changePasswordSchema = z
  .object({
    currentPassword: existingPasswordField,
    newPassword: newPasswordField,
    confirmPassword: z.string().min(1, "Please confirm your password."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  })
  // PRF-005, mirrored client-side so the user finds out before a round trip.
  .refine((values) => values.currentPassword !== values.newPassword, {
    message: "The new password must be different from your current password.",
    path: ["newPassword"],
  });

export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;

export const forcedPasswordChangeSchema = z
  .object({
    newPassword: newPasswordField,
    confirmPassword: z.string().min(1, "Please confirm your password."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

export type ForcedPasswordChangeFormValues = z.infer<typeof forcedPasswordChangeSchema>;

/** The one form a profile_completion_required token may submit. */
export const completeGoogleProfileSchema = z.object({
  contactNumber: requiredContactNumberField,
});

export type CompleteGoogleProfileFormValues = z.infer<typeof completeGoogleProfileSchema>;

export const updateProfileSchema = z.object({
  firstName: nameFieldFactory("First name"),
  lastName: nameFieldFactory("Last name"),
  contactNumber: z
    .string()
    .max(32, "Contact number must be at most 32 characters.")
    .optional()
    .or(z.literal("")),
});

export type UpdateProfileFormValues = z.infer<typeof updateProfileSchema>;

export const changeEmailSchema = z.object({
  newEmail: emailField,
  currentPassword: existingPasswordField,
});

export type ChangeEmailFormValues = z.infer<typeof changeEmailSchema>;

export const confirmWithPasswordSchema = z.object({
  password: existingPasswordField,
});

export type ConfirmWithPasswordFormValues = z.infer<typeof confirmWithPasswordSchema>;

export const MAX_NIC_LENGTH = 12;
export const MAX_ADDRESS_LENGTH = 255;
export const MAX_PROPERTY_INFO_LENGTH = 4000;

/** Sri Lankan NIC: old 9-digit + V/X, or new 12-digit. Mirrors the backend's NicPattern. */
const NIC_PATTERN = /^([0-9]{9}[VXvx]|[0-9]{12})$/;

const nicField = z
  .string()
  .min(1, "An NIC number is required.")
  .regex(NIC_PATTERN, "Enter a valid NIC number (old 9-digit or new 12-digit format).");

const addressField = z
  .string()
  .min(1, "An address is required.")
  .max(MAX_ADDRESS_LENGTH, `Address must be at most ${MAX_ADDRESS_LENGTH} characters.`);

const propertyInfoField = z
  .string()
  .min(1, "Property information is required.")
  .max(
    MAX_PROPERTY_INFO_LENGTH,
    `Property information must be at most ${MAX_PROPERTY_INFO_LENGTH} characters.`,
  );

/** Digits only, so formatting differences don't hide the same number. Mirrors the backend's NormalisePhone. */
const normalisePhoneDigits = (phone: string) => phone.replace(/\D/g, "");

/**
 * The account's existing contact number (collected at registration) is phone 1 and is never
 * re-collected here - this schema only covers the new fields, and requires phoneNumber2 to
 * differ from phone 1. currentContactNumber must be supplied at validation time, which is why
 * this is a factory rather than a static schema.
 */
export function upgradeRequestSchema(currentContactNumber: string) {
  return z.object({
    nicNumber: nicField,
    address: addressField,
    phoneNumber2: requiredContactNumberField.refine(
      (value) => normalisePhoneDigits(value) !== normalisePhoneDigits(currentContactNumber),
      "This must be different from your registered phone number.",
    ),
    propertyInfo: propertyInfoField,
  });
}

export type UpgradeRequestFormValues = z.infer<ReturnType<typeof upgradeRequestSchema>>;

export const rejectUpgradeSchema = z.object({
  // SET-007: a rejection without a reason is not a valid decision.
  reason: z
    .string()
    .min(1, "A rejection reason is required.")
    .max(1000, "Reason must be at most 1000 characters."),
});

export type RejectUpgradeFormValues = z.infer<typeof rejectUpgradeSchema>;

export const suspendUserSchema = z.object({
  // Part 4: a suspension without a reason is not a valid action - mirrors rejectUpgradeSchema.
  reason: z
    .string()
    .min(1, "A reason is required to suspend an account.")
    .max(255, "Reason must be at most 255 characters."),
});

export type SuspendUserFormValues = z.infer<typeof suspendUserSchema>;

/**
 * Compile-time proof that the form values still line up with the request DTOs. If a DTO gains a
 * required field, these stop compiling rather than failing silently at run time.
 */
type AssertAssignable<TSource, TTarget> = TSource extends TTarget ? true : never;

export type _RegisterMatches = AssertAssignable<
  Omit<RegisterFormValues, "confirmPassword">,
  RegisterRequest
>;
export type _LoginMatches = AssertAssignable<LoginFormValues, LoginRequest>;
export type _ForgotMatches = AssertAssignable<ForgotPasswordFormValues, ForgotPasswordRequest>;
export type _ResetMatches = AssertAssignable<
  Omit<ResetPasswordFormValues, "confirmPassword"> & { token: string },
  ResetPasswordRequest
>;
export type _ChangePasswordMatches = AssertAssignable<
  Omit<ChangePasswordFormValues, "confirmPassword">,
  ChangePasswordRequest
>;
export type _UpdateProfileMatches = AssertAssignable<UpdateProfileFormValues, UpdateProfileRequest>;
export type _ChangeEmailMatches = AssertAssignable<
  ChangeEmailFormValues,
  RequestEmailChangeRequest
>;
export type _CompleteGoogleProfileMatches = AssertAssignable<
  CompleteGoogleProfileFormValues,
  CompleteGoogleProfileRequest
>;
export type _UpgradeRequestMatches = AssertAssignable<
  UpgradeRequestFormValues,
  RequestPropertyOwnerUpgradeRequest
>;
export type _SuspendUserMatches = AssertAssignable<SuspendUserFormValues, SuspendUserRequest>;
