import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api } from "../api";
import type { Group, User } from "../types";

export default function GroupDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [group, setGroup] = useState<Group | null>(null);
  const [users, setUsers] = useState<User[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    if (!id) return;
    const [g, u] = await Promise.all([
      api.get<Group>(`/api/admin/groups/${id}`),
      api.get<User[]>("/api/admin/users")
    ]);
    setGroup(g);
    setUsers(u);
  }

  useEffect(() => { void load().catch((e: Error) => setError(e.message)); }, [id]);
  if (!group) return <p className="muted">{error ?? "Loading…"}</p>;

  async function toggle(userId: string, member: boolean) {
    const path = `/api/admin/groups/${group.id}/members/${userId}`;
    if (member) await api.post(path);
    else await api.del(path);
    await load();
  }

  return (
    <>
      <p className="muted"><Link to="/groups">Groups</Link> / {group.displayName}</p>
      <div className="spread">
        <h1>{group.displayName}</h1>
        <div className="row">
          <button className="btn primary" onClick={async () => { await api.put(`/api/admin/groups/${group.id}`, group); await load(); }}>Save</button>
          <button className="btn danger" onClick={async () => { await api.del(`/api/admin/groups/${group.id}`); navigate("/groups"); }}>Delete</button>
        </div>
      </div>
      <div className="card">
        <div className="field"><label>Name</label><input value={group.displayName} onChange={(e) => setGroup({ ...group, displayName: e.target.value })} /></div>
        <div className="field"><label>Description</label><input value={group.description ?? ""} onChange={(e) => setGroup({ ...group, description: e.target.value })} /></div>
        <p className="muted mono">Object ID {group.id}</p>
      </div>
      <div className="card" style={{ marginTop: 16 }}>
        <h2>Members</h2>
        {users.map((u) => {
          const member = (group.members ?? []).some((m) => m.id === u.id);
          return (
            <label key={u.id} className="row" style={{ marginBottom: 8 }}>
              <input type="checkbox" checked={member} onChange={(e) => void toggle(u.id, e.target.checked)} />
              {u.displayName} <span className="muted">{u.userPrincipalName}</span>
            </label>
          );
        })}
      </div>
    </>
  );
}
