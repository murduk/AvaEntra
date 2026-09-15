import { FormEvent, useState } from "react";
import { api } from "../api";

type Props = {
  onLoggedIn: (username: string) => void;
};

export default function Login({ onLoggedIn }: Props) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const result = await api.post<{ username: string }>("/api/admin/login", { username, password });
      onLoggedIn(result.username);
    } catch (err) {
      setError((err as Error).message || "Invalid username or password.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-screen">
      <form className="login-card" onSubmit={(e) => void submit(e)}>
        <div className="brand" style={{ marginBottom: 8 }}>
          <div className="mark">A</div>
          <strong>AvaEntra</strong>
        </div>
        <h1>Admin sign in</h1>
        <p className="muted">Management UI credentials (not directory user passwords).</p>
        {error && <p className="error">{error}</p>}
        <div className="field">
          <label htmlFor="admin-user">Username</label>
          <input id="admin-user" autoComplete="username" value={username} onChange={(e) => setUsername(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="admin-pass">Password</label>
          <input id="admin-pass" type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>
        <button className="btn primary" style={{ width: "100%", marginTop: 8 }} disabled={busy || !username || !password}>
          {busy ? "Signing in…" : "Sign in"}
        </button>
      </form>
    </div>
  );
}
