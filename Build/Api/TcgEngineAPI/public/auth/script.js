//Card Archive admin tool.
//Operation pages (maintenance, audit log) are static pages in index.html.
//Stat tables are rendered generically from the columns definition sent by
//the server (/stats/:id), so adding a new stat only requires a new backend
//module in stats/modules/. To customize how one stat is displayed, register
//a function in CustomRenderers below: CustomRenderers["stat-id"] = (stat, container) => {...}

const CustomRenderers = {};

var token = sessionStorage.getItem("admin_token") || "";
var admin_name = sessionStorage.getItem("admin_name") || "";
var current_page = null;   //"maintenance", "audit" or "stat:<id>"
var current_stat = null;   //Last loaded stat data {id, title, columns, rows}
var sort_key = null;
var sort_asc = true;

// ---- API ----

async function apiRequest(method, path, body) {
    const options = { method: method, headers: { "Authorization": token } };
    if (body !== undefined) {
        options.headers["Content-Type"] = "application/json";
        options.body = JSON.stringify(body);
    }
    const res = await fetch(path, options);
    if (res.status === 401 || res.status === 403)
        throw { auth: true };
    const data = await res.json().catch(() => ({}));
    if (!res.ok)
        throw { error: data.error || ("HTTP " + res.status) };
    return data;
}

function apiGet(path) {
    return apiRequest("GET", path);
}

function apiPost(path, body) {
    return apiRequest("POST", path, body || {});
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

//Shows the error of a failed request, or goes back to login when the session ended
function handleError(e, error_box) {
    if (e.auth)
        return logout("세션이 만료되었습니다. 다시 로그인하세요.");
    document.getElementById(error_box).textContent = e.error || String(e);
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
    stopPageTimers();
    show("login-view");
    document.getElementById("login-error").textContent = message || "";
}

function openPage(page) {
    current_page = page;
    const page_id = page.startsWith("stat:") ? "page-stat" : "page-" + page;
    for (const elem of document.querySelectorAll(".page"))
        elem.classList.toggle("hidden", elem.id !== page_id);
    for (const item of document.querySelectorAll(".menu-item"))
        item.classList.toggle("active", item.dataset.page === page);

    stopPageTimers();
    if (page === "maintenance")
        loadMaintenance();
    else if (page === "messages")
        loadMessages();
    else if (page === "audit")
        loadAudit();
    else
        loadStat(page.substring(5));
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
        button.dataset.page = "stat:" + stat.id;
        button.textContent = stat.title;
        button.title = stat.description;
        button.addEventListener("click", () => openPage("stat:" + stat.id));
        menu.appendChild(button);
    }

    openPage("maintenance");
}

// ---- Confirm modal ----

//Resolves true when confirmed, type_text requires the admin to type that text first
function confirmAction(title, message, type_text) {
    return new Promise((resolve) => {
        const modal = document.getElementById("confirm-modal");
        const form = document.getElementById("confirm-form");
        const input = document.getElementById("confirm-type-input");
        const ok = document.getElementById("confirm-ok");

        document.getElementById("confirm-title").textContent = title;
        document.getElementById("confirm-message").textContent = message;
        document.getElementById("confirm-type-label").classList.toggle("hidden", !type_text);
        document.getElementById("confirm-type-hint").textContent = type_text ? "확인하려면 \"" + type_text + "\"을(를) 입력하세요." : "";
        input.value = "";
        ok.disabled = !!type_text;
        modal.classList.remove("hidden");
        if (type_text) input.focus(); else ok.focus();

        const onInput = () => { ok.disabled = input.value.trim() !== type_text; };
        const close = (result) => {
            modal.classList.add("hidden");
            input.removeEventListener("input", onInput);
            form.removeEventListener("submit", onSubmit);
            document.getElementById("confirm-cancel").removeEventListener("click", onCancel);
            resolve(result);
        };
        const onSubmit = (e) => { e.preventDefault(); if (!ok.disabled) close(true); };
        const onCancel = () => close(false);

        input.addEventListener("input", onInput);
        form.addEventListener("submit", onSubmit);
        document.getElementById("confirm-cancel").addEventListener("click", onCancel);
    });
}

// ---- Maintenance ----

var mt_state = null;       //Last /admin/maintenance response
var mt_loaded_at = 0;      //performance.now() when mt_state was received
var mt_clock_offset = 0;   //Server time - local time, in ms
var mt_tick_timer = null;
var mt_poll_timer = null;
var mt_refresh_pending = false;

