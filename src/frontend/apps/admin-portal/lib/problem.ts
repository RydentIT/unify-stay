import { ApiError, ApiNetworkError } from "@unify/api-client";

export interface FormFailure {
  message: string;
  fieldErrors: Record<string, string[]>;
}

/**
 * Turns whatever a call threw into something a form can render. Keeping this in one place
 * stops each page from inventing its own interpretation of an error.
 */
export function toFormFailure(error: unknown): FormFailure {
  if (error instanceof ApiError) {
    if (error.isNotImplemented) {
      return {
        message: "This feature is not built yet. The backend endpoint is still a scaffold stub.",
        fieldErrors: {},
      };
    }

    if (error.isRateLimited) {
      return { message: "Too many attempts. Please wait and try again.", fieldErrors: {} };
    }

    return {
      message: error.problem?.title ?? "The request could not be completed.",
      fieldErrors: error.fieldErrors,
    };
  }

  if (error instanceof ApiNetworkError) {
    return { message: "Could not reach the server. Check that the API is running.", fieldErrors: {} };
  }

  return { message: "Something went wrong.", fieldErrors: {} };
}
