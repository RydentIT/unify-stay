/**
 * MailHog helpers for the end-to-end suite.
 *
 * The tests read the real verification and reset links out of the inbox rather than querying
 * the database, so what they exercise is exactly what a user would receive - including the
 * URL construction in AppUrlOptions, which a database-level shortcut would skip entirely.
 */
const MAILHOG_URL = process.env.MAILHOG_URL ?? "http://localhost:8025";

interface MailHogMessage {
  Content: {
    Headers: Record<string, string[]>;
    Body: string;
  };
}

/** Unique per run, so repeated runs never collide on the unique email index. */
export function uniqueEmail(prefix: string): string {
  const suffix = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
  return `${prefix}-${suffix}@example.com`;
}

/**
 * Polls for the newest message to an address and returns the first link containing `pathFragment`.
 * Returns null if nothing arrives within the timeout, so the caller can assert a clear failure.
 */
export async function fetchLatestLink(
  toAddress: string,
  pathFragment: string,
  timeoutMs = 20_000,
): Promise<string | null> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const messages = await fetchMessages();

    // Newest first: MailHog returns most-recent-first, so the first hit is the latest link,
    // which matters because issuing a new token invalidates the previous one.
    for (const message of messages) {
      if (!addressedTo(message, toAddress)) {
        continue;
      }

      const link = extractLink(decodeBody(message.Content.Body), pathFragment);

      if (link) {
        return link;
      }
    }

    await delay(500);
  }

  return null;
}

export async function clearInbox(): Promise<void> {
  try {
    await fetch(`${MAILHOG_URL}/api/v1/messages`, { method: "DELETE" });
  } catch {
    // A missing MailHog is reported by the assertions in the tests themselves.
  }
}

async function fetchMessages(): Promise<MailHogMessage[]> {
  try {
    const response = await fetch(`${MAILHOG_URL}/api/v2/messages?limit=100`);

    if (!response.ok) {
      return [];
    }

    const payload = (await response.json()) as { items?: MailHogMessage[] };
    return payload.items ?? [];
  } catch {
    return [];
  }
}

function addressedTo(message: MailHogMessage, address: string): boolean {
  const recipients = message.Content.Headers.To ?? [];
  return recipients.some((value) => value.toLowerCase().includes(address.toLowerCase()));
}

/** MailHog stores quoted-printable bodies, which split long URLs across lines with "=\r\n". */
function decodeBody(body: string): string {
  return body
    .replace(/=\r?\n/g, "")
    .replace(/=3D/gi, "=")
    .replace(/=([0-9A-F]{2})/gi, (_, hex: string) => String.fromCharCode(parseInt(hex, 16)));
}

function extractLink(body: string, pathFragment: string): string | null {
  const pattern = new RegExp(`https?://[^\\s"'<>]*${escapeRegExp(pathFragment)}[^\\s"'<>]+`, "i");
  const match = pattern.exec(body);

  return match ? match[0] : null;
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