const MaintenanceLabels = {
    none: "정상 운영",
    scheduled: "점검 예약됨",
    active: "점검 중",
};

//Each page refreshes itself while open, leaving it stops its timers
function stopPageTimers() {
    clearInterval(mt_tick_timer);
    clearInterval(mt_poll_timer);
    clearInterval(msg_tick_timer);
    clearInterval(msg_poll_timer);
    mt_tick_timer = null;
    mt_poll_timer = null;
    msg_tick_timer = null;
    msg_poll_timer = null;
}

function formatDate(value) {
    return value ? new Date(value).toLocaleString("ko-KR") : "-";
}

function formatDuration(sec) {
    sec = Math.max(0, Math.floor(sec));
    const h = Math.floor(sec / 3600);
    const m = Math.floor((sec % 3600) / 60);
    const s = sec % 60;
    const mm = String(m).padStart(2, "0");
    const ss = String(s).padStart(2, "0");
    return h > 0 ? h + ":" + mm + ":" + ss : mm + ":" + ss;
}

//datetime-local inputs use the browser's local time without timezone
function toLocalInput(date) {
    const pad = (n) => String(n).padStart(2, "0");
    return date.getFullYear() + "-" + pad(date.getMonth() + 1) + "-" + pad(date.getDate())
        + "T" + pad(date.getHours()) + ":" + pad(date.getMinutes());
}

function serverNow() {
    return Date.now() + mt_clock_offset;
}

function getRemainSec() {
    if (!mt_state || mt_state.info.state !== "scheduled")
        return 0;
    return mt_state.info.remain_sec - (performance.now() - mt_loaded_at) / 1000;
}

function setMaintenanceState(state) {
    mt_state = state;
    mt_loaded_at = performance.now();
    mt_clock_offset = new Date(state.server_time).getTime() - Date.now();
    renderMaintenance();
}

async function loadMaintenance() {
    document.getElementById("mt-error").textContent = "";
    try {
        setMaintenanceState(await apiGet("/admin/maintenance"));
        document.getElementById("mt-whitelist-input").value = mt_state.whitelist.join("\n");
        fillMaintenanceForm();
    } catch (e) {
        return handleError(e, "mt-error");
    }

    stopPageTimers();
    mt_tick_timer = setInterval(renderCountdown, 1000);
    mt_poll_timer = setInterval(refreshMaintenance, 15000);
}

//Periodic refresh keeps the form untouched so an admin typing is not interrupted
async function refreshMaintenance() {
    try {
        setMaintenanceState(await apiGet("/admin/maintenance"));
    } catch (e) {
        handleError(e, "mt-error");
    }
}

//datetime-local drops the seconds, one extra minute keeps "+30 min" above the minimum notice
function timeFromNow(minutes) {
    return new Date(serverNow() + (minutes + 1) * 60 * 1000);
}

function getMinNoticeMinutes() {
    return Math.ceil(mt_state.min_notice / 60);
}

function fillMaintenanceForm() {
    const info = mt_state.info;
    const scheduled = info.state === "scheduled";
    const default_time = timeFromNow(getMinNoticeMinutes());
    document.getElementById("mt-shutdown-input").value = toLocalInput(scheduled ? new Date(info.shutdown_at) : default_time);
    document.getElementById("mt-end-input").value = scheduled && info.end_at ? toLocalInput(new Date(info.end_at)) : "";
    document.getElementById("mt-message-input").value = scheduled ? info.message : "";
    document.getElementById("mt-block-input").value = mt_state.block_match_minutes ?? 5;
}

function renderMaintenance() {
    const info = mt_state.info;
    const badge = document.getElementById("mt-badge");
    badge.textContent = MaintenanceLabels[info.state] || info.state;
    badge.className = "badge badge-" + info.state;

    document.getElementById("mt-shutdown").textContent = formatDate(info.shutdown_at);
    document.getElementById("mt-end").textContent = formatDate(info.end_at);
    document.getElementById("mt-message").textContent = info.message || "-";
    document.getElementById("mt-match").textContent = info.matchmaking_blocked ? "차단됨" : "허용";
    document.getElementById("mt-updated").textContent = mt_state.updated_by
        ? mt_state.updated_by + " · " + formatDate(mt_state.updated_at) : "-";

    document.getElementById("mt-cancel").classList.toggle("hidden", info.state !== "scheduled");
    document.getElementById("mt-open").classList.toggle("hidden", info.state !== "active");
    document.getElementById("mt-form").classList.toggle("hidden", info.state === "active");
    document.getElementById("mt-form-title").textContent = info.state === "scheduled" ? "점검 예약 변경" : "점검 예약";
    document.getElementById("mt-submit").textContent = info.state === "scheduled" ? "예약 변경" : "점검 예약";
    document.getElementById("mt-form-note").textContent =
        "종료 시각은 지금부터 최소 " + getMinNoticeMinutes() + "분 뒤여야 합니다. 유저에게는 종료 10분·5분 전에 안내 문구가, 마지막 1분 동안 카운트다운이 표시됩니다.";

    renderCountdown();
}

