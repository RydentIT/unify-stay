import { ApiError, ApiNetworkError } from "@unify/api-client";

export interface FormFailure {
  message: string;
  fieldErrors: Record<string, string[]>;
  code?: string;
}

/**
 * Turns whatever a call threw into something a form can render. Keeping this in one place stops
 * each page from inventing its own interpretation of an error.
 */
export function toFormFailure(error: unknown): FormFailure {
  if (error instanceof ApiError) {
    if (error.isRateLimited) {
      return {
        message: "Too many attempts. Please wait a few minutes and try again.",
        fieldErrors: {},
        code: error.code,
      };
    }

    return {
      message: error.problem?.title ?? "The request could not be completed.",
      fieldErrors: error.fieldErrors,
      code: error.code,
    };
  }

  if (error instanceof ApiNetworkError) {
    return {
      message: "Could not reach the server. Check that the API is running.",
      fieldErrors: {},
    };
  }

  return { message: "Something went wrong. Please try again.", fieldErrors: {} };
}

/**
 * Merges server-side field errors into a react-hook-form error setter.
 *
 * The backend returns PascalCase field names (matching the C# DTOs) while the form uses
 * camelCase, so the key is lower-cased on the first character before matching.
 */
export function applyFieldErrors(
  failure: FormFailure,
  setError: (field: string, error: { type: string; message: string }) => void,
  knownFields: readonly string[],
): void {
  for (const [field, messages] of Object.entries(failure.fieldErrors)) {
    const camel = field.charAt(0).toLowerCase() + field.slice(1);

    if (knownFields.includes(camel) && messages[0]) {
      setError(camel, { type: "server", message: messages[0] });
    }
  }
}
