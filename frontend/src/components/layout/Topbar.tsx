import { useMemo } from "react";
import { useLocation } from "react-router-dom";
import { useCurrentUser } from "../../hooks/useCurrentUser";

export default function Topbar() {
  const { pathname } = useLocation();
  const { userId } = useCurrentUser();

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
      <div>{userId}</div>
    </div>
  );
}