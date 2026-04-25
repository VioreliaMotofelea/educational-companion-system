import AppLayout from "../components/layout/AppLayout";
import TodayPlan from "../components/dashboard/TodayPlan";

export default function CalendarPage() {
  return (
    <AppLayout>
      <TodayPlan title="AI suggested schedule" />
    </AppLayout>
  );
}