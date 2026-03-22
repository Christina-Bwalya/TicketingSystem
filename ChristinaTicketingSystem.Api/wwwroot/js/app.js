const authApiBaseUrl = "/api/auth";
const ticketsApiBaseUrl = "/api/tickets";
const sessionStorageKey = "cts.session";

const loginView = document.getElementById("login-view");
const dashboardView = document.getElementById("dashboard-view");
const showLoginBtn = document.getElementById("show-login-btn");
const showRegisterBtn = document.getElementById("show-register-btn");
const loginForm = document.getElementById("login-form");
const loginMessage = document.getElementById("login-message");
const registerForm = document.getElementById("register-form");
const registerMessage = document.getElementById("register-message");
const logoutBtn = document.getElementById("logout-btn");
const sessionPanel = document.getElementById("session-panel");
const sessionUser = document.getElementById("session-user");
const sessionRole = document.getElementById("session-role");
const dashboardSubtitle = document.getElementById("dashboard-subtitle");
const ticketFormTitle = document.getElementById("ticket-form-title");
const ticketFormIntro = document.getElementById("ticket-form-intro");
const ticketsListTitle = document.getElementById("tickets-list-title");
const ticketsListIntro = document.getElementById("tickets-list-intro");
const adminOnlyFields = document.querySelectorAll(".admin-only-field");
const staffOnlyFields = document.querySelectorAll(".staff-only-field");

const ticketForm = document.getElementById("ticket-form");
const formMessage = document.getElementById("form-message");
const ticketsContainer = document.getElementById("tickets-container");
const refreshBtn = document.getElementById("refresh-btn");
const ticketCardTemplate = document.getElementById("ticket-card-template");

let currentSession = readSession();

async function fetchJson(url, options) {
    const headers = {
        ...(options && options.headers)
    };

    const isFormData = options?.body instanceof FormData;
    if (!isFormData && !headers["Content-Type"]) {
        headers["Content-Type"] = "application/json";
    }

    if (currentSession?.token) {
        headers.Authorization = `Bearer ${currentSession.token}`;
    }

    const response = await fetch(url, {
        headers,
        ...options
    });

    if (!response.ok) {
        const errorText = await response.text().catch(() => "");
        throw new Error(errorText || `Request failed with status ${response.status}`);
    }

    if (response.status === 204) {
        return null;
    }

    return await response.json();
}

async function fetchBlob(url, options) {
    const headers = {
        ...(options && options.headers)
    };

    if (currentSession?.token) {
        headers.Authorization = `Bearer ${currentSession.token}`;
    }

    const response = await fetch(url, {
        ...options,
        headers
    });

    if (!response.ok) {
        const errorText = await response.text().catch(() => "");
        throw new Error(errorText || `Request failed with status ${response.status}`);
    }

    return await response.blob();
}

function readSession() {
    const raw = localStorage.getItem(sessionStorageKey);

    if (!raw) {
        return null;
    }

    try {
        const session = JSON.parse(raw);
        if (!session?.token || !session?.displayName) {
            return null;
        }

        if (!session.role) {
            session.role = "Customer";
        }

        return session;
    } catch {
        return null;
    }
}

function saveSession(session) {
    currentSession = session;
    localStorage.setItem(sessionStorageKey, JSON.stringify(session));
}

function clearSession() {
    currentSession = null;
    localStorage.removeItem(sessionStorageKey);
}

function setLoginMessage(message, type) {
    loginMessage.textContent = message || "";
    loginMessage.classList.toggle("error", type === "error");
    loginMessage.classList.toggle("success", type === "success");
}

function setRegisterMessage(message, type) {
    registerMessage.textContent = message || "";
    registerMessage.classList.toggle("error", type === "error");
    registerMessage.classList.toggle("success", type === "success");
}

function setFormMessage(message, type) {
    formMessage.textContent = message || "";
    formMessage.classList.toggle("error", type === "error");
    formMessage.classList.toggle("success", type === "success");
}

