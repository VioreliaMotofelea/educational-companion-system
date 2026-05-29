import { UI_LOCALE } from "../constants/uiLocale";

export function formatUtcDateTime(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString(UI_LOCALE, { dateStyle: "medium", timeStyle: "short" });
}

export function formatUtcDate(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleDateString(UI_LOCALE, { year: "numeric", month: "short", day: "2-digit" });
}
