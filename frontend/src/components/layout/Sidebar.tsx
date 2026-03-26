import { Link } from "react-router-dom";

export default function Sidebar() {
  return (
    <div
      style={{
        width: "220px",
        background: "#1e293b",
        color: "white",
        padding: "20px",
      }}
    >
      <h2>AI Companion</h2>

      <nav style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
        <Link to="/" style={{ color: "white" }}>Dashboard</Link>
        <Link to="/recommendations" style={{ color: "white" }}>Recommendations</Link>
        <Link to="/tasks" style={{ color: "white" }}>Tasks</Link>
        <Link to="/calendar" style={{ color: "white" }}>Calendar</Link>
        <Link to="/profile" style={{ color: "white" }}>Profile</Link>
      </nav>
    </div>
  );
}