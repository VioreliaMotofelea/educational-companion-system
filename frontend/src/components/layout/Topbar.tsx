import { useMemo } from "react";
import { useLocation } from "react-router-dom";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import { useAuth } from "../../hooks/useAuth";

export default function Topbar() {
  const { pathname } = useLocation();
  const { userId } = useCurrentUser();
  const { user, logout } = useAuth();

  const pageTitle = useMemo(() => {
    if (pathname === "/") return "Dashboard";
    if (pathname === "/recommendations") return "Recommendations";
    if (pathname === "/tasks") return "Tasks";
    if (pathname === "/calendar") return "Calendar";
    if (pathname === "/profile") return "Profile";
    return "Educational Companion";
  }, [pathname]);

  return (
    <div
      style={{
        height: "60px",
        borderBottom: "1px solid var(--border)",
        background: "rgba(255, 255, 255, 0.02)",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "0 20px",
        color: "var(--text)",
      }}
    >
      <h3>{pageTitle}</h3>
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <div>{user?.email ?? userId}</div>
        <button
          onClick={() => void logout()}
          style={{
            border: "1px solid var(--border-strong)",
            background: "transparent",
            color: "var(--text)",
            borderRadius: "var(--radius-md)",
            padding: "6px 10px",
            cursor: "pointer",
          }}
        >
          Logout
        </button>
      </div>
    </div>
  );
}