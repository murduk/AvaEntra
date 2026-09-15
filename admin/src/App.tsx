import { useEffect, useState } from "react";
import { NavLink, Route, Routes } from "react-router-dom";
import { api } from "./api";
import Overview from "./pages/Overview";
import Users from "./pages/Users";
import UserDetail from "./pages/UserDetail";
import Groups from "./pages/Groups";
import GroupDetail from "./pages/GroupDetail";
import Applications from "./pages/Applications";
import ApplicationDetail from "./pages/ApplicationDetail";
import Logs from "./pages/Logs";
import Login from "./pages/Login";

export default function App() {
  const [username, setUsername] = useState<string | null>(null);
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    api.get<{ username: string }>("/api/admin/me")
      .then((me) => setUsername(me.username))
      .catch(() => setUsername(null))
      .finally(() => setChecking(false));
  }, []);

  async function logout() {
    await api.post("/api/admin/logout");
    setUsername(null);
  }

  if (checking) {
    return <div className="login-screen"><p className="muted">Loading…</p></div>;
  }

  if (!username) {
    return <Login onLoggedIn={setUsername} />;
  }

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="mark">A</div>
          <div>
            <strong>AvaEntra</strong>
            <small>Local identity</small>
          </div>
        </div>
        <NavLink to="/" end className={({ isActive }) => "nav-link" + (isActive ? " active" : "")}>Overview</NavLink>
        <NavLink to="/users" className={({ isActive }) => "nav-link" + (isActive ? " active" : "")}>Users</NavLink>
        <NavLink to="/groups" className={({ isActive }) => "nav-link" + (isActive ? " active" : "")}>Groups</NavLink>
        <NavLink to="/applications" className={({ isActive }) => "nav-link" + (isActive ? " active" : "")}>App registrations</NavLink>
        <NavLink to="/logs" className={({ isActive }) => "nav-link" + (isActive ? " active" : "")}>Sign-in logs</NavLink>
        <div className="sidebar-foot">Local Entra-compatible IDP. Do not expose to the internet.</div>
      </aside>
      <div className="main">
        <div className="topbar">
          <span className="muted">Signed in as {username}</span>
          <div className="row">
            <a href="/login" target="_blank" rel="noreferrer">Open IdP sign-in</a>
            <button className="btn small" onClick={() => void logout()}>Sign out</button>
          </div>
        </div>
        <div className="content">
          <Routes>
            <Route path="/" element={<Overview />} />
            <Route path="/users" element={<Users />} />
            <Route path="/users/:id" element={<UserDetail />} />
            <Route path="/groups" element={<Groups />} />
            <Route path="/groups/:id" element={<GroupDetail />} />
            <Route path="/applications" element={<Applications />} />
            <Route path="/applications/:id" element={<ApplicationDetail />} />
            <Route path="/logs" element={<Logs />} />
          </Routes>
        </div>
      </div>
    </div>
  );
}
