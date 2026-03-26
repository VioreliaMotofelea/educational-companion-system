import { BrowserRouter, Routes, Route } from "react-router-dom";
import DashboardPage from "../pages/DashboardPage";
import RecommendationsPage from "../pages/RecommendationsPage";
import TasksPage from "../pages/TasksPage";
import CalendarPage from "../pages/CalendarPage";
import ProfilePage from "../pages/ProfilePage";

export default function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/recommendations" element={<RecommendationsPage />} />
        <Route path="/tasks" element={<TasksPage />} />
        <Route path="/calendar" element={<CalendarPage />} />
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="*" element={<DashboardPage />} />
      </Routes>
    </BrowserRouter>
  );
}