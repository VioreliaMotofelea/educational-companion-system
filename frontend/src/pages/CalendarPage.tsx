import { Link } from "react-router-dom";
import AppLayout from "../components/layout/AppLayout";
import MonthCalendar from "../components/calendar/MonthCalendar";
import TodayPlan from "../components/dashboard/TodayPlan";
import { calendarTimezoneLabel } from "../constants/calendarTime";
import { useAuth } from "../hooks/useAuth";
import { useCurrentUser } from "../hooks/useCurrentUser";
import { useTasks } from "../hooks/useTasks";

export default function CalendarPage() {
  const { user } = useAuth();
  const { userId } = useCurrentUser();
  const { tasks, loading, error } = useTasks(userId);

  return (
    <AppLayout>
      <h2 style={{ margin: "0 0 8px 0" }}>Calendar</h2>
      <p style={{ margin: 0, color: "var(--muted)", maxWidth: 680, lineHeight: 1.55 }}>
        Month view shows <b>task deadlines</b> by day ({calendarTimezoneLabel()}). Use{" "}
        <Link to="/tasks#schedule" style={{ color: "var(--color-ai-600)", fontWeight: 700 }}>
          Tasks → Suggested schedule
        </Link>{" "}
        for the AI time blocks and{" "}
        <Link to="/tasks#tasks-list" style={{ color: "var(--color-ai-600)", fontWeight: 700 }}>
          your task list
        </Link>{" "}
        to edit or complete work.
      </p>

      {error ? (
        <p style={{ marginTop: 14, color: "rgba(239, 68, 68, 0.95)" }}>{error}</p>
      ) : (
        <div style={{ marginTop: 18 }}>
          <MonthCalendar tasks={tasks} loading={loading} signedInEmail={user?.email ?? null} />
        </div>
      )}

      <div style={{ marginTop: 20 }}>
        <TodayPlan title="Suggested study times (today)" />
      </div>
    </AppLayout>
  );
}
