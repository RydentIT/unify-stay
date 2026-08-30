import type { Metadata } from "next";
import type { ReactNode } from "react";

import { SiteNav } from "@/components/SiteNav";

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
        <SiteNav />
        <main className="flex flex-col gap-4">{children}</main>
      </body>
    </html>
  );
}
