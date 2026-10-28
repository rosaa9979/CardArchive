//Card Archive admin tool.
//Operation pages (users, maintenance, messages, audit log) are static pages in index.html.
//Stat tables are rendered generically from the columns definition sent by
//the server (/stats/:id), so adding a new stat only requires a new backend
//module in stats/modules/. To customize how one stat is displayed, register
//a function in CustomRenderers below: CustomRenderers["stat-id"] = (stat, container) => {...}

const CustomRenderers = {};

var token = sessionStorage.getItem("admin_token") || "";
var admin_name = sessionStorage.getItem("admin_name") || "";
var current_page = null;   //"users", "maintenance", "messages", "audit" or "stat:<id>"
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
    if (page === "users")
        openUserList();
    else if (page === "maintenance")
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

    openPage("users");
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

// ---- Users ----

var users_query = "";      //Search text of the list being shown
var users_page = 0;
var user_detail = null;    //Last /admin/users/:id response
var user_tab = "decks";
var open_deck = null;      //Deck tid expanded in the decks tab
var users_notice = "";     //Result of the last action shown once above the list (account deleted)
var catalog = null;        //Last /admin/catalog response, loaded once when the first user is opened

const PermissionLabels = { 1: "유저", 5: "게임 서버", 10: "관리자" };

const MatchResultLabels = {
    win: "승리",
    lose: "패배",
    none: "무승부·취소",
    ongoing: "진행 중",
};

const ActivityLabels = {
    register: "가입",
    edit_email: "이메일 변경",
    edit_password: "비밀번호 변경",
    edit_permission: "권한 변경",
    fix_variants: "카드 변형 수정",
    user_buy_card: "카드 구매",
    user_sell_card: "카드 판매",
    user_buy_pack: "팩 구매",
    user_sell_pack: "팩 판매",
    user_open_pack: "팩 개봉",
    market_add: "거래소 등록",
    market_remove: "거래소 등록 취소",
    market_trade: "거래소 거래",
    reward_add: "보상 등록",
    reward_give: "보상 지급",
    reward_gain: "보상 수령",
};

function getPermissionLabel(level) {
    return PermissionLabels[level] ? PermissionLabels[level] + " (" + level + ")" : String(level);
}

//"3분 전" style age, relative to the server clock of the last response
function formatAgo(value, server_time) {
    if (!value) return "-";
    const sec = Math.max(0, (new Date(server_time).getTime() - new Date(value).getTime()) / 1000);
    if (sec < 60) return "방금";
    if (sec < 3600) return Math.floor(sec / 60) + "분 전";
    if (sec < 86400) return Math.floor(sec / 3600) + "시간 전";
    return Math.floor(sec / 86400) + "일 전";
}

function formatRecord(user) {
    const rate = user.matches > 0 ? Math.round(user.victories / user.matches * 100) + "%" : "-";
    return user.matches + "전 " + user.victories + "승 " + user.defeats + "패 (" + rate + ")";
}

function makeCell(text, class_name) {
    const td = document.createElement("td");
    td.textContent = text;
    if (class_name) td.className = class_name;
    return td;
}

function makeStatusCell(online, banned) {
    const td = document.createElement("td");
    const dot = document.createElement("span");
    dot.className = "dot" + (banned ? " dot-banned" : online ? " dot-online" : "");
    td.appendChild(dot);
    td.appendChild(document.createTextNode(banned ? "정지" : online ? "접속 중" : "오프라인"));
    td.className = banned ? "text-banned" : online ? "text-online" : "text-offline";
    return td;
}

function setUserView(detail) {
    document.getElementById("users-list-view").classList.toggle("hidden", detail);
    document.getElementById("user-detail-view").classList.toggle("hidden", !detail);
}

//Coming back from a user keeps the last search and page
function openUserList() {
    setUserView(false);
    loadUsers(users_query, users_page);
}

async function loadUsers(query, page) {
    document.getElementById("users-error").textContent = "";
    var result;
    try {
        result = await apiGet("/admin/users?q=" + encodeURIComponent(query) + "&page=" + page);
    } catch (e) {
        return handleError(e, "users-error");
    }

    users_query = query;
    users_page = page;
    renderUsers(result);
}

