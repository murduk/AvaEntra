import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api";
import type { Application } from "../types";

export default function Applications() {
  const [apps, setApps] = useState<Application[]>([]);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  function load() {
    api.get<Application[]>("/api/admin/applications").then(setApps).catch((e: Error) => setError(e.message));
  }
  useEffect(load, []);

  return (
    <>
      <div className="spread">
        <h1>App registrations</h1>
        <button className="btn primary" onClick={() => setOpen(true)}>New registration</button>
      </div>
      {error && <p className="error">{error}</p>}
      <div className="card">
        <table>
          <thead><tr><th>Name</th><th>Kind</th><th>Client ID</th><th>Audience</th></tr></thead>
          <tbody>
            {apps.map((a) => (
              <tr key={a.id} className="clickable" onClick={() => navigate(`/applications/${a.id}`)}>
                <td>{a.displayName}</td>
                <td><span className="pill">{a.kind}</span></td>
                <td className="mono">{a.clientId}</td>
                <td className="mono">{a.identifierUri || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {open && <CreateApp onClose={() => setOpen(false)} onCreated={(id) => { setOpen(false); load(); navigate(`/applications/${id}`); }} />}
    </>
  );
}

function CreateApp({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const [form, setForm] = useState({ displayName: "", kind: "Spa", identifierUri: "" });
  const [error, setError] = useState<string | null>(null);
  async function save() {
    try {
      const created = await api.post<Application>("/api/admin/applications", form);
      onCreated(created.id);
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <div className="modal-back" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h2>New app registration</h2>
        {error && <p className="error">{error}</p>}
        <div className="field"><label>Name</label><input value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} /></div>
        <div className="field">
          <label>Kind</label>
          <select value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })}>
            <option>Spa</option>
            <option>Web</option>
            <option>Api</option>
            <option>Confidential</option>
          </select>
        </div>
        <div className="field"><label>Identifier URI (APIs)</label><input value={form.identifierUri} onChange={(e) => setForm({ ...form, identifierUri: e.target.value })} placeholder="api://my-api" /></div>
        <div className="row">
          <button className="btn primary" onClick={() => void save()}>Create</button>
          <button className="btn" onClick={onClose}>Cancel</button>
        </div>
      </div>
    </div>
  );
}
