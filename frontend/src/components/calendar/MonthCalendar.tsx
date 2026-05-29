import { useMemo, useState, type CSSProperties } from "react";
import { Link } from "react-router-dom";
import { CALENDAR_DEADLINE_TIMEZONE, calendarTimezoneLabel } from "../../constants/calendarTime";
import { UI_LOCALE } from "../../constants/uiLocale";
import type { StudyTask } from "../../types";
import {
  buildMonthGrid,
  buildWeekdayLabels,
  calendarDayKeyFromInstant,
  cellCalendarDayKey,
  currentCalendarDate,
  deadlineCalendarDayKey,
  formatCalendarDayKey,
  formatDeadlineTime,
} from "../../utils/calendarMonth";
import { humanizeResourceTitle } from "../../utils/recommendationUtils";

type Props = {
  tasks: StudyTask[];
  loading?: boolean;
  signedInEmail?: string | null;
};

function statusColor(status: StudyTask["status"]): string {
  if (status === "Completed") return "var(--color-progress-600)";
  if (status === "Overdue") return "rgba(239, 68, 68, 0.95)";
  return "var(--color-ai-600)";
}

function taskLabel(task: StudyTask): string {
  const raw = task.learningResourceTitle ?? task.title;
  return humanizeResourceTitle(raw);
}

