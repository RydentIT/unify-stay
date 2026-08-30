import { describe, expect, it } from "vitest";

import {
  changePasswordSchema,
  loginSchema,
  registerSchema,
  rejectUpgradeSchema,
  resetPasswordSchema,
  suspendUserSchema,
  upgradeRequestSchema,
  MIN_PASSWORD_LENGTH,
} from "../src/schemas";

/**
 * These schemas are the client-side mirror of the backend's FluentValidation rules. The tests
 * pin the rules that carry a business requirement, so a drift from the server shows up here
 * rather than as a confusing 400 in the UI.
 */
describe("registerSchema", () => {
  const valid = {
    firstName: "Test",
    lastName: "Student",
    email: "student@example.com",
    password: "correct-horse-battery",
    confirmPassword: "correct-horse-battery",
    contactNumber: "+94 71 234 5678",
    acceptedTerms: true as const,
  };

  it("accepts a well-formed registration", () => {
    expect(registerSchema.safeParse(valid).success).toBe(true);
  });

  it.each(["", "   ", "no-at-sign", "missing@tld"])("rejects the malformed email %s", (email) => {
    expect(registerSchema.safeParse({ ...valid, email }).success).toBe(false);
  });

  it("rejects a password below the minimum length", () => {
    const short = "a".repeat(MIN_PASSWORD_LENGTH - 1);
    const result = registerSchema.safeParse({ ...valid, password: short, confirmPassword: short });

    expect(result.success).toBe(false);
  });

  it("rejects mismatched passwords", () => {
    const result = registerSchema.safeParse({ ...valid, confirmPassword: "something-else-here" });

    expect(result.success).toBe(false);
    expect(result.error?.issues.some((issue) => issue.path.includes("confirmPassword"))).toBe(true);
  });

  /** REG-006. */
  it("requires the terms to be accepted", () => {
    const result = registerSchema.safeParse({ ...valid, acceptedTerms: false });

    expect(result.success).toBe(false);
    expect(result.error?.issues.some((issue) => issue.path.includes("acceptedTerms"))).toBe(true);
  });

  it("requires a phone number", () => {
    const result = registerSchema.safeParse({ ...valid, contactNumber: "" });

    expect(result.success).toBe(false);
    expect(result.error?.issues.some((issue) => issue.path.includes("contactNumber"))).toBe(true);
  });

  it.each(["not-a-phone-number", "12345", "abc-defg-hijk"])(
    "rejects the malformed phone number %s",
    (contactNumber) => {
      expect(registerSchema.safeParse({ ...valid, contactNumber }).success).toBe(false);
    },
  );

  it.each(["+94712345678", "(94) 71-234-5678", "0712345678"])(
    "accepts the well-formed phone number %s",
    (contactNumber) => {
      expect(registerSchema.safeParse({ ...valid, contactNumber }).success).toBe(true);
    },
  );
});

describe("loginSchema", () => {
  it("only checks that a password is present, not its shape", () => {
    // An existing password may predate a rule change; the server decides correctness.
    const result = loginSchema.safeParse({ email: "a@b.com", password: "short" });

    expect(result.success).toBe(true);
  });

  it("rejects an empty password", () => {
    expect(loginSchema.safeParse({ email: "a@b.com", password: "" }).success).toBe(false);
  });
});

describe("changePasswordSchema", () => {
  /** PRF-005, mirrored client-side. */
  it("rejects a new password identical to the current one", () => {
    const same = "correct-horse-battery";
    const result = changePasswordSchema.safeParse({
      currentPassword: same,
      newPassword: same,
      confirmPassword: same,
    });

    expect(result.success).toBe(false);
    expect(result.error?.issues.some((issue) => issue.path.includes("newPassword"))).toBe(true);
  });

  it("accepts a genuinely new password", () => {
    const result = changePasswordSchema.safeParse({
      currentPassword: "old-password-value",
      newPassword: "brand-new-password-value",
      confirmPassword: "brand-new-password-value",
    });

    expect(result.success).toBe(true);
  });
});

