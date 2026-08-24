import { type ButtonHTMLAttributes, type ReactNode } from "react";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode;
  /** Renders a busy state and blocks further clicks. */
  pending?: boolean;
}

/**
 * Plain button. Styling is deliberately minimal - final designs are pending, so this exists to
 * carry behaviour (disabled/pending semantics) rather than to look like anything in particular.
 */
export function Button({ children, pending = false, disabled, type = "button", ...rest }: ButtonProps) {
  return (
    <button
      {...rest}
      type={type}
      disabled={disabled === true || pending}
      aria-busy={pending}
      className="rounded border border-gray-400 px-3 py-2 text-sm disabled:opacity-50"
    >
      {pending ? "Working..." : children}
    </button>
  );
}