function renderUsers(result) {
    const online = result.users.filter((user) => user.online).length;
    document.getElementById("users-info").textContent = (users_notice ? users_notice + " · " : "") + (users_query ? "\"" + users_query + "\" 검색 · " : "최근 접속순 · ")
        + "이 페이지 " + result.users.length + "명 중 접속 중 " + online + "명 · 조회 시각 " + formatDate(result.server_time);

    const columns = [
        { label: "상태" }, { label: "유저명" }, { label: "권한" }, { label: "코인", type: "number" },
        { label: "ELO", type: "number" }, { label: "전적" }, { label: "최근 접속" }, { label: "가입일" },
    ];
    const table = document.getElementById("users-table");
    table.innerHTML = "";
    table.appendChild(buildHeader(columns, null));

    const tbody = document.createElement("tbody");
    for (const user of result.users) {
        const tr = document.createElement("tr");
        tr.className = "clickable";
        tr.appendChild(makeStatusCell(user.online, user.banned));
        tr.appendChild(makeCell(user.username));
        tr.appendChild(makeCell(getPermissionLabel(user.permission_level)));
        tr.appendChild(makeCell(formatValue(user.coins, "number"), "num"));
        tr.appendChild(makeCell(formatValue(user.elo, "number"), "num"));
        tr.appendChild(makeCell(formatRecord(user)));
        tr.appendChild(makeCell(formatAgo(user.last_online_time, result.server_time)));
        tr.appendChild(makeCell(formatDate(user.account_create_time)));
        tr.addEventListener("click", () => openUser(user.id));
        tbody.appendChild(tr);
    }
    if (result.users.length === 0) {
        const tr = document.createElement("tr");
        const td = makeCell("검색 결과가 없습니다.", "text-offline");
        td.colSpan = columns.length;
        tr.appendChild(td);
        tbody.appendChild(tr);
    }
    table.appendChild(tbody);

    users_notice = "";
    document.getElementById("users-page").textContent = (users_page + 1) + "페이지";
    document.getElementById("users-prev").disabled = users_page === 0;
    document.getElementById("users-next").disabled = !result.has_more;
}

async function openUser(id) {
    setUserView(true);
    document.getElementById("user-error").textContent = "";
    var detail;
    try {
        detail = await apiGet("/admin/users/" + encodeURIComponent(id));
    } catch (e) {
        return handleError(e, "user-error");
    }

    if (!user_detail || user_detail.user.id !== detail.user.id) {
        user_tab = "decks";
        open_deck = null;
        resetManageForms();
        resetGrantForm();
    }
    if (!catalog)
        await loadCatalog();
    user_detail = detail;
    renderUser();
}

function fillInfoList(id, items) {
    const list = document.getElementById(id);
    list.innerHTML = "";
    for (const [label, value] of items) {
        const dt = document.createElement("dt");
        dt.textContent = label;
        const dd = document.createElement("dd");
        dd.textContent = value === null || value === undefined || value === "" ? "-" : value;
        list.appendChild(dt);
        list.appendChild(dd);
    }
}

function renderUser() {
    const detail = user_detail;
    const user = detail.user;

    const title = document.getElementById("user-title");
    title.innerHTML = "";
    const dot = document.createElement("span");
    dot.className = "dot" + (detail.banned ? " dot-banned" : detail.online ? " dot-online" : "");
    title.appendChild(dot);
    title.appendChild(document.createTextNode(user.username));

    document.getElementById("user-info").textContent = (detail.banned ? "정지 · " : "") + (detail.online ? "접속 중" : "오프라인")
        + " · 최근 접속 " + formatAgo(user.last_online_time, detail.server_time)
        + " · 조회 시각 " + formatDate(detail.server_time);

    fillInfoList("user-profile", [
        ["ID", user.id],
        ["이메일", user.email],
        ["권한", getPermissionLabel(user.permission_level)],
        ["이메일 인증", user.validation_level > 0 ? "인증됨" : "미인증"],
        ["가입일", formatDate(user.account_create_time)],
        ["최근 로그인", formatDate(user.last_login_time)],
        ["최근 접속", formatDate(user.last_online_time)],
        ["아바타", user.avatar],
        ["카드 뒷면", user.cardback],
        ["친구", detail.friend_count + "명"],
    ]);
    fillInfoList("user-stats", [
        ["코인", formatValue(user.coins, "number")],
        ["XP", formatValue(user.xp, "number")],
        ["ELO", formatValue(user.elo, "number")],
        ["전적", formatRecord(user)],
        ["덱", detail.decks.length + "개"],
        ["보유 카드", detail.cards.length + "종 " + detail.cards.reduce((n, c) => n + c.quantity, 0) + "장"],
    ]);

    const counts = {
        decks: detail.decks.length,
        cards: detail.cards.length,
        packs: detail.packs.length,
        matches: detail.matches.length,
        activities: detail.activities.length,
    };
    for (const tab of document.querySelectorAll("#user-tabs .tab")) {
        tab.classList.toggle("active", tab.dataset.tab === user_tab);
        tab.textContent = UserTabLabels[tab.dataset.tab] + " (" + counts[tab.dataset.tab] + ")";
    }
    renderManage();
    renderUserTab();
}

// ---- User account actions ----

function resetManageForms() {
    document.getElementById("user-ban-days").value = 7;
    document.getElementById("user-ban-reason").value = "";
    document.getElementById("user-password-input").value = "";
}

