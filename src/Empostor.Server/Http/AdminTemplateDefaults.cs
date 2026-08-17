using System;
using System.IO;

namespace Empostor.Server.Http;

internal static class AdminTemplateDefaults
{
    private static readonly string PagesDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages");

    internal static string PagesDirectory => PagesDir;

    internal static void EnsureCreated()
    {
        try
        {
            Directory.CreateDirectory(PagesDir);
            var loginPath = Path.Combine(PagesDir, "login.html");
            var adminPath = Path.Combine(PagesDir, "admin.html");
            if (!File.Exists(loginPath))
            {
                File.WriteAllText(loginPath, LoginHtml);
            }

            if (!File.Exists(adminPath))
            {
                File.WriteAllText(adminPath, AdminHtml);
            }
        }
        catch
        {

        }
    }

    internal static string LoginHtml { get; } = """
<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width,initial-scale=1">
    <title>Empostor Admin</title>
    <style>
        :root {
            --bg: #0d1117;
            --s: #161b22;
            --b: #30363d;
            --t: #e6edf3;
            --m: #7d8590;
            --a: #2f81f7;
            --r: #f85149
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0
        }

        body {
            background: var(--bg);
            color: var(--t);
            font: 14px/1.5 'Segoe UI', system-ui, sans-serif;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center
        }

        .card {
            background: var(--s);
            border: 1px solid var(--b);
            border-radius: 12px;
            padding: 36px 40px;
            width: 340px
        }

        h1 {
            font-size: 18px;
            font-weight: 700;
            margin-bottom: 24px;
            text-align: center;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px
        }

        h1 svg {
            color: var(--a);
            flex-shrink: 0
        }

        label {
            display: block;
            font-size: 12px;
            color: var(--m);
            margin-bottom: 5px
        }

        input {
            width: 100%;
            background: #0d1117;
            border: 1px solid var(--b);
            border-radius: 6px;
            color: var(--t);
            padding: 9px 12px;
            font-size: 14px;
            outline: none;
            margin-bottom: 14px
        }

        input:focus {
            border-color: var(--a)
        }

        button {
            width: 100%;
            background: var(--a);
            color: #fff;
            border: none;
            border-radius: 6px;
            padding: 10px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer
        }

        button:hover {
            opacity: .88
        }
    </style>
</head>

<body>
    <div class="card">
        <h1><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg><span data-i18n="login.title">Empostor Admin</span></h1>
        <form method="POST" action="/admin/login"><label data-i18n="login.password">Password</label><input type="password" name="password"
                autofocus data-i18n-placeholder="login.placeholder" placeholder="Enter admin password"><button type="submit" data-i18n="login.signin">Sign in</button><!--ERR--></form>
    </div>
    <script>
        fetch('/api/admin/strings').then(r => r.json()).then(s => {
            document.querySelectorAll('[data-i18n]').forEach(el => { const k = el.getAttribute('data-i18n'); if (s[k]) el.textContent = s[k]; });
            document.querySelectorAll('[data-i18n-placeholder]').forEach(el => { const k = el.getAttribute('data-i18n-placeholder'); if (s[k]) el.placeholder = s[k]; });
        }).catch(() => {});
    </script>
</body>

</html>
""";