function getFriendlyErrorMessage(error, fallbackMessage) {
    if (!error?.message) {
        return fallbackMessage;
    }

    if (error.message.includes("Failed to fetch")) {
        return "Could not reach the server. Please make sure the app is running.";
    }

    try {
        const parsed = JSON.parse(error.message);
        if (parsed?.message) {
            return parsed.message;
        }
    } catch {
        // Keep the fallback path below for plain-text and validation responses.
    }

    if (error.message.includes("errors")) {
        return "Please check the form and make sure all required fields are filled correctly.";
    }

    return error.message || fallbackMessage;
}

function setAuthMode(mode) {
    const isLogin = mode === "login";

    loginForm.classList.toggle("hidden", !isLogin);
    registerForm.classList.toggle("hidden", isLogin);
    showLoginBtn.classList.toggle("active", isLogin);
    showRegisterBtn.classList.toggle("active", !isLogin);
    showLoginBtn.setAttribute("aria-selected", String(isLogin));
    showRegisterBtn.setAttribute("aria-selected", String(!isLogin));

    setLoginMessage("", "");
    setRegisterMessage("", "");
}

function setAuthenticatedUi(isAuthenticated) {
    loginView.classList.toggle("hidden", isAuthenticated);
    dashboardView.classList.toggle("hidden", !isAuthenticated);
    sessionPanel.classList.toggle("hidden", !isAuthenticated);
    sessionUser.textContent = isAuthenticated ? currentSession.displayName : "";
    sessionRole.textContent = isAuthenticated ? currentSession.role : "";
    const isAdmin = isAuthenticated && currentSession?.role === "Admin";
    const isIt = isAuthenticated && currentSession?.role === "I.T";
    adminOnlyFields.forEach((field) => field.classList.toggle("hidden", !isAdmin));
    staffOnlyFields.forEach((field) => field.classList.toggle("hidden", !(isAdmin || isIt)));

    if (!isAuthenticated) {
        dashboardSubtitle.textContent = "Simple, radiant ticket management with a protected workspace";
        ticketFormTitle.textContent = "Create Ticket";
        ticketFormIntro.textContent = "Submit a new ticket, set its priority, and track progress from your dashboard.";
        ticketsListTitle.textContent = "Tickets";
        ticketsListIntro.textContent = "View ticket progress and the latest updates here.";
        return;
    }

    if (isAdmin) {
        dashboardSubtitle.textContent = "Admin workspace with full ticket visibility and team oversight";
        ticketFormTitle.textContent = "Create Ticket";
        ticketFormIntro.textContent = "Admins can create tickets, set due dates, write overviews, add review notes, and manage full ticket lifecycles.";
        ticketsListTitle.textContent = "All Tickets";
        ticketsListIntro.textContent = "Review every ticket, every status, comments, reviews, due dates, and priorities.";
    } else if (isIt) {
        dashboardSubtitle.textContent = "I.T workspace for all customer tickets and any internal tickets assigned to you";
        ticketFormTitle.textContent = "Create Internal Ticket";
        ticketFormIntro.textContent = "I.T staff can create tickets, set due dates, update statuses, add comments, and coordinate assignments.";
        ticketsListTitle.textContent = "Work Queue";
        ticketsListIntro.textContent = "View all customer tickets plus your assigned internal tickets, update statuses, and collaborate through comments.";
    } else {
        dashboardSubtitle.textContent = "Customer workspace for submitting tickets and following your request status";
        ticketFormTitle.textContent = "Submit Ticket";
        ticketFormIntro.textContent = "Create a ticket, choose its priority, and submit it for review.";
        ticketsListTitle.textContent = "My Tickets";
        ticketsListIntro.textContent = "Track your ticket statuses and add comments when needed.";
    }
}

async function loadTickets() {
    try {
        const tickets = await fetchJson(ticketsApiBaseUrl, { method: "GET" });
        renderTickets(tickets);
    } catch (error) {
        console.error(error);
        renderTickets([]);

        if (error.message.includes("401")) {
            clearSession();
            setAuthenticatedUi(false);
            setLoginMessage("Your session expired. Please log in again.", "error");
            return;
        }

        setFormMessage("Could not load tickets. Is the API running?", "error");
    }
}

