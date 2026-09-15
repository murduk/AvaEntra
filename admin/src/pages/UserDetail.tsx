import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api } from "../api";
import type { AppRole, Application, Group, User } from "../types";

export default function UserDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [user, setUser] = useState<User | null>(null);
  const [groups, setGroups] = useState<Group[]>([]);
  const [apps, setApps] = useState<Application[]>([]);
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load() {
    if (!id) return;
    const [u, g, a] = await Promise.all([
      api.get<User>(`/api/admin/users/${id}`),
      api.get<Group[]>("/api/admin/groups"),
      api.get<Application[]>("/api/admin/applications")
    ]);
    setUser(u);
    setGroups(g);
    setApps(a);
  }

  useEffect(() => { void load().catch((e: Error) => setError(e.message)); }, [id]);

  if (!user) return <p className="muted">{error ?? "Loading…"}</p>;

  async function save() {
    await api.put(`/api/admin/users/${user.id}`, user);
    await load();
  }

  async function toggleGroup(groupId: string, member: boolean) {
    const path = `/api/admin/users/${user.id}/groups/${groupId}`;
    if (member) await api.post(path);
    else await api.del(path);
    await load();
  }

  return (
    <>
      <p className="muted"><Link to="/users">Users</Link> / {user.displayName}</p>
      <div className="spread">
        <h1>{user.displayName}</h1>
        <div className="row">
          <button className="btn primary" onClick={() => void save()}>Save</button>
          <button className="btn danger" onClick={async () => { await api.del(`/api/admin/users/${user.id}`); navigate("/users"); }}>Delete</button>
        </div>
      </div>
      {error && <p className="error">{error}</p>}
      <div className="grid two">
        <div className="card">
          <h2>Profile</h2>
          <div className="field"><label>Display name</label><input value={user.displayName} onChange={(e) => setUser({ ...user, displayName: e.target.value })} /></div>
          <div className="field"><label>User principal name</label><input value={user.userPrincipalName} onChange={(e) => setUser({ ...user, userPrincipalName: e.target.value })} /></div>
          <div className="field"><label>Mail</label><input value={user.mail ?? ""} onChange={(e) => setUser({ ...user, mail: e.target.value })} /></div>
          <div className="field"><label>Given name</label><input value={user.givenName ?? ""} onChange={(e) => setUser({ ...user, givenName: e.target.value })} /></div>
          <div className="field"><label>Surname</label><input value={user.surname ?? ""} onChange={(e) => setUser({ ...user, surname: e.target.value })} /></div>
          <label className="row"><input type="checkbox" checked={user.enabled} onChange={(e) => setUser({ ...user, enabled: e.target.checked })} /> Enabled</label>
          <p className="muted mono">Object ID {user.id}</p>
        </div>
        <div className="card">
          <h2>Reset password</h2>
          <div className="field"><label>New password</label><input value={password} onChange={(e) => setPassword(e.target.value)} /></div>
          <button className="btn" onClick={async () => { await api.post(`/api/admin/users/${user.id}/password`, { password }); setPassword(""); }}>Update password</button>
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h2>Groups</h2>
        {groups.map((g) => {
          const member = user.groups.some((x) => x.id === g.id);
          return (
            <label key={g.id} className="row" style={{ marginBottom: 8 }}>
              <input type="checkbox" checked={member} onChange={(e) => void toggleGroup(g.id, e.target.checked)} />
              {g.displayName}
            </label>
          );
        })}
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h2>App roles</h2>
        <table>
          <thead><tr><th>Application</th><th>Role</th><th></th></tr></thead>
          <tbody>
            {(user.roles ?? []).map((r) => (
              <tr key={r.id}>
                <td>{r.app}</td>
                <td>{r.role}</td>
                <td><button className="btn small danger" onClick={async () => { await api.del(`/api/admin/users/${user.id}/roles/${r.id}`); await load(); }}>Remove</button></td>
              </tr>
            ))}
          </tbody>
        </table>
        <AssignRole apps={apps} onAssign={async (appRoleId) => { await api.post(`/api/admin/users/${user.id}/roles`, { appRoleId }); await load(); }} />
      </div>
    </>
  );
}

function AssignRole({ apps, onAssign }: { apps: Application[]; onAssign: (id: string) => void }) {
  const [appId, setAppId] = useState("");
  const [roleId, setRoleId] = useState("");
  const [roles, setRoles] = useState<AppRole[]>([]);

  async function pickApp(id: string) {
    setAppId(id);
    setRoleId("");
    if (!id) { setRoles([]); return; }
    const detail = await api.get<Application>(`/api/admin/applications/${id}`);
    setRoles(detail.roles ?? []);
  }

  return (
    <div className="row" style={{ marginTop: 12 }}>
      <select value={appId} onChange={(e) => void pickApp(e.target.value)}>
        <option value="">Application</option>
        {apps.map((a) => <option key={a.id} value={a.id}>{a.displayName}</option>)}
      </select>
      <select value={roleId} onChange={(e) => setRoleId(e.target.value)}>
        <option value="">Role</option>
        {roles.map((r) => <option key={r.id} value={r.id}>{r.value}</option>)}
      </select>
      <button className="btn" disabled={!roleId} onClick={() => onAssign(roleId)}>Assign</button>
    </div>
  );
}
