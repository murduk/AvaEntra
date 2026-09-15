import { useEffect, useState } from "react";
import { api } from "../api";
import type { Overview as OverviewData } from "../types";

export default function Overview() {
  const [data, setData] = useState<OverviewData | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  useEffect(() => {
    api.get<OverviewData>("/api/admin/overview").then(setData).catch((e: Error) => setError(e.message));
  }, []);

  if (error) return <p className="error">{error}</p>;
  if (!data) return <p className="muted">Loading directory…</p>;

  const msal = `const msalConfig = {
  auth: {
    clientId: "${data.seed.spaClientId}",
    authority: "${data.authority}",
    knownAuthorities: ${JSON.stringify(data.msalBrowser.auth.knownAuthorities)},
    redirectUri: "http://localhost:3000"
  }
};

const loginRequest = {
  scopes: ["openid", "profile", "offline_access", "${data.seed.sampleScope}"]
};`;

  const identityWeb = `"AzureAd": {
  "Instance": "${data.origin}/",
  "TenantId": "${data.tenant.id}",
  "ClientId": "${data.seed.apiClientId}",
  "Audience": "${data.seed.apiIdentifier}"
}`;

  const obo = `POST ${data.token}
grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer
client_id=${data.seed.backendClientId}
client_secret=${data.seed.backendSecret}
assertion=<user access token>
requested_token_use=on_behalf_of
scope=${data.seed.sampleScope}`;

  function copy(label: string, value: string) {
    void navigator.clipboard.writeText(value);
    setCopied(label);
    setTimeout(() => setCopied(null), 1500);
  }

  return (
    <>
      <div className="spread">
        <div>
          <h1>{data.tenant.name}</h1>
          <p className="muted">{data.tenant.domain} · {data.tenant.id}</p>
        </div>
        <a className="btn primary" href={`${data.authorize}?client_id=${data.seed.spaClientId}&response_type=code&redirect_uri=${encodeURIComponent(data.origin + "/dev/callback")}&scope=${encodeURIComponent("openid profile offline_access " + data.seed.sampleScope)}&code_challenge=devchallenge&code_challenge_method=plain`}>
          Try sign-in
        </a>
      </div>

      <div className="banner">
        Local identity provider for development. Seed password for all sample users is <strong>{data.seed.defaultPassword}</strong>.
      </div>

      <div className="grid stats">
        <div className="card stat"><div className="label">Users</div><div className="value">{data.counts.users}</div></div>
        <div className="card stat"><div className="label">Groups</div><div className="value">{data.counts.groups}</div></div>
        <div className="card stat"><div className="label">Applications</div><div className="value">{data.counts.applications}</div></div>
        <div className="card stat"><div className="label">Sign-ins</div><div className="value">{data.counts.logs}</div></div>
      </div>

      <div className="grid two" style={{ marginTop: 16 }}>
        <div className="card">
          <h2>Endpoints</h2>
          <table>
            <tbody>
              <Row label="Authority" value={data.authority} onCopy={() => copy("authority", data.authority)} />
              <Row label="Issuer" value={data.issuer} onCopy={() => copy("issuer", data.issuer)} />
              <Row label="Discovery" value={data.discovery} onCopy={() => copy("discovery", data.discovery)} />
              <Row label="Token" value={data.token} onCopy={() => copy("token", data.token)} />
              <Row label="JWKS" value={data.jwks} onCopy={() => copy("jwks", data.jwks)} />
            </tbody>
          </table>
          {copied && <p className="muted">Copied {copied}</p>}
        </div>
        <div className="card">
          <h2>Seed apps</h2>
          <p><span className="pill">SPA</span> {data.seed.spaClientId}</p>
          <p><span className="pill">API</span> {data.seed.apiIdentifier}</p>
          <p><span className="pill">Backend</span> {data.seed.backendClientId}</p>
          <p className="muted">Confidential client secret: <span className="mono">{data.seed.backendSecret}</span></p>
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <div className="spread"><h2>MSAL.js (SPA + PKCE)</h2><button className="btn small" onClick={() => copy("msal", msal)}>Copy</button></div>
        <pre className="code">{msal}</pre>
      </div>
      <div className="card" style={{ marginTop: 16 }}>
        <div className="spread"><h2>Microsoft.Identity.Web</h2><button className="btn small" onClick={() => copy("idweb", identityWeb)}>Copy</button></div>
        <pre className="code">{identityWeb}</pre>
      </div>
      <div className="card" style={{ marginTop: 16 }}>
        <div className="spread"><h2>On-behalf-of</h2><button className="btn small" onClick={() => copy("obo", obo)}>Copy</button></div>
        <pre className="code">{obo}</pre>
      </div>
    </>
  );
}

function Row({ label, value, onCopy }: { label: string; value: string; onCopy: () => void }) {
  return (
    <tr>
      <th style={{ width: 110 }}>{label}</th>
      <td className="mono">{value}</td>
      <td style={{ width: 70 }}><button className="btn small" onClick={onCopy}>Copy</button></td>
    </tr>
  );
}