function renderTickets(tickets) {
    ticketsContainer.innerHTML = "";

    if (!tickets || tickets.length === 0) {
        ticketsContainer.classList.add("empty");
        return;
    }

    ticketsContainer.classList.remove("empty");

    for (const ticket of tickets) {
        const card = createTicketCard(ticket);
        ticketsContainer.appendChild(card);
    }
}

function normalizeStatus(status) {
    if (typeof status === "number") {
        const lookup = {
            0: "Open",
            1: "InProgress",
            2: "WaitingForUser",
            3: "Resolved",
            4: "Closed"
        };
        return lookup[status] ?? "Open";
    }

    return status || "Open";
}

function mapStatusToClass(status) {
    switch (status) {
        case "Open":
            return "status-open";
        case "InProgress":
            return "status-inprogress";
        case "Resolved":
            return "status-resolved";
        case "WaitingForUser":
            return "status-waitingforuser";
        case "Closed":
            return "status-closed";
        default:
            return "";
    }
}

function formatDate(iso) {
    if (!iso) {
        return "";
    }

    const date = new Date(iso);
    return date.toLocaleString();
}

function formatDateOnly(iso) {
    if (!iso) {
        return "Not set";
    }

    const date = new Date(iso);
    return date.toLocaleDateString();
}

async function downloadAttachment(ticket) {
    if (!ticket?.attachmentUrl) {
        return;
    }

    const blob = await fetchBlob(ticket.attachmentUrl, { method: "GET" });
    const blobUrl = URL.createObjectURL(blob);
    const tempLink = document.createElement("a");
    tempLink.href = blobUrl;
    tempLink.download = ticket.attachmentFileName || "attachment";
    tempLink.target = "_blank";
    tempLink.rel = "noopener noreferrer";
    document.body.appendChild(tempLink);
    tempLink.click();
    tempLink.remove();

    window.setTimeout(() => URL.revokeObjectURL(blobUrl), 1000);
}

