import { Link } from "react-router-dom";

export default function Sidebar() {
  return (
    <div
      style={{
        width: "220px",
        background: "var(--color-ai-900)",
        color: "var(--text)",
        padding: "20px",
      }}
    >
      <h2>AI Companion</h2>

      <nav style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
        <Link to="/" style={{ color: "var(--text)" }}>Dashboard</Link>
        <Link to="/recommendations" style={{ color: "var(--text)" }}>Recommendations</Link>
        <Link to="/tasks" style={{ color: "var(--text)" }}>Tasks</Link>
        <Link to="/calendar" style={{ color: "var(--text)" }}>Calendar</Link>
        <Link to="/profile" style={{ color: "var(--text)" }}>Profile</Link>
      </nav>
    </div>
  );
}