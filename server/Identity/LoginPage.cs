using System.Net;
using AvaEntra.Server.Data;
using Microsoft.Extensions.Options;

namespace AvaEntra.Server.Identity;

public static class LoginPage
{
    public static string Render(
        DirectorySnapshot dir,
        AvaEntraOptions options,
        string action,
        string? error = null,
        string? loginHint = null)
    {
        var users = dir.Users.Where(u => u.Enabled).OrderBy(u => u.DisplayName).ToList();
        var userButtons = options.AllowPasswordlessDevLogin
            ? string.Join("", users.Select(u => $"""
                <button type="submit" name="userId" value="{u.Id}" class="chip">
                  <span class="avatar">{WebUtility.HtmlEncode(Initials(u.DisplayName))}</span>
                  <span>
                    <strong>{WebUtility.HtmlEncode(u.DisplayName)}</strong>
                    <small>{WebUtility.HtmlEncode(u.UserPrincipalName)}</small>
                  </span>
                </button>
                """))
            : "";

        var picker = options.AllowPasswordlessDevLogin
            ? $"""
               <p class="muted">Local development — sign in as</p>
               <div class="chips">{userButtons}</div>
               <div class="or">or use a password</div>
               """
            : "";

        var err = string.IsNullOrEmpty(error) ? "" : $"<div class=\"error\">{WebUtility.HtmlEncode(error)}</div>";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <title>Sign in · AvaEntra</title>
              <style>
                :root { --bg:#0e1a2b; --card:#fff; --line:#d7e0ea; --text:#1b2430; --muted:#5b6b7c; --accent:#0f6cbd; --danger:#c50f1f; }
                * { box-sizing: border-box; }
                body { margin:0; min-height:100vh; font: 15px/1.45 "Segoe UI", system-ui, sans-serif; color:var(--text);
                  background: radial-gradient(1200px 500px at 50% -10%, #1d4e7a, var(--bg)); display:grid; place-items:center; padding:24px; }
                .card { width:min(440px, 100%); background:var(--card); border-radius:12px; padding:32px 28px; }
                .brand { display:flex; align-items:center; gap:10px; margin-bottom:8px; }
                .mark { width:28px; height:28px; border-radius:6px; background:var(--accent); color:#fff; display:grid; place-items:center; font-weight:700; }
                h1 { font-size:22px; font-weight:600; margin:12px 0 4px; }
                .sub { color:var(--muted); margin-bottom:20px; }
                label { display:block; font-size:12px; font-weight:600; margin:12px 0 6px; }
                input { width:100%; border:1px solid var(--line); border-radius:6px; padding:10px 12px; font: inherit; }
                input:focus { outline:2px solid #b4d6f5; border-color:var(--accent); }
                .primary { width:100%; margin-top:16px; background:var(--accent); color:#fff; border:0; border-radius:6px; padding:11px; font: inherit; font-weight:600; cursor:pointer; }
                .error { background:#fde7e9; color:var(--danger); padding:10px 12px; border-radius:6px; margin-bottom:12px; }
                .muted { color:var(--muted); font-size:13px; margin:0 0 8px; }
                .chips { display:flex; flex-direction:column; gap:8px; margin-bottom:16px; }
                .chip { display:flex; gap:10px; align-items:center; text-align:left; border:1px solid var(--line); background:#f7fafc; border-radius:8px; padding:8px 10px; cursor:pointer; font: inherit; }
                .chip:hover { border-color:var(--accent); background:#f0f7fc; }
                .chip small { display:block; color:var(--muted); }
                .avatar { width:32px; height:32px; border-radius:50%; background:#0f6cbd; color:#fff; display:grid; place-items:center; font-size:12px; font-weight:700; }
                .or { text-align:center; color:var(--muted); font-size:12px; margin: 4px 0 8px; }
              </style>
            </head>
            <body>
              <form class="card" method="post" action="{{action}}">
                <button type="submit" hidden>Sign in</button>
                <div class="brand"><div class="mark">A</div><strong>AvaEntra</strong></div>
                <h1>Sign in</h1>
                <p class="sub">{{WebUtility.HtmlEncode(dir.Tenant.Name)}} · {{WebUtility.HtmlEncode(dir.Tenant.Domain)}}</p>
                {{err}}
                {{picker}}
                <label for="username">Email or username</label>
                <input id="username" name="username" value="{{WebUtility.HtmlEncode(loginHint ?? "")}}" autocomplete="username"/>
                <label for="password">Password</label>
                <input id="password" name="password" type="password" autocomplete="current-password"/>
                <button class="primary" type="submit">Sign in</button>
              </form>
            </body>
            </html>
            """;
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }
}
