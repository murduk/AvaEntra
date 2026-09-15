import { NavLink, Route, Routes } from "react-router-dom";
import Overview from "./pages/Overview";
import Users from "./pages/Users";
import UserDetail from "./pages/UserDetail";
import Groups from "./pages/Groups";
import GroupDetail from "./pages/GroupDetail";
import Applications from "./pages/Applications";
import ApplicationDetail from "./pages/ApplicationDetail";
import Logs from "./pages/Logs";

export default function App() {
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
          <span className="muted">Directory</span>
          <a href="http://localhost:5100/login" target="_blank" rel="noreferrer">Open sign-in</a>
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
