import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api";
import type { User } from "../types";

export default function Users() {
  const [users, setUsers] = useState<User[]>([]);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  function load() {
    api.get<User[]>("/api/admin/users").then(setUsers).catch((e: Error) => setError(e.message));
  }

  useEffect(load, []);

  return (
    <>
      <div className="spread">
        <h1>Users</h1>
        <button className="btn primary" onClick={() => setOpen(true)}>New user</button>
      </div>
      {error && <p className="error">{error}</p>}
      <div className="card">
        <table>
          <thead>
            <tr><th>Name</th><th>UPN</th><th>Groups</th><th>Status</th></tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id} className="clickable" onClick={() => navigate(`/users/${u.id}`)}>
                <td>{u.displayName}</td>
                <td className="mono">{u.userPrincipalName}</td>
                <td>{u.groups.map((g) => g.displayName).join(", ") || "—"}</td>
                <td>{u.enabled ? <span className="pill ok">Enabled</span> : <span className="pill off">Disabled</span>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {open && <CreateUser onClose={() => setOpen(false)} onCreated={(id) => { setOpen(false); load(); navigate(`/users/${id}`); }} />}
    </>
  );
}

function CreateUser({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const [form, setForm] = useState({ displayName: "", userPrincipalName: "", password: "Passw0rd!" });
  const [error, setError] = useState<string | null>(null);

  async function save() {
    try {
      const created = await api.post<User>("/api/admin/users", form);
      onCreated(created.id);
    } catch (e) {
      setError((e as Error).message);
    }
  }

  return (
    <div className="modal-back" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h2>New user</h2>
        {error && <p className="error">{error}</p>}
        <div className="field"><label>Display name</label><input value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} /></div>
        <div className="field"><label>User principal name</label><input value={form.userPrincipalName} onChange={(e) => setForm({ ...form, userPrincipalName: e.target.value })} placeholder="user@avaentra.local" /></div>
        <div className="field"><label>Password</label><input value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></div>
        <div className="row">
          <button className="btn primary" onClick={() => void save()}>Create</button>
          <button className="btn" onClick={onClose}>Cancel</button>
        </div>
      </div>
    </div>
  );
}
