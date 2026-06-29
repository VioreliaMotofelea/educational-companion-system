import { useEffect, useState } from "react";
import AppLayout from "../components/layout/AppLayout";
import MonthCalendar from "../components/calendar/MonthCalendar";
import TodayPlan from "../components/dashboard/TodayPlan";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useTasks } from "../hooks/useTasks";
import { formatRelativeMinutesAgo } from "../utils/formatDate";

export default function CalendarPage() {
  const { userId } = useCurrentUser();
  const { tasks, loading, error, lastUpdatedAt } = useTasks(userId);
  const [nowMs, setNowMs] = useState(() => Date.now());

  useEffect(() => {
    const timerId = window.setInterval(() => setNowMs(Date.now()), 60_000);
    return () => window.clearInterval(timerId);
  }, []);

  return (
    <AppLayout>
      <h2 style={{ margin: "0 0 8px 0" }}>Calendar</h2>
      <p style={{ margin: 0, color: "var(--muted)", maxWidth: 680, lineHeight: 1.55, fontSize: 14 }}>
        Task deadlines from your open study list.
        {!loading && lastUpdatedAt != null ? (
          <> Auto-updated {formatRelativeMinutesAgo(lastUpdatedAt, nowMs)}.</>
        ) : null}
      </p>

      {error ? (
        <p style={{ marginTop: 14, color: "rgba(239, 68, 68, 0.95)" }}>{error}</p>
      ) : (
        <div style={{ marginTop: 18 }}>
          <MonthCalendar tasks={tasks} loading={loading} />
        </div>
      )}

      <div style={{ marginTop: 20 }}>
        <TodayPlan title="Today's study plan" compact />
      </div>
    </AppLayout>
  );
}
