import { describe, expect, it } from "vitest";
import {
  buildMonthGrid,
  buildWeekdayLabels,
  calendarDayKeyFromInstant,
  cellCalendarDayKey,
  civilDateKey,
  currentCalendarDate,
  deadlineCalendarDayKey,
  formatCalendarDayKey,
  formatDeadlineDateTime,
  formatDeadlineTime,
  localDateKey,
  mondayWeekIndex,
} from "./calendarMonth";

describe("deadlineCalendarDayKey", () => {
  it("without timeZone matches local civil date of the instant", () => {
    const iso = "2026-05-01T12:00:00.000Z";
    expect(deadlineCalendarDayKey(iso, undefined)).toBe(localDateKey(new Date(iso)));
  });

  it("with UTC timeZone uses the UTC calendar date", () => {
    expect(deadlineCalendarDayKey("2026-05-01T22:00:00.000Z", "UTC")).toBe("2026-05-01");
  });

  it("maps a UTC instant to the configured IANA zone civil date", () => {
    expect(deadlineCalendarDayKey("2026-05-01T21:30:00.000Z", "Europe/Bucharest")).toBe("2026-05-02");
  });
});

describe("cellCalendarDayKey", () => {
  it("without timeZone matches localDateKey", () => {
    const d = new Date(2027, 1, 5);
    expect(cellCalendarDayKey(d, undefined)).toBe(localDateKey(d));
  });

  it("with timeZone uses UTC civil parts from the grid anchor", () => {
    const d = new Date(Date.UTC(2027, 1, 5, 12, 0, 0));
    expect(cellCalendarDayKey(d, "Europe/Bucharest")).toBe("2027-02-05");
  });
});

describe("calendarDayKeyFromInstant", () => {
  it("matches deadlineCalendarDayKey for the same instant and zone", () => {
    const iso = "2026-05-01T21:30:00.000Z";
    expect(calendarDayKeyFromInstant(new Date(iso), "Europe/Bucharest")).toBe(
      deadlineCalendarDayKey(iso, "Europe/Bucharest"),
    );
  });
});

describe("currentCalendarDate", () => {
  it("returns a valid day key for UTC zone", () => {
    const today = currentCalendarDate("UTC");
    expect(today.dayKey).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(today.monthIndex).toBeGreaterThanOrEqual(0);
    expect(today.monthIndex).toBeLessThanOrEqual(11);
  });
});

describe("mondayWeekIndex", () => {
  it("maps Sunday (0) to 6", () => {
    expect(mondayWeekIndex(0)).toBe(6);
  });

  it("maps Monday (1) to 0", () => {
    expect(mondayWeekIndex(1)).toBe(0);
  });

  it("maps Saturday (6) to 5", () => {
    expect(mondayWeekIndex(6)).toBe(5);
  });
});

describe("buildMonthGrid", () => {
  it("always returns 42 cells (6 weeks × 7 days)", () => {
    expect(buildMonthGrid(2027, 1)).toHaveLength(42);
    expect(buildMonthGrid(2027, 1, "Europe/Bucharest")).toHaveLength(42);
  });

  it("February 2027 starts on a Monday: first in-month cell is Feb 1", () => {
    expect(new Date(2027, 1, 1).getDay()).toBe(1);

    const grid = buildMonthGrid(2027, 1);
    expect(grid[0].date.getFullYear()).toBe(2027);
    expect(grid[0].date.getMonth()).toBe(1);
    expect(grid[0].date.getDate()).toBe(1);
    expect(grid[0].inCurrentMonth).toBe(true);

    const inMonth = grid.filter((c) => c.inCurrentMonth);
    expect(inMonth).toHaveLength(28);
    expect(inMonth.at(-1)?.date.getDate()).toBe(28);
  });

  it("February 2026 starts on Sunday: grid begins with leading January days", () => {
    expect(new Date(2026, 1, 1).getDay()).toBe(0);

    const grid = buildMonthGrid(2026, 1);
    expect(grid[0].inCurrentMonth).toBe(false);
    expect(grid[0].date.getMonth()).toBe(0);
    expect(grid[0].date.getDate()).toBe(26);

    const firstFeb = grid.find((c) => c.inCurrentMonth);
    expect(firstFeb?.date.getDate()).toBe(1);
    expect(grid.filter((c) => c.inCurrentMonth)).toHaveLength(28);
  });

  it("IANA zone grid cell keys align with deadline keys for the same civil day", () => {
    const grid = buildMonthGrid(2026, 4, "Europe/Bucharest");
    const mayFirstCell = grid.find((c) => c.inCurrentMonth && cellCalendarDayKey(c.date, "Europe/Bucharest") === "2026-05-01");
    expect(mayFirstCell).toBeDefined();
    expect(deadlineCalendarDayKey("2026-05-01T10:00:00.000Z", "Europe/Bucharest")).toBe("2026-05-01");
  });
});

describe("civilDateKey", () => {
  it("formats as YYYY-MM-DD", () => {
    expect(civilDateKey(2027, 2, 5)).toBe("2027-02-05");
  });
});

describe("localDateKey", () => {
  it("formats as YYYY-MM-DD in local calendar", () => {
    expect(localDateKey(new Date(2027, 1, 5))).toBe("2027-02-05");
  });
});

describe("formatCalendarDayKey", () => {
  it("formats a day key using the configured time zone", () => {
    const label = formatCalendarDayKey("2026-05-01", "en-GB", "UTC");
    expect(label).toContain("1");
    expect(label.toLowerCase()).toContain("may");
  });
});

describe("buildWeekdayLabels", () => {
  it("returns seven Monday-first labels", () => {
    expect(buildWeekdayLabels("en-GB")).toHaveLength(7);
  });
});

describe("formatDeadlineTime", () => {
  it("returns a time string for a valid UTC instant", () => {
    expect(formatDeadlineTime("2026-05-01T14:30:00.000Z", "en-GB", "UTC")).toMatch(/\d/);
  });

  it("returns a placeholder for invalid input", () => {
    expect(formatDeadlineTime("not-a-date", "en-GB", "UTC")).toBe("—");
  });
});

describe("formatDeadlineDateTime", () => {
  it("returns a date-time string for a valid UTC instant", () => {
    expect(formatDeadlineDateTime("2026-05-01T14:30:00.000Z", "en-GB", "UTC")).toMatch(/\d/);
  });
});