function renderManage() {
    const detail = user_detail;
    const user = detail.user;

    const badge = document.getElementById("user-ban-badge");
    badge.textContent = detail.banned ? "정지 중" : "정상";
    badge.className = "badge " + (detail.banned ? "badge-active" : "badge-none");

    document.getElementById("user-manage-note").classList.toggle("hidden", detail.manageable);
    document.getElementById("user-manage-body").classList.toggle("hidden", !detail.manageable);
    document.getElementById("user-grant-form").classList.toggle("hidden", !detail.manageable);

    //An expired ban stays in the document until it is replaced or cleared, shown as the last ban
    const items = [];
    if (user.ban_time) {
        items.push([detail.banned ? "정지 해제" : "지난 정지 해제", user.ban_until ? formatDate(user.ban_until) : "영구"]);
        items.push(["정지 사유", user.ban_reason]);
        items.push(["처리", (user.ban_by || "-") + " · " + formatDate(user.ban_time)]);
    }
    items.push(["마지막 강제 종료", user.session_revoke_time ? formatDate(user.session_revoke_time) : ""]);
    fillInfoList("user-ban-info", items);

    document.getElementById("user-ban-submit").textContent = detail.banned ? "정지 변경" : "정지";
    document.getElementById("user-unban").classList.toggle("hidden", !user.ban_time);

    const result = document.getElementById("user-password-result");
    result.classList.toggle("hidden", !detail.temp_password);
    result.innerHTML = "";
    if (detail.temp_password) {
        result.appendChild(document.createTextNode("임시 비밀번호: "));
        const code = document.createElement("code");
        code.textContent = detail.temp_password;
        result.appendChild(code);
        result.appendChild(document.createTextNode("\n이 화면을 벗어나면 다시 볼 수 없습니다. 유저에게 전달하고 로그인 후 변경하도록 안내하세요."));
    }
}

//Posts an account action and shows the refreshed detail it answers
async function runUserAction(action, body) {
    document.getElementById("user-error").textContent = "";
    try {
        user_detail = await apiPost("/admin/users/" + encodeURIComponent(user_detail.user.id) + "/" + action, body);
    } catch (e) {
        handleError(e, "user-error");
        return false;
    }
    renderUser();
    return true;
}

async function banUser(e) {
    e.preventDefault();
    const user = user_detail.user;
    const days = parseInt(document.getElementById("user-ban-days").value);
    const reason = document.getElementById("user-ban-reason").value.trim();
    if (isNaN(days) || days < 0 || days > 3650 || !reason) {
        document.getElementById("user-error").textContent = "정지 기간(0~3650일)과 사유를 입력하세요.";
        return;
    }

    const period = days > 0 ? days + "일" : "영구";
    if (!await confirmAction(user_detail.banned ? "정지 변경" : "계정 정지",
        user.username + " 계정을 " + period + " 정지합니다.\n사유: " + reason + "\n\n접속 중이면 다음 요청이나 하트비트(최대 1분) 때 종료되고, 기간 동안 로그인할 수 없습니다.",
        user.username))
        return;

    if (await runUserAction("ban", { days: days, reason: reason }))
        resetManageForms();
}

async function unbanUser() {
    const user = user_detail.user;
    const message = user_detail.banned
        ? user.username + " 계정의 정지를 해제합니다. 바로 다시 로그인할 수 있습니다."
        : user.username + " 계정의 지난 정지 기록을 지웁니다.";
    if (!await confirmAction("정지 해제", message))
        return;
    await runUserAction("unban");
}

async function kickUser() {
    const user = user_detail.user;
    if (!await confirmAction("강제 로그아웃", user.username + " 계정을 모든 기기에서 로그아웃시킵니다. 접속 중이면 다음 요청이나 하트비트(최대 1분) 때 종료되며, 다시 로그인할 수 있습니다."))
        return;
    await runUserAction("kick");
}

async function setUserPassword(e) {
    e.preventDefault();
    const user = user_detail.user;
    const password = document.getElementById("user-password-input").value;
    if (password && (password.length < 4 || password.length > 50)) {
        document.getElementById("user-error").textContent = "비밀번호는 4~50자여야 합니다.";
        return;
    }

    const message = (password ? user.username + " 계정의 비밀번호를 입력한 값으로 바꿉니다." : user.username + " 계정의 비밀번호를 임시 비밀번호로 바꿉니다. 임시 비밀번호는 한 번만 표시됩니다.")
        + "\n\n모든 기기에서 로그아웃됩니다.";
    if (!await confirmAction("비밀번호 변경", message))
        return;

    if (await runUserAction("password", { password: password }))
        document.getElementById("user-password-input").value = "";
}

