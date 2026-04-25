import AppLayout from "../components/layout/AppLayout";
import WelcomeHeader from "../components/dashboard/WelcomeHeader";
import RecommendationsPreview from "../components/dashboard/RecommendationsPreview";
import TodayPlan from "../components/dashboard/TodayPlan";
import ProgressOverview from "../components/dashboard/ProgressOverview";
import QuickStats from "../components/dashboard/QuickStats";

export default function DashboardPage() {
  return (
    <AppLayout>
      <WelcomeHeader />
      <div style={{ display: "grid", gridTemplateColumns: "minmax(360px, 1fr) minmax(320px, 420px)", gap: 16, alignItems: "start" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <RecommendationsPreview />
          <TodayPlan />
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <QuickStats />
          <ProgressOverview />
        </div>
      </div>
    </AppLayout>
  );
}