function createTicketCard(ticket) {
    const fragment = ticketCardTemplate.content.cloneNode(true);

    const titleEl = fragment.querySelector(".ticket-title");
    const descEl = fragment.querySelector(".ticket-description");
    const idEl = fragment.querySelector(".ticket-id");
    const createdEl = fragment.querySelector(".ticket-created");
    const assignedEl = fragment.querySelector(".ticket-assigned");
    const requesterEl = fragment.querySelector(".ticket-requester");
    const dueDateEl = fragment.querySelector(".ticket-due-date");
    const priorityEl = fragment.querySelector(".ticket-priority");
    const categoryPill = fragment.querySelector(".pill-category");
    const statusPill = fragment.querySelector(".pill-status");
    const statusSelect = fragment.querySelector(".status-select");
    const adminDetails = fragment.querySelector(".ticket-admin-details");
    const overviewEl = fragment.querySelector(".ticket-overview");
    const reviewNotesEl = fragment.querySelector(".ticket-review-notes");
    const commentsList = fragment.querySelector(".ticket-comments-list");
    const attachmentEl = fragment.querySelector(".ticket-attachment");
    const commentInput = fragment.querySelector(".comment-input");
    const addCommentBtn = fragment.querySelector(".add-comment-btn");
    const updateStatusBtn = fragment.querySelector(".update-status-btn");
    const deleteBtn = fragment.querySelector(".delete-btn");
    const adminOnlyControls = fragment.querySelectorAll(".admin-only-control");
    const staffOnlyControls = fragment.querySelectorAll(".staff-only-control");

    titleEl.textContent = ticket.title;
    descEl.textContent = ticket.description;
    idEl.textContent = `#${ticket.id}`;
    createdEl.textContent = formatDate(ticket.createdDate);
    assignedEl.textContent = ticket.assignedTo || "Unassigned";
    requesterEl.textContent = ticket.createdByDisplayName || ticket.createdByUsername || "Unknown";
    dueDateEl.textContent = formatDateOnly(ticket.dueDate);
    priorityEl.textContent = ticket.priority || "Medium";

    categoryPill.textContent = ticket.category;

    const normalizedStatus = normalizeStatus(ticket.status);
    statusPill.textContent = normalizedStatus;

    const statusClass = mapStatusToClass(normalizedStatus);
    if (statusClass) {
        statusPill.classList.add(statusClass);
    }

    statusSelect.value = normalizedStatus;

    const isAdmin = currentSession?.role === "Admin";
    const isIt = currentSession?.role === "I.T";
    adminDetails.classList.toggle("hidden", !isAdmin);
    adminOnlyControls.forEach((control) => control.classList.toggle("hidden", !isAdmin));
    staffOnlyControls.forEach((control) => control.classList.toggle("hidden", !(isAdmin || isIt)));
    overviewEl.textContent = ticket.overview || "No overview added.";
    reviewNotesEl.textContent = ticket.reviewNotes || "No review notes added.";
    attachmentEl.innerHTML = "";

    if (ticket.hasAttachment && ticket.attachmentUrl) {
        const attachmentLink = document.createElement("button");
        attachmentLink.type = "button";
        attachmentLink.className = "attachment-link";
        attachmentLink.textContent = ticket.attachmentFileName || "Open attachment";
        attachmentLink.addEventListener("click", async () => {
            try {
                attachmentLink.disabled = true;
                await downloadAttachment(ticket);
            } catch (error) {
                console.error(error);
                alert("Could not open attachment.");
            } finally {
                attachmentLink.disabled = false;
            }
        });
        attachmentEl.appendChild(attachmentLink);
    } else {
        attachmentEl.textContent = "No attachment added.";
    }

    commentsList.innerHTML = "";

    const comments = Array.isArray(ticket.comments) ? ticket.comments : [];
    if (comments.length === 0) {
        commentsList.innerHTML = "<span>No comments yet.</span>";
    } else {
        for (const comment of comments) {
            const item = document.createElement("article");
            item.className = "ticket-comment";
            item.innerHTML = `<strong>${comment.authorName}</strong><span>${comment.message}</span><span>${formatDate(comment.createdDate)}</span>`;
            commentsList.appendChild(item);
        }
    }

    updateStatusBtn.addEventListener("click", async () => {
        const newStatus = statusSelect.value;

        try {
            updateStatusBtn.disabled = true;
            await fetchJson(`${ticketsApiBaseUrl}/${ticket.id}/status`, {
                method: "PATCH",
                body: JSON.stringify({ status: newStatus })
            });
            await loadTickets();
        } catch (error) {
            console.error(error);
            alert("Could not update status.");
        } finally {
            updateStatusBtn.disabled = false;
        }
    });

    addCommentBtn.addEventListener("click", async () => {
        const message = commentInput.value.trim();
        if (!message) {
            return;
        }

        try {
            addCommentBtn.disabled = true;
            await fetchJson(`${ticketsApiBaseUrl}/${ticket.id}/comments`, {
                method: "POST",
                body: JSON.stringify({ message })
            });
            await loadTickets();
        } catch (error) {
            console.error(error);
            alert("Could not add comment.");
        } finally {
            addCommentBtn.disabled = false;
        }
    });

    deleteBtn.addEventListener("click", async () => {
        if (!confirm("Delete this ticket?")) {
            return;
        }

        try {
            deleteBtn.disabled = true;
            await fetchJson(`${ticketsApiBaseUrl}/${ticket.id}`, {
                method: "DELETE"
            });
            await loadTickets();
        } catch (error) {
            console.error(error);
            alert("Could not delete ticket.");
        } finally {
            deleteBtn.disabled = false;
        }
    });

    return fragment;
}

ticketForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    setFormMessage("", "");

    const formData = new FormData(ticketForm);
    const payload = new FormData();
    payload.append("title", formData.get("title")?.toString() ?? "");
    payload.append("description", formData.get("description")?.toString() ?? "");
    payload.append("category", formData.get("category")?.toString() ?? "");
    payload.append("priority", formData.get("priority")?.toString() ?? "Medium");

    const assignedTo = formData.get("assignedTo")?.toString();
    if (assignedTo) {
        payload.append("assignedTo", assignedTo);
    }

    const dueDate = formData.get("dueDate")?.toString();
    if (dueDate) {
        payload.append("dueDate", dueDate);
    }

    const overview = formData.get("overview")?.toString();
    if (overview) {
        payload.append("overview", overview);
    }

    const reviewNotes = formData.get("reviewNotes")?.toString();
    if (reviewNotes) {
        payload.append("reviewNotes", reviewNotes);
    }

    const attachment = formData.get("attachment");
    if (attachment instanceof File && attachment.size > 0) {
        payload.append("attachment", attachment);
    }

    try {
        const createBtn = ticketForm.querySelector("button[type='submit']");
        createBtn.disabled = true;
        await fetchJson(ticketsApiBaseUrl, {
            method: "POST",
            body: payload
        });

        ticketForm.reset();
        document.getElementById("priority").value = "Medium";
        setFormMessage("Ticket created!", "success");
        await loadTickets();
    } catch (error) {
        console.error(error);
        setFormMessage(getFriendlyErrorMessage(error, "Could not create ticket. Please check your input."), "error");
    } finally {
        const createBtn = ticketForm.querySelector("button[type='submit']");
        createBtn.disabled = false;
    }
});

refreshBtn.addEventListener("click", () => {
    loadTickets();
});

showLoginBtn.addEventListener("click", () => {
    setAuthMode("login");
});

showRegisterBtn.addEventListener("click", () => {
    setAuthMode("register");
});

loginForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    setLoginMessage("", "");

    const formData = new FormData(loginForm);
    const payload = {
        username: formData.get("username")?.toString() ?? "",
        password: formData.get("password")?.toString() ?? ""
    };

    try {
        const loginBtn = loginForm.querySelector("button[type='submit']");
        loginBtn.disabled = true;

        const session = await fetchJson(`${authApiBaseUrl}/login`, {
            method: "POST",
            body: JSON.stringify(payload)
        });

        saveSession(session);
        loginForm.reset();
        setAuthenticatedUi(true);
        setFormMessage("", "");
        await loadTickets();
    } catch (error) {
        console.error(error);
        setLoginMessage(getFriendlyErrorMessage(error, "Login failed. Please check your username and password."), "error");
    } finally {
        const loginBtn = loginForm.querySelector("button[type='submit']");
        loginBtn.disabled = false;
    }
});

registerForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    setRegisterMessage("", "");

    const formData = new FormData(registerForm);
    const payload = {
        username: formData.get("username")?.toString() ?? "",
        displayName: formData.get("displayName")?.toString() ?? "",
        password: formData.get("password")?.toString() ?? "",
        role: formData.get("role")?.toString() ?? "Customer"
    };

    try {
        const registerBtn = registerForm.querySelector("button[type='submit']");
        registerBtn.disabled = true;

        const session = await fetchJson(`${authApiBaseUrl}/register`, {
            method: "POST",
            body: JSON.stringify(payload)
        });

        saveSession(session);
        registerForm.reset();
        setAuthenticatedUi(true);
        setFormMessage("", "");
        await loadTickets();
    } catch (error) {
        console.error(error);
        setRegisterMessage(getFriendlyErrorMessage(error, "Could not create account. Try a different username or stronger password."), "error");
    } finally {
        const registerBtn = registerForm.querySelector("button[type='submit']");
        registerBtn.disabled = false;
    }
});

logoutBtn.addEventListener("click", async () => {
    try {
        await fetchJson(`${authApiBaseUrl}/logout`, { method: "POST" });
    } catch (error) {
        console.error(error);
    } finally {
        clearSession();
        setAuthenticatedUi(false);
        renderTickets([]);
        ticketForm.reset();
        setFormMessage("", "");
        setLoginMessage("You have been logged out.", "success");
    }
});

setAuthenticatedUi(Boolean(currentSession));
setAuthMode("login");

if (currentSession) {
    loadTickets();
}
