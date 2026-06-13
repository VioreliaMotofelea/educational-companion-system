import { useMemo } from "react";
import { useLocation } from "react-router-dom";
import { useCurrentUser } from "../../hooks/useCurrentUser";
import { useAuth } from "../../hooks/useAuth";

type Props = {
  narrow?: boolean;
  onOpenMenu?: () => void;
};

export default function Topbar({ narrow = false, onOpenMenu }: Props) {
  const { pathname } = useLocation();
  const { userId } = useCurrentUser();
  const { user, logout } = useAuth();

  const pageTitle = useMemo(() => {
    if (pathname === "/") return "Dashboard";
    if (pathname === "/recommendations") return "Recommendations";
    if (pathname === "/tasks") return "Tasks";
    if (pathname === "/calendar") return "Calendar";
    if (pathname === "/profile") return "Profile";
    if (pathname === "/materials" || pathname === "/demo/ingestion") return "Supplementary materials";
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
        gap: 12,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 12, minWidth: 0, flex: 1 }}>
        {narrow ? (
          <button
            type="button"
            aria-label="Open menu"
            onClick={onOpenMenu}
            style={{
              border: "1px solid var(--border-strong)",
              background: "transparent",
              color: "var(--text)",
              borderRadius: "var(--radius-md)",
              padding: "8px 12px",
              cursor: "pointer",
              fontWeight: 700,
              flexShrink: 0,
            }}
          >
            Menu
          </button>
        ) : null}
        <h3 style={{ margin: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{pageTitle}</h3>
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 12, flexShrink: 0 }}>
        <div style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: 200 }}>
          {user?.email ?? userId}
        </div>
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
