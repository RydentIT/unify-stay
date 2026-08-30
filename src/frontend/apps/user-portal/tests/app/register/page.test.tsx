import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const registerMock = vi.fn();

// The page must reach the backend only through @unify/api-client, so that is the seam replaced
// here. A page calling fetch directly could not be tested this way.
vi.mock("@/lib/api", () => ({
  api: { auth: { register: (...args: unknown[]) => registerMock(...args) } },
  apiBaseUrl: "http://api.test",
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const { default: RegisterPage } = await import("@/app/register/page");

describe("RegisterPage", () => {
  beforeEach(() => {
    registerMock.mockReset();
    window.localStorage.clear();
  });

  async function fillValidForm() {
    await userEvent.type(screen.getByLabelText("First name"), "Test");
    await userEvent.type(screen.getByLabelText("Last name"), "Student");
    await userEvent.type(screen.getByLabelText("Email"), "student@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "correct-horse-battery");
    await userEvent.type(screen.getByLabelText("Confirm password"), "correct-horse-battery");
    await userEvent.type(screen.getByLabelText("Phone number"), "+94712345678");
    await userEvent.click(screen.getByLabelText("I accept the terms of service"));
  }

  it("blocks submission and reports every invalid field", async () => {
    render(<RegisterPage />);

    await userEvent.click(screen.getByRole("button", { name: "Register" }));

    // An empty field reports "required"; the format rule only applies once something is typed.
    expect(await screen.findByText("First name is required.")).toBeInTheDocument();
    expect(screen.getByText("Last name is required.")).toBeInTheDocument();
    expect(screen.getByText("Email is required.")).toBeInTheDocument();
    expect(screen.getByText("A phone number is required.")).toBeInTheDocument();
    expect(screen.getByText("You must accept the terms of service.")).toBeInTheDocument();

    // Nothing was sent: client-side validation short-circuits the request.
    expect(registerMock).not.toHaveBeenCalled();
  });

  it("reports a malformed email once one is typed", async () => {
    render(<RegisterPage />);

    await userEvent.type(screen.getByLabelText("Email"), "not-an-email");
    await userEvent.click(screen.getByRole("button", { name: "Register" }));

    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
    expect(registerMock).not.toHaveBeenCalled();
  });

  /** REG-006 at the form level. */
  it("refuses to submit without accepting the terms", async () => {
    render(<RegisterPage />);

    await userEvent.type(screen.getByLabelText("First name"), "Test");
    await userEvent.type(screen.getByLabelText("Last name"), "Student");
    await userEvent.type(screen.getByLabelText("Email"), "student@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "correct-horse-battery");
    await userEvent.type(screen.getByLabelText("Confirm password"), "correct-horse-battery");
    await userEvent.type(screen.getByLabelText("Phone number"), "+94712345678");

    await userEvent.click(screen.getByRole("button", { name: "Register" }));

    expect(await screen.findByText("You must accept the terms of service.")).toBeInTheDocument();
    expect(registerMock).not.toHaveBeenCalled();
  });

  it("reports mismatched passwords without contacting the server", async () => {
    render(<RegisterPage />);

    await userEvent.type(screen.getByLabelText("First name"), "Test");
    await userEvent.type(screen.getByLabelText("Last name"), "Student");
    await userEvent.type(screen.getByLabelText("Email"), "student@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "correct-horse-battery");
    await userEvent.type(screen.getByLabelText("Confirm password"), "something-different-here");
    await userEvent.type(screen.getByLabelText("Phone number"), "+94712345678");
    await userEvent.click(screen.getByLabelText("I accept the terms of service"));

    await userEvent.click(screen.getByRole("button", { name: "Register" }));

    expect(await screen.findByText("Passwords do not match.")).toBeInTheDocument();
    expect(registerMock).not.toHaveBeenCalled();
  });

  it("submits the DTO without the UI-only confirmation field", async () => {
    registerMock.mockResolvedValue({
      userId: "user-1",
      email: "student@example.com",
      emailVerificationRequired: true,
    });

    render(<RegisterPage />);
    await fillValidForm();
    await userEvent.click(screen.getByRole("button", { name: "Register" }));

    await waitFor(() =>
      expect(registerMock).toHaveBeenCalledWith({
        firstName: "Test",
        lastName: "Student",
        email: "student@example.com",
        password: "correct-horse-battery",
        contactNumber: "+94712345678",
        acceptedTerms: true,
      }),
    );
  });

  /** EVR-001: registration always lands on the "check your email" state. */
  it("shows the check-your-email state after a successful registration", async () => {
    registerMock.mockResolvedValue({
      userId: "user-1",
      email: "student@example.com",
      emailVerificationRequired: true,
    });

    render(<RegisterPage />);
    await fillValidForm();
    await userEvent.click(screen.getByRole("button", { name: "Register" }));

    expect(await screen.findByRole("heading", { name: "Check your email" })).toBeInTheDocument();
    expect(screen.getByText("student@example.com")).toBeInTheDocument();
  });

  /** REG-003 surfaces as a 409 and must be shown, not swallowed. */
  it("surfaces a duplicate-address conflict from the server", async () => {
    const { ApiError } = await import("@unify/api-client");

    registerMock.mockRejectedValue(
      new ApiError(409, "conflict", {
        title: "An account with this email address already exists.",
        code: "auth.register.email_in_use",
      }),
    );

    render(<RegisterPage />);
    await fillValidForm();
    await userEvent.click(screen.getByRole("button", { name: "Register" }));

    expect(
      await screen.findByText("An account with this email address already exists."),
    ).toBeInTheDocument();
  });
});
