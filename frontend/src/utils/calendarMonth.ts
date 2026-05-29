export function mondayWeekIndex(jsDayOfWeek: number): number {
  return (jsDayOfWeek + 6) % 7;
}

export type MonthCell = {
  date: Date;
  inCurrentMonth: boolean;
};

export type CalendarDateParts = {
  year: number;
  month: number;
  day: number;
};

export function civilDateKey(year: number, month: number, day: number): string {
  return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

export function calendarDatePartsInZone(instant: Date, timeZone?: string): CalendarDateParts {
  if (!timeZone?.trim()) {
    return {
      year: instant.getFullYear(),
      month: instant.getMonth() + 1,
      day: instant.getDate(),
    };
  }
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: timeZone.trim(),
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(instant);
  const pick = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((p) => p.type === type)?.value ?? "0");
  return { year: pick("year"), month: pick("month"), day: pick("day") };
}

export function calendarDayKeyFromInstant(instant: Date, timeZone?: string): string {
  const p = calendarDatePartsInZone(instant, timeZone);
  return civilDateKey(p.year, p.month, p.day);
}

export function currentCalendarDate(timeZone?: string): {
  year: number;
  monthIndex: number;
  dayKey: string;
} {
  const parts = calendarDatePartsInZone(new Date(), timeZone);
  return {
    year: parts.year,
    monthIndex: parts.month - 1,
    dayKey: civilDateKey(parts.year, parts.month, parts.day),
  };
}

export function buildMonthGrid(year: number, monthIndex: number, timeZone?: string): MonthCell[] {
  if (!timeZone?.trim()) {
    const firstOfMonth = new Date(year, monthIndex, 1);
    const lead = mondayWeekIndex(firstOfMonth.getDay());
    const start = new Date(year, monthIndex, 1 - lead);
    const cells: MonthCell[] = [];
    for (let i = 0; i < 42; i++) {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      cells.push({
        date: d,
        inCurrentMonth: d.getMonth() === monthIndex && d.getFullYear() === year,
      });
    }
    return cells;
  }

  const firstOfMonth = new Date(Date.UTC(year, monthIndex, 1, 12, 0, 0));
  const lead = mondayWeekIndex(firstOfMonth.getUTCDay());
  const startDay = 1 - lead;
  const cells: MonthCell[] = [];
  for (let i = 0; i < 42; i++) {
    const d = new Date(Date.UTC(year, monthIndex, startDay + i, 12, 0, 0));
    cells.push({
      date: d,
      inCurrentMonth: d.getUTCMonth() === monthIndex && d.getUTCFullYear() === year,
    });
  }
  return cells;
}

export function localDateKey(d: Date): string {
  return civilDateKey(d.getFullYear(), d.getMonth() + 1, d.getDate());
}

export function cellCalendarDayKey(d: Date, timeZone?: string): string {
  if (!timeZone?.trim()) return localDateKey(d);
  return civilDateKey(d.getUTCFullYear(), d.getUTCMonth() + 1, d.getUTCDate());
}

export function deadlineCalendarDayKey(isoUtc: string, timeZone?: string): string {
  const d = new Date(isoUtc);
  if (Number.isNaN(d.getTime())) return calendarDayKeyFromInstant(new Date(), timeZone);
  return calendarDayKeyFromInstant(d, timeZone);
}

export function formatCalendarDayKey(key: string, locale: string, timeZone?: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(key);
  if (!match) return key;
  const y = Number(match[1]);
  const m = Number(match[2]);
  const d = Number(match[3]);
  const anchor = new Date(Date.UTC(y, m - 1, d, 12, 0, 0));
  return new Intl.DateTimeFormat(locale, {
    weekday: "long",
    month: "short",
    day: "numeric",
    timeZone: timeZone?.trim() || undefined,
  }).format(anchor);
}

export function buildWeekdayLabels(locale: string): string[] {
  return Array.from({ length: 7 }, (_, i) => {
    const day = new Date(Date.UTC(2024, 0, 1 + i, 12, 0, 0));
    return new Intl.DateTimeFormat(locale, { weekday: "short", timeZone: "UTC" }).format(day);
  });
}

export function formatDeadlineTime(isoUtc: string, locale: string, timeZone?: string): string {
  const d = new Date(isoUtc);
  if (Number.isNaN(d.getTime())) return "—";
  return new Intl.DateTimeFormat(locale, {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: timeZone?.trim() || undefined,
  }).format(d);
}

export function formatDeadlineDateTime(isoUtc: string, locale: string, timeZone?: string): string {
  const d = new Date(isoUtc);
  if (Number.isNaN(d.getTime())) return "—";
  return new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: timeZone?.trim() || undefined,
  }).format(d);
}
