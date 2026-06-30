import { useState } from "react";
import { useMediaQuery } from "../../hooks/useMediaQuery";
import Sidebar from "./Sidebar";
import Topbar from "./Topbar";

type Props = {
  children: React.ReactNode;
};

export default function AppLayout({ children }: Props) {
  const narrow = useMediaQuery("(max-width: 900px)");
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  const drawerOpen = narrow && mobileNavOpen;

  return (
    <div
      style={{
        display: "flex",
        minHeight: "100vh",
        width: "100%",
        background: "var(--bg-0)",
      }}
    >
      {drawerOpen ? (
        <button
          type="button"
          aria-label="Close menu"
          onClick={() => setMobileNavOpen(false)}
          style={{
            position: "fixed",
            inset: 0,
            zIndex: 1090,
            border: "none",
            margin: 0,
            padding: 0,
            background: "rgba(4, 8, 18, 0.55)",
            cursor: "pointer",
          }}
        />
      ) : null}
      <div style={{ width: narrow ? 0 : 260, flexShrink: 0, position: "relative" }}>
        <Sidebar narrow={narrow} open={drawerOpen} onNavigate={() => setMobileNavOpen(false)} />
      </div>
      <div style={{ flex: 1, display: "flex", flexDirection: "column", minWidth: 0 }}>
        <Topbar narrow={narrow} onOpenMenu={() => setMobileNavOpen(true)} />
        <main style={{ padding: narrow ? "16px 14px" : "20px", flex: 1 }}>{children}</main>
      </div>
    </div>
  );
}