export default function MonthCalendar({ tasks, loading = false, signedInEmail }: Props) {
  const tz = CALENDAR_DEADLINE_TIMEZONE;
  const timezoneLabel = calendarTimezoneLabel(tz);
  const initialCalendar = useMemo(() => currentCalendarDate(tz), [tz]);

  const [year, setYear] = useState(initialCalendar.year);
  const [monthIndex, setMonthIndex] = useState(initialCalendar.monthIndex);
  const [selectedKey, setSelectedKey] = useState<string | null>(initialCalendar.dayKey);

  const weekdayLabels = useMemo(() => buildWeekdayLabels(UI_LOCALE), []);

  const todayKey = calendarDayKeyFromInstant(new Date(), tz);

  const cells = useMemo(() => buildMonthGrid(year, monthIndex, tz), [year, monthIndex, tz]);

  const tasksByDay = useMemo(() => {
    const map = new Map<string, StudyTask[]>();
    for (const t of tasks) {
      const key = deadlineCalendarDayKey(t.deadlineUtc, tz);
      const list = map.get(key) ?? [];
      list.push(t);
      map.set(key, list);
    }
    for (const [, list] of map) {
      list.sort((a, b) => new Date(a.deadlineUtc).getTime() - new Date(b.deadlineUtc).getTime());
    }
    return map;
  }, [tasks, tz]);

  const monthTitle = tz
    ? new Intl.DateTimeFormat(UI_LOCALE, {
        month: "long",
        year: "numeric",
        timeZone: "UTC",
      }).format(new Date(Date.UTC(year, monthIndex, 1, 12, 0, 0)))
    : new Intl.DateTimeFormat(UI_LOCALE, { month: "long", year: "numeric" }).format(
        new Date(year, monthIndex, 1),
      );

  const shiftMonth = (delta: number) => {
    if (tz) {
      const d = new Date(Date.UTC(year, monthIndex + delta, 1, 12, 0, 0));
      setYear(d.getUTCFullYear());
      setMonthIndex(d.getUTCMonth());
      return;
    }
    const d = new Date(year, monthIndex + delta, 1);
    setYear(d.getFullYear());
    setMonthIndex(d.getMonth());
  };

  const selectedTasks = selectedKey ? (tasksByDay.get(selectedKey) ?? []) : [];

  return (
    <section style={{ border: "1px solid var(--border)", background: "var(--panel)", borderRadius: "var(--radius-md)", padding: 16 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
        <h3 style={{ margin: 0, lineHeight: 1.2 }}>{monthTitle}</h3>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <button
            type="button"
            onClick={() => shiftMonth(-1)}
            style={navBtnStyle}
            aria-label="Previous month"
          >
            ←
          </button>
          <button type="button" onClick={() => shiftMonth(1)} style={navBtnStyle} aria-label="Next month">
            →
          </button>
          <button
            type="button"
            onClick={() => {
              const today = currentCalendarDate(tz);
              setYear(today.year);
              setMonthIndex(today.monthIndex);
              setSelectedKey(today.dayKey);
            }}
            style={{ ...navBtnStyle, fontWeight: 700 }}
          >
            Today
          </button>
        </div>
      </div>

      {!loading ? (
        <p style={{ margin: "8px 0 0", fontSize: 13, color: "var(--muted)", lineHeight: 1.5 }}>
          Signed in as <strong style={{ color: "var(--text)" }}>{signedInEmail ?? "—"}</strong>.
          {tasks.length === 0 ? (
            <>
              {" "}
              No task deadlines yet. Open{" "}
              <Link to="/tasks" style={{ color: "var(--color-ai-600)", fontWeight: 700 }}>
                Tasks
              </Link>{" "}
              to review your study plan, or generate recommendations from the dashboard to create new tasks.
            </>
          ) : (
            <>
              {" "}
              <strong>{tasks.length}</strong> task{tasks.length === 1 ? "" : "s"} with deadlines shown in{" "}
              <strong>{timezoneLabel}</strong>.
            </>
          )}
        </p>
      ) : null}

      {loading ? (
        <p style={{ margin: "10px 0 0", color: "var(--muted)" }}>Loading your tasks…</p>
      ) : (
        <>
          <div
            role="grid"
            aria-label="Month calendar"
            style={{
              marginTop: 10,
              display: "grid",
              gridTemplateColumns: "repeat(7, minmax(0, 1fr))",
              gap: 5,
            }}
          >
            {weekdayLabels.map((wd, index) => (
              <div
                key={`${index}-${wd}`}
                role="columnheader"
                style={{
                  fontSize: 11,
                  fontWeight: 800,
                  letterSpacing: "0.06em",
                  textTransform: "uppercase",
                  color: "var(--muted)",
                  textAlign: "center",
                  padding: "4px 0",
                }}
              >
                {wd}
              </div>
            ))}
            {cells.map(({ date, inCurrentMonth }) => {
              const key = cellCalendarDayKey(date, tz);
              const dayTasks = tasksByDay.get(key) ?? [];
              const isToday = key === todayKey;
              const isSelected = key === selectedKey;
              return (
                <button
                  key={key}
                  type="button"
                  role="gridcell"
                  onClick={() => setSelectedKey(key)}
                  style={{
                    minHeight: 64,
                    borderRadius: 10,
                    border: `1px solid ${
                      isSelected ? "var(--color-ai-600)" : isToday ? "rgba(37, 99, 235, 0.45)" : "var(--border)"
                    }`,
                    background: isSelected
                      ? "rgba(37, 99, 235, 0.15)"
                      : isToday
                        ? "rgba(37, 99, 235, 0.08)"
                        : "rgba(255,255,255,0.02)",
                    color: inCurrentMonth ? "var(--text)" : "var(--muted)",
                    cursor: "pointer",
                    textAlign: "left",
                    padding: "6px 7px 8px",
                    display: "flex",
                    flexDirection: "column",
                    gap: 4,
                    opacity: inCurrentMonth ? 1 : 0.55,
                  }}
                >
                  <span style={{ fontWeight: 800, fontSize: 13 }}>{tz ? date.getUTCDate() : date.getDate()}</span>
                  <span style={{ display: "flex", flexWrap: "wrap", gap: 3 }}>
                    {dayTasks.slice(0, 3).map((t) => (
                      <span
                        key={t.id}
                        title={taskLabel(t)}
                        style={{
                          width: 7,
                          height: 7,
                          borderRadius: 999,
                          background: statusColor(t.status),
                          flexShrink: 0,
                        }}
                      />
                    ))}
                    {dayTasks.length > 3 ? (
                      <span style={{ fontSize: 10, color: "var(--muted)", fontWeight: 700 }}>+{dayTasks.length - 3}</span>
                    ) : null}
                  </span>
                </button>
              );
            })}
          </div>

          <div
            style={{
              marginTop: 10,
              paddingTop: 10,
              borderTop: "1px solid var(--border)",
            }}
          >
            <h4 style={{ margin: "0 0 8px 0", fontSize: 14 }}>
              {selectedKey
                ? `Tasks on ${formatCalendarDayKey(selectedKey, UI_LOCALE, tz)}`
                : "Select a day"}
            </h4>
            {selectedTasks.length === 0 ? (
              <p style={{ margin: 0, color: "var(--muted)", fontSize: 13 }}>No deadlines on this day.</p>
            ) : (
              <ul style={{ margin: 0, padding: 0, listStyle: "none", display: "flex", flexDirection: "column", gap: 8 }}>
                {selectedTasks.map((t) => (
                  <li
                    key={t.id}
                    style={{
                      border: "1px solid var(--border)",
                      borderRadius: "var(--radius-md)",
                      padding: "8px 10px",
                      fontSize: 13,
                    }}
                  >
                    <div style={{ fontWeight: 700 }}>{taskLabel(t)}</div>
                    <div style={{ color: "var(--muted)", marginTop: 4 }}>
                      <span style={{ color: statusColor(t.status), fontWeight: 700 }}>{t.status}</span>
                      {" · "}
                      {formatDeadlineTime(t.deadlineUtc, UI_LOCALE, tz)}
                      {t.estimatedMinutes ? ` · ${t.estimatedMinutes} min` : ""}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}
    </section>
  );
}

const navBtnStyle: CSSProperties = {
  border: "1px solid var(--border-strong)",
  background: "rgba(255,255,255,0.04)",
  color: "var(--text)",
  borderRadius: "var(--radius-md)",
  padding: "6px 12px",
  cursor: "pointer",
  fontSize: 14,
};
