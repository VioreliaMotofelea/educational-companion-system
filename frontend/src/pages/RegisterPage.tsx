import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

export default function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [dailyMinutes, setDailyMinutes] = useState("60");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (loading) return;
    setLoading(true);
    setError(null);

    try {
      await register({
        email,
        password,
        dailyAvailableMinutes: Number(dailyMinutes),
      });
      navigate("/", { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create account.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", background: "var(--bg-0)", color: "var(--text)" }}>
      <form onSubmit={onSubmit} style={{ width: 380, border: "1px solid var(--border)", borderRadius: "var(--radius-md)", background: "var(--panel)", padding: 20, display: "flex", flexDirection: "column", gap: 12 }}>
        <h2 style={{ margin: 0 }}>Create account</h2>
        <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          Email
          <input value={email} onChange={(e) => setEmail(e.target.value)} required type="email" style={{ padding: "10px 12px", borderRadius: "var(--radius-md)", border: "1px solid var(--border-strong)", background: "rgba(255,255,255,0.02)", color: "var(--text)" }} />
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          Password (min 8, upper/lower/digit)
          <input value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} type="password" style={{ padding: "10px 12px", borderRadius: "var(--radius-md)", border: "1px solid var(--border-strong)", background: "rgba(255,255,255,0.02)", color: "var(--text)" }} />
        </label>
        <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          Daily available minutes
          <input value={dailyMinutes} onChange={(e) => setDailyMinutes(e.target.value)} required min={15} max={600} type="number" style={{ padding: "10px 12px", borderRadius: "var(--radius-md)", border: "1px solid var(--border-strong)", background: "rgba(255,255,255,0.02)", color: "var(--text)" }} />
        </label>
        {error ? <p style={{ margin: 0, color: "rgba(239, 68, 68, 0.95)" }}>{error}</p> : null}
        <button type="submit" disabled={loading} style={{ background: "var(--color-ai-600)", color: "#fff", border: "none", borderRadius: "var(--radius-md)", padding: "10px 14px", cursor: loading ? "not-allowed" : "pointer", opacity: loading ? 0.7 : 1 }}>
          {loading ? "Creating account..." : "Create account"}
        </button>
        <p style={{ margin: 0, color: "var(--muted)" }}>
          Already registered? <Link to="/login">Sign in</Link>
        </p>
      </form>
    </div>
  );
}