function renderCountdown() {
    if (!mt_state) return;
    const elem = document.getElementById("mt-countdown");
    if (mt_state.info.state !== "scheduled") {
        elem.textContent = "";
        return;
    }

    const remain = getRemainSec();
    if (remain <= 0) {
        elem.textContent = "종료 시각 도달";
        if (!mt_refresh_pending) {
            mt_refresh_pending = true; //Server now reports the active state
            refreshMaintenance().finally(() => { mt_refresh_pending = false; });
        }
        return;
    }
    elem.textContent = "종료까지 " + formatDuration(remain);
}

async function submitMaintenance(e) {
    e.preventDefault();
    document.getElementById("mt-error").textContent = "";

    const shutdown_value = document.getElementById("mt-shutdown-input").value;
    const end_value = document.getElementById("mt-end-input").value;
    const shutdown_at = new Date(shutdown_value);
    const body = {
        shutdown_at: shutdown_at.toISOString(),
        end_at: end_value ? new Date(end_value).toISOString() : "",
        message: document.getElementById("mt-message-input").value.trim(),
        block_match_minutes: parseInt(document.getElementById("mt-block-input").value),
    };

    if (isNaN(shutdown_at.getTime()) || shutdown_at.getTime() - serverNow() < mt_state.min_notice * 1000) {
        document.getElementById("mt-error").textContent = "종료 시각은 지금부터 최소 " + getMinNoticeMinutes() + "분 뒤여야 합니다.";
        return;
    }

    const minutes = Math.round((shutdown_at.getTime() - serverNow()) / 60000);
    const confirmed = await confirmAction(
        mt_state.info.state === "scheduled" ? "점검 예약 변경" : "점검 예약",
        shutdown_at.toLocaleString("ko-KR") + " (약 " + minutes + "분 후)에 접속 중인 모든 유저가 종료되고 서버가 점검 상태가 됩니다.",
        "점검");
    if (!confirmed) return;

    try {
        setMaintenanceState(await apiPost("/admin/maintenance/schedule", body));
        fillMaintenanceForm();
    } catch (err) {
        handleError(err, "mt-error");
    }
}

async function cancelMaintenance() {
    document.getElementById("mt-error").textContent = "";
    var message = "예약된 점검을 취소합니다.";
    if (getRemainSec() < mt_state.heartbeat_interval)
        message += " 종료까지 " + mt_state.heartbeat_interval + "초도 남지 않아, 취소를 받기 전에 종료되는 클라이언트가 있을 수 있습니다.";
    if (!await confirmAction("점검 예약 취소", message))
        return;

    try {
        setMaintenanceState(await apiPost("/admin/maintenance/cancel"));
        fillMaintenanceForm();
    } catch (err) {
        handleError(err, "mt-error");
    }
}

async function openServer() {
    document.getElementById("mt-error").textContent = "";
    if (!await confirmAction("점검 해제", "점검을 끝내고 모든 유저의 접속을 허용합니다. 게임 서버가 켜져 있는지 먼저 확인하세요.", "오픈"))
        return;

    try {
        setMaintenanceState(await apiPost("/admin/maintenance/open"));
        fillMaintenanceForm();
    } catch (err) {
        handleError(err, "mt-error");
    }
}

async function saveWhitelist(e) {
    e.preventDefault();
    document.getElementById("mt-error").textContent = "";
    const usernames = document.getElementById("mt-whitelist-input").value
        .split("\n").map((name) => name.trim()).filter((name) => name.length > 0);

    try {
        setMaintenanceState(await apiPost("/admin/maintenance/whitelist", { usernames: usernames }));
        document.getElementById("mt-whitelist-input").value = mt_state.whitelist.join("\n");
    } catch (err) {
        handleError(err, "mt-error");
    }
}

// ---- Messages ----

