import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api";
import type { Group } from "../types";

export default function Groups() {
  const [groups, setGroups] = useState<Group[]>([]);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  function load() {
    api.get<Group[]>("/api/admin/groups").then(setGroups).catch((e: Error) => setError(e.message));
  }
  useEffect(load, []);

  return (
    <>
      <div className="spread">
        <h1>Groups</h1>
        <button className="btn primary" onClick={() => setOpen(true)}>New group</button>
      </div>
      {error && <p className="error">{error}</p>}
      <div className="card">
        <table>
          <thead><tr><th>Name</th><th>Description</th><th>Members</th></tr></thead>
          <tbody>
            {groups.map((g) => (
              <tr key={g.id} className="clickable" onClick={() => navigate(`/groups/${g.id}`)}>
                <td>{g.displayName}</td>
                <td className="muted">{g.description || "—"}</td>
                <td>{g.memberCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {open && (
        <CreateGroup onClose={() => setOpen(false)} onCreated={(id) => { setOpen(false); load(); navigate(`/groups/${id}`); }} />
      )}
    </>
  );
}

function CreateGroup({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const [form, setForm] = useState({ displayName: "", description: "" });
  const [error, setError] = useState<string | null>(null);
  async function save() {
    try {
      const created = await api.post<Group>("/api/admin/groups", form);
      onCreated(created.id);
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <div className="modal-back" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h2>New group</h2>
        {error && <p className="error">{error}</p>}
        <div className="field"><label>Name</label><input value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} /></div>
        <div className="field"><label>Description</label><input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
        <div className="row">
          <button className="btn primary" onClick={() => void save()}>Create</button>
          <button className="btn" onClick={onClose}>Cancel</button>
        </div>
      </div>
    </div>
  );
}
