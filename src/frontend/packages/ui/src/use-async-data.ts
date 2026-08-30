"use client";

import { useCallback, useEffect, useState } from "react";

export interface AsyncData<TValue> {
  data: TValue | undefined;
  error: unknown;
  loading: boolean;
  /** Re-runs the fetch, keeping the current data on screen while it is in flight. */
  reload: () => Promise<void>;
}

/**
 * Fetch-on-mount for client components.
 *
 * This is the one place in the codebase that fetches from an effect. Centralising it keeps the
 * pages free of the react-hooks/set-state-in-effect noise that ad-hoc fetching effects trigger,
 * and means the cancellation handling is written correctly once instead of per page.
 *
 * `fetcher` must be stable - wrap it in useCallback at the call site, or define it outside the
 * component - otherwise this refetches on every render.
 */
export function useAsyncData<TValue>(fetcher: () => Promise<TValue>): AsyncData<TValue> {
  const [data, setData] = useState<TValue | undefined>(undefined);
  const [error, setError] = useState<unknown>(undefined);
  const [loading, setLoading] = useState(true);

  const run = useCallback(async () => {
    try {
      setData(await fetcher());
      setError(undefined);
    } catch (caught) {
      setError(caught);
    } finally {
      setLoading(false);
    }
  }, [fetcher]);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const value = await fetcher();

        // Guard against a late response from a fetcher that has since been replaced.
        if (!cancelled) {
          setData(value);
          setError(undefined);
        }
      } catch (caught) {
        if (!cancelled) {
          setError(caught);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [fetcher]);

  return { data, error, loading, reload: run };
}
