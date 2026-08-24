import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { Field } from "../src/field";

describe("Field", () => {
  it("associates the label with its input", () => {
    render(<Field label="Email" name="email" />);

    expect(screen.getByLabelText("Email")).toBeInTheDocument();
  });

  it("marks the input invalid and links the messages when there are errors", () => {
    render(<Field label="Email" name="email" errors={["Not a valid address."]} />);

    const input = screen.getByLabelText("Email");

    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Not a valid address.");
  });

  it("adds no description when the field is valid", () => {
    render(<Field label="Email" name="email" />);

    expect(screen.getByLabelText("Email")).not.toHaveAttribute("aria-describedby");
  });
});