async function deleteUser() {
    const user = user_detail.user;
    if (!await confirmAction("계정 삭제",
        user.username + " 계정과 보유 카드, 덱, 재화, 거래소 등록을 모두 삭제합니다. 되돌릴 수 없습니다.\n매치 기록과 활동 기록에는 이름이 남습니다.",
        user.username))
        return;

    document.getElementById("user-error").textContent = "";
    var result;
    try {
        result = await apiPost("/admin/users/" + encodeURIComponent(user.id) + "/delete", { confirm: user.username });
    } catch (e) {
        return handleError(e, "user-error");
    }

    user_detail = null;
    users_notice = result.deleted.username + " 계정을 삭제했습니다";
    openUserList();
}

const UserTabLabels = {
    decks: "덱",
    cards: "보유 카드",
    packs: "팩",
    matches: "최근 매치",
    activities: "활동 기록",
};

function cardName(entry) {
    return entry.title ? entry.title + " (" + entry.tid + ")" : entry.tid;
}

function renderUserTab() {
    const detail = user_detail;
    const table = document.getElementById("user-tab-table");
    table.innerHTML = "";
    document.getElementById("user-deck-tools").classList.toggle("hidden", user_tab !== "decks" || !detail.manageable);

    if (user_tab === "decks")
        return renderDecks(table);
    if (user_tab === "matches")
        return renderMatches(table);

    var columns, rows;
    if (user_tab === "cards") {
        columns = [
            { key: "name", label: "카드", type: "string" },
            { key: "variant", label: "변형", type: "string" },
            { key: "quantity", label: "수량", type: "number" },
        ];
        rows = detail.cards.map((c) => ({ name: cardName(c), variant: c.variant, quantity: c.quantity }));
    } else if (user_tab === "packs") {
        columns = [
            { key: "tid", label: "팩", type: "string" },
            { key: "quantity", label: "수량", type: "number" },
        ];
        rows = detail.packs;
    } else {
        columns = [
            { key: "timestamp", label: "시각", type: "date" },
            { key: "type", label: "활동", type: "string" },
            { key: "data", label: "내용", type: "string" },
        ];
        rows = detail.activities.map((a) => ({
            timestamp: a.timestamp,
            type: ActivityLabels[a.type] || AuditLabels[a.type] || a.type,
            data: a.data && Object.keys(a.data).length > 0 ? JSON.stringify(a.data) : "",
        }));
    }

    table.appendChild(buildHeader(columns, null));
    table.appendChild(buildBody(columns, rows));
}

function renderMatches(table) {
    const columns = [
        { label: "시작" }, { label: "상대" }, { label: "결과" }, { label: "모드" }, { label: "종료" },
    ];
    table.appendChild(buildHeader(columns, null));
    const tbody = document.createElement("tbody");
    for (const match of user_detail.matches) {
        const tr = document.createElement("tr");
        tr.appendChild(makeCell(formatDate(match.start)));
        tr.appendChild(makeCell(match.opponent || "-"));
        tr.appendChild(makeCell(MatchResultLabels[match.result] || match.result, "result-" + match.result));
        tr.appendChild(makeCell((match.ranked ? "랭크" : "일반") + (match.mode ? " · " + match.mode : "")));
        tr.appendChild(makeCell(formatDate(match.end)));
        tbody.appendChild(tr);
    }
    table.appendChild(tbody);
}

function makeButtonCell(buttons) {
    const td = document.createElement("td");
    for (const [label, action] of buttons) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "table-btn";
        button.textContent = label;
        button.addEventListener("click", (e) => { e.stopPropagation(); action(); });
        td.appendChild(button);
    }
    return td;
}

//Clicking a deck shows its cards and rule warnings below it
function renderDecks(table) {
    const manageable = user_detail.manageable;
    const columns = [
        { label: "덱 이름" }, { label: "대표" }, { label: "클럽" }, { label: "카드 수", type: "number" }, { label: "검사" }, { label: "" },
    ];
    table.appendChild(buildHeader(columns, null));
    const tbody = document.createElement("tbody");
    for (const deck of user_detail.decks) {
        const opened = open_deck === deck.tid;
        const tr = document.createElement("tr");
        tr.className = "clickable";
        tr.appendChild(makeCell(deck.title || deck.tid));
        tr.appendChild(makeCell(deck.hero ? cardName(deck.hero) : "-"));
        tr.appendChild(makeCell(deck.clubs.map((c) => c.title || c.tid).join(", ") || "-"));
        tr.appendChild(makeCell(formatValue(deck.card_count, "number"), "num"));
        tr.appendChild(makeCell(deck.issues.length ? "경고 " + deck.issues.length : "정상", deck.issues.length ? "text-warning" : "text-online"));
        tr.appendChild(manageable
            ? makeButtonCell([["수정", () => openDeckEditor(deck)], ["삭제", () => deleteDeck(deck)], [opened ? "▲" : "▼", () => toggleDeck(deck)]])
            : makeCell(opened ? "▲" : "▼", "text-offline"));
        tr.addEventListener("click", () => {
            open_deck = opened ? null : deck.tid;
            renderUserTab();
        });
        tbody.appendChild(tr);

        if (opened) {
            const sub = document.createElement("tr");
            sub.className = "sub-row";
            const lines = deck.issues.map((issue) => "⚠ " + issue);
            if (lines.length) lines.push("");
            const td = makeCell(lines.concat(deck.cards.map((c) => cardName(c) + " ×" + c.quantity)).join("\n") || "카드 없음", "wrap");
            td.colSpan = columns.length;
            sub.appendChild(td);
            tbody.appendChild(sub);
        }
    }
    table.appendChild(tbody);
}

