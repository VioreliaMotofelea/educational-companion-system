import { Link, useLocation } from "react-router-dom";

function isActive(pathname: string, to: string) {
  if (to === "/") return pathname === "/";
  return pathname === to || pathname.startsWith(`${to}/`);
}

export default function Sidebar() {
  const { pathname } = useLocation();

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
        <Link
          to="/"
          style={
            isActive(pathname, "/")
              ? { color: "var(--text)", fontWeight: 900, borderLeft: "4px solid var(--color-ai-600)", paddingLeft: 12, background: "rgba(255,255,255,0.04)" }
              : { color: "var(--text)" }
          }
        >
          Dashboard
        </Link>
        <Link
          to="/recommendations"
          style={
            isActive(pathname, "/recommendations")
              ? { color: "var(--text)", fontWeight: 900, borderLeft: "4px solid var(--color-ai-600)", paddingLeft: 12, background: "rgba(255,255,255,0.04)" }
              : { color: "var(--text)" }
          }
        >
          Recommendations
        </Link>
        <Link
          to="/tasks"
          style={
            isActive(pathname, "/tasks")
              ? { color: "var(--text)", fontWeight: 900, borderLeft: "4px solid var(--color-ai-600)", paddingLeft: 12, background: "rgba(255,255,255,0.04)" }
              : { color: "var(--text)" }
          }
        >
          Tasks
        </Link>
        <Link
          to="/calendar"
          style={
            isActive(pathname, "/calendar")
              ? { color: "var(--text)", fontWeight: 900, borderLeft: "4px solid var(--color-ai-600)", paddingLeft: 12, background: "rgba(255,255,255,0.04)" }
              : { color: "var(--text)" }
          }
        >
          Calendar
        </Link>
        <Link
          to="/profile"
          style={
            isActive(pathname, "/profile")
              ? { color: "var(--text)", fontWeight: 900, borderLeft: "4px solid var(--color-ai-600)", paddingLeft: 12, background: "rgba(255,255,255,0.04)" }
              : { color: "var(--text)" }
          }
        >
          Profile
        </Link>
      </nav>
    </div>
  );
}