import type { Metadata } from "next";
import Link from "next/link";
import type { ReactNode } from "react";

import "./globals.css";

export const metadata: Metadata = {
  title: "UnifyStay Admin",
  description: "UnifyStay internal staff portal",
  // Staff tooling should never be indexed, even if it somehow becomes reachable.
  robots: { index: false, follow: false },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body className="mx-auto flex max-w-2xl flex-col gap-6 p-6">
        <header className="flex flex-wrap gap-4 border-b pb-3 text-sm">
          <Link href="/login">Log in</Link>
          <Link href="/dashboard">Dashboard</Link>
        </header>
        <main className="flex flex-col gap-4">{children}</main>
      </body>
    </html>
  );
}
