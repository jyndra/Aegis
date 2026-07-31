using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aegis.Service.Api;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/dashboard", () => Results.Content(GetDashboardHtml(), "text/html", Encoding.UTF8));
        endpoints.MapGet("/", () => Results.Redirect("/dashboard"));
    }

    private static string GetDashboardHtml()
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Aegis Control Center</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet">
    <style>
        :root {
            --bg: #0F172A;
            --card-bg: #1E293B;
            --input-bg: #0F172A;
            --accent: #38BDF8;
            --accent-hover: #0284C7;
            --danger: #EF4444;
            --success: #22C55E;
            --warning: #F59E0B;
            --text: #F8FAFC;
            --text-muted: #94A3B8;
            --border: #334155;
        }

        * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Inter', sans-serif; }

        body {
            background-color: var(--bg);
            color: var(--text);
            min-height: 100vh;
            padding: 30px 20px;
        }

        .container {
            max-width: 1000px;
            margin: 0 auto;
        }

        header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 25px;
            padding-bottom: 15px;
            border-bottom: 1px solid var(--border);
        }

        .brand h1 { font-size: 24px; font-weight: 700; color: var(--text); }
        .brand p { font-size: 13px; color: var(--text-muted); margin-top: 4px; }

        .badge {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 6px 14px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
            background: rgba(56, 189, 248, 0.1);
            color: var(--accent);
            border: 1px solid rgba(56, 189, 248, 0.2);
        }

        .badge.test-mode {
            background: rgba(245, 158, 11, 0.1);
            color: var(--warning);
            border-color: rgba(245, 158, 11, 0.3);
        }

        .tabs {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
        }

        .tab-btn {
            background: var(--card-bg);
            color: var(--text-muted);
            border: 1px solid var(--border);
            padding: 10px 20px;
            border-radius: 8px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.2s ease;
        }

        .tab-btn.active, .tab-btn:hover {
            color: var(--text);
            border-color: var(--accent);
            background: rgba(56, 189, 248, 0.05);
        }

        .tab-content { display: none; }
        .tab-content.active { display: block; }

        .card {
            background: var(--card-bg);
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 24px;
            margin-bottom: 20px;
        }

        .card h2 { font-size: 16px; font-weight: 600; margin-bottom: 15px; color: var(--text); }

        .form-grid {
            display: grid;
            grid-template-columns: 1fr 120px auto;
            gap: 12px;
            margin-bottom: 15px;
        }

        input, select {
            background: var(--input-bg);
            border: 1px solid var(--border);
            color: var(--text);
            padding: 10px 14px;
            border-radius: 6px;
            font-size: 14px;
            outline: none;
            transition: border-color 0.2s;
        }

        input:focus { border-color: var(--accent); }

        button.btn-primary {
            background: var(--accent);
            color: #0F172A;
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            font-weight: 600;
            cursor: pointer;
            transition: background 0.2s;
        }

        button.btn-primary:hover { background: var(--accent-hover); }

        button.btn-danger {
            background: rgba(239, 68, 68, 0.15);
            color: var(--danger);
            border: 1px solid rgba(239, 68, 68, 0.3);
            padding: 8px 16px;
            border-radius: 6px;
            font-weight: 600;
            cursor: pointer;
        }

        button.btn-danger:hover { background: var(--danger); color: white; }

        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 10px;
            font-size: 13px;
        }

        th, td {
            text-align: left;
            padding: 12px;
            border-bottom: 1px solid var(--border);
        }

        th { color: var(--text-muted); font-weight: 500; }

        .toast {
            padding: 12px 16px;
            border-radius: 6px;
            font-size: 13px;
            margin-bottom: 15px;
            display: none;
        }

        .toast.success { background: rgba(34, 197, 94, 0.15); color: var(--success); border: 1px solid rgba(34, 197, 94, 0.3); }
        .toast.error { background: rgba(239, 68, 68, 0.15); color: var(--danger); border: 1px solid rgba(239, 68, 68, 0.3); }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <div class="brand">
                <h1>Aegis Control Center</h1>
                <p>Custom Website, Keyword & Regex Policy Manager</p>
            </div>
            <div id="statusBadge" class="badge test-mode">⚡ Test Mode Active</div>
        </header>

        <div id="toastMsg" class="toast"></div>

        <div class="tabs">
            <button class="tab-btn active" onclick="switchTab('websites')">🌐 Custom Websites</button>
            <button class="tab-btn" onclick="switchTab('keywords')">🔑 Custom Keywords</button>
            <button class="tab-btn" onclick="switchTab('regex')">⚡ Custom Regex</button>
            <button class="tab-btn" onclick="switchTab('testing')">🛠️ Testing & Uninstaller</button>
        </div>

        <!-- Websites Tab -->
        <div id="tab-websites" class="tab-content active">
            <div class="card">
                <h2>Add Custom Blocked Website</h2>
                <div class="form-grid" style="grid-template-columns: 1fr auto;">
                    <input type="text" id="txtDomain" placeholder="e.g. gambling-example.com">
                    <button class="btn-primary" onclick="addWebsite()">Add Website</button>
                </div>
                <p style="font-size: 12px; color: var(--text-muted);">Hot-reloads DNS proxy immediately. Always allowed even during 25-day lock.</p>
            </div>

            <div class="card">
                <h2>Custom Blocked Websites List</h2>
                <table id="tblWebsites">
                    <thead>
                        <tr><th>Domain Hash / Name</th><th>Status</th></tr>
                    </thead>
                    <tbody><tr><td colspan="2">Loading custom rules...</td></tr></tbody>
                </table>
            </div>
        </div>

        <!-- Keywords Tab -->
        <div id="tab-keywords" class="tab-content">
            <div class="card">
                <h2>Add Custom Keyword Trigger</h2>
                <div class="form-grid">
                    <input type="text" id="txtKeyword" placeholder="e.g. casino">
                    <input type="number" id="numKwWeight" value="50" placeholder="Weight">
                    <button class="btn-primary" onclick="addKeyword()">Add Keyword</button>
                </div>
                <p style="font-size: 12px; color: var(--text-muted);">Hot-reloads KeywordEngine immediately. High weight = higher block sensitivity.</p>
            </div>
        </div>

        <!-- Regex Tab -->
        <div id="tab-regex" class="tab-content">
            <div class="card">
                <h2>Add Custom Regex Rule</h2>
                <div class="form-grid">
                    <input type="text" id="txtRegexPattern" placeholder="e.g. \b(poker|betting)\b">
                    <input type="number" id="numRegexScore" value="60" placeholder="Score">
                    <button class="btn-primary" onclick="addRegex()">Add Regex</button>
                </div>
                <p style="font-size: 12px; color: var(--text-muted);">Validated for syntax prior to persistence. Hot-reloads compiled RegexEngine.</p>
            </div>
        </div>

        <!-- Testing Tab -->
        <div id="tab-testing" class="tab-content">
            <div class="card">
                <h2>Test Mode & Zero-Friction Uninstaller</h2>
                <p style="font-size: 14px; color: var(--text-muted); margin-bottom: 20px;">
                    Test Mode is currently active in <code>appsettings.json</code>. Content blocking is 100% active, but uninstallation and custom rule deletion have zero friction for easy development testing.
                </p>
                <button class="btn-danger" onclick="triggerTestUninstall()">Uninstall App (Instant Test Mode Teardown)</button>
            </div>
        </div>
    </div>

    <script>
        const API_BASE = 'http://127.0.0.1:9443';

        function switchTab(tabName) {
            document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
            event.target.classList.add('active');
            document.getElementById('tab-' + tabName).classList.add('active');
        }

        function showToast(msg, isError = false) {
            const toast = document.getElementById('toastMsg');
            toast.textContent = msg;
            toast.className = 'toast ' + (isError ? 'error' : 'success');
            toast.style.display = 'block';
            setTimeout(() => { toast.style.display = 'none'; }, 4000);
        }

        async function loadOverview() {
            try {
                const res = await fetch(API_BASE + '/policy/custom-rules');
                const data = await res.json();
                
                const badge = document.getElementById('statusBadge');
                if (data.testModeActive) {
                    badge.className = 'badge test-mode';
                    badge.textContent = '⚡ Test Mode Active (Zero-Friction Testing)';
                } else {
                    badge.className = 'badge';
                    badge.textContent = '🔒 Production Mode (25-Day Lock Active)';
                }

                const tbody = document.querySelector('#tblWebsites tbody');
                if (!data.websites || data.websites.length === 0) {
                    tbody.innerHTML = '<tr><td colspan="2">No custom blocked websites added yet.</td></tr>';
                } else {
                    tbody.innerHTML = data.websites.map(domain => `
                        <tr>
                            <td><strong>${domain}</strong></td>
                            <td><span style="color:var(--success)">Active</span></td>
                        </tr>
                    `).join('');
                }
            } catch (err) {
                console.error('Failed to load rules', err);
            }
        }

        async function addWebsite() {
            const domain = document.getElementById('txtDomain').value.trim();
            if (!domain) return showToast('Please enter a valid domain name', true);

            try {
                const res = await fetch(API_BASE + '/policy/custom-websites', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ domain })
                });
                const result = await res.json();
                if (res.ok) {
                    showToast(result.message);
                    document.getElementById('txtDomain').value = '';
                    loadOverview();
                } else {
                    showToast(result.message || 'Error adding website', true);
                }
            } catch (err) {
                showToast('API Connection Error', true);
            }
        }

        async function addKeyword() {
            const keyword = document.getElementById('txtKeyword').value.trim();
            const weight = parseInt(document.getElementById('numKwWeight').value) || 50;
            if (!keyword) return showToast('Please enter a keyword', true);

            try {
                const res = await fetch(API_BASE + '/policy/custom-keywords', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ keyword, weight })
                });
                const result = await res.json();
                if (res.ok) {
                    showToast(result.message);
                    document.getElementById('txtKeyword').value = '';
                    loadOverview();
                } else {
                    showToast(result.message || 'Error adding keyword', true);
                }
            } catch (err) {
                showToast('API Connection Error', true);
            }
        }

        async function addRegex() {
            const pattern = document.getElementById('txtRegexPattern').value.trim();
            const score = parseInt(document.getElementById('numRegexScore').value) || 60;
            if (!pattern) return showToast('Please enter a regex pattern', true);

            try {
                const res = await fetch(API_BASE + '/policy/custom-regex', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ pattern, score })
                });
                const result = await res.json();
                if (res.ok) {
                    showToast(result.message);
                    document.getElementById('txtRegexPattern').value = '';
                    loadOverview();
                } else {
                    showToast(result.message || 'Invalid regex syntax', true);
                }
            } catch (err) {
                showToast('API Connection Error', true);
            }
        }

        async function triggerTestUninstall() {
            if (!confirm('Are you sure you want to trigger test uninstallation?')) return;
            try {
                const res = await fetch(API_BASE + '/deployment/uninstall?forceConfirm=true', { method: 'POST' });
                const result = await res.json();
                showToast(result.message, !result.success);
            } catch (err) {
                showToast('API Connection Error', true);
            }
        }

        loadOverview();
    </script>
</body>
</html>
""";
    }
}