function toggleDeck(deck) {
    open_deck = open_deck === deck.tid ? null : deck.tid;
    renderUserTab();
}

// ---- User items and decks ----

const HeroType = "아로나";
const DeckCardTypes = ["학생", "비학생", "이벤트", "장소"];

var deck_edit = null;      //{tid ("" for a new deck), cards: [{tid, variant, quantity}]} while the deck modal is open
var deck_check_timer = null;
var deck_check_id = 0;     //Only the answer of the last check is shown

async function loadCatalog() {
    try {
        catalog = await apiGet("/admin/catalog");
    } catch (e) {
        handleError(e, "user-error");
        return;
    }
    catalog.card_map = new Map(catalog.cards.map((c) => [c.tid, c]));
    catalog.search_map = new Map();

    const fill = (id, items) => {
        const list = document.getElementById(id);
        list.innerHTML = "";
        for (const [value, tid] of items) {
            const option = document.createElement("option");
            option.value = value;
            list.appendChild(option);
            catalog.search_map.set(value, tid);
        }
    };
    const label = (card) => (card.title || card.tid) + " · " + card.tid;
    fill("catalog-cards", catalog.cards.map((c) => [label(c), c.tid]));
    fill("catalog-deck-cards", catalog.cards.filter((c) => DeckCardTypes.includes(c.type)).map((c) => [label(c), c.tid]));
    fill("catalog-packs", catalog.packs.map((tid) => [tid, tid]));
}

//Picked datalist text, or a typed id
function resolveSearch(text) {
    text = text.trim();
    return catalog.search_map.get(text) || text;
}

function catalogName(tid) {
    const card = catalog && catalog.card_map.get(tid);
    return card && card.title ? card.title + " (" + tid + ")" : tid;
}

function makeVariantSelect(value) {
    const select = document.createElement("select");
    for (const tid of catalog.variants) {
        const option = document.createElement("option");
        option.value = tid;
        option.textContent = tid === catalog.default_variant ? tid + " (기본)" : tid;
        select.appendChild(option);
    }
    select.value = value || catalog.default_variant;
    return select;
}

// ---- Grant ----

function resetGrantForm() {
    document.getElementById("grant-coins").value = 0;
    document.getElementById("grant-xp").value = 0;
    document.getElementById("grant-items").innerHTML = "";
    document.getElementById("grant-reason").value = "";
}

function addGrantRow(kind) {
    const row = document.createElement("div");
    row.className = "item-row";
    row.dataset.kind = kind;

    const kind_label = document.createElement("span");
    kind_label.className = "item-kind";
    kind_label.textContent = kind === "card" ? "카드" : "팩";
    row.appendChild(kind_label);

    const name = document.createElement("input");
    name.className = "item-name";
    name.setAttribute("list", kind === "card" ? "catalog-cards" : "catalog-packs");
    name.placeholder = kind === "card" ? "카드 이름 또는 ID" : "팩 ID";
    name.autocomplete = "off";
    row.appendChild(name);

    if (kind === "card") {
        const variant = makeVariantSelect("");
        variant.className = "item-variant";
        row.appendChild(variant);
    }

    const qty = document.createElement("input");
    qty.className = "item-qty";
    qty.type = "number";
    qty.step = "1";
    qty.value = 1;
    row.appendChild(qty);

    const remove = document.createElement("button");
    remove.type = "button";
    remove.className = "table-btn";
    remove.textContent = "삭제";
    remove.addEventListener("click", () => row.remove());
    row.appendChild(remove);

    document.getElementById("grant-items").appendChild(row);
    name.focus();
}

//Reads the grant form, null with an error shown when a value is invalid
function readGrant() {
    const grant = {
        coins: parseInt(document.getElementById("grant-coins").value) || 0,
        xp: parseInt(document.getElementById("grant-xp").value) || 0,
        cards: [],
        packs: [],
    };
    for (const row of document.querySelectorAll("#grant-items .item-row")) {
        const tid = resolveSearch(row.querySelector(".item-name").value);
        const quantity = parseInt(row.querySelector(".item-qty").value);
        if (!tid || !quantity) continue;
        if (row.dataset.kind === "card") {
            if (!catalog.card_map.has(tid))
                return grantError("없는 카드입니다: " + tid);
            grant.cards.push({ tid: tid, variant: row.querySelector(".item-variant").value, quantity: quantity });
        } else {
            if (!catalog.packs.includes(tid))
                return grantError("없는 팩입니다: " + tid);
            grant.packs.push({ tid: tid, quantity: quantity });
        }
    }
    return grant;
}

