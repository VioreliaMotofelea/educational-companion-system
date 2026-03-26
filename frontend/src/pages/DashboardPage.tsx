import AppLayout from "../components/layout/AppLayout";
import WelcomeHeader from "../components/dashboard/WelcomeHeader";
import RecommendationsPreview from "../components/dashboard/RecommendationsPreview";
import TodayPlan from "../components/dashboard/TodayPlan";

export default function DashboardPage() {
  return (
    <AppLayout>
      <WelcomeHeader />
      <RecommendationsPreview />
      <TodayPlan />
    </AppLayout>
  );
}