describe("resetPasswordSchema", () => {
  it("enforces the minimum length and confirmation", () => {
    expect(
      resetPasswordSchema.safeParse({ newPassword: "short", confirmPassword: "short" }).success,
    ).toBe(false);

    expect(
      resetPasswordSchema.safeParse({
        newPassword: "correct-horse-battery",
        confirmPassword: "correct-horse-battery",
      }).success,
    ).toBe(true);
  });
});

describe("rejectUpgradeSchema", () => {
  /** SET-007: a rejection is not valid without a reason. */
  it("requires a reason", () => {
    expect(rejectUpgradeSchema.safeParse({ reason: "" }).success).toBe(false);
    expect(rejectUpgradeSchema.safeParse({ reason: "Documents unreadable" }).success).toBe(true);
  });
});

describe("suspendUserSchema", () => {
  /** Part 4: a suspension without a reason is not a valid action. */
  it("requires a reason", () => {
    expect(suspendUserSchema.safeParse({ reason: "" }).success).toBe(false);
    expect(suspendUserSchema.safeParse({ reason: "Repeated policy violations" }).success).toBe(true);
  });

  it("rejects a reason over 255 characters", () => {
    expect(suspendUserSchema.safeParse({ reason: "a".repeat(256) }).success).toBe(false);
  });
});

describe("upgradeRequestSchema", () => {
  const currentContactNumber = "+94 71 234 5678";
  const valid = {
    nicNumber: "199912345678",
    address: "123 Galle Road, Colombo",
    phoneNumber2: "+94 77 987 6543",
    propertyInfo: "Two-bedroom apartment.",
  };

  it("accepts a well-formed request", () => {
    expect(upgradeRequestSchema(currentContactNumber).safeParse(valid).success).toBe(true);
  });

  it.each(["", "123456789", "abc123456789", "912345678A"])(
    "rejects the malformed NIC %s",
    (nicNumber) => {
      expect(
        upgradeRequestSchema(currentContactNumber).safeParse({ ...valid, nicNumber }).success,
      ).toBe(false);
    },
  );

  it.each(["912345678V", "912345678v", "912345678X", "199912345678"])(
    "accepts the well-formed NIC %s",
    (nicNumber) => {
      expect(
        upgradeRequestSchema(currentContactNumber).safeParse({ ...valid, nicNumber }).success,
      ).toBe(true);
    },
  );

  it("requires an address", () => {
    const result = upgradeRequestSchema(currentContactNumber).safeParse({ ...valid, address: "" });

    expect(result.success).toBe(false);
    expect(result.error?.issues.some((issue) => issue.path.includes("address"))).toBe(true);
  });

  it("requires property information", () => {
    const result = upgradeRequestSchema(currentContactNumber).safeParse({
      ...valid,
      propertyInfo: "",
    });

    expect(result.success).toBe(false);
    expect(result.error?.issues.some((issue) => issue.path.includes("propertyInfo"))).toBe(true);
  });

  /** The second phone number must differ from the account's existing (phone 1) number. */
  it("rejects a second phone number identical to the current contact number", () => {
    const result = upgradeRequestSchema(currentContactNumber).safeParse({
      ...valid,
      phoneNumber2: currentContactNumber,
    });

    expect(result.success).toBe(false);
    expect(result.error?.issues.some((issue) => issue.path.includes("phoneNumber2"))).toBe(true);
  });

  it("treats differently formatted versions of the same number as equal", () => {
    const result = upgradeRequestSchema("+94-71-234-5678").safeParse({
      ...valid,
      phoneNumber2: "+94 71 234 5678",
    });

    expect(result.success).toBe(false);
  });

  it("accepts a second phone number that genuinely differs", () => {
    const result = upgradeRequestSchema(currentContactNumber).safeParse(valid);

    expect(result.success).toBe(true);
  });
});