function grantError(message) {
    document.getElementById("user-error").textContent = message;
    return null;
}

function formatSigned(value) {
    return (value > 0 ? "+" : "") + formatValue(value, "number");
}

function describeGrant(grant) {
    const lines = [];
    if (grant.coins) lines.push("코인 " + formatSigned(grant.coins));
    if (grant.xp) lines.push("XP " + formatSigned(grant.xp));
    for (const card of grant.cards) lines.push("카드 " + catalogName(card.tid) + " [" + card.variant + "] " + formatSigned(card.quantity));
    for (const pack of grant.packs) lines.push("팩 " + pack.tid + " " + formatSigned(pack.quantity));
    return lines;
}

async function submitGrant(e) {
    e.preventDefault();
    document.getElementById("user-error").textContent = "";
    const grant = readGrant();
    if (!grant) return;
    const reason = document.getElementById("grant-reason").value.trim();
    const lines = describeGrant(grant);
    if (lines.length === 0)
        return grantError("지급하거나 회수할 항목을 입력하세요.");
    if (!reason)
        return grantError("사유를 입력하세요.");

    if (!await confirmAction("지급 · 회수", user_detail.user.username + "\n\n" + lines.join("\n") + "\n\n사유: " + reason))
        return;
    if (await runUserAction("grant", { grant: grant, reason: reason }))
        resetGrantForm();
}

// ---- Premade deck ----

function openPremadeModal() {
    const select = document.getElementById("premade-deck");
    select.innerHTML = "";
    for (const deck of catalog.decks) {
        const option = document.createElement("option");
        option.value = deck.tid;
        option.textContent = (deck.title || deck.tid) + " · " + deck.card_count + "장";
        select.appendChild(option);
    }
    document.getElementById("premade-reason").value = "";
    document.getElementById("premade-error").textContent = catalog.decks.length ? "" : "등록된 기본 덱이 없습니다.";
    document.getElementById("premade-modal").classList.remove("hidden");
}

async function submitPremade(e) {
    e.preventDefault();
    const tid = document.getElementById("premade-deck").value;
    const reason = document.getElementById("premade-reason").value.trim();
    if (!tid || !reason) {
        document.getElementById("premade-error").textContent = "덱과 사유를 선택하세요.";
        return;
    }
    document.getElementById("premade-modal").classList.add("hidden");
    await runUserAction("grant", { grant: { decks: [tid] }, reason: reason });
}

// ---- Deck editor ----

function openDeckEditor(deck) {
    deck_edit = {
        tid: deck ? deck.tid : "",
        cards: deck ? deck.cards.map((c) => ({ tid: c.tid, variant: c.variant || catalog.default_variant, quantity: c.quantity })) : [],
    };
    document.getElementById("deck-modal-title").textContent = deck ? "덱 수정 · " + (deck.title || deck.tid) : "새 덱 만들기";
    document.getElementById("deck-title").value = deck ? deck.title : "";
    document.getElementById("deck-reason").value = "";
    document.getElementById("deck-card-search").value = "";
    document.getElementById("deck-error").textContent = "";

    const hero = document.getElementById("deck-hero");
    hero.innerHTML = "";
    const none = document.createElement("option");
    none.value = "";
    none.textContent = "없음";
    hero.appendChild(none);
    for (const card of catalog.cards.filter((c) => c.type === HeroType)) {
        const option = document.createElement("option");
        option.value = card.tid;
        option.textContent = card.title || card.tid;
        hero.appendChild(option);
    }
    hero.value = deck && deck.hero ? deck.hero.tid : "";

    document.getElementById("deck-modal").classList.remove("hidden");
    renderDeckEditor();
}

function closeDeckEditor() {
    deck_edit = null;
    clearTimeout(deck_check_timer);
    document.getElementById("deck-modal").classList.add("hidden");
}

function readDeck() {
    const hero = document.getElementById("deck-hero").value;
    return {
        title: document.getElementById("deck-title").value.trim(),
        hero: hero ? { tid: hero, variant: catalog.default_variant } : {},
        cards: deck_edit.cards,
    };
}

