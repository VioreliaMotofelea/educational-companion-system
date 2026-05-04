import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const nextPath = (location.state as { from?: string } | null)?.from ?? "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (loading) return;
    setLoading(true);
    setError(null);

    try {
      await login({ email, password });
      navigate(nextPath, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not sign in.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", background: "var(--bg-0)", color: "var(--text)" }}>
      <form onSubmit={onSubmit} style={{ width: 360, border: "1px solid var(--border)", borderRadius: "var(--radius-md)", background: "var(--panel)", padding: 20, display: "flex", flexDirection: "column", gap: 12 }}>
        <h2 style={{ margin: 0 }}>Sign in</h2>
        <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          Email
          <input value={email} onChange={(e) => setEmail(e.target.value)} required type="email" style={{ padding: "10px 12px", borderRadius: "var(--radius-md)", border: "1px solid var(--border-strong)", background: "rgba(255,255,255,0.02)", color: "var(--text)" }} />
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          Password
          <input value={password} onChange={(e) => setPassword(e.target.value)} required type="password" style={{ padding: "10px 12px", borderRadius: "var(--radius-md)", border: "1px solid var(--border-strong)", background: "rgba(255,255,255,0.02)", color: "var(--text)" }} />
        </label>
        {error ? <p style={{ margin: 0, color: "rgba(239, 68, 68, 0.95)" }}>{error}</p> : null}
        <button type="submit" disabled={loading} style={{ background: "var(--color-ai-600)", color: "#fff", border: "none", borderRadius: "var(--radius-md)", padding: "10px 14px", cursor: loading ? "not-allowed" : "pointer", opacity: loading ? 0.7 : 1 }}>
          {loading ? "Signing in..." : "Sign in"}
        </button>
        <p style={{ margin: 0, color: "var(--muted)" }}>
          No account yet? <Link to="/register">Create one</Link>
        </p>
      </form>
    </div>
  );
}
