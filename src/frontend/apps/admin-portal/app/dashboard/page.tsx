"use client";

import Link from "next/link";

import { RequireSession } from "@/components/RequireSession";

export default function DashboardPage() {
  return (
    <RequireSession>
      <section className="flex flex-col gap-3">
        <h1 className="text-xl font-semibold">Dashboard</h1>
        <p className="text-sm">Staff tools.</p>
        <ul className="flex flex-col gap-1 text-sm">
          <li>
            <Link href="/users">Manage users</Link>
          </li>
          <li>
            <Link href="/upgrade-requests">Review Property Owner upgrade requests</Link>
          </li>
        </ul>
      </section>
    </RequireSession>
  );
}
