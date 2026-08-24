import type { Metadata } from "next";
import Link from "next/link";
import type { ReactNode } from "react";

import "./globals.css";

export const metadata: Metadata = {
  title: "UnifyStay",
  description: "UnifyStay user portal",
};

/**
 * Structural only. No design investment here on purpose - final Figma designs are pending, so
 * this exists to make the pages navigable, not to look finished.
 */
export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body className="mx-auto flex max-w-2xl flex-col gap-6 p-6">
        <header className="flex flex-wrap gap-4 border-b pb-3 text-sm">
          <Link href="/">Home</Link>
          <Link href="/register">Register</Link>
          <Link href="/login">Log in</Link>
          <Link href="/forgot-password">Forgot password</Link>
          <Link href="/profile">Profile</Link>
          <Link href="/settings">Settings</Link>
        </header>
        <main className="flex flex-col gap-4">{children}</main>
      </body>
    </html>
  );
}
