import { Link } from "react-router-dom";

export default function NotFoundPage() {
  return (
    <div
      style={{
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
        background: "var(--bg-0)",
        color: "var(--text)",
        padding: 24,
      }}
    >
      <div
        style={{
          maxWidth: 420,
          textAlign: "center",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-md)",
          background: "var(--panel)",
          padding: 28,
        }}
      >
        <h1 style={{ margin: "0 0 8px 0", fontSize: 22 }}>Page not found</h1>
        <p style={{ margin: 0, color: "var(--muted)", lineHeight: 1.5 }}>
          That address does not exist in this app. Use the menu to go back, or open your dashboard.
        </p>
        <Link
          to="/"
          style={{
            display: "inline-block",
            marginTop: 20,
            padding: "10px 18px",
            borderRadius: "var(--radius-md)",
            background: "var(--color-ai-600)",
            color: "#fff",
            fontWeight: 700,
          }}
        >
          Go to dashboard
        </Link>
      </div>
    </div>
  );
}