function renderDeckEditor() {
    const table = document.getElementById("deck-card-table");
    table.innerHTML = "";
    table.appendChild(buildHeader([{ label: "카드" }, { label: "종류" }, { label: "변형" }, { label: "수량" }, { label: "" }], null));
    const tbody = document.createElement("tbody");
    deck_edit.cards.forEach((entry, index) => {
        const card = catalog.card_map.get(entry.tid);
        const tr = document.createElement("tr");
        tr.appendChild(makeCell(catalogName(entry.tid)));
        tr.appendChild(makeCell(card ? card.type : "?"));

        const variant_td = document.createElement("td");
        const variant = makeVariantSelect(entry.variant);
        variant.addEventListener("change", () => { entry.variant = variant.value; scheduleDeckCheck(); });
        variant_td.appendChild(variant);
        tr.appendChild(variant_td);

        const qty_td = document.createElement("td");
        const qty = document.createElement("input");
        qty.type = "number";
        qty.min = "1";
        qty.value = entry.quantity;
        qty.addEventListener("input", () => { entry.quantity = parseInt(qty.value) || 0; updateDeckSummary(); scheduleDeckCheck(); });
        qty_td.appendChild(qty);
        tr.appendChild(qty_td);

        tr.appendChild(makeButtonCell([["삭제", () => { deck_edit.cards.splice(index, 1); renderDeckEditor(); }]]));
        tbody.appendChild(tr);
    });
    if (deck_edit.cards.length === 0) {
        const tr = document.createElement("tr");
        const td = makeCell("카드를 추가하세요.", "text-offline");
        td.colSpan = 5;
        tr.appendChild(td);
        tbody.appendChild(tr);
    }
    table.appendChild(tbody);
    updateDeckSummary();
    scheduleDeckCheck();
}

function updateDeckSummary() {
    const total = deck_edit.cards.reduce((n, c) => n + (c.quantity || 0), 0);
    document.getElementById("deck-summary").textContent = "카드 " + total + " / " + catalog.rules.deck_size + "장";
}

function addDeckCard() {
    const input = document.getElementById("deck-card-search");
    const tid = resolveSearch(input.value);
    const card = catalog.card_map.get(tid);
    if (!card || !DeckCardTypes.includes(card.type)) {
        document.getElementById("deck-error").textContent = "덱에 넣을 수 있는 카드가 아닙니다: " + input.value;
        return;
    }
    document.getElementById("deck-error").textContent = "";
    const found = deck_edit.cards.find((c) => c.tid === tid && c.variant === catalog.default_variant);
    if (found) found.quantity++;
    else deck_edit.cards.push({ tid: tid, variant: catalog.default_variant, quantity: 1 });
    input.value = "";
    input.focus();
    renderDeckEditor();
}

//Asks the server for the derived clubs and the rule warnings, shortly after the last change
function scheduleDeckCheck() {
    clearTimeout(deck_check_timer);
    deck_check_timer = setTimeout(checkDeck, 300);
}

async function checkDeck() {
    if (!deck_edit) return;
    const id = ++deck_check_id;
    const deck = readDeck();
    const list = document.getElementById("deck-issues");
    var result;
    try {
        result = await apiPost("/admin/decks/check", { deck: Object.assign({}, deck, { title: deck.title || "-" }) });
    } catch (e) {
        if (e.auth) return handleError(e, "deck-error");
        result = { issues: [e.error], deck: null };
    }
    if (id !== deck_check_id || !deck_edit) return;

    list.innerHTML = "";
    for (const issue of result.issues) {
        const li = document.createElement("li");
        li.textContent = "⚠ " + issue;
        list.appendChild(li);
    }
    const clubs = result.deck ? result.deck.clubs.map((c) => catalogName(c.tid)).join(", ") : "";
    updateDeckSummary();
    document.getElementById("deck-summary").textContent += " · 동아리 " + (clubs || "없음");
    deck_edit.issues = result.issues;
}

async function submitDeck(e) {
    e.preventDefault();
    const deck = readDeck();
    const reason = document.getElementById("deck-reason").value.trim();
    const error = document.getElementById("deck-error");
    if (!deck.title || !reason) {
        error.textContent = "덱 이름과 사유를 입력하세요.";
        return;
    }
    if (deck.cards.some((c) => !c.quantity || c.quantity < 1)) {
        error.textContent = "수량은 1 이상이어야 합니다.";
        return;
    }

    var message = (deck_edit.tid ? "덱을 수정합니다." : "새 덱을 만듭니다.") + "\n사유: " + reason;
    if (deck_edit.issues && deck_edit.issues.length)
        message += "\n\n규칙에 맞지 않아 게임에서 사용할 수 없습니다:\n" + deck_edit.issues.join("\n");
    if (!await confirmAction(deck_edit.tid ? "덱 수정" : "새 덱 만들기", user_detail.user.username + " · " + deck.title + "\n" + message))
        return;

    error.textContent = "";
    const path = "/admin/users/" + encodeURIComponent(user_detail.user.id) + "/decks/" + encodeURIComponent(deck_edit.tid || "new");
    try {
        user_detail = await apiPost(path, { deck: deck, reason: reason });
    } catch (err) {
        return handleError(err, "deck-error");
    }
    closeDeckEditor();
    renderUser();
}

