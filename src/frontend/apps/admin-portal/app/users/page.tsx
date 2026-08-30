"use client";

import { type UserSummaryPage } from "@unify/api-client";
import { Alert, useAsyncData } from "@unify/ui";
import Link from "next/link";
import { useCallback, useState } from "react";

import { RequireSession } from "@/components/RequireSession";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";

const PAGE_SIZE = 20;

export default function UsersPage() {
  return (
    <RequireSession>
      <UsersDirectory />
    </RequireSession>
  );
}

/**
 * Part 3: the admin user directory - the entry point for every other admin user-management
 * action. Suspend/lift live on the detail page ({@link "./[id]/page"}), not here.
 */
function UsersDirectory() {
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);

  const fetchUsers = useCallback(
    () => api.adminUsers.getUsers({ search: search || undefined, role: role || undefined, status: status || undefined, page, pageSize: PAGE_SIZE }),
    [search, role, status, page],
  );

  const { data, error, loading } = useAsyncData<UserSummaryPage>(fetchUsers);
  const failure: FormFailure | null = error ? toFormFailure(error) : null;

  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  function resetToFirstPage() {
    setPage(1);
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Users</h1>

      <div className="flex flex-wrap gap-2">
        <input
          type="search"
          placeholder="Search by name or email"
          aria-label="Search users"
          className="rounded border border-gray-400 px-2 py-1 text-sm"
          value={search}
          onChange={(event) => {
            setSearch(event.target.value);
            resetToFirstPage();
          }}
        />

        <select
          aria-label="Filter by role"
          className="rounded border border-gray-400 px-2 py-1 text-sm"
          value={role}
          onChange={(event) => {
            setRole(event.target.value);
            resetToFirstPage();
          }}
        >
          <option value="">All roles</option>
          <option value="Student">Student</option>
          <option value="PropertyOwner">Property Owner</option>
          <option value="Admin">Admin</option>
          <option value="Staff">Staff</option>
        </select>

        <select
          aria-label="Filter by status"
          className="rounded border border-gray-400 px-2 py-1 text-sm"
          value={status}
          onChange={(event) => {
            setStatus(event.target.value);
            resetToFirstPage();
          }}
        >
          <option value="">All statuses</option>
          <option value="Active">Active</option>
          <option value="Suspended">Suspended</option>
          <option value="Deactivated">Deactivated</option>
          <option value="Deleted">Deleted</option>
        </select>
      </div>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}

      {loading ? (
        <p className="text-sm">Loading users...</p>
      ) : items.length === 0 ? (
        <p className="text-sm">No users match these filters.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b">
                <th className="py-1 pr-4">Name</th>
                <th className="py-1 pr-4">Email</th>
                <th className="py-1 pr-4">Role</th>
                <th className="py-1 pr-4">Status</th>
                <th className="py-1 pr-4">Joined</th>
              </tr>
            </thead>
            <tbody>
              {items.map((user) => (
                <tr key={user.id} className="border-b">
                  <td className="py-1 pr-4">
                    <Link href={`/users/${user.id}`}>
                      {user.firstName} {user.lastName}
                    </Link>
                  </td>
                  <td className="py-1 pr-4">{user.email}</td>
                  <td className="py-1 pr-4">{user.roles.join(", ")}</td>
                  <td className="py-1 pr-4">{user.status}</td>
                  <td className="py-1 pr-4">{new Date(user.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {totalCount > 0 ? (
        <div className="flex items-center gap-3 text-sm">
          <button
            type="button"
            className="underline disabled:no-underline disabled:text-gray-400"
            disabled={page <= 1}
            onClick={() => setPage((current) => Math.max(1, current - 1))}
          >
            Previous
          </button>
          <span>
            Page {page} of {totalPages} ({totalCount} total)
          </span>
          <button
            type="button"
            className="underline disabled:no-underline disabled:text-gray-400"
            disabled={page >= totalPages}
            onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
          >
            Next
          </button>
        </div>
      ) : null}
    </section>
  );
}