var msg_state = null;      //Last /admin/messages response
var msg_clock_offset = 0;  //Server time - local time, in ms
var msg_tick_timer = null;
var msg_poll_timer = null;

function msgServerNow() {
    return Date.now() + msg_clock_offset;
}

function setMessagesState(state) {
    msg_state = state;
    msg_clock_offset = new Date(state.server_time).getTime() - Date.now();
    renderMessages();
}

async function loadMessages() {
    document.getElementById("msg-error").textContent = "";
    try {
        setMessagesState(await apiGet("/admin/messages"));
    } catch (e) {
        return handleError(e, "msg-error");
    }

    document.getElementById("msg-hint").textContent = "발송하고 " + Math.round(msg_state.delay / 60)
        + "분 뒤, 그 시각에 접속 중인 모든 유저에게 동시에 표시됩니다. 표시 전까지는 취소할 수 있습니다.";
    document.getElementById("msg-text").maxLength = msg_state.max_length;
    updateMessageCount();

    stopPageTimers();
    msg_tick_timer = setInterval(renderMessages, 1000);
    msg_poll_timer = setInterval(refreshMessages, 15000);
}

async function refreshMessages() {
    try {
        setMessagesState(await apiGet("/admin/messages"));
    } catch (e) {
        handleError(e, "msg-error");
    }
}

function updateMessageCount() {
    if (!msg_state) return;
    const length = document.getElementById("msg-text").value.trim().length;
    document.getElementById("msg-count").textContent = length + " / " + msg_state.max_length + "자";
}

function getMessageRemainSec(message) {
    return (new Date(message.show_at).getTime() - msgServerNow()) / 1000;
}

function getMessageStatus(message) {
    if (message.canceled)
        return { key: "canceled", text: "취소됨 · " + message.canceled_by };
    const remain = getMessageRemainSec(message);
    if (remain > 0)
        return { key: "pending", text: "대기 · " + formatDuration(remain) + " 후 표시" };
    return { key: "sent", text: "표시됨" };
}

function renderMessages() {
    if (!msg_state) return;
    const table = document.getElementById("msg-table");
    table.innerHTML = "";

    const columns = [
        { label: "표시 시각" }, { label: "문구" }, { label: "상태" }, { label: "보낸 관리자" }, { label: "" },
    ];
    table.appendChild(buildHeader(columns, null));

    const tbody = document.createElement("tbody");
    for (const message of msg_state.messages) {
        const status = getMessageStatus(message);
        const tr = document.createElement("tr");
        const cells = [formatDate(message.show_at), message.text, status.text, message.created_by];
        cells.forEach((value, i) => {
            const td = document.createElement("td");
            td.textContent = value;
            if (i === 1) td.className = "wrap";
            if (i === 2) td.className = "status-" + status.key;
            tr.appendChild(td);
        });

        const action = document.createElement("td");
        if (status.key === "pending") {
            const button = document.createElement("button");
            button.className = "table-btn";
            button.textContent = "취소";
            button.addEventListener("click", () => cancelMessage(message));
            action.appendChild(button);
        }
        tr.appendChild(action);
        tbody.appendChild(tr);
    }
    table.appendChild(tbody);
}

async function sendMessage(e) {
    e.preventDefault();
    document.getElementById("msg-error").textContent = "";
    const text = document.getElementById("msg-text").value.trim();
    if (text.length === 0)
        return document.getElementById("msg-error").textContent = "보낼 문구를 입력하세요.";

    const minutes = Math.round(msg_state.delay / 60);
    if (!await confirmAction("메시지 발송", minutes + "분 뒤 접속 중인 모든 유저에게 표시됩니다.\n\n" + text))
        return;

    try {
        setMessagesState(await apiPost("/admin/messages/send", { text: text }));
        document.getElementById("msg-text").value = "";
        updateMessageCount();
    } catch (err) {
        handleError(err, "msg-error");
    }
}

async function cancelMessage(message) {
    document.getElementById("msg-error").textContent = "";
    var text = "표시 전인 메시지를 취소합니다.\n\n" + message.text;
    if (getMessageRemainSec(message) < 60)
        text += "\n\n표시까지 1분도 남지 않아, 취소를 받기 전에 표시되는 클라이언트가 있을 수 있습니다.";
    if (!await confirmAction("메시지 취소", text))
        return;

    try {
        setMessagesState(await apiPost("/admin/messages/cancel", { id: message.id }));
    } catch (err) {
        handleError(err, "msg-error");
    }
}

// ---- Audit log ----