    internal static string AdminHtml { get; } = """
<!DOCTYPE html>
<html lang="en-US">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width,initial-scale=1">
    <title>Empostor Admin</title>
    <style>
        :root {
            --bg: #ffffff;
            --s: #f6f8fa;
            --b: #d0d7de;
            --t: #1f2328;
            --m: #57606a;
            --a: #0969da;
            --g: #1a7f37;
            --y: #9a6700;
            --r: #cf222e;
            --p: #8250df;
            --o: #bc4c00;
            --field: #ffffff;
            --hover: rgba(31, 35, 40, .05);
            --on-a: #ffffff
        }

        [data-theme="dark"] {
            --bg: #0d1117;
            --s: #161b22;
            --b: #30363d;
            --t: #e6edf3;
            --m: #7d8590;
            --a: #2f81f7;
            --g: #3fb950;
            --y: #d29922;
            --r: #f85149;
            --p: #bc8cff;
            --o: #ffa657;
            --field: #0d1117;
            --hover: rgba(255, 255, 255, .04);
            --on-a: #ffffff
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0
        }

        body {
            background: var(--bg);
            color: var(--t);
            font: 14px/1.5 'Segoe UI', system-ui, sans-serif;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            transition: background-color .18s ease, color .18s ease
        }

        header,
        nav,
        .sc,
        .form,
        input,
        select,
        textarea,
        button,
        table,
        td,
        th {
            transition: background-color .18s ease, border-color .18s ease, color .18s ease
        }

        header {
            background: var(--s);
            border-bottom: 1px solid var(--b);
            padding: 0 20px;
            display: flex;
            align-items: center;
            gap: 12px;
            height: 52px;
            position: sticky;
            top: 0;
            z-index: 100
        }

        header h1 {
            font-size: 15px;
            font-weight: 700;
            display: flex;
            align-items: center;
            gap: 6px
        }

        .dot {
            width: 8px;
            height: 8px;
            border-radius: 50%;
            background: var(--g);
            box-shadow: 0 0 6px var(--g);
            flex-shrink: 0
        }

        .sp {
            flex: 1
        }

        #upd {
            font-size: 11px;
            color: var(--m)
        }

        .logout {
            padding: 5px 12px;
            background: rgba(248, 81, 73, .15);
            color: var(--r);
            border: 1px solid rgba(248, 81, 73, .3);
            border-radius: 6px;
            font-size: 12px;
            cursor: pointer;
            text-decoration: none
        }

        .theme-sel {
            width: auto;
            min-width: 120px;
            background: var(--field);
            border: 1px solid var(--b);
            border-radius: 6px;
            color: var(--t);
            padding: 5px 8px;
            font-size: 12px;
            outline: none;
            cursor: pointer
        }

        .theme-toggle {
            padding: 5px 9px;
            background: var(--s);
            color: var(--t);
            border: 1px solid var(--b);
            border-radius: 6px;
            cursor: pointer;
            font-size: 15px;
            line-height: 1;
            transition: transform .25s ease
        }

        .theme-toggle:hover {
            transform: rotate(18deg) scale(1.08)
        }

        .aw-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 16px
        }

        .aw-text {
            font-size: 13px;
            margin-bottom: 8px
        }

        .aw-text.mono {
            font-family: monospace
        }

        .aw-desc {
            font-size: 12px;
            color: var(--m);
            margin-bottom: 10px
        }

        .aw-children {
            display: flex;
            flex-direction: column;
            gap: 10px
        }

        .aw-toggle {
            display: flex;
            align-items: center;
            gap: 8px;
            cursor: pointer;
            font-size: 13px;
            margin-bottom: 8px
        }

        .aw-toggle input {
            width: auto
        }

        .aw-chips .chips-list {
            display: flex;
            flex-wrap: wrap;
            gap: 6px;
            margin-bottom: 10px
        }

        .aw-chips .chip {
            display: inline-flex;
            align-items: center;
            gap: 4px;
            background: var(--b);
            color: var(--t);
            padding: 3px 8px;
            border-radius: 12px;
            font-size: 13px
        }

        .aw-chips .chip button {
            background: none;
            border: none;
            color: var(--m);
            cursor: pointer;
            font-size: 14px;
            padding: 0;
            line-height: 1
        }

        .aw-divider {
            border: none;
            border-top: 1px solid var(--b);
            margin: 12px 0
        }

        td.mono {
            font-family: monospace
        }

        .tone-muted {
            color: var(--m)
        }

        .tone-success {
            color: var(--g)
        }

        .tone-danger {
            color: var(--r)
        }

        .tone-warning {
            color: var(--y)
        }

        .tone-accent {
            color: var(--a)
        }

        main {
            display: flex;
            flex: 1
        }

        nav {
            width: 210px;
            background: var(--s);
            border-right: 1px solid var(--b);
            padding: 12px 0;
            flex-shrink: 0;
            position: sticky;
            top: 52px;
            height: calc(100vh - 52px);
            overflow-y: auto
        }

        .ni {
            display: flex;
            align-items: center;
            gap: 10px;
            padding: 9px 16px;
            cursor: pointer;
            color: var(--m);
            font-size: 13px;
            border-left: 3px solid transparent;
            transition: all .15s
        }

        .ni svg {
            flex-shrink: 0;
            width: 16px;
            height: 16px
        }

        .ni:hover {
            color: var(--t);
            background: var(--hover)
        }

        .ni.active {
            color: var(--a);
            border-left-color: var(--a);
            background: rgba(47, 129, 247, .08)
        }

        .nsep {
            margin: 8px 16px;
            border-top: 1px solid var(--b)
        }

        .nlbl {
            padding: 8px 16px 4px;
            font-size: 11px;
            color: var(--m);
            text-transform: uppercase;
            letter-spacing: .5px
        }

        ct {
            flex: 1;
            padding: 20px;
            overflow: hidden
        }

        .pnl {
            display: none
        }

        .pnl.active {
            display: block
        }

        .sr {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
            gap: 10px;
            margin-bottom: 20px
        }

        .sc {
            background: var(--s);
            border: 1px solid var(--b);
            border-radius: 8px;
            padding: 14px 18px
        }

        .sc .lbl {
            color: var(--m);
            font-size: 11px;
            text-transform: uppercase;
            letter-spacing: .5px;
            margin-bottom: 5px
        }

        .sc .val {
            font-size: 26px;
            font-weight: 700
        }

        .sc .sub {
            font-size: 11px;
            color: var(--m);
            margin-top: 3px
        }

        h2 {
            font-size: 14px;
            font-weight: 600;
            margin-bottom: 14px;
            color: var(--m);
            text-transform: uppercase;
            letter-spacing: .5px;
            display: flex;
            align-items: center;
            gap: 6px
        }

        h2 svg {
            flex-shrink: 0;
            width: 16px;
            height: 16px
        }

        table {
            width: 100%;
            border-collapse: collapse
        }

        th {
            text-align: left;
            padding: 7px 10px;
            color: var(--m);
            font-size: 11px;
            text-transform: uppercase;
            letter-spacing: .5px;
            border-bottom: 1px solid var(--b);
            font-weight: 500
        }

        td {
            padding: 9px 10px;
            border-bottom: 1px solid var(--b);
            vertical-align: top
        }

        tr:hover td {
            background: var(--hover)
        }

        .code {
            font-family: monospace;
            color: var(--a);
            font-weight: 700;
            letter-spacing: 1px
        }

        .fc {
            color: var(--p);
            font-size: 11px
        }

        .ip {
            color: var(--m);
            font-size: 11px;
            font-family: monospace
        }

        .badge {
            display: inline-flex;
            align-items: center;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 11px;
            font-weight: 600
        }

        .bs {
            background: rgba(63, 185, 80, .15);
            color: var(--g)
        }

        .bn {
            background: rgba(48, 54, 61, .8);
            color: var(--m)
        }

        .by {
            background: rgba(210, 153, 34, .2);
            color: var(--y)
        }

        .be {
            background: rgba(248, 81, 73, .15);
            color: var(--r)
        }

        .bpub {
            background: rgba(63, 185, 80, .12);
            color: var(--g)
        }

        .bprv {
            background: rgba(125, 133, 144, .12);
            color: var(--m)
        }

        .chips {
            display: flex;
            flex-wrap: wrap;
            gap: 3px
        }

        .chip {
            background: rgba(47, 129, 247, .1);
            border: 1px solid rgba(47, 129, 247, .2);
            border-radius: 20px;
            padding: 1px 8px;
            font-size: 11px;
            color: var(--a)
        }

        .chip.host {
            background: rgba(255, 166, 87, .1);
            border-color: rgba(255, 166, 87, .25);
            color: var(--o)
        }

        .form {
            background: var(--s);
            border: 1px solid var(--b);
            border-radius: 8px;
            padding: 16px;
            margin-bottom: 16px
        }

        .form h3 {
            font-size: 13px;
            font-weight: 600;
            margin-bottom: 12px;
            color: var(--t);
            display: flex;
            align-items: center;
            gap: 6px
        }

        .form h3 svg {
            flex-shrink: 0;
            width: 16px;
            height: 16px
        }

        .field {
            margin-bottom: 10px
        }

        .field label {
            display: block;
            font-size: 12px;
            color: var(--m);
            margin-bottom: 4px
        }

        input,
        select,
        textarea {
            width: 100%;
            background: var(--field);
            border: 1px solid var(--b);
            border-radius: 6px;
            color: var(--t);
            padding: 7px 10px;
            font-size: 13px;
            outline: none;
            font-family: inherit
        }

        input:focus,
        select:focus,
        textarea:focus {
            border-color: var(--a)
        }

        textarea {
            resize: vertical;
            min-height: 60px
        }

        .row {
            display: flex;
            gap: 8px
        }

        .row input,
        .row select {
            flex: 1
        }

        button {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 7px 14px;
            border-radius: 6px;
            font-size: 13px;
            font-weight: 500;
            cursor: pointer;
            border: none;
            transition: opacity .15s
        }

        button:hover {
            opacity: .85
        }

        .bp {
            background: var(--a);
            color: #fff
        }

        .bd {
            background: var(--r);
            color: #fff
        }

        .bw {
            background: var(--y);
            color: #000
        }

        .bsm {
            padding: 4px 10px;
            font-size: 12px
        }

        .msg {
            padding: 8px 12px;
            border-radius: 6px;
            font-size: 12px;
            margin-top: 8px;
            display: none
        }

        .msg.ok {
            background: rgba(63, 185, 80, .15);
            color: var(--g);
            border: 1px solid rgba(63, 185, 80, .3)
        }

        .msg.err {
            background: rgba(248, 81, 73, .12);
            color: var(--r);
            border: 1px solid rgba(248, 81, 73, .3)
        }

        .empty {
            text-align: center;
            padding: 40px;
            color: var(--m)
        }

        .ig {
            display: grid;
            grid-template-columns: 200px 1fr;
            gap: 0;
            background: var(--s);
            border: 1px solid var(--b);
            border-radius: 8px;
            overflow: hidden
        }

        .ik,
        .iv {
            padding: 8px 14px;
            border-bottom: 1px solid var(--b)
        }

        .ik {
            color: var(--m);
            font-size: 12px
        }

        .iv {
            font-family: monospace;
            font-size: 12px
        }

        .bi {
            display: flex;
            align-items: center;
            gap: 10px;
            padding: 8px 12px;
            background: var(--s);
            border: 1px solid var(--b);
            border-radius: 6px;
            margin-bottom: 6px
        }

        .bv {
            flex: 1;
            font-family: monospace;
            font-size: 13px
        }

        .br2 {
            font-size: 11px;
            color: var(--m)
        }

        .bt {
            font-size: 11px;
            color: var(--m);
            margin-left: auto
        }

        .ver-sel {
            width: auto;
            min-width: 200px;
            padding: 3px 6px;
            font-size: 11px;
            background: var(--field);
            border: 1px solid var(--b);
            border-radius: 4px;
            color: var(--t);
            margin-top: 4px
        }

        .lang-btn {
            padding: 3px 10px;
            background: rgba(188, 140, 255, .12);
            color: var(--p);
            border: 1px solid rgba(188, 140, 255, .25);
            border-radius: 4px;
            font-size: 11px;
            cursor: pointer;
            margin: 4px 12px 0
        }
    </style>
</head>

<body>
    <header>
        <div class="dot" id="dot"></div>
        <h1><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--a)" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg><span data-i18n="login.title">Empostor Admin</span></h1><span class="sp"></span><span id="upd"></span>
        <select id="theme-sel" class="theme-sel" title="Theme" onchange="selectTheme(this.value)"></select>
        <button id="theme-toggle" class="theme-toggle" type="button" title="Toggle light/dark" onclick="toggleMode()" aria-label="Toggle light/dark">🌙</button>
        <form method="POST" action="/admin/logout" style="margin:0"><button class="logout" type="submit" data-i18n="header.signout">Sign out</button></form>
    </header>
    <main>
        <nav>
            <div class="nlbl" data-i18n="nav.monitor">Monitor</div>
            <div class="ni active" onclick="nav('ov')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 20V10M12 20V4M6 20v-6"/></svg><span data-i18n="nav.overview">Overview</span></div>
            <div class="ni" onclick="nav('gm')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="6" width="20" height="12" rx="2"/><path d="M6 12h4M14 12h4M12 10v4"/></svg><span data-i18n="nav.games">Games</span></div>
            <div class="ni" onclick="nav('cl')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="7" r="4"/><path d="M1 20v-2a4 4 0 014-4h8a4 4 0 014 4v2"/><circle cx="17" cy="7" r="4"/><path d="M23 20v-2a4 4 0 00-3-3.87"/></svg><span data-i18n="nav.clients">Clients</span></div>
            <div class="ni" onclick="nav('pl')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M8 7h8M8 11h8M8 15h5"/></svg><span data-i18n="nav.player_logs">Player Logs</span></div>
            <div class="ni" onclick="nav('hp')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z"/></svg><span data-i18n="nav.hplp">HPLP</span></div>
            <div class="nsep"></div>
            <div class="nlbl" data-i18n="nav.actions">Actions</div>
            <div class="ni" onclick="nav('bc')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 2H7a2 2 0 00-2 2v16l5-3 5 3V4a2 2 0 00-2-2z"/><path d="M10 9h4M10 13h4"/></svg><span data-i18n="nav.broadcast">Broadcast</span></div>
            <div class="ni" onclick="nav('ki')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M13 5h3l1 4H7l1-4h3V3h2v2zM5 9h14v10a2 2 0 01-2 2H7a2 2 0 01-2-2V9zM10 13v4"/></svg><span data-i18n="nav.kick">Kick</span></div>
            <div class="ni" onclick="nav('ba')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14.7 6.3a1 1 0 000 1.4l1.6 1.6a1 1 0 001.4 0l3.77-3.77a6 6 0 01-7.94 7.94L5.62 21a2 2 0 01-2.83-2.83l7.91-7.91a6 6 0 017.94-7.94l-3.76 3.76z"/></svg><span data-i18n="nav.ban">Ban</span></div>
            <div class="ni" onclick="nav('bl')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M8 8h8M8 12h8M8 16h5"/></svg><span data-i18n="nav.banlist">Ban List</span></div>
            <div class="nsep"></div>
            <div class="nlbl" data-i18n="nav.gamecontrol">Game Control</div>
            <div class="ni" onclick="nav('ms')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/></svg><span data-i18n="nav.message">Message</span></div>
            <div class="ni" onclick="nav('ge')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M15 9l-6 6M9 9l6 6"/></svg><span data-i18n="nav.endgame">End Game</span></div>
            <div class="ni" onclick="nav('gp')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10M12 2a15.3 15.3 0 00-4 10 15.3 15.3 0 004 10"/></svg><span data-i18n="nav.privacy">Privacy</span></div>
            <div class="nsep"></div>
            <div class="nlbl" data-i18n="nav.extend">Extend</div>
            <div class="ni" onclick="nav('mk')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18.5 5.5h-13A2.5 2.5 0 003 8v8a2.5 2.5 0 002.5 2.5h13A2.5 2.5 0 0021 16V8a2.5 2.5 0 00-2.5-2.5z"/><path d="M12 3v2M12 19v2M3 12h2M19 12h2"/></svg><span data-i18n="nav.marketplace">Marketplace</span></div>
            <div class="ni" onclick="nav('ud')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="1 4 1 10 7 10"/><polyline points="23 20 23 14 17 14"/><path d="M20.49 9A9 9 0 005.64 5.64L1 10m22 4l-4.64 4.36A9 9 0 013.51 15"/></svg><span data-i18n="nav.updates">Updates</span></div>
            <div class="nsep"></div>
            <div class="nlbl" data-i18n="nav.system">System</div>
            <div class="ni" onclick="nav('rp')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="2" width="18" height="20" rx="2"/><path d="M8 8h8M8 12h8M8 16h5"/></svg><span data-i18n="nav.reports">Reports</span></div>
            <div class="ni" onclick="nav('si')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z"/></svg><span data-i18n="nav.serverinfo">Server Info</span></div>
            <div class="nsep"></div>
            <div class="nlbl" data-i18n="nav.plugins">Plugins</div>
            <div id="plugins-nav"></div>
            <button class="lang-btn" onclick="reloadLang()"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10"/></svg><span data-i18n="nav.reload_lang">Reload Lang</span></button>
        </nav>
        <ct>
            <div id="p-ov" class="pnl active">
                <div class="sr">
                    <div class="sc">
                        <div class="lbl" data-i18n="status.games">Games</div>
                        <div class="val" id="s1">—</div>
                        <div class="sub" id="s1b"></div>
                    </div>
                    <div class="sc">
                        <div class="lbl" data-i18n="status.active">Active</div>
                        <div class="val" id="s2">—</div>
                    </div>
                    <div class="sc">
                        <div class="lbl" data-i18n="status.players">Players</div>
                        <div class="val" id="s3">—</div>
                    </div>
                    <div class="sc">
                        <div class="lbl" data-i18n="status.bans">Bans</div>
                        <div class="val" id="s4">—</div>
                        <div class="sub" id="s4b"></div>
                    </div>
                    <div class="sc">
                        <div class="lbl" data-i18n="status.uptime">Uptime</div>
                        <div class="val" id="s5" style="font-size:16px">—</div>
                    </div>
                </div>
                <h2 data-i18n="status.active_games">Active Games</h2>
                <table>
                    <thead>
                        <tr>
                            <th data-i18n="table.code">Code</th>
                            <th data-i18n="table.state">State</th>
                            <th data-i18n="table.visibility">Visibility</th>
                            <th data-i18n="table.map">Map</th>
                            <th data-i18n="table.players">Players</th>
                            <th data-i18n="table.host">Host</th>
                        </tr>
                    </thead>
                    <tbody id="ov-t">
                        <tr>
                            <td colspan="6" class="empty">Loading...</td>
                        </tr>
                    </tbody>
                </table>
            </div>
            <div id="p-gm" class="pnl">
                <h2 data-i18n="nav.games">All Games</h2>
                <table>
                    <thead>
                        <tr>
                            <th data-i18n="table.code">Code</th>
                            <th data-i18n="table.state">State</th>
                            <th data-i18n="table.visibility">Visibility</th>
                            <th data-i18n="table.map">Map</th>
                            <th data-i18n="table.players">Players</th>
                            <th data-i18n="table.host_fc">Host / FC</th>
                            <th data-i18n="table.members">Members</th>
                        </tr>
                    </thead>
                    <tbody id="gm-t"></tbody>
                </table>
            </div>
            <div id="p-cl" class="pnl">
                <h2 data-i18n="nav.clients">Clients</h2>
                <table>
                    <thead>
                        <tr>
                            <th data-i18n="table.id">ID</th>
                            <th data-i18n="table.name">Name</th>
                            <th data-i18n="table.friend_code">Friend Code</th>
                            <th data-i18n="table.ip">IP</th>
                            <th data-i18n="table.version">Version</th>
                            <th data-i18n="table.platform">Platform</th>
                            <th data-i18n="table.mods">Mods</th>
                            <th data-i18n="table.in_game">In Game</th>
                        </tr>
                    </thead>
                    <tbody id="cl-t"></tbody>
                </table>
            </div>
            <div id="p-bc" class="pnl">
                <div class="form">
                    <h3><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 2H7a2 2 0 00-2 2v16l5-3 5 3V4a2 2 0 00-2-2z"/><path d="M10 9h4M10 13h4"/></svg><span data-i18n="broadcast.title">Broadcast to All Games</span></h3>
                    <div class="field"><label data-i18n="broadcast.placeholder">Message...</label><textarea id="bc-m" data-i18n-placeholder="broadcast.placeholder" placeholder="Message..."></textarea></div>
                    <button class="bp" onclick="doBc()"><span data-i18n="broadcast.send">Send to All Games</span></button>
                    <div id="bc-r" class="msg"></div>
                </div>
            </div>
            <div id="p-ki" class="pnl">
                <div class="form">
                    <h3><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M13 5h3l1 4H7l1-4h3V3h2v2zM5 9h14v10a2 2 0 01-2 2H7a2 2 0 01-2-2V9zM10 13v4"/></svg><span data-i18n="kick.title">Kick by Client ID</span></h3>
                    <div class="field"><label data-i18n="kick.placeholder">Client ID</label><input id="ki-id" type="number" data-i18n-placeholder="kick.placeholder" placeholder="Client ID"></div>
                    <div class="field"><label data-i18n="kick.reason_placeholder">Reason (optional)</label><input id="ki-reason" type="text" data-i18n-placeholder="kick.reason_placeholder" placeholder="Reason (optional)"></div>
                    <button class="bw" onclick="doKick()"><span data-i18n="kick.button">Kick</span></button>
                    <div id="ki-r" class="msg"></div>
                </div>
                <div class="form">
                    <h3 data-i18n="kick.quick">Quick Kick</h3>
                    <table>
                        <thead>
                            <tr>
                                <th data-i18n="table.id">ID</th>
                                <th data-i18n="table.name">Name</th>
                                <th data-i18n="table.friend_code">FC</th>
                                <th data-i18n="table.game">Game</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody id="ki-t"></tbody>
                    </table>
                </div>
            </div>
            <div id="p-ba" class="pnl">
                <div style="display:grid;grid-template-columns:1fr 1fr;gap:16px">
                    <div class="form">
                        <h3><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14.7 6.3a1 1 0 000 1.4l1.6 1.6a1 1 0 001.4 0l3.77-3.77a6 6 0 01-7.94 7.94L5.62 21a2 2 0 01-2.83-2.83l7.91-7.91a6 6 0 017.94-7.94l-3.76 3.76z"/></svg><span data-i18n="ban.title_ip">Ban IP</span></h3>
                        <div class="field"><label data-i18n="table.ip">IP</label><input id="bi-v" data-i18n-placeholder="ban.ip_placeholder" placeholder="1.2.3.4"></div>
                        <div class="field"><label data-i18n="table.reason">Reason</label><input id="bi-r" data-i18n-placeholder="ban.reason_placeholder" placeholder="Optional..."></div>
                        <button class="bd" onclick="doBanIp()"><span data-i18n="ban.button_ip">Ban IP</span></button>
                        <div id="bi-msg" class="msg"></div>
                    </div>
                    <div class="form">
                        <h3><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14.7 6.3a1 1 0 000 1.4l1.6 1.6a1 1 0 001.4 0l3.77-3.77a6 6 0 01-7.94 7.94L5.62 21a2 2 0 01-2.83-2.83l7.91-7.91a6 6 0 017.94-7.94l-3.76 3.76z"/></svg><span data-i18n="ban.title_fc">Ban Friend Code</span></h3>
                        <div class="field"><label data-i18n="table.friend_code">Friend Code</label><input id="bf-v" data-i18n-placeholder="ban.fc_placeholder" placeholder="Name#1234"></div>
                        <div class="field"><label data-i18n="table.reason">Reason</label><input id="bf-r" data-i18n-placeholder="ban.reason_placeholder" placeholder="Optional..."></div>
                        <button class="bd" onclick="doBanFc()"><span data-i18n="ban.button_fc">Ban FC</span></button>
                        <div id="bf-msg" class="msg"></div>
                    </div>
                </div>
            </div>
            <div id="p-bl" class="pnl">
                <div style="display:grid;grid-template-columns:1fr 1fr;gap:16px">
                    <div>
                        <h2 data-i18n="ban.banned_ips">Banned IPs</h2>
                        <div id="bl-ip"></div>
                    </div>
                    <div>
                        <h2 data-i18n="ban.banned_fcs">Banned Friend Codes</h2>
                        <div id="bl-fc"></div>
                    </div>
                </div>
            </div>
            <div id="p-ms" class="pnl">
                <div class="form">
                    <h3><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/></svg><span data-i18n="message.title">Message to Game</span></h3>
                    <div class="row">
                        <div class="field" style="flex:0 0 140px"><label data-i18n="table.code">Code</label><input id="ms-c" data-i18n-placeholder="message.code_placeholder" placeholder="ABCDEF" style="text-transform:uppercase"></div>
                        <div class="field" style="flex:1"><label data-i18n="table.name">Message</label><input id="ms-m" data-i18n-placeholder="message.msg_placeholder" placeholder="Message..."></div>
                    </div>
                    <button class="bp" onclick="doMsg()"><span data-i18n="message.send">Send</span></button>
                    <div id="ms-r" class="msg"></div>
                </div>
            </div>
            <div id="p-ge" class="pnl">
                <div class="form">
                    <h3><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M15 9l-6 6M9 9l6 6"/></svg><span data-i18n="endgame.title">Force End Game</span></h3>
                    <div class="field"><label data-i18n="table.code">Code</label><input id="ge-c" data-i18n-placeholder="endgame.code_placeholder" placeholder="ABCDEF" style="text-transform:uppercase"></div>
                    <div class="field"><label data-i18n="kick.reason_placeholder">Reason (optional)</label><input id="ge-reason" type="text" data-i18n-placeholder="kick.reason_placeholder" placeholder="Reason (optional)"></div>
                    <button class="bd" onclick="doEnd()"><span data-i18n="endgame.button">End Game</span></button>
                    <div id="ge-r" class="msg"></div>
                </div>
                <div class="form">
                    <h3 data-i18n="endgame.active_games">Active Games</h3>
                    <table>
                        <thead>
                            <tr>
                                <th data-i18n="table.code">Code</th>
                                <th data-i18n="table.state">State</th>
                                <th data-i18n="table.players">Players</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody id="ge-t"></tbody>
                    </table>
                </div>
            </div>
            <div id="p-gp" class="pnl">
                <div class="form">
                    <h3><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10M12 2a15.3 15.3 0 00-4 10 15.3 15.3 0 004 10"/></svg><span data-i18n="privacy.title">Set Game Privacy</span></h3>
                    <div class="row">
                        <div class="field" style="flex:0 0 140px"><label data-i18n="table.code">Code</label><input id="gp-c" data-i18n-placeholder="privacy.code_placeholder" placeholder="ABCDEF" style="text-transform:uppercase"></div>
                        <div class="field" style="flex:0 0 140px"><label data-i18n="table.visibility">Visibility</label><select id="gp-v">
                                <option value="true" data-i18n="privacy.public">Public</option>
                                <option value="false" data-i18n="privacy.private">Private</option>
                            </select></div>
                    </div>
                    <button class="bp" onclick="doPrivacy()"><span data-i18n="privacy.apply">Apply</span></button>
                    <div id="gp-r" class="msg"></div>
                </div>
            </div>
            <div id="p-mk" class="pnl">
                <div style="display:flex;align-items:center;gap:10px;margin-bottom:16px">
                    <h2 style="margin:0"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18.5 5.5h-13A2.5 2.5 0 003 8v8a2.5 2.5 0 002.5 2.5h13A2.5 2.5 0 0021 16V8a2.5 2.5 0 00-2.5-2.5z"/><path d="M12 3v2M12 19v2M3 12h2M19 12h2"/></svg><span data-i18n="marketplace.title">Plugin Marketplace</span></h2><button class="bp bsm" onclick="fMarket()"><span data-i18n="marketplace.refresh">Refresh</span></button>
                </div>
                <div id="mk-list">
                    <div class="empty">Loading...</div>
                </div>
            </div>
            <div id="p-ud" class="pnl">
                <div class="form" style="max-width:480px">
                    <h3><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="1 4 1 10 7 10"/><polyline points="23 20 23 14 17 14"/><path d="M20.49 9A9 9 0 005.64 5.64L1 10m22 4l-4.64 4.36A9 9 0 013.51 15"/></svg><span data-i18n="updates.title">Server Update Check</span></h3>
                    <div id="ud-box">
                        <div class="empty" data-i18n="updates.click_check">Click Check to query GitHub.</div>
                    </div>
                    <button class="bp" style="margin-top:12px" onclick="fUpdate()"><span data-i18n="updates.check">Check for Updates</span></button>
                </div>
            </div>
            <div id="p-rp" class="pnl">
                <div style="display:flex;align-items:center;gap:10px;margin-bottom:14px">
                    <h2 style="margin:0"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="2" width="18" height="20" rx="2"/><path d="M8 8h8M8 12h8M8 16h5"/></svg><span data-i18n="reports.title">Player Reports</span></h2>
                    <button class="bp bsm" onclick="fReports()"><span data-i18n="reports.refresh">Refresh</span></button>
                </div>
                <table>
                    <thead>
                        <tr>
                            <th data-i18n="table.time">Time</th>
                            <th data-i18n="table.game">Game</th>
                            <th data-i18n="table.reporter">Reporter</th>
                            <th data-i18n="table.reported">Reported</th>
                            <th data-i18n="table.reason">Reason</th>
                            <th data-i18n="table.outcome">Outcome</th>
                        </tr>
                    </thead>
                    <tbody id="rp-t">
                        <tr>
                            <td colspan="6" class="empty">Loading...</td>
                        </tr>
                    </tbody>
                </table>
            </div>
            <div id="p-si" class="pnl">
                <h2><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z"/></svg><span data-i18n="serverinfo.title">Server Info</span></h2>
                <div class="ig" id="si-d"></div>
            </div>
            <div id="p-pl" class="pnl">
                <div style="display:flex;align-items:center;gap:10px;margin-bottom:14px;flex-wrap:wrap">
                    <h2 style="margin:0"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M8 7h8M8 11h8M8 15h5"/></svg><span data-i18n="player_logs.title">Player Logs</span></h2>
                    <select id="pl-client" onchange="document.getElementById('pl-type').value='';fPlayerLogs()" style="width:auto;min-width:180px">
                        <option value="" data-i18n="player_logs.select_client">Select a player...</option>
                    </select>
                    <select id="pl-type" onchange="fPlayerLogs()" style="width:auto">
                        <option value="" data-i18n="player_logs.all_types">All types</option>
                    </select>
                    <button class="bp bsm" onclick="fPlayerLogs()"><span data-i18n="player_logs.refresh">Refresh</span></button>
                    <button class="bsm" style="background:rgba(188,140,255,.12);color:var(--p);border:1px solid rgba(188,140,255,.25)" onclick="exportLogs()"><span data-i18n="player_logs.export">Export JSON</span></button>
                    <select id="pl-clear-range" style="width:auto;min-width:160px">
                        <option value="all" data-i18n="player_logs.clear_all">All logs</option>
                        <option value="1h" data-i18n="player_logs.clear_1h">Older than 1 hour</option>
                        <option value="24h" data-i18n="player_logs.clear_24h">Older than 24 hours</option>
                        <option value="7d" data-i18n="player_logs.clear_7d">Older than 7 days</option>
                        <option value="30d" data-i18n="player_logs.clear_30d">Older than 30 days</option>
                    </select>
                    <button class="bd bsm" onclick="clearLogs()"><span data-i18n="player_logs.clear">Clear Logs</span></button>
                </div>
                <table>
                    <thead>
                        <tr>
                            <th data-i18n="table.time">Time</th>
                            <th data-i18n="table.type">Type</th>
                            <th data-i18n="table.player">Player</th>
                            <th data-i18n="table.game">Game</th>
                            <th data-i18n="table.detail">Detail</th>
                        </tr>
                    </thead>
                    <tbody id="pl-t">
                        <tr><td colspan="5" class="empty">Loading...</td></tr>
                    </tbody>
                </table>
            </div>
            <div id="p-hp" class="pnl">
                <div class="form">
                    <h2 style="margin:0"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z"/></svg><span data-i18n="hplp.title">HPLP (Public Lobby List)</span></h2>
                    <button class="bp bsm" onclick="saveHplpSettings()"><span data-i18n="hplp.save">Save Settings</span></button>
                    <div style="margin-top:12px">
                        <label style="display:flex;align-items:center;gap:6px;cursor:pointer">
                            <input type="checkbox" id="hp-enabled" onchange="saveHplpSettings()">
                            <span data-i18n="hplp.enabled">Enable HPLP endpoint (GET /x-api/games)</span>
                        </label>
                    </div>
                    <div class="field" style="margin-top:10px">
                        <label data-i18n="hplp.region_id">Region ID</label>
                        <input type="text" id="hp-region-id" data-i18n-placeholder="hplp.region_id_placeholder" placeholder="e.g. meu, default" onchange="saveHplpSettings()">
                    </div>
                    <div class="field" style="margin-top:10px">
                        <label data-i18n="hplp.region_name">Region Name</label>
                        <input type="text" id="hp-region-name" data-i18n-placeholder="hplp.region_name_placeholder" placeholder="e.g. Modded EU" onchange="saveHplpSettings()">
                    </div>
                    <div class="field" style="margin-top:10px">
                        <label data-i18n="hplp.public_url">Public URL (optional)</label>
                        <input type="text" id="hp-public-url" data-i18n-placeholder="hplp.public_url_placeholder" placeholder="Leave empty to auto-generate from server config" onchange="saveHplpSettings()">
                        <div style="font-size:11px;color:var(--m);margin-top:4px"><span data-i18n="hplp.public_url_desc">Used by Starlight clients to connect. Auto-generated as http://ip:port if empty.</span></div>
                    </div>
                </div>
                <div id="hp-r" class="msg"></div>
            </div>
        </ct>
    </main>
    <!-- Client Detail Modal -->
    <div id="cl-modal" style="display:none;position:fixed;inset:0;background:rgba(0,0,0,.7);z-index:200;align-items:center;justify-content:center">
        <div style="background:var(--s);border:1px solid var(--b);border-radius:10px;width:520px;max-width:95vw;max-height:85vh;overflow-y:auto;padding:20px">
            <div style="display:flex;align-items:center;margin-bottom:16px">
                <h2 style="margin:0;flex:1" id="cl-modal-title" data-i18n="clients.detail">Client Detail</h2>
                <button onclick="closeDetail()" style="background:none;border:none;color:var(--m);font-size:18px;cursor:pointer"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6L6 18M6 6l12 12"/></svg></button>
            </div>
            <div id="cl-modal-body"></div>
        </div>
    </div>
    <script>
        let _s = {};
        let cur = 'ov';
        function _(k, fb) { return _s[k] || fb || k; }
        function nav(id) {
            document.querySelectorAll('.ni').forEach(e => e.classList.remove('active'));
            event.currentTarget.classList.add('active');
            document.querySelectorAll('.pnl').forEach(e => e.classList.remove('active'));
            document.getElementById('p-' + id).classList.add('active');
            cur = id; refreshTab();
        }
        async function api(m, p, b) {
            const o = { method: m, headers: { 'Content-Type': 'application/json' } };
            if (b) o.body = JSON.stringify(b);
            const r = await fetch(p, o);
            if (r.status === 401) { location.reload(); return { ok: false, data: {} }; }
            return { ok: r.ok, data: await r.json() };
        }
        function msg(id, ok, t) {
            const e = document.getElementById(id);
            e.className = 'msg ' + (ok ? 'ok' : 'err');
            e.textContent = t; e.style.display = 'block';
            setTimeout(() => e.style.display = 'none', 4000);
        }
        function e(s) { return String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
        function sc(s) { return { Started: 'bs', NotStarted: 'bn', Starting: 'by', Ended: 'be' }[s] || 'bn'; }

        async function fetchStatus() {
            try {
                const { data: d } = await api('GET', '/api/admin/status');
                document.getElementById('s1').textContent = d.totalGames;
                document.getElementById('s1b').textContent = d.publicGames + ' ' + _('status.public', 'public');
                document.getElementById('s2').textContent = d.activeGames;
                document.getElementById('s3').textContent = d.totalPlayers;
                document.getElementById('s4').textContent = d.bannedIps + d.bannedFriendCodes;
                document.getElementById('s4b').textContent = d.bannedIps + ' IPs · ' + d.bannedFriendCodes + ' FCs';
                document.getElementById('s5').textContent = d.uptime;
                document.getElementById('upd').textContent = 'Updated ' + new Date().toLocaleTimeString();
                document.getElementById('dot').style.background = 'var(--g)';
                document.getElementById('si-d').innerHTML = [
                    [_('serverinfo.started', 'Started'), d.startTime],
                    [_('serverinfo.uptime', 'Uptime'), d.uptime],
                    [_('serverinfo.pid', 'PID'), d.pid],
                    [_('serverinfo.runtime', 'Runtime'), d.runtime],
                    [_('serverinfo.os', 'OS'), d.os],
                    [_('status.bans', 'Bans'), d.bannedIps + ' IPs, ' + d.bannedFriendCodes + ' FCs']
                ].map(([k, v]) => `<div class="ik">${e(k)}</div><div class="iv">${e(v)}</div>`).join('');
            } catch { document.getElementById('dot').style.background = 'var(--r)'; }
        }

        async function fGames(tid, short, state) {
            const url = '/api/admin/games' + (state ? '?state=' + state : '');
            const { data: gs } = await api('GET', url);
            const tb = document.getElementById(tid);
            if (!gs.length) {
                tb.innerHTML = `<tr><td colspan="${short ? 6 : 7}" class="empty">${_('games.none', 'No games')}</td></tr>`;
                return;
            }
            tb.innerHTML = gs.map(g => `<tr><td><span class="code">${e(g.code)}</span></td><td><span class="badge ${sc(g.state)}">${e(g.state)}</span></td><td><span class="badge ${g.isPublic ? 'bpub' : 'bprv'}">${g.isPublic ? _('games.public', 'Public') : _('games.private', 'Private')}</span></td><td>${e(g.map)}</td><td>${g.playerCount}/${g.maxPlayers}</td><td>${e(g.host)}<br><span class="fc">${e(g.hostFc)}</span></td>${short ? '' : '<td><div class="chips">' + g.players.map(p => `<span class="chip${p.isHost ? ' host' : ''}" title="${e(p.friendCode)}\n${e(p.ip)}">${e(p.name)}</span>`).join('') + '</div></td>'}</tr>`).join('');
        }

        async function fClients() {
            const { data: cs } = await api('GET', '/api/admin/clients');
            const tb = document.getElementById('cl-t');
            if (!cs.length) {
                tb.innerHTML = '<tr><td colspan="7" class="empty">' + _('clients.none', 'No clients') + '</td></tr>';
                return;
            }
            tb.innerHTML = cs.map(c => {
                let modsHtml = '<span style="color:var(--m)">—</span>';
                if (c.reactor && c.reactor.mods && c.reactor.mods.length) {
                    modsHtml = `<span style="font-size:11px;color:var(--p)" title="${e(c.reactor.mods.map(m => m.id + ' ' + m.version).join('\n'))}">${c.reactor.mods.length} ${_('clients.mod_count', 'mod(s)')}</span>`;
                }
                return `<tr><td style="color:var(--m)">${c.id}</td><td>${e(c.name)}</td><td><span class="fc">${e(c.friendCode)}</span></td><td><span class="ip">${e(c.ip)}</span></td><td>${e(c.gameVersion)}</td><td>${e(c.platform)}</td><td>${modsHtml}</td><td>${c.inGame ? `<span class="code">${e(c.gameCode)}</span>` : '<span style="color:var(--m)">' + _('clients.lobby', 'Lobby') + '</span>'}</td></tr>`;
            }).join('');
        }

        async function fKickList() {
            const { data: cs } = await api('GET', '/api/admin/clients');
            const tb = document.getElementById('ki-t');
            if (!cs.length) {
                tb.innerHTML = '<tr><td colspan="5" class="empty">' + _('clients.none', 'No clients') + '</td></tr>';
                return;
            }
            tb.innerHTML = cs.map(c => `<tr><td style="color:var(--m)">${c.id}</td><td>${e(c.name)}</td><td><span class="fc">${e(c.friendCode)}</span></td><td>${c.inGame ? `<span class="code">${e(c.gameCode)}</span>` : '—'}</td><td><button class="bw bsm" onclick="qkick(${c.id})">${_('kick.button', 'Kick')}</button></td></tr>`).join('');
        }

        async function fBans() {
            const { data: d } = await api('GET', '/api/admin/bans');
            document.getElementById('bl-ip').innerHTML = d.ips.length ? d.ips.map(b => bi(b, 'ip')).join('') : '<div class="empty">' + _('ban.none', 'None') + '</div>';
            document.getElementById('bl-fc').innerHTML = d.friendCodes.length ? d.friendCodes.map(b => bi(b, 'fc')).join('') : '<div class="empty">' + _('ban.none', 'None') + '</div>';
        }
        function bi(b, t) {
            return `<div class="bi"><div><div class="bv">${e(b.value)}</div><div class="br2">${e(b.reason)}</div></div><div class="bt">${new Date(b.bannedAt).toLocaleString()}</div><button class="bsm" style="background:rgba(248,81,73,.15);color:var(--r);border:1px solid rgba(248,81,73,.3)" onclick="doUnban('${t}','${e(b.value)}')">${_('ban.unban', 'Unban')}</button></div>`;
        }

        async function fGamesEnd() {
            const { data: gs } = await api('GET', '/api/admin/games');
            const tb = document.getElementById('ge-t');
            if (!gs.length) {
                tb.innerHTML = '<tr><td colspan="4" class="empty">' + _('games.none', 'No games') + '</td></tr>';
                return;
            }
            tb.innerHTML = gs.map(g => `<tr><td><span class="code">${e(g.code)}</span></td><td><span class="badge ${sc(g.state)}">${e(g.state)}</span></td><td>${g.playerCount}/${g.maxPlayers}</td><td><button class="bd bsm" onclick="qend('${e(g.code)}')">${_('endgame.end', 'End')}</button></td></tr>`).join('');
        }

        async function fMarket() {
            const el = document.getElementById('mk-list');
            el.innerHTML = '<div class="empty">' + _('marketplace.loading', 'Loading...') + '</div>';
            try {
                const { ok, data } = await api('GET', '/api/admin/marketplace/plugins');
                if (!ok) {
                    el.innerHTML = `<div class="empty" style="color:var(--r)">${e(data.error ?? 'Error')}</div>`;
                    return;
                }
                if (!data.length) {
                    el.innerHTML = '<div class="empty">' + _('marketplace.no_plugins', 'No plugins.') + '</div>';
                    return;
                }
                el.innerHTML = data.map(p => {
                    const versions = p.versions || [];
                    const verOpts = versions.map((v, i) =>
                        `<option value="${e(v.download_url)}" data-ver="${e(v.version)}" data-imp="${e(v.empostor_version)}">v${e(v.version)} (Empostor ${e(v.empostor_version)})</option>`
                    ).join('');
                    const latestVer = versions.length ? versions[versions.length - 1] : null;
                    return `<div class="form" style="margin-bottom:10px" data-url="${e(latestVer?.download_url ?? '')}">
                <div style="display:flex;align-items:flex-start;gap:12px">
                    <div style="flex:1">
                        <div style="font-weight:600;font-size:14px">${e(p.name)}</div>
                        <div style="font-size:12px;color:var(--m);margin:4px 0">${e(p.description)}</div>
                        <div style="font-size:12px;color:var(--m)">By ${e(p.author)}</div>
                        ${versions.length > 1
                            ? `<select class="ver-sel" id="ver-${e(p.id)}">${verOpts}</select>`
                            : `<div style="font-size:11px;color:var(--m);margin-top:4px">v${e(versions.length ? versions[versions.length - 1].version : '-')} (Empostor ${e(versions.length ? versions[versions.length - 1].empostor_version : '-')})</div>`
                        }
                    </div>
                    ${p.installed
                        ? `<button class="bp bsm" style="flex-shrink:0" disabled>${_('marketplace.installed', 'Installed')}</button>`
                        : `<button class="bp bsm" style="flex-shrink:0" onclick="install('${e(p.id)}',this)">${_('marketplace.install', 'Install')}</button>`
                    }
                </div>
                <div class="install-msg" style="font-size:12px;margin-top:6px;display:none"></div>
            </div>`;
                }).join('');
            } catch (err) { el.innerHTML = `<div class="empty" style="color:var(--r)">${e(String(err))}</div>`; }
        }

        async function install(pluginId, btn) {
            const verSel = document.getElementById('ver-' + pluginId);
            const url = verSel ? verSel.value : btn.closest('.form')?.getAttribute('data-url');
            btn.disabled = true;
            btn.textContent = _('marketplace.installing', 'Installing...');
            const msgEl = btn.closest('.form').querySelector('.install-msg');
            const { ok, data } = await api('POST', '/api/admin/marketplace/install', { downloadUrl: url, pluginId: pluginId });
            if (ok) {
                btn.textContent = _('marketplace.installed', 'Installed');
                msgEl.style.display = 'block';
                msgEl.style.color = 'var(--g)';
                msgEl.textContent = _('marketplace.restart', 'Installed. Restart server to enable.');
            } else {
                btn.disabled = false;
                btn.textContent = _('marketplace.install', 'Install');
                msgEl.style.display = 'block';
                msgEl.style.color = 'var(--r)';
                msgEl.textContent = (data.error ?? 'Error');
            }
        }

        async function fUpdate() {
            const box = document.getElementById('ud-box');
            box.innerHTML = '<div class="empty">' + _('updates.checking', 'Checking...') + '</div>';
            const { ok, data } = await api('GET', '/api/admin/update/check');
            if (!ok) { box.innerHTML = `<div class="empty" style="color:var(--r)">${e(data.error ?? 'Error')}</div>`; return; }
            const badge = data.upToDate
                ? '<span class="badge bs">' + _('updates.up_to_date', 'Up to date') + '</span>'
                : '<span class="badge be">' + _('updates.update_available', 'Update available') + '</span>';
            box.innerHTML = `<div class="ig" style="border-radius:6px;overflow:hidden">
                <div class="ik">${_('updates.current', 'Current')}</div><div class="iv">${e(data.currentVersion)}</div>
                <div class="ik">${_('updates.latest', 'Latest')}</div><div class="iv">${e(data.latestVersion)} ${badge}</div>
                <div class="ik">${_('updates.release', 'Release')}</div><div class="iv"><a href="${e(data.releaseUrl)}" target="_blank" style="color:var(--a)">${e(data.latestName)}</a></div>
            </div>${!data.upToDate ? '<p style="font-size:12px;color:var(--y);margin-top:10px">' + _('updates.update_hint', 'A new version is available. Update manually.') + '</p>' : ''}`;
        }

        async function fPlayerLogs() {
            const sel = document.getElementById('pl-client');
            const curVal = sel.value;
            const typeSel = document.getElementById('pl-type');
            const curType = typeSel.value;
            const tb = document.getElementById('pl-t');

            // populate client list if empty
            if (!sel.hasAttribute('data-loaded')) {
                try {
                    const { data: cls } = await api('GET', '/api/admin/player/logs/clients');
                    sel.innerHTML = '<option value="" data-i18n="player_logs.select_client">' + _('player_logs.select_client', 'Select a player...') + '</option>';
                    cls.forEach(c => { sel.innerHTML += `<option value="${c.clientId}">#${c.clientId} ${e(c.name)} (${e(c.friendCode)})</option>`; });
                    sel.value = curVal;
                    sel.setAttribute('data-loaded', '1');
                } catch { }
            }

            // populate type filter
            if (!typeSel.hasAttribute('data-loaded')) {
                const types = ['Chat','Report','Murder','Exile','Vote','Task','Vent','Meeting','Connect','Game','Join','Leave'];
                types.forEach(t => { typeSel.innerHTML += `<option value="${t}">${t}</option>`; });
                typeSel.value = curType;
                typeSel.setAttribute('data-loaded', '1');
            }

            const url = sel.value ? `/api/admin/player/logs?clientId=${sel.value}` : '/api/admin/player/logs';
            const { data: logs } = await api('GET', url);
            let items = logs;
            if (curType) items = items.filter(l => l.type === curType);
            if (!items.length) {
                tb.innerHTML = '<tr><td colspan="5" class="empty">' + _('player_logs.no_logs', 'No logs.') + '</td></tr>';
                return;
            }
            const typeColor = { Chat: 'var(--a)', Report: 'var(--r)', Murder: 'var(--r)', Exile: 'var(--o)', Vote: 'var(--y)', Task: 'var(--g)', Vent: 'var(--p)', Meeting: 'var(--m)', Connect: 'var(--g)', Game: 'var(--m)', Join: 'var(--g)', Leave: 'var(--o)' };
            tb.innerHTML = items.map(l => `<tr>
                <td style="font-size:11px;color:var(--m);white-space:nowrap">${e(l.time)}</td>
                <td><span style="color:${typeColor[l.type] ?? 'var(--t)'};font-weight:600;font-size:12px">${e(l.type)}</span></td>
                <td>${l.clientId ? '<b>' + e(l.playerName) + '</b><br><span class="fc">' + e(l.friendCode) + '</span>' : '—'}</td>
                <td>${l.gameCode && l.gameCode !== '—' ? '<span class="code" style="font-size:11px">' + e(l.gameCode) + '</span>' : '—'}</td>
                <td style="font-size:12px">${e(l.detail)}</td>
            </tr>`).join('');
        }

        function exportLogs() {
            const sel = document.getElementById('pl-client');
            const url = sel.value ? `/api/admin/player/logs/export?clientId=${sel.value}` : '/api/admin/player/logs/export';
            window.open(url, '_blank');
        }

        async function clearLogs() {
            const range = document.getElementById('pl-clear-range').value;
            const msgKey = range === 'all'
                ? _('player_logs.confirm_clear_all', 'Clear all player logs? This cannot be undone.')
                : _('player_logs.confirm_clear_range', 'Clear logs older than the selected period? This cannot be undone.');
            if (!confirm(msgKey)) return;
            const { ok, data } = await api('POST', `/api/admin/player/logs/clear?olderThan=${encodeURIComponent(range)}`);
            msg('bi-msg', ok, ok ? _('player_logs.cleared', 'Player logs cleared.') : (data.error ?? 'Error'));
            if (!ok) return;
            // Reset the client dropdown cache and table so the cleared state is
            // reflected immediately (no stale player list / logs remain).
            const sel = document.getElementById('pl-client');
            sel.removeAttribute('data-loaded');
            sel.value = '';
            document.getElementById('pl-type').value = '';
            fPlayerLogs();
        }

        async function fHplp() {
            const { data } = await api('GET', '/api/admin/hplp');
            document.getElementById('hp-enabled').checked = data.enabled;
            const regionIdEl = document.getElementById('hp-region-id');
            const regionNameEl = document.getElementById('hp-region-name');
            const publicUrlEl = document.getElementById('hp-public-url');
            if (document.activeElement !== regionIdEl) {
                regionIdEl.value = data.regionId || '';
            }
            if (document.activeElement !== regionNameEl) {
                regionNameEl.value = data.regionName || '';
            }
            if (document.activeElement !== publicUrlEl) {
                publicUrlEl.value = data.publicUrl || '';
            }
        }

        async function saveHplpSettings() {
            const { ok, data } = await api('POST', '/api/admin/hplp', {
                enabled: document.getElementById('hp-enabled').checked,
                regionId: document.getElementById('hp-region-id').value.trim(),
                regionName: document.getElementById('hp-region-name').value.trim(),
                publicUrl: document.getElementById('hp-public-url').value.trim()
            });
            if (ok) {
                msg('hp-r', true, _('hplp.saved', 'HPLP settings saved.'));
            } else {
                msg('hp-r', false, data.error ?? 'Error');
            }
        }

        function refreshTab() {
            if (cur === 'ov') fGames('ov-t', true, 'Started');
            if (cur === 'gm') fGames('gm-t', false);
            if (cur === 'cl') fClients();
            if (cur === 'ki') fKickList();
            if (cur === 'bl') fBans();
            if (cur === 'ge') fGamesEnd();
            if (cur === 'pl') fPlayerLogs();
            if (cur === 'hp') fHplp();
            if (cur.startsWith('ext-')) loadExtension(cur.slice(4));
        }

        async function doBc() {
            const m = document.getElementById('bc-m').value.trim();
            if (!m) return msg('bc-r', false, _('alert.message_required', 'Message required'));
            const { ok, data } = await api('POST', '/api/admin/broadcast', { message: m });
            msg('bc-r', ok, ok ? _('alert.sent_to', 'Sent to {0} game(s)').replace('{0}', data.sent) : (data.error ?? 'Error'));
        }

        async function doKick() {
            const id = parseInt(document.getElementById('ki-id').value);
            if (!id) return msg('ki-r', false, _('kick.placeholder', 'Enter client ID'));
            const reason = document.getElementById('ki-reason').value.trim();
            const { ok, data } = await api('POST', '/api/admin/kick', { clientId: id, reason: reason || undefined });
            msg('ki-r', ok, ok ? _('alert.kicked', 'Kicked {0}').replace('{0}', data.name) : (data.error ?? 'Error'));
            if (ok) fKickList();
        }

        async function qkick(id) {
            const { ok, data } = await api('POST', '/api/admin/kick', { clientId: id });
            if (!ok) alert(data.error ?? 'Error');
            fKickList();
        }

        async function doBanIp() {
            const v = document.getElementById('bi-v').value.trim(), r = document.getElementById('bi-r').value.trim();
            if (!v) return msg('bi-msg', false, _('alert.ip_required', 'IP required'));
            const { ok, data } = await api('POST', '/api/admin/ban/ip', { ip: v, reason: r });
            msg('bi-msg', ok, ok ? _('alert.banned', 'Banned {0} ({1} disconnected)').replace('{0}', data.banned).replace('{1}', data.disconnected) : (data.error ?? 'Error'));
        }

        async function doBanFc() {
            const v = document.getElementById('bf-v').value.trim(), r = document.getElementById('bf-r').value.trim();
            if (!v) return msg('bf-msg', false, _('alert.fc_required', 'FC required'));
            const { ok, data } = await api('POST', '/api/admin/ban/fc', { friendCode: v, reason: r });
            msg('bf-msg', ok, ok ? _('alert.banned', 'Banned {0} ({1} disconnected)').replace('{0}', data.banned).replace('{1}', data.disconnected) : (data.error ?? 'Error'));
        }

        async function doUnban(t, v) { await api('POST', `/api/admin/unban/${t}`, { value: v }); fBans(); }

        async function doMsg() {
            const c = document.getElementById('ms-c').value.trim().toUpperCase(), m = document.getElementById('ms-m').value.trim();
            if (!c || !m) return msg('ms-r', false, _('alert.both_required', 'Both required'));
            const { ok, data } = await api('POST', '/api/admin/message', { gameCode: c, message: m });
            msg('ms-r', ok, ok ? _('alert.sent', 'Sent') : (data.error ?? 'Error'));
        }

        async function doEnd() {
            const c = document.getElementById('ge-c').value.trim().toUpperCase();
            if (!c) return msg('ge-r', false, _('alert.code_required', 'Code required'));
            if (!confirm(`End game ${c}?`)) return;
            const reason = document.getElementById('ge-reason').value.trim();
            const { ok, data } = await api('POST', '/api/admin/game/end', { gameCode: c, reason: reason || undefined });
            msg('ge-r', ok, ok ? _('alert.ended', 'Ended ({0} kicked)').replace('{0}', data.playersKicked) : (data.error ?? 'Error'));
            if (ok) fGamesEnd();
        }

        async function qend(c) {
            if (!confirm(`End ${c}?`)) return;
            await api('POST', '/api/admin/game/end', { gameCode: c });
            fGamesEnd();
        }

        async function doPrivacy() {
            const c = document.getElementById('gp-c').value.trim().toUpperCase(),
                  p = document.getElementById('gp-v').value === 'true';
            if (!c) return msg('gp-r', false, _('alert.code_required', 'Code required'));
            const { ok, data } = await api('POST', '/api/admin/game/public', { gameCode: c, isPublic: p });
            msg('gp-r', ok, ok ? `${c} -> ${p ? _('privacy.public', 'public') : _('privacy.private', 'private')}` : (data.error ?? 'Error'));
        }

        async function fReports() {
            const { data: rs } = await api('GET', '/api/admin/reports');
            const tb = document.getElementById('rp-t');
            if (!rs.length) { tb.innerHTML = '<tr><td colspan="6" class="empty">' + _('reports.no_reports', 'No reports yet.') + '</td></tr>'; return; }
            const rColor = { Cheating_Hacking: 'var(--r)', Harassment_Misconduct: 'var(--y)', InappropriateName: 'var(--m)', InappropriateChat: 'var(--m)' };
            tb.innerHTML = rs.map(r => `<tr>
                <td style="font-size:11px;color:var(--m);white-space:nowrap">${e(r.time)}</td>
                <td><span class="code" style="font-size:11px">${e(r.gameCode)}</span></td>
                <td><b>${e(r.reporterName)}</b><br><span class="fc">${e(r.reporterFc)}</span></td>
                <td><b>${e(r.reportedName)}</b><br><span class="fc">${e(r.reportedFc)}</span></td>
                <td><span style="color:${rColor[r.reason] ?? 'var(--t)'};font-size:12px">${e(r.reason.replace('_', ' '))}</span></td>
                <td><span style="font-size:12px;color:${r.outcome === 'Reported' ? 'var(--g)' : 'var(--m)'}">${e(r.outcome)}</span></td>
            </tr>`).join('');
        }

        function showDetail(clientJson) {
            const c = JSON.parse(clientJson);
            document.getElementById('cl-modal-title').textContent = c.name + ' — ' + _('clients.detail', 'Detail');
            let body = `<div class="ig" style="border-radius:6px;overflow:hidden;margin-bottom:14px">
                <div class="ik">${_('clients.name_label', 'Name')}</div><div class="iv">${e(c.name)}</div>
                <div class="ik">${_('clients.fc_label', 'Friend Code')}</div><div class="iv">${e(c.friendCode)}</div>
                <div class="ik">${_('clients.ip_label', 'IP')}</div><div class="iv">${e(c.ip)}</div>
                <div class="ik">${_('clients.id_label', 'Client ID')}</div><div class="iv">${c.id}</div>
                <div class="ik">${_('clients.version_label', 'Version')}</div><div class="iv">${e(c.gameVersion)}</div>
                <div class="ik">${_('clients.platform_label', 'Platform')}</div><div class="iv">${e(c.platform)}</div>
                <div class="ik">${_('clients.language_label', 'Language')}</div><div class="iv">${e(c.language || '—')}</div>
                <div class="ik">${_('clients.ingame_label', 'In Game')}</div><div class="iv">${c.inGame ? '<span class="code">' + e(c.gameCode) + '</span>' : _('clients.lobby', 'No')}</div>
            </div>`;
            if (c.reactor) {
                body += `<h3 style="font-size:13px;color:var(--m);text-transform:uppercase;letter-spacing:.5px;margin-bottom:10px">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18.5 5.5h-13A2.5 2.5 0 003 8v8a2.5 2.5 0 002.5 2.5h13A2.5 2.5 0 0021 16V8a2.5 2.5 0 00-2.5-2.5z"/><path d="M12 3v2M12 19v2M3 12h2M19 12h2"/></svg> ${_('clients.reactor_mods', 'Reactor Mods')} (${c.reactor.mods.length})</h3>`;
                if (c.reactor.mods.length) {
                    body += `<table><thead><tr><th>${_('table.id', 'Mod ID')}</th><th>${_('table.version', 'Version')}</th><th>Required</th></tr></thead><tbody>`;
                    body += c.reactor.mods.map(m => `<tr><td style="font-family:monospace;font-size:12px">${e(m.id)}</td><td style="font-size:12px">${e(m.version)}</td><td style="font-size:12px">${m.required ? '<span style="color:var(--y)">' + _('reactor.required_yes', 'Yes') + '</span>' : _('reactor.required_no', 'No')}</td></tr>`).join('');
                    body += `</tbody></table>`;
                    body += `<div style="font-size:11px;color:var(--m);margin-top:6px">${_('clients.protocol', 'Protocol')}: ${e(c.reactor.protocolVersion)}</div>`;
                } else {
                    body += `<div style="font-size:12px;color:var(--m)">${_('clients.no_mods', 'No mods.')}</div>`;
                }
            }
            document.getElementById('cl-modal-body').innerHTML = body;
            const modal = document.getElementById('cl-modal');
            modal.style.display = 'flex';
            modal.onclick = ev => { if (ev.target === modal) closeDetail(); };
        }
        function closeDetail() { document.getElementById('cl-modal').style.display = 'none'; }

        // i18n
        async function loadStrings() {
            try {
                const r = await fetch('/api/admin/strings');
                _s = await r.json();
                applyI18n();
            } catch {}
        }
        function applyI18n() {
            document.querySelectorAll('[data-i18n]').forEach(el => {
                const k = el.getAttribute('data-i18n');
                if (_s[k]) el.textContent = _s[k];
            });
            document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
                const k = el.getAttribute('data-i18n-placeholder');
                if (_s[k]) el.placeholder = _s[k];
            });
        }
        async function reloadLang() {
            await api('POST', '/api/admin/strings/reload');
            await loadStrings();
            refreshTab();
            fetchStatus();
        }

        // ---- Plugin extensions (declarative widgets) ----
        const ICONS = {
            puzzle: '<path d="M10 3a1 1 0 011 1v1h2a1 1 0 011 1v1h4a1 1 0 011 1v4h1a1 1 0 011 1v2a1 1 0 01-1 1h-1v4a1 1 0 01-1 1h-4v1a1 1 0 01-1 1h-2a1 1 0 01-1-1v-1H5a1 1 0 01-1-1v-4H3a1 1 0 01-1-1v-2a1 1 0 011-1h1V5a1 1 0 011-1h4V3a1 1 0 011-1z"/>',
            link: '<path d="M10 13a5 5 0 007.54.54l3-3a5 5 0 00-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 00-7.54-.54l-3 3a5 5 0 007.07 7.07l1.71-1.71"/>',
            shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>',
            stats: '<path d="M18 20V10M12 20V4M6 20v-6"/>',
            chat: '<path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/>',
            gear: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z"/>',
            refresh: '<polyline points="1 4 1 10 7 10"/><polyline points="23 20 23 14 17 14"/><path d="M20.49 9A9 9 0 005.64 5.64L1 10m22 4l-4.64 4.36A9 9 0 013.51 15"/>',
            trash: '<path d="M3 6h18M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2m3 0v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6"/>',
            plus: '<path d="M12 5v14M5 12h14"/>',
            save: '<path d="M19 21H5a2 2 0 01-2-2V5a2 2 0 012-2h11l5 5v11a2 2 0 01-2 2z"/><path d="M17 21v-8H7v8M7 3v5h8"/>',
            webhook: '<path d="M10 13a5 5 0 007.54.54l3-3a5 5 0 00-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 00-7.54-.54l-3 3a5 5 0 007.07 7.07l1.71-1.71"/>',
            database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/>',
            filter: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="M9 12l2 2 4-4"/>',
            list: '<path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01"/>'
        };
        function iconSvg(name, size) {
            const p = ICONS[name] || ICONS.puzzle;
            return '<svg width="' + (size || 16) + '" height="' + (size || 16) + '" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' + p + '</svg>';
        }
        function btnClass(style) {
            switch (style) {
                case 'danger': return 'bd';
                case 'warning': return 'bw';
                case 'secondary': return 'bsm';
                default: return 'bp';
            }
        }
        function renderWidget(w) {
            if (!w) return '';
            switch (w.type) {
                case 'button': return '<button class="' + btnClass(w.style) + '" data-act="' + e(w.action) + '" data-w="button">' + (w.icon ? iconSvg(w.icon, 14) : '') + '<span>' + e(w.label) + '</span></button>';
                case 'text': return '<div class="aw-text' + (w.tone && w.tone !== 'default' ? ' tone-' + w.tone : '') + (w.monospace ? ' mono' : '') + '">' + e(w.content) + '</div>';
                case 'block': return '<div class="form"><h3>' + e(w.title || '') + '</h3>' + (w.description ? '<div class="aw-desc">' + e(w.description) + '</div>' : '') + '<div class="aw-children">' + (w.children || []).map(renderWidget).join('') + '</div></div>';
                case 'textbox': return '<div class="field"><label>' + e(w.label) + '</label>' + (w.multiline ? '<textarea data-act="' + e(w.action) + '" data-w="textbox" placeholder="' + e(w.placeholder || '') + '">' + e(w.value || '') + '</textarea>' : '<input type="' + (w.secret ? 'password' : 'text') + '" data-act="' + e(w.action) + '" data-w="textbox" value="' + e(w.value || '') + '" placeholder="' + e(w.placeholder || '') + '">') + '</div>';
                case 'toggle': return '<label class="aw-toggle"><input type="checkbox" data-act="' + e(w.action) + '" data-w="toggle"' + (w.value ? ' checked' : '') + '><span>' + e(w.label) + '</span></label>';
                case 'select': return '<div class="field"><label>' + e(w.label) + '</label><select data-act="' + e(w.action) + '" data-w="select">' + (w.options || []).map(function (o) { return '<option value="' + e(o.value) + '"' + (o.value === w.value ? ' selected' : '') + '>' + e(o.label) + '</option>'; }).join('') + '</select></div>';
                case 'number': return '<div class="field"><label>' + e(w.label) + '</label><input type="number" data-act="' + e(w.action) + '" data-w="number" value="' + e(w.value) + '"' + (w.min != null ? ' min="' + w.min + '"' : '') + (w.max != null ? ' max="' + w.max + '"' : '') + '></div>';
                case 'table': return '<table><thead><tr>' + (w.columns || []).map(function (c) { return '<th>' + e(c) + '</th>'; }).join('') + '</tr></thead><tbody>' + (w.rows || []).map(function (r) { return '<tr>' + (r || []).map(function (c) { return '<td class="' + (c.tone && c.tone !== 'default' ? 'tone-' + c.tone : '') + (c.monospace ? ' mono' : '') + '">' + e(c.text) + '</td>'; }).join('') + '</tr>'; }).join('') + '</tbody></table>';
                case 'chips': return '<div class="aw-chips"><div class="chips-list">' + (w.items || []).map(function (c) { return '<span class="chip">' + e(c.label) + '<button data-act="' + e(w.removeAction) + '" data-w="chip-remove" data-value="' + e(c.value) + '" title="Remove">&times;</button></span>'; }).join('') + '</div><div class="row"><input type="text" data-w="chip-input" placeholder="' + e(w.placeholder || '') + '"><button class="bp bsm" data-act="' + e(w.addAction) + '" data-w="chip-add">' + e(w.addLabel || 'Add') + '</button></div></div>';
                case 'divider': return '<hr class="aw-divider">';
                default: return '';
            }
        }
        let _extCur = null;
        async function loadExtension(id) {
            _extCur = id;
            const body = document.querySelector('#p-ext-' + id + ' .ext-body');
            if (!body) return;
            const ae = document.activeElement;
            if (ae && body.contains(ae) && /^(INPUT|TEXTAREA|SELECT)$/.test(ae.tagName)) return;
            const { ok, data } = await api('GET', '/api/admin/ext/' + encodeURIComponent(id));
            if (!ok) { body.innerHTML = '<div class="empty">' + (data && data.error ? e(data.error) : 'Error') + '</div>'; return; }
            body.innerHTML = (data.widgets || []).map(renderWidget).join('');
        }
        function toast(ok, text) {
            let t = document.getElementById('ext-toast');
            if (!t) {
                t = document.createElement('div');
                t.id = 'ext-toast';
                t.style.cssText = 'position:fixed;bottom:24px;right:24px;z-index:300;padding:10px 16px;border-radius:8px;font-size:13px;box-shadow:0 4px 16px rgba(0,0,0,.25);transition:opacity .2s;max-width:360px';
                document.body.appendChild(t);
            }
            t.textContent = text;
            t.style.background = ok ? 'rgba(63,185,80,.95)' : 'rgba(248,81,73,.95)';
            t.style.color = '#fff';
            t.style.opacity = '1';
            clearTimeout(t._h);
            t._h = setTimeout(function () { t.style.opacity = '0'; }, 3500);
        }
        async function dispatch(extId, action, value) {
            const { ok, data } = await api('POST', '/api/admin/ext/' + encodeURIComponent(extId) + '/' + encodeURIComponent(action), { value: value });
            if (data && data.message) toast(ok, data.message);
            if (ok && data && data.refresh !== false) loadExtension(extId);
            return { ok: ok, data: data };
        }
        document.addEventListener('click', function (ev) {
            const el = ev.target.closest('[data-act]');
            if (!el) return;
            const holder = el.closest('[data-ext]');
            const extId = holder ? holder.getAttribute('data-ext') : _extCur;
            if (!extId) return;
            const w = el.getAttribute('data-w');
            let value;
            if (w === 'chip-remove') value = el.getAttribute('data-value');
            else if (w === 'chip-add') { const inp = el.parentElement.querySelector('[data-w="chip-input"]'); value = inp ? inp.value : ''; }
            else if (w === 'toggle') value = el.checked;
            else value = el.value;
            dispatch(extId, el.getAttribute('data-act'), value);
        });
        document.addEventListener('change', function (ev) {
            const el = ev.target.closest('[data-act]');
            if (!el) return;
            const w = el.getAttribute('data-w');
            if (w !== 'textbox' && w !== 'number' && w !== 'select') return;
            const holder = el.closest('[data-ext]');
            const extId = holder ? holder.getAttribute('data-ext') : _extCur;
            if (!extId) return;
            dispatch(extId, el.getAttribute('data-act'), el.value);
        });
        async function initExtensions() {
            try {
                const { data } = await api('GET', '/api/admin/ext');
                const navBox = document.getElementById('plugins-nav');
                const ct = document.querySelector('ct');
                (data || []).forEach(function (x) {
                    const ni = document.createElement('div');
                    ni.className = 'ni';
                    ni.innerHTML = iconSvg(x.icon, 16) + '<span>' + e(x.title) + '</span>';
                    ni.addEventListener('click', function () { nav('ext-' + x.id); });
                    navBox.appendChild(ni);

                    const pnl = document.createElement('div');
                    pnl.id = 'p-ext-' + x.id;
                    pnl.className = 'pnl';
                    pnl.innerHTML = '<div class="ext-body" data-ext="' + e(x.id) + '"></div>';
                    ct.appendChild(pnl);
                });
            } catch (err) { }
        }

        // ---- Theme (light/dark + third-party themes) ----
        let _themeDefault = 'default';
        let _modeDefault = 'dark';
        let _themeCss = null;
        function applyMode(mode) {
            document.documentElement.setAttribute('data-theme', mode);
            const btn = document.getElementById('theme-toggle');
            if (btn) btn.textContent = mode === 'dark' ? '☀️' : '🌙';
            try { localStorage.setItem('empostor_mode', mode); } catch (err) { }
        }
        function toggleMode() {
            const cur = document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
            applyMode(cur === 'dark' ? 'light' : 'dark');
        }
        async function loadThemeCss(id) {
            if (!_themeCss) {
                _themeCss = document.createElement('style');
                _themeCss.id = 'theme-css';
                document.head.appendChild(_themeCss);
            }
            const r = await fetch('/api/admin/theme/' + encodeURIComponent(id) + '/css');
            if (r.ok) {
                _themeCss.textContent = await r.text();
                try { localStorage.setItem('empostor_theme', id); } catch (err) { }
            }
        }
        function selectTheme(id) {
            if (id === 'default') {
                if (_themeCss) _themeCss.textContent = '';
                try { localStorage.setItem('empostor_theme', id); } catch (err) { }
                return;
            }
            loadThemeCss(id);
        }
        async function initThemes() {
            try {
                const { data } = await api('GET', '/api/admin/themes');
                _themeDefault = data.default || 'default';
                _modeDefault = data.defaultMode === 'light' ? 'light' : 'dark';
                const sel = document.getElementById('theme-sel');
                (data.themes || []).forEach(function (t) {
                    const o = document.createElement('option');
                    o.value = t.id;
                    o.textContent = t.name;
                    sel.appendChild(o);
                });
                let theme = _themeDefault;
                let mode = _modeDefault;
                try { theme = localStorage.getItem('empostor_theme') || _themeDefault; } catch (err) { }
                try { mode = localStorage.getItem('empostor_mode') || _modeDefault; } catch (err) { }
                sel.value = theme;
                applyMode(mode);
                if (theme !== 'default') await loadThemeCss(theme);
            } catch (err) { }
        }

        // Init
        fetchStatus();
        refreshTab();
        loadStrings();
        initExtensions();
        initThemes();
        setInterval(fetchStatus, 1000);
        setInterval(() => { if (document.visibilityState === 'visible') refreshTab(); }, 1000);
        document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') { fetchStatus(); } });
    </script>
    <!-- Privacy Policy Overlay -->
    <div id="privacy-overlay" style="position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.75);z-index:99999;display:none;align-items:center;justify-content:center;">
        <div style="background:var(--s);border:1px solid var(--b);border-radius:12px;max-width:720px;width:90%;max-height:85%;display:flex;flex-direction:column;padding:24px;">
            <h2 style="margin:0 0 12px 0;color:var(--t);font-size:18px;" data-i18n="privacy_policy.title">Privacy Policy for this Server</h2>
            <div id="privacy-content" style="flex:1;overflow-y:auto;padding-right:8px;color:var(--t);font-size:13px;line-height:1.6;white-space:pre-wrap;">
            </div>
            <div style="margin-top:16px;display:flex;align-items:center;gap:12px;border-top:1px solid var(--b);padding-top:14px;">
                <label style="color:var(--m);font-size:12px;display:flex;align-items:center;gap:6px;">
                    <input type="checkbox" id="privacy-agree-check" style="width:auto;accent-color:var(--a);">
                    <span data-i18n="privacy_policy.agree">I have read and understand this privacy policy, and I will ensure it is accessible to all players on my server.</span>
                </label>
                <button id="privacy-confirm-btn" style="margin-left:auto;background:var(--a);color:#fff;border:none;border-radius:6px;padding:8px 20px;font-size:14px;font-weight:600;cursor:pointer;opacity:0.5;pointer-events:none;" disabled data-i18n="privacy_policy.confirm">Confirm</button>
            </div>
        </div>
    </div>

    <script>
        (function () {
            if (document.cookie.split('; ').find(row => row.startsWith('privacy_agreed='))) return;

            const privacyText = `Privacy Policy for this Private Server

1. Introduction
The server operator is committed to protecting player privacy. This privacy policy explains how we collect, use, store, and safeguard player information while operating this private Among Us server.

This document must be made clearly visible to all players who join your server, so that every player understands how their data is handled before they start playing.

2. Types of Information Collected
When a player joins and uses this server, the following information is automatically collected:
- IP address - used for server connection, security protection, and abuse prevention.
- In-game friend code - used for identity verification and in-server permission management.
- In-game chat messages - including all content sent in the game chat channels.
- Player name - the display name shown in the game.

3. Purpose of Information Use
The information collected is used solely for the following purposes:
- Ensuring normal server operation and stable connections.
- Maintaining server order, preventing cheating, harassment, or other rule-breaking behavior.
- Investigating violations of server rules when necessary.
- Improving server management and player experience.

4. Information Storage and Protection
All collected information is stored only in protected server environments, with access restricted to authorized members of the server operator's management team.
Reasonable technical measures are taken to prevent information leakage, tampering, or unauthorized access.
Unless required by law or in response to a security incident, we will not proactively provide or disclose your information to any third party.

5. Information Retention Period
Unless needed for violation investigations or legal compliance, player information will be deleted within a reasonable period after the player ceases using the server.

6. User Rights
Players have the following rights:
- To ask whether the server holds their information and how it is used.
- To request deletion of their information (unless retention is required by law).
- To refuse collection of certain information, though this may result in inability to use the server.

For any related requests or inquiries, players should contact us.

7. Information Sharing and Disclosure
We do not sell, rent, or trade player information to any third party. Disclosure may only occur in the following extremely limited circumstances:
- When required by mandatory laws, regulations, judicial or administrative authorities.
- To protect the safety, rights, and property of the server operator, other players, or the public, such as in cases of investigating fraud or malicious attacks.

8. Privacy Policy Updates
This policy may be updated from time to time. The updated version will be published on the server announcement or relevant page. Continued use of the server constitutes acceptance of the revised policy.

9. Disclaimer
Please note that the Among Us game itself is developed by Innersloth and is subject to its own Terms of Service and Privacy Policy. This policy applies only to the management practices of this server.

10. Contact Us
If players have any questions about this privacy policy or how their data is handled, please contact us.

Note: As this is a volunteer-operated private server, responses may take some time. We appreciate your patience.

Notice to the Server Operator:
I am providing this privacy policy text to you, the server operator. It is your responsibility to post this policy in a location that is easily and prominently accessible to all players - for example, on your server welcome page, in a dedicated announcement channel, or on a publicly visible notice board.
You must require that all players read and acknowledge this policy before joining the game. Protecting player privacy is not just a legal and ethical duty; it also helps build trust in your server community. If you have any questions about implementing this policy or explaining it to your players, please reach out to me.`;

            document.getElementById('privacy-content').textContent = privacyText;
            const overlay = document.getElementById('privacy-overlay');
            const check = document.getElementById('privacy-agree-check');
            const btn = document.getElementById('privacy-confirm-btn');

            overlay.style.display = 'flex';

            check.addEventListener('change', () => {
                btn.style.opacity = check.checked ? '1' : '0.5';
                btn.style.pointerEvents = check.checked ? 'auto' : 'none';
                btn.disabled = !check.checked;
            });

            btn.addEventListener('click', () => {
                if (!check.checked) return;
                document.cookie = "privacy_agreed=1; path=/; max-age=" + 60 * 60 * 24 * 365 + "; SameSite=Lax";
                overlay.style.display = 'none';
            });
        })();
    </script>
</body>

</html>
""";
}
