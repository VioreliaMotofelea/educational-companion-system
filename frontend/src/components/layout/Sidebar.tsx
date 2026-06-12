import type { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import "./sidebar.css";

type Props = {
  narrow?: boolean;
  open?: boolean;
  onNavigate?: () => void;
};

function IconBrand() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
      <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
    </svg>
  );
}

function IconDashboard() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
      <polyline points="9 22 9 12 15 12 15 22" />
    </svg>
  );
}

function IconRecommendations() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
      <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
      <path d="M8 7h8M8 11h6" />
    </svg>
  );
}

function IconTasks() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M9 11l3 3L22 4" />
      <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
    </svg>
  );
}

function IconCalendar() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
      <line x1="16" y1="2" x2="16" y2="6" />
      <line x1="8" y1="2" x2="8" y2="6" />
      <line x1="3" y1="10" x2="21" y2="10" />
    </svg>
  );
}

function IconProfile() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
      <circle cx="12" cy="7" r="4" />
    </svg>
  );
}

type NavItem = {
  to: string;
  end?: boolean;
  label: string;
  icon: ReactNode;
};

const navItems: NavItem[] = [
  { to: "/", end: true, label: "Dashboard", icon: <IconDashboard /> },
  { to: "/recommendations", label: "Recommendations", icon: <IconRecommendations /> },
  { to: "/tasks", label: "Tasks", icon: <IconTasks /> },
  { to: "/calendar", label: "Calendar", icon: <IconCalendar /> },
  { to: "/profile", label: "Profile", icon: <IconProfile /> },
  { to: "/demo/ingestion", label: "Ingestion demo", icon: <IconRecommendations /> },
];

export default function Sidebar({ narrow = false, open = false, onNavigate }: Props) {
  const rootClass = ["app-sidebar", narrow ? "app-sidebar--drawer" : "", narrow && open ? "app-sidebar--open" : ""]
    .filter(Boolean)
    .join(" ");

  return (
    <aside className={rootClass} aria-label="Main navigation">
      <div className="app-sidebar__brand">
        <div className="app-sidebar__mark" aria-hidden>
          <IconBrand />
        </div>
        <div className="app-sidebar__titles">
          <h2 className="app-sidebar__title">AI Companion</h2>
          <p className="app-sidebar__tagline">Personal learning assistant</p>
        </div>
      </div>

      <nav className="app-sidebar__nav">
        {navItems.map(({ to, end, label, icon }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            className={({ isActive }) => `sidebar-link${isActive ? " sidebar-link--active" : ""}`}
            onClick={() => onNavigate?.()}
          >
            {icon}
            <span>{label}</span>
          </NavLink>
        ))}
      </nav>

      <div className="app-sidebar__footer">
        <p className="app-sidebar__footer-panel">Your progress is saved when you complete or rate a resource.</p>
      </div>
    </aside>
  );
}
