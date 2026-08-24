import { type ReactNode } from "react";

export interface AlertProps {
  children: ReactNode;
  tone?: "info" | "error";
}

/** Status message. role=alert so screen readers announce failures without a focus change. */
export function Alert({ children, tone = "info" }: AlertProps) {
  return (
    <p
      role={tone === "error" ? "alert" : "status"}
      className={tone === "error" ? "text-sm text-red-700" : "text-sm"}
    >
      {children}
    </p>
  );
}
