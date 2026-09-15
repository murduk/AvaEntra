import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api } from "../api";
import type { Application } from "../types";

export default function ApplicationDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [app, setApp] = useState<Application | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [redirect, setRedirect] = useState("http://localhost:3000");
  const [scope, setScope] = useState("access_as_user");
  const [role, setRole] = useState("Reader");
  const [secret, setSecret] = useState<string | null>(null);

  async function load() {
    if (!id) return;
    setApp(await api.get<Application>(`/api/admin/applications/${id}`));
  }
  useEffect(() => { void load().catch((e: Error) => setError(e.message)); }, [id]);
  if (!app) return <p className="muted">{error ?? "Loading…"}</p>;

  return (
    <>
      <p className="muted"><Link to="/applications">App registrations</Link> / {app.displayName}</p>
      <div className="spread">
        <h1>{app.displayName}</h1>
        <div className="row">
          <button className="btn primary" onClick={async () => { await api.put(`/api/admin/applications/${app.id}`, app); await load(); }}>Save</button>
          <button className="btn danger" onClick={async () => { await api.del(`/api/admin/applications/${app.id}`); navigate("/applications"); }}>Delete</button>
        </div>
      </div>
      {error && <p className="error">{error}</p>}

      <div className="grid two">
        <div className="card">
          <h2>Properties</h2>
          <div className="field"><label>Display name</label><input value={app.displayName} onChange={(e) => setApp({ ...app, displayName: e.target.value })} /></div>
          <div className="field">
            <label>Kind</label>
            <select value={app.kind} onChange={(e) => setApp({ ...app, kind: e.target.value })}>
              <option>Spa</option><option>Web</option><option>Api</option><option>Confidential</option>
            </select>
          </div>
          <div className="field"><label>Identifier URI</label><input value={app.identifierUri ?? ""} onChange={(e) => setApp({ ...app, identifierUri: e.target.value })} /></div>
          <div className="checks">
            <label><input type="checkbox" checked={app.isPublicClient} onChange={(e) => setApp({ ...app, isPublicClient: e.target.checked })} /> Public client / SPA</label>
            <label><input type="checkbox" checked={app.requirePkce} onChange={(e) => setApp({ ...app, requirePkce: e.target.checked })} /> Require PKCE</label>
            <label><input type="checkbox" checked={app.allowClientCredentials} onChange={(e) => setApp({ ...app, allowClientCredentials: e.target.checked })} /> Client credentials</label>
            <label><input type="checkbox" checked={app.allowOnBehalfOf} onChange={(e) => setApp({ ...app, allowOnBehalfOf: e.target.checked })} /> On-behalf-of</label>
          </div>
          <p className="muted">Client ID <span className="mono">{app.clientId}</span></p>
          <p className="muted">Object ID <span className="mono">{app.id}</span></p>
        </div>
        <div className="card">
          <h2>Certificates & secrets</h2>
          {secret && <div className="banner">Copy this secret now. It will not be shown again.<br /><span className="mono">{secret}</span></div>}
          <table>
            <thead><tr><th>Name</th><th>Hint</th><th></th></tr></thead>
            <tbody>
              {(app.secrets ?? []).map((s) => (
                <tr key={s.id}>
                  <td>{s.displayName}</td>
                  <td className="mono">***{s.hint}</td>
                  <td><button className="btn small danger" onClick={async () => { await api.del(`/api/admin/applications/${app.id}/secrets/${s.id}`); await load(); }}>Delete</button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <button className="btn" style={{ marginTop: 12 }} onClick={async () => {
            const created = await api.post<{ secret: string }>(`/api/admin/applications/${app.id}/secrets`, { displayName: "client secret" });
            setSecret(created.secret);
            await load();
          }}>New client secret</button>
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h2>Redirect URIs</h2>
        <table>
          <tbody>
            {(app.redirects ?? []).map((r) => (
              <tr key={r.id}>
                <td className="mono">{r.uri}</td>
                <td><span className="pill">{r.type}</span></td>
                <td><button className="btn small danger" onClick={async () => { await api.del(`/api/admin/applications/${app.id}/redirects/${r.id}`); await load(); }}>Remove</button></td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="row" style={{ marginTop: 12 }}>
          <input style={{ flex: 1 }} value={redirect} onChange={(e) => setRedirect(e.target.value)} />
          <button className="btn" onClick={async () => { await api.post(`/api/admin/applications/${app.id}/redirects`, { uri: redirect, type: app.isPublicClient ? "spa" : "web" }); setRedirect(""); await load(); }}>Add</button>
        </div>
      </div>

      <div className="grid two" style={{ marginTop: 16 }}>
        <div className="card">
          <h2>Expose an API</h2>
          <table>
            <tbody>
              {(app.scopes ?? []).map((s) => (
                <tr key={s.id}>
                  <td className="mono">{s.value}</td>
                  <td>{s.displayName}</td>
                  <td><button className="btn small danger" onClick={async () => { await api.del(`/api/admin/applications/${app.id}/scopes/${s.id}`); await load(); }}>Remove</button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="row" style={{ marginTop: 12 }}>
            <input value={scope} onChange={(e) => setScope(e.target.value)} placeholder="access_as_user" />
            <button className="btn" onClick={async () => { await api.post(`/api/admin/applications/${app.id}/scopes`, { value: scope, displayName: scope }); setScope(""); await load(); }}>Add scope</button>
          </div>
        </div>
        <div className="card">
          <h2>App roles</h2>
          <table>
            <tbody>
              {(app.roles ?? []).map((r) => (
                <tr key={r.id}>
                  <td className="mono">{r.value}</td>
                  <td>{r.displayName}</td>
                  <td><button className="btn small danger" onClick={async () => { await api.del(`/api/admin/applications/${app.id}/roles/${r.id}`); await load(); }}>Remove</button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="row" style={{ marginTop: 12 }}>
            <input value={role} onChange={(e) => setRole(e.target.value)} placeholder="Reader" />
            <button className="btn" onClick={async () => { await api.post(`/api/admin/applications/${app.id}/roles`, { value: role, displayName: role }); setRole(""); await load(); }}>Add role</button>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h2>Role assignments</h2>
        <table>
          <thead><tr><th>User</th><th>Role</th></tr></thead>
          <tbody>
            {(app.assignments ?? []).map((a) => (
              <tr key={a.id}><td>{a.user} <span className="muted">{a.upn}</span></td><td>{a.role}</td></tr>
            ))}
          </tbody>
        </table>
        <p className="muted">Assign roles from a user profile.</p>
      </div>
    </>
  );
}
