import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

import { Button } from "../src/button";

describe("Button", () => {
  it("renders its children and calls onClick", async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Sign in</Button>);

    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(onClick).toHaveBeenCalledOnce();
  });

  it("blocks clicks and reports busy while pending", async () => {
    const onClick = vi.fn();
    render(
      <Button pending onClick={onClick}>
        Sign in
      </Button>,
    );

    const button = screen.getByRole("button");

    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("aria-busy", "true");

    await userEvent.click(button);
    expect(onClick).not.toHaveBeenCalled();
  });

  it("defaults to type=button so it cannot submit a form by accident", () => {
    render(<Button>Do a thing</Button>);

    expect(screen.getByRole("button")).toHaveAttribute("type", "button");
  });
});