const AuditLabels = {
    admin_maintenance_schedule: "점검 예약",
    admin_maintenance_cancel: "점검 예약 취소",
    admin_maintenance_open: "점검 해제",
    admin_maintenance_whitelist: "화이트리스트 변경",
    admin_message_send: "메시지 발송",
    admin_message_cancel: "메시지 취소",
};

async function loadAudit() {
    document.getElementById("audit-error").textContent = "";
    var logs;
    try {
        logs = await apiGet("/admin/audit");
    } catch (e) {
        return handleError(e, "audit-error");
    }

    const rows = logs.map((log) => ({
        timestamp: log.timestamp,
        username: log.username,
        action: AuditLabels[log.type] || log.type,
        data: log.data && Object.keys(log.data).length > 0 ? JSON.stringify(log.data) : "",
    }));
    const columns = [
        { key: "timestamp", label: "시각", type: "date" },
        { key: "username", label: "관리자", type: "string" },
        { key: "action", label: "작업", type: "string" },
        { key: "data", label: "내용", type: "string" },
    ];

    document.getElementById("audit-info").textContent = "최근 " + rows.length + "건";
    const table = document.getElementById("audit-table");
    table.innerHTML = "";
    table.appendChild(buildHeader(columns, null));
    table.appendChild(buildBody(columns, rows));
}

// ---- Stats ----

async function loadStat(statId) {
    document.getElementById("stat-error").textContent = "";
    document.getElementById("table-filter").value = "";
    sort_key = null;

    var stat;
    try {
        stat = await apiGet("/stats/" + encodeURIComponent(statId));
    } catch (e) {
        return handleError(e, "stat-error");
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

//on_sort is optional, headers are clickable only when it is set
function buildHeader(columns, on_sort) {
    const thead = document.createElement("thead");
    const header_row = document.createElement("tr");
    for (const col of columns) {
        const th = document.createElement("th");
        th.textContent = col.label + (on_sort && sort_key === col.key ? (sort_asc ? " ▲" : " ▼") : "");
        th.className = (col.type === "number" ? "num" : "") + (on_sort ? " sortable" : "");
        if (on_sort)
            th.addEventListener("click", () => on_sort(col));
        header_row.appendChild(th);
    }
    thead.appendChild(header_row);
    return thead;
}

function buildBody(columns, rows) {
    const tbody = document.createElement("tbody");
    for (const row of rows) {
        const tr = document.createElement("tr");
        for (const col of columns) {
            const td = document.createElement("td");
            td.textContent = formatValue(row[col.key], col.type);
            td.className = col.type === "number" ? "num" : "";
            tr.appendChild(td);
        }
        tbody.appendChild(tr);
    }
    return tbody;
}

function renderTable(stat, table) {
    const rows = getFilteredRows(stat);

    document.getElementById("stat-info").textContent =
        "총 " + rows.length + "건 · 조회 시각 " + new Date(stat.generated_at).toLocaleString("ko-KR");

    table.innerHTML = "";
    table.appendChild(buildHeader(stat.columns, (col) => {
        sort_asc = (sort_key === col.key) ? !sort_asc : (col.type !== "number" && col.type !== "date");
        sort_key = col.key;
        renderStat();
    }));
    table.appendChild(buildBody(stat.columns, rows));
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

for (const item of document.querySelectorAll(".menu-item[data-page]"))
    item.addEventListener("click", () => openPage(item.dataset.page));

document.getElementById("mt-refresh").addEventListener("click", () => loadMaintenance());
document.getElementById("mt-form").addEventListener("submit", submitMaintenance);
document.getElementById("mt-cancel").addEventListener("click", cancelMaintenance);
document.getElementById("mt-open").addEventListener("click", openServer);
document.getElementById("mt-whitelist-form").addEventListener("submit", saveWhitelist);
for (const button of document.querySelectorAll("[data-add-min]")) {
    button.addEventListener("click", () => {
        const minutes = parseInt(button.dataset.addMin);
        document.getElementById("mt-shutdown-input").value = toLocalInput(timeFromNow(minutes));
    });
}
document.getElementById("msg-refresh").addEventListener("click", () => loadMessages());
document.getElementById("msg-form").addEventListener("submit", sendMessage);
document.getElementById("msg-text").addEventListener("input", updateMessageCount);
document.getElementById("audit-refresh").addEventListener("click", () => loadAudit());

// ---- Init ----

if (token)
    openDashboard();
else
    show("login-view");
