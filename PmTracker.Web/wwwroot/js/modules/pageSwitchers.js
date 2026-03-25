import { fetchHtmlFragment } from "./navigationShared.js";

function readBooleanStorageDefaultTrue(key) {
    const rawValue = window.localStorage.getItem(key);
    if (rawValue === null) {
        return true;
    }

    return rawValue === "true";
}

function writeBooleanStorage(key, value) {
    window.localStorage.setItem(key, value ? "true" : "false");
}

function resolveQueryRoot(scope) {
    return scope && typeof scope.querySelector === "function"
        ? scope
        : document;
}

function readProjectListStatusFilterState(doneKey, deletedKey) {
    return {
        hideDone: readBooleanStorageDefaultTrue(doneKey),
        hideDeleted: readBooleanStorageDefaultTrue(deletedKey)
    };
}

function syncProjectListStatusFilterInputs(root, doneKey, deletedKey) {
    const state = readProjectListStatusFilterState(doneKey, deletedKey);
    root.querySelectorAll("[data-project-status-hide]").forEach((input) => {
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        const statusCode = (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase();
        if (statusCode === "DONE") {
            input.checked = state.hideDone;
        }
        else if (statusCode === "DELETED") {
            input.checked = state.hideDeleted;
        }
    });
}

function isPrimaryNavigationClick(event) {
    return !(event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0);
}

function setActiveLink(container, selector, activeKey) {
    container.querySelectorAll(selector).forEach((link) => {
        if (!(link instanceof HTMLAnchorElement)) {
            return;
        }

        const isActive = link.dataset.key === activeKey;
        link.classList.toggle("active", isActive);
        if (isActive) {
            link.setAttribute("aria-current", "page");
        }
        else {
            link.removeAttribute("aria-current");
        }
    });
}

async function fetchPanelHtml(endpoint) {
    return fetchHtmlFragment(endpoint);
}

export function applyProjectIndexFilters(scope, options = {}) {
    const root = resolveQueryRoot(scope);
    const shell = root.querySelector("[data-project-list-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    syncProjectListStatusFilterInputs(root, options.hideDoneStorageKey, options.hideDeletedStorageKey);

    const hiddenStatusCodes = Array.from(root.querySelectorAll("[data-project-status-hide]"))
        .filter((input) => input instanceof HTMLInputElement && input.checked)
        .map((input) => (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase())
        .filter(Boolean);

    shell.querySelectorAll("[data-project-list-row]").forEach((row) => {
        if (!(row instanceof HTMLElement)) {
            return;
        }

        const statusCode = (row.getAttribute("data-project-status") || "").trim().toUpperCase();
        row.hidden = hiddenStatusCodes.includes(statusCode);
    });
}

export function toggleProjectStatusFilterPanel(button) {
    if (!(button instanceof HTMLButtonElement)) {
        return;
    }

    const panel = document.querySelector("[data-project-status-filter-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const shouldOpen = panel.hidden;
    panel.hidden = !shouldOpen;
    button.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
}

export function handleProjectStatusFilterInput(input, options = {}) {
    if (!(input instanceof HTMLInputElement)) {
        return;
    }

    const statusCode = (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase();
    if (statusCode === "DONE") {
        writeBooleanStorage(options.hideDoneStorageKey, input.checked);
    }
    else if (statusCode === "DELETED") {
        writeBooleanStorage(options.hideDeletedStorageKey, input.checked);
    }

    applyProjectIndexFilters(document, options);
}

export function initProjectIndexStatusFilters(scope, options = {}) {
    const root = resolveQueryRoot(scope);
    const shell = root.querySelector("[data-project-list-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    const toggleButton = document.querySelector("[data-project-status-filter-toggle]");
    if (toggleButton instanceof HTMLButtonElement && toggleButton.dataset.boundProjectStatusFilter !== "true") {
        toggleButton.dataset.boundProjectStatusFilter = "true";
        toggleButton.addEventListener("click", () => {
            toggleProjectStatusFilterPanel(toggleButton);
        });
    }

    document.querySelectorAll("[data-project-status-hide]").forEach((input) => {
        if (input instanceof HTMLInputElement && input.dataset.boundProjectStatusFilter !== "true") {
            input.dataset.boundProjectStatusFilter = "true";
            input.addEventListener("change", () => {
                handleProjectStatusFilterInput(input, options);
            });
        }
    });

    syncProjectListStatusFilterInputs(document, options.hideDoneStorageKey, options.hideDeletedStorageKey);
    applyProjectIndexFilters(root, options);
}

export function toggleMeetingAttendancePanel(button) {
    if (!(button instanceof HTMLButtonElement)) {
        return;
    }

    const card = button.closest("[data-meeting-attendance-card]");
    if (!(card instanceof HTMLElement)) {
        return;
    }

    const panel = card.querySelector("[data-meeting-attendance-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const shouldOpen = panel.hidden;
    panel.hidden = !shouldOpen;
    button.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
    button.textContent = shouldOpen ? "Skrýt účast" : "Zobrazit účast";
}

export function initCiselnikAjaxSwitch(options = {}) {
    const shell = document.querySelector("[data-ciselnik-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    const panel = shell.querySelector("[data-ciselnik-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const contentUrl = shell.getAttribute("data-content-url");
    if (!contentUrl) {
        return;
    }

    const initRecordFormEnhancements = typeof options.initRecordFormEnhancements === "function"
        ? options.initRecordFormEnhancements
        : () => {};

    const getCurrentKeyFromUrl = () => new URL(window.location.href).searchParams.get("id");

    const loadDetail = async (key, push, href) => {
        if (!key) {
            return;
        }

        panel.setAttribute("aria-busy", "true");

        try {
            const endpoint = `${contentUrl}?id=${encodeURIComponent(key)}`;
            const html = await fetchPanelHtml(endpoint);
            panel.innerHTML = html;
            initRecordFormEnhancements(panel);
            setActiveLink(shell, "[data-ciselnik-link]", key);

            const nextState = { ...(history.state || {}), ciselnikKey: key };
            if (push && href) {
                history.pushState(nextState, "", href);
            }
            else if (!push) {
                history.replaceState(nextState, "", window.location.href);
            }
        }
        catch (error) {
            if (href) {
                window.location.href = href;
            }
        }
        finally {
            panel.setAttribute("aria-busy", "false");
        }
    };

    const initialActiveLink = shell.querySelector("[data-ciselnik-link].active");
    const initialKey = initialActiveLink instanceof HTMLAnchorElement
        ? initialActiveLink.dataset.key
        : getCurrentKeyFromUrl();

    if (initialKey) {
        history.replaceState(
            { ...(history.state || {}), ciselnikKey: initialKey },
            "",
            window.location.href);
        setActiveLink(shell, "[data-ciselnik-link]", initialKey);
    }

    shell.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const link = target.closest("[data-ciselnik-link]");
        if (!(link instanceof HTMLAnchorElement) || !isPrimaryNavigationClick(event)) {
            return;
        }

        event.preventDefault();
        void loadDetail(link.dataset.key, true, link.href);
    });

    window.addEventListener("popstate", (event) => {
        const key = event.state?.ciselnikKey || getCurrentKeyFromUrl();
        const link = shell.querySelector(`[data-ciselnik-link][data-key="${key}"]`);
        const href = link instanceof HTMLAnchorElement ? link.href : null;
        if (key) {
            void loadDetail(key, false, href);
        }
    });
}

export function initSettingsAjaxSwitch() {
    const shell = document.querySelector("[data-settings-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    const panel = shell.querySelector("[data-settings-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const panelUrl = shell.getAttribute("data-panel-url");
    if (!panelUrl) {
        return;
    }

    const getCurrentStateFromUrl = () => {
        const url = new URL(window.location.href);
        return {
            section: url.searchParams.get("section") || "role",
            userId: url.searchParams.get("userId"),
            projektId: url.searchParams.get("projektId")
        };
    };

    const buildHref = (section, userId, projektId) => {
        const url = new URL(window.location.href);
        url.searchParams.set("section", section);

        if (userId) {
            url.searchParams.set("userId", userId);
        }
        else {
            url.searchParams.delete("userId");
        }

        if (projektId) {
            url.searchParams.set("projektId", projektId);
        }
        else {
            url.searchParams.delete("projektId");
        }

        return `${url.pathname}${url.search}${url.hash}`;
    };

    const loadSection = async (section, push, options = {}) => {
        if (!section) {
            return;
        }

        const userId = options.userId ?? null;
        const projektId = options.projektId ?? null;
        const fallbackHref = options.href ?? null;
        panel.setAttribute("aria-busy", "true");

        try {
            const endpointUrl = new URL(panelUrl, window.location.origin);
            endpointUrl.searchParams.set("section", section);
            if (userId) {
                endpointUrl.searchParams.set("userId", userId);
            }
            if (projektId) {
                endpointUrl.searchParams.set("projektId", projektId);
            }

            const html = await fetchPanelHtml(`${endpointUrl.pathname}${endpointUrl.search}`);
            panel.innerHTML = html;
            setActiveLink(shell, "[data-settings-link]", section);

            const nextHref = buildHref(section, userId, projektId);
            const nextState = {
                ...(history.state || {}),
                settingsSection: section,
                settingsUserId: userId,
                settingsProjektId: projektId
            };

            if (push) {
                history.pushState(nextState, "", nextHref);
            }
            else {
                history.replaceState(nextState, "", nextHref);
            }
        }
        catch (error) {
            if (fallbackHref) {
                window.location.href = fallbackHref;
            }
        }
        finally {
            panel.setAttribute("aria-busy", "false");
        }
    };

    const initialStateFromUrl = getCurrentStateFromUrl();
    const initialActiveLink = shell.querySelector("[data-settings-link].active");
    const initialSection = initialActiveLink instanceof HTMLAnchorElement
        ? initialActiveLink.dataset.key
        : initialStateFromUrl.section;

    history.replaceState(
        {
            ...(history.state || {}),
            settingsSection: initialSection || "role",
            settingsUserId: initialStateFromUrl.userId,
            settingsProjektId: initialStateFromUrl.projektId
        },
        "",
        window.location.href);

    shell.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const link = target.closest("[data-settings-link]");
        if (!(link instanceof HTMLAnchorElement) || !isPrimaryNavigationClick(event)) {
            return;
        }

        event.preventDefault();
        const linkUrl = new URL(link.href);
        void loadSection(link.dataset.key, true, {
            href: link.href,
            userId: linkUrl.searchParams.get("userId"),
            projektId: linkUrl.searchParams.get("projektId")
        });
    });

    shell.addEventListener("change", (event) => {
        const target = event.target;
        if (!(target instanceof Element) || !target.matches("[data-settings-filter-user], [data-settings-filter-project]")) {
            return;
        }

        const form = target.closest("[data-settings-filter-form]");
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const sectionInput = form.querySelector('input[name="section"]');
        const userSelect = form.querySelector("[data-settings-filter-user]");
        const projectSelect = form.querySelector("[data-settings-filter-project]");

        void loadSection(
            sectionInput instanceof HTMLInputElement ? sectionInput.value : "efektivni-prava",
            true,
            {
                userId: userSelect instanceof HTMLSelectElement ? userSelect.value : null,
                projektId: projectSelect instanceof HTMLSelectElement ? projectSelect.value : null
            });
    });

    shell.addEventListener("submit", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLFormElement) || !target.matches("[data-settings-filter-form]")) {
            return;
        }

        event.preventDefault();
        const sectionInput = target.querySelector('input[name="section"]');
        const userSelect = target.querySelector("[data-settings-filter-user]");
        const projectSelect = target.querySelector("[data-settings-filter-project]");

        void loadSection(
            sectionInput instanceof HTMLInputElement ? sectionInput.value : "efektivni-prava",
            true,
            {
                userId: userSelect instanceof HTMLSelectElement ? userSelect.value : null,
                projektId: projectSelect instanceof HTMLSelectElement ? projectSelect.value : null
            });
    });

    window.addEventListener("popstate", (event) => {
        const urlState = getCurrentStateFromUrl();
        const section = event.state?.settingsSection || urlState.section || "role";
        const userId = event.state?.settingsUserId ?? urlState.userId;
        const projektId = event.state?.settingsProjektId ?? urlState.projektId;
        const link = shell.querySelector(`[data-settings-link][data-key="${section}"]`);
        const href = link instanceof HTMLAnchorElement ? link.href : null;
        void loadSection(section, false, { href, userId, projektId });
    });
}

export function initProfileRightsFilter() {
    const form = document.querySelector("[data-profile-rights-form]");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const projectSelect = form.querySelector("[data-profile-rights-project]");
    if (!(projectSelect instanceof HTMLSelectElement)) {
        return;
    }

    projectSelect.addEventListener("change", () => {
        const url = new URL(window.location.href);
        if (projectSelect.value) {
            url.searchParams.set("projektId", projectSelect.value);
        }
        else {
            url.searchParams.delete("projektId");
        }

        url.hash = "moje-prava";
        window.location.href = url.toString();
    });
}

export function initUserMenu() {
    const menu = document.querySelector("[data-user-menu]");
    if (!(menu instanceof HTMLElement)) {
        return;
    }

    const toggle = menu.querySelector("[data-user-menu-toggle]");
    const panel = menu.querySelector("[data-user-menu-panel]");
    if (!(toggle instanceof HTMLButtonElement) || !(panel instanceof HTMLElement)) {
        return;
    }

    const setOpen = (open) => {
        panel.hidden = !open;
        toggle.setAttribute("aria-expanded", String(open));
    };

    toggle.addEventListener("click", () => {
        setOpen(panel.hidden);
    });

    document.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        if (!menu.contains(target)) {
            setOpen(false);
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            setOpen(false);
        }
    });
}
