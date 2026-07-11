//Card Archive admin stats dashboard.
//Stat tables are rendered generically from the columns definition sent by
//the server (/stats/:id), so adding a new stat only requires a new backend
//module in stats/modules/. To customize how one stat is displayed, register
//a function in CustomRenderers below: CustomRenderers["stat-id"] = (stat, container) => {...}

const CustomRenderers = {};

var token = sessionStorage.getItem("admin_token") || "";
var admin_name = sessionStorage.getItem("admin_name") || "";
var current_stat = null;   //Last loaded stat data {id, title, columns, rows}
var sort_key = null;
var sort_asc = true;

// ---- API ----

async function apiGet(path) {
    const res = await fetch(path, { headers: { "Authorization": token } });
    if (res.status === 401 || res.status === 403)
        throw { auth: true };
    if (!res.ok)
        throw { error: (await res.json().catch(() => ({}))).error || ("HTTP " + res.status) };
    return await res.json();
}

async function apiLogin(username, password) {
    const res = await fetch("/auth", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: username, password: password }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok)
        throw { error: data.error || "로그인에 실패했습니다." };
    return data;
}

// ---- Views ----

function show(id) {
    document.getElementById("login-view").classList.add("hidden");
    document.getElementById("dashboard-view").classList.add("hidden");
    document.getElementById(id).classList.remove("hidden");
}

function logout(message) {
    token = "";
    sessionStorage.removeItem("admin_token");
    sessionStorage.removeItem("admin_name");
    show("login-view");
    document.getElementById("login-error").textContent = message || "";
}

// ---- Dashboard ----

async function openDashboard() {
    var stat_list;
    try {
        stat_list = await apiGet("/stats");  //Also validates the token + admin permission
    } catch (e) {
        logout(e.auth ? "관리자 권한이 있는 계정으로 로그인하세요." : e.error);
        return;
    }

    show("dashboard-view");
    document.getElementById("admin-name").textContent = admin_name;

    const menu = document.getElementById("stat-menu");
    menu.innerHTML = "";
    for (const stat of stat_list) {
        const button = document.createElement("button");
        button.className = "menu-item";
        button.dataset.statId = stat.id;
        button.textContent = stat.title;
        button.title = stat.description;
        button.addEventListener("click", () => loadStat(stat.id));
        menu.appendChild(button);
    }

    if (stat_list.length > 0)
        loadStat(stat_list[0].id);
}

async function loadStat(statId) {
    for (const item of document.querySelectorAll(".menu-item"))
        item.classList.toggle("active", item.dataset.statId === statId);

    document.getElementById("stat-error").textContent = "";
    document.getElementById("table-filter").value = "";
    sort_key = null;

    var stat;
    try {
        stat = await apiGet("/stats/" + encodeURIComponent(statId));
    } catch (e) {
        if (e.auth) return logout("세션이 만료되었습니다. 다시 로그인하세요.");
        document.getElementById("stat-error").textContent = e.error;
        return;
    }

    current_stat = stat;
    document.getElementById("stat-title").textContent = stat.title;
    renderStat();
}

function renderStat() {
    if (!current_stat) return;

    const container = document.getElementById("stat-table");
    if (CustomRenderers[current_stat.id])
        return CustomRenderers[current_stat.id](current_stat, container);

    renderTable(current_stat, container);
}

// ---- Generic table renderer ----

function formatValue(value, type) {
    if (value === null || value === undefined || value === "")
        return "-";
    if (type === "date")
        return new Date(value).toLocaleString("ko-KR");
    if (type === "number")
        return Number(value).toLocaleString("ko-KR");
    return String(value);
}

function getFilteredRows(stat) {
    const filter = document.getElementById("table-filter").value.trim().toLowerCase();
    var rows = stat.rows;

    if (filter) {
        rows = rows.filter((row) =>
            stat.columns.some((col) => String(row[col.key] ?? "").toLowerCase().includes(filter)));
    }

    if (sort_key) {
        const col = stat.columns.find((c) => c.key === sort_key);
        rows = [...rows].sort((a, b) => {
            var va = a[sort_key], vb = b[sort_key];
            if (va === null || va === undefined) return 1;
            if (vb === null || vb === undefined) return -1;
            if (col && (col.type === "number"))
                return sort_asc ? va - vb : vb - va;
            if (col && (col.type === "date"))
                return sort_asc ? new Date(va) - new Date(vb) : new Date(vb) - new Date(va);
            return sort_asc ? String(va).localeCompare(String(vb)) : String(vb).localeCompare(String(va));
        });
    }
    return rows;
}

function renderTable(stat, table) {
    const rows = getFilteredRows(stat);

    document.getElementById("stat-info").textContent =
        "총 " + rows.length + "건 · 조회 시각 " + new Date(stat.generated_at).toLocaleString("ko-KR");

    table.innerHTML = "";

    const thead = document.createElement("thead");
    const header_row = document.createElement("tr");
    for (const col of stat.columns) {
        const th = document.createElement("th");
        th.textContent = col.label + (sort_key === col.key ? (sort_asc ? " ▲" : " ▼") : "");
        th.className = col.type === "number" ? "num" : "";
        th.addEventListener("click", () => {
            sort_asc = (sort_key === col.key) ? !sort_asc : (col.type !== "number" && col.type !== "date");
            sort_key = col.key;
            renderStat();
        });
        header_row.appendChild(th);
    }
    thead.appendChild(header_row);
    table.appendChild(thead);

    const tbody = document.createElement("tbody");
    for (const row of rows) {
        const tr = document.createElement("tr");
        for (const col of stat.columns) {
            const td = document.createElement("td");
            td.textContent = formatValue(row[col.key], col.type);
            td.className = col.type === "number" ? "num" : "";
            tr.appendChild(td);
        }
        tbody.appendChild(tr);
    }
    table.appendChild(tbody);
}

// ---- Events ----

document.getElementById("login-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const error_box = document.getElementById("login-error");
    error_box.textContent = "";
    document.getElementById("login-button").disabled = true;

    try {
        const data = await apiLogin(
            document.getElementById("login-username").value.trim(),
            document.getElementById("login-password").value);

        token = data.access_token;
        admin_name = data.username;
        sessionStorage.setItem("admin_token", token);
        sessionStorage.setItem("admin_name", admin_name);
        await openDashboard();  //Rejects non-admin accounts (403 on /stats)
    } catch (err) {
        error_box.textContent = err.error || "로그인에 실패했습니다.";
    } finally {
        document.getElementById("login-button").disabled = false;
    }
});

document.getElementById("logout-button").addEventListener("click", () => logout());
document.getElementById("refresh-button").addEventListener("click", () => {
    if (current_stat) loadStat(current_stat.id);
});
document.getElementById("table-filter").addEventListener("input", () => renderStat());

// ---- Init ----

if (token)
    openDashboard();
else
    show("login-view");
