import { useEffect, useState } from "react";
import { api } from "../api";
import type { SignInLog } from "../types";

export default function Logs() {
  const [logs, setLogs] = useState<SignInLog[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.get<SignInLog[]>("/api/admin/logs").then(setLogs).catch((e: Error) => setError(e.message));
  }, []);

  return (
    <>
      <h1>Sign-in logs</h1>
      {error && <p className="error">{error}</p>}
      <div className="card">
        <table>
          <thead>
            <tr><th>Time</th><th>Grant</th><th>User</th><th>Client</th><th>Audience</th><th>Result</th></tr>
          </thead>
          <tbody>
            {logs.map((l) => (
              <tr key={l.id}>
                <td className="muted">{new Date(l.at).toLocaleString()}</td>
                <td><span className="pill">{l.grantType}</span></td>
                <td>{l.user ?? "—"}</td>
                <td>{l.client ?? "—"}</td>
                <td className="mono">{l.audience ?? "—"}</td>
                <td>{l.success ? <span className="pill ok">Success</span> : <span className="pill bad">{l.error}</span>}</td>
              </tr>
            ))}
            {logs.length === 0 && <tr><td colSpan={6} className="muted">No sign-ins yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </>
  );
}
