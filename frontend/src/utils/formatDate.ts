import { CALENDAR_DEADLINE_TIMEZONE } from "../constants/calendarTime";
import { UI_LOCALE } from "../constants/uiLocale";

/** Parse API UTC instants; appends Z when the payload omits a timezone suffix. */
export function parseApiUtcInstant(value: string): Date | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const hasZone = /[zZ]$|[+-]\d{2}:\d{2}$/.test(trimmed);
  const normalized =
    /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(trimmed) && !hasZone ? `${trimmed}Z` : trimmed;
  const d = new Date(normalized);
  return Number.isNaN(d.getTime()) ? null : d;
}

export function formatAppDateTime(value: string, options?: { includeSeconds?: boolean }): string {
  const d = parseApiUtcInstant(value);
  if (!d) return value;
  return new Intl.DateTimeFormat(UI_LOCALE, {
    dateStyle: "medium",
    timeStyle: options?.includeSeconds ? "medium" : "short",
    timeZone: CALENDAR_DEADLINE_TIMEZONE,
  }).format(d);
}

export function formatUtcDateTime(value: string): string {
  return formatAppDateTime(value);
}

export function formatUtcDate(value: string): string {
  const d = parseApiUtcInstant(value);
  if (!d) return value;
  return new Intl.DateTimeFormat(UI_LOCALE, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    timeZone: CALENDAR_DEADLINE_TIMEZONE,
  }).format(d);
}

/** e.g. "just now", "2 min ago", "1 hour ago" */
export function formatRelativeMinutesAgo(updatedAtMs: number, nowMs: number = Date.now()): string {
  const deltaMs = Math.max(0, nowMs - updatedAtMs);
  const seconds = Math.floor(deltaMs / 1000);
  if (seconds < 45) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hour${hours === 1 ? "" : "s"} ago`;
  const days = Math.floor(hours / 24);
  return `${days} day${days === 1 ? "" : "s"} ago`;
}