async function deleteDeck(deck) {
    const reason = await promptReason("덱 삭제", user_detail.user.username + " · " + (deck.title || deck.tid) + " 덱을 삭제합니다. 덱에 든 카드는 보유 카드에 그대로 남습니다.");
    if (!reason) return;
    document.getElementById("user-error").textContent = "";
    try {
        user_detail = await apiPost("/admin/users/" + encodeURIComponent(user_detail.user.id) + "/decks/" + encodeURIComponent(deck.tid) + "/delete", { reason: reason });
    } catch (e) {
        return handleError(e, "user-error");
    }
    renderUser();
}

//Confirm modal with a free text reason, resolves the reason or "" when canceled
function promptReason(title, message) {
    return new Promise((resolve) => {
        const modal = document.getElementById("confirm-modal");
        const form = document.getElementById("confirm-form");
        const input = document.getElementById("confirm-type-input");
        const ok = document.getElementById("confirm-ok");

        document.getElementById("confirm-title").textContent = title;
        document.getElementById("confirm-message").textContent = message;
        document.getElementById("confirm-type-label").classList.remove("hidden");
        document.getElementById("confirm-type-hint").textContent = "사유 (감사 로그에 기록됩니다)";
        input.value = "";
        input.maxLength = 200;
        ok.disabled = true;
        modal.classList.remove("hidden");
        input.focus();

        const onInput = () => { ok.disabled = input.value.trim() === ""; };
        const close = (result) => {
            modal.classList.add("hidden");
            input.removeAttribute("maxLength");
            input.removeEventListener("input", onInput);
            form.removeEventListener("submit", onSubmit);
            document.getElementById("confirm-cancel").removeEventListener("click", onCancel);
            resolve(result);
        };
        const onSubmit = (e) => { e.preventDefault(); if (!ok.disabled) close(input.value.trim()); };
        const onCancel = () => close("");

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
    admin_user_ban: "계정 정지",
    admin_user_unban: "정지 해제",
    admin_user_kick: "강제 로그아웃",
    admin_user_password: "비밀번호 변경 (관리자)",
    admin_user_delete: "계정 삭제",
    admin_user_grant: "지급 · 회수",
    admin_user_deck_save: "덱 저장",
    admin_user_deck_delete: "덱 삭제",
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

document.getElementById("users-search-form").addEventListener("submit", (e) => {
    e.preventDefault();
    loadUsers(document.getElementById("users-search-input").value.trim(), 0);
});
document.getElementById("users-prev").addEventListener("click", () => loadUsers(users_query, Math.max(users_page - 1, 0)));
document.getElementById("users-next").addEventListener("click", () => loadUsers(users_query, users_page + 1));
document.getElementById("user-back").addEventListener("click", () => openUserList());
document.getElementById("user-refresh").addEventListener("click", () => openUser(user_detail.user.id));
document.getElementById("user-ban-form").addEventListener("submit", banUser);
document.getElementById("user-unban").addEventListener("click", unbanUser);
document.getElementById("user-kick").addEventListener("click", kickUser);
document.getElementById("user-password-form").addEventListener("submit", setUserPassword);
document.getElementById("user-delete").addEventListener("click", deleteUser);
document.getElementById("user-grant-form").addEventListener("submit", submitGrant);
document.getElementById("grant-add-card").addEventListener("click", () => addGrantRow("card"));
document.getElementById("grant-add-pack").addEventListener("click", () => addGrantRow("pack"));
document.getElementById("deck-new").addEventListener("click", () => openDeckEditor(null));
document.getElementById("deck-premade").addEventListener("click", openPremadeModal);
document.getElementById("premade-form").addEventListener("submit", submitPremade);
document.getElementById("premade-cancel").addEventListener("click", () => document.getElementById("premade-modal").classList.add("hidden"));
document.getElementById("deck-form").addEventListener("submit", submitDeck);
document.getElementById("deck-cancel").addEventListener("click", closeDeckEditor);
document.getElementById("deck-card-add").addEventListener("click", addDeckCard);
document.getElementById("deck-card-search").addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); addDeckCard(); } });
document.getElementById("deck-title").addEventListener("input", scheduleDeckCheck);
document.getElementById("deck-hero").addEventListener("change", scheduleDeckCheck);
for (const button of document.querySelectorAll("[data-ban-days]"))
    button.addEventListener("click", () => { document.getElementById("user-ban-days").value = button.dataset.banDays; });
for (const tab of document.querySelectorAll("#user-tabs .tab")) {
    tab.addEventListener("click", () => {
        user_tab = tab.dataset.tab;
        renderUser();
    });
}
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
