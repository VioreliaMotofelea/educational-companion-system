function resolveCalendarDeadlineTimezone(): string | undefined {
  if (typeof import.meta === "undefined") return "Europe/Bucharest";
  const v = import.meta.env.VITE_CALENDAR_TIMEZONE;
  if (v === undefined) return "Europe/Bucharest";
  const raw = String(v).trim();
  if (raw === "" || raw.toLowerCase() === "local") return undefined;
  return raw;
}

export const CALENDAR_DEADLINE_TIMEZONE: string | undefined = resolveCalendarDeadlineTimezone();

export function calendarTimezoneLabel(timeZone: string | undefined = CALENDAR_DEADLINE_TIMEZONE): string {
  if (!timeZone?.trim()) return "your local timezone";
  return timeZone.trim().replace(/_/g, " ");
}
