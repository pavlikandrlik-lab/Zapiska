/**
 * pickers/adPerson.js — Active Directory person search picker.
 * Obsahuje: initAdPersonPickers (plná AD search integrace — autocomplete, org/unit mapping).
 */
import {
    debounce,
    isButtonLike,
    parseJsonPayload,
    reportClientDiagnostic,
    setButtonDisabled
} from "../utils.js";
import {
    isInteractionInsideFloatingControl,
    mountFloatingPanel,
    unmountFloatingPanel
} from "../ui.js";

export function initAdPersonPickers(scope) {
    scope.querySelectorAll("[data-ad-picker]").forEach((wrapper) => {
        if (!(wrapper instanceof HTMLElement) || wrapper.dataset.adPickerReady === "true") {
            return;
        }

        const searchUrl = wrapper.dataset.searchUrl || "";
        const form = wrapper.closest("form");
        const anchor = wrapper.querySelector("[data-floating-anchor]");
        const queryInput = wrapper.querySelector("[data-ad-query-input]");
        const queryHidden = form?.querySelector("[data-ad-query-hidden]");
        const guidInput = form?.querySelector("[data-ad-guid]");
        const adLoginInput = form?.querySelector("[data-ad-login]");
        const adCompanyInput = form?.querySelector("[data-ad-company]");
        const adDepartmentInput = form?.querySelector("[data-ad-department]");
        const jmenoInput = form?.querySelector("[data-ad-jmeno]");
        const prijmeniInput = form?.querySelector("[data-ad-prijmeni]");
        const titulInput = form?.querySelector("[data-ad-titul]");
        const emailInput = form?.querySelector("[data-ad-email]");
        const orgSelect = form?.querySelector("[data-ad-org-select]");
        const orgCreateHint = form?.querySelector("[data-ad-org-create-hint]");
        const orgUnitSelect = form?.querySelector("[data-ad-org-unit-select]");
        const orgUnitCreateHint = form?.querySelector("[data-ad-org-unit-create-hint]");
        const submitButton = form?.querySelector("[data-ad-submit]");
        const panel = wrapper.querySelector("[data-ad-search-panel]");
        const results = wrapper.querySelector("[data-ad-results]");

        if (!searchUrl
            || !(form instanceof HTMLFormElement)
            || !(anchor instanceof HTMLElement)
            || !(queryInput instanceof HTMLInputElement)
            || !(queryHidden instanceof HTMLInputElement)
            || !(guidInput instanceof HTMLInputElement)
            || !(adLoginInput instanceof HTMLInputElement)
            || !(adCompanyInput instanceof HTMLInputElement)
            || !(adDepartmentInput instanceof HTMLInputElement)
            || !(jmenoInput instanceof HTMLInputElement)
            || !(prijmeniInput instanceof HTMLInputElement)
            || !(titulInput instanceof HTMLInputElement)
            || !(emailInput instanceof HTMLInputElement)
            || !(orgSelect instanceof HTMLSelectElement)
            || !(orgCreateHint instanceof HTMLElement)
            || !(orgUnitSelect instanceof HTMLSelectElement)
            || !(orgUnitCreateHint instanceof HTMLElement)
            || !(panel instanceof HTMLElement)
            || !(results instanceof HTMLElement)
            || !isButtonLike(submitButton)) {
            return;
        }

        wrapper.dataset.adPickerReady = "true";
        let activeIndex = -1;
        let currentResults = [];
        let adAvailabilityKnown = false;
        let adIsUnavailable = false;
        const adUnavailableMessage = "Active Directory ACR není dostupné.";

        const closePanel = () => {
            panel.hidden = true;
            activeIndex = -1;
            unmountFloatingPanel(panel);
        };

        const clearGeneratedOption = (select, hint) => {
            Array.from(select.options)
                .filter((option) => option.dataset.generated === "true")
                .forEach((option) => option.remove());
            hint.hidden = true;
        };

        const clearSelection = () => {
            guidInput.value = "";
            adLoginInput.value = "";
            adCompanyInput.value = "";
            adDepartmentInput.value = "";
            jmenoInput.value = "";
            prijmeniInput.value = "";
            titulInput.value = "";
            emailInput.value = "";
            clearGeneratedOption(orgSelect, orgCreateHint);
            clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
            setButtonDisabled(submitButton, true);
        };

        const normalizeText = (value) => (value || "").toString().trim().toLowerCase();

        const ensureGeneratedOption = (select, hint, rawValue) => {
            clearGeneratedOption(select, hint);
            const normalized = (rawValue || "").toString().trim();
            if (!normalized) {
                return;
            }

            const option = document.createElement("option");
            option.value = `__new__:${normalized}`;
            option.textContent = `${normalized} (+ bude přidáno do číselníku)`;
            option.dataset.generated = "true";
            select.appendChild(option);
            select.value = option.value;
            hint.hidden = false;
        };

        const ensureGeneratedOrgUnitOption = (code, name) => {
            clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
            const normalizedCode = (code || "").toString().trim();
            const normalizedName = (name || "").toString().trim();
            if (!normalizedCode && !normalizedName) {
                return;
            }

            const option = document.createElement("option");
            const encoded = normalizedCode && normalizedName
                ? `${normalizedCode}|${normalizedName}`
                : normalizedCode || normalizedName;
            option.value = `__new__:${encoded}`;
            option.textContent = normalizedCode && normalizedName
                ? `${normalizedCode} - ${normalizedName} (+ bude přidáno do číselníku)`
                : `${encoded} (+ bude přidáno do číselníku)`;
            option.dataset.generated = "true";
            orgUnitSelect.appendChild(option);
            orgUnitSelect.value = option.value;
            orgUnitCreateHint.hidden = false;
        };

        const extractCodePrefix = (value) => {
            const text = (value || "").toString().trim();
            const hyphenIndex = text.indexOf("-");
            if (hyphenIndex < 2 || hyphenIndex > 6) {
                return null;
            }

            const prefix = text.slice(0, hyphenIndex).trim().toUpperCase();
            if (!/^[A-Z0-9]{2,8}$/.test(prefix)) {
                return null;
            }

            return prefix;
        };

        const findOptionByCodeOrText = (select, rawText, preferredCodes = []) => {
            const options = Array.from(select.options);
            const normalizedText = normalizeText(rawText);
            const codeFromText = extractCodePrefix(rawText);
            const normalizedCodes = preferredCodes
                .map((code) => normalizeText(code))
                .filter((code) => code);
            if (codeFromText) {
                normalizedCodes.unshift(normalizeText(codeFromText));
            }

            for (const normalizedCode of normalizedCodes) {
                const byCode = options.find((option) => normalizeText(option.value) === normalizedCode);
                if (byCode) {
                    return byCode.value;
                }
            }

            if (normalizedText) {
                const byText = options.find((option) => normalizeText(option.textContent).includes(normalizedText));
                if (byText) {
                    return byText.value;
                }
            }

            return null;
        };

        const parseCompanyLocation = (rawCompany) => {
            const company = (rawCompany || "").toString().trim();
            if (!company) {
                return {
                    organizationCode: null,
                    organizationName: null,
                    orgUnitCode: null,
                    orgUnitName: null,
                    source: ""
                };
            }

            const slashIndex = company.indexOf("/");
            const left = slashIndex >= 0 ? company.slice(0, slashIndex).trim() : company;
            const right = slashIndex >= 0 ? company.slice(slashIndex + 1).trim() : "";

            let organizationCode = null;
            let organizationName = left;
            const hyphenIndex = left.indexOf("-");
            if (hyphenIndex >= 2 && hyphenIndex <= 6) {
                const maybeCode = left.slice(0, hyphenIndex).trim().toUpperCase();
                if (/^[A-Z0-9]{2,8}$/.test(maybeCode)) {
                    organizationCode = maybeCode;
                    organizationName = left.slice(hyphenIndex + 1).trim();
                }
            }

            return {
                organizationCode,
                organizationName: organizationName || null,
                orgUnitCode: right || null,
                orgUnitName: organizationName || null,
                source: company
            };
        };

        const tryAutoSelectOrganization = (row) => {
            const parsed = parseCompanyLocation(row.company);
            const company = parsed.source;
            const normalizedCompany = normalizeText(company);
            const forcedCodes = [];
            if (parsed.organizationCode) {
                forcedCodes.push(parsed.organizationCode);
            }
            if (normalizedCompany.includes("ministerstvo obrany")
                || normalizedCompany.includes("armada ceske republiky")
                || normalizedCompany.includes("armáda české republiky")
                || normalizedCompany.includes("acr")) {
                forcedCodes.push("MO");
            } else if (normalizedCompany.includes("gordic")) {
                forcedCodes.push("DOD");
            }

            const selected = findOptionByCodeOrText(orgSelect, parsed.organizationName || company, forcedCodes);
            if (selected) {
                clearGeneratedOption(orgSelect, orgCreateHint);
                orgSelect.value = selected;
                return;
            }

            if (parsed.organizationName || company) {
                ensureGeneratedOption(orgSelect, orgCreateHint, parsed.organizationName || company);
            } else if (orgSelect.options.length > 0) {
                clearGeneratedOption(orgSelect, orgCreateHint);
                const fallback = findOptionByCodeOrText(orgSelect, "", ["MO"]);
                orgSelect.value = fallback || orgSelect.options[0].value;
            }
        };

        const tryAutoSelectOrgUnit = (row) => {
            const parsedCompany = parseCompanyLocation(row.company);
            const departmentRaw = (row.department || "").toString().trim();
            const preferredCodes = [];
            if (parsedCompany.orgUnitCode) {
                preferredCodes.push(parsedCompany.orgUnitCode);
            }
            if (departmentRaw && /^[0-9]+$/.test(departmentRaw)) {
                preferredCodes.push(departmentRaw);
            }

            const selected = findOptionByCodeOrText(
                orgUnitSelect,
                parsedCompany.orgUnitName || departmentRaw,
                preferredCodes);
            if (selected) {
                clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
                orgUnitSelect.value = selected;
                return;
            }

            const generatedCode = parsedCompany.orgUnitCode || (/^[0-9]+$/.test(departmentRaw) ? departmentRaw : "");
            const generatedName = parsedCompany.orgUnitName || (!/^[0-9]+$/.test(departmentRaw) ? departmentRaw : "");
            if (generatedCode || generatedName) {
                ensureGeneratedOrgUnitOption(generatedCode, generatedName);
            } else {
                clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
                orgUnitSelect.value = "";
            }
        };

        const renderStatusRow = (text, type = "info") => {
            currentResults = [];
            activeIndex = -1;
            results.innerHTML = "";

            const status = document.createElement("div");
            status.className = `office-search-info ${type}`.trim();
            status.textContent = text;
            status.setAttribute("role", "status");
            status.setAttribute("aria-live", "polite");
            results.appendChild(status);
            panel.hidden = false;
            mountFloatingPanel(panel, anchor, {
                gap: 6,
                flipVertical: true,
                kind: "ad-search",
                matchWidth: true
            });
        };

        const fillFromResult = (row) => {
            if (!row.canSelect) {
                renderStatusRow(row.disabledReason || "Tuto osobu nelze vybrat.", "error");
                return;
            }

            guidInput.value = row.guidAd || "";
            adLoginInput.value = row.adLogin || "";
            adCompanyInput.value = row.company || "";
            adDepartmentInput.value = row.department || "";
            jmenoInput.value = row.jmeno || "";
            prijmeniInput.value = row.prijmeni || "";
            titulInput.value = row.titul || "";
            emailInput.value = row.email || "";
            tryAutoSelectOrganization(row);
            tryAutoSelectOrgUnit(row);
            queryHidden.value = queryInput.value.trim();
            queryInput.value = row.email ? `${row.displayName} <${row.email}>` : row.displayName;
            setButtonDisabled(submitButton, false);
            closePanel();
        };

        const renderResults = () => {
            results.innerHTML = "";

            if (currentResults.length === 0) {
                closePanel();
                return;
            }

            currentResults.forEach((row, index) => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "office-search-item";
                button.dataset.index = String(index);
                button.setAttribute("role", "option");
                if (index === activeIndex) {
                    button.classList.add("active");
                }
                if (!row.canSelect) {
                    button.classList.add("disabled");
                    button.disabled = true;
                }

                const primary = document.createElement("span");
                primary.className = "office-search-primary";
                primary.textContent = row.displayName || `${row.jmeno || ""} ${row.prijmeni || ""}`.trim();
                button.appendChild(primary);

                const secondary = document.createElement("span");
                secondary.className = "office-search-secondary";
                const orgPart = `${row.company || "-"} / ${row.department || "-"}`;
                secondary.textContent = row.email ? `${row.email} | ${orgPart}` : orgPart;
                button.appendChild(secondary);

                if (!row.canSelect && row.disabledReason) {
                    const reason = document.createElement("span");
                    reason.className = "office-search-warning";
                    reason.textContent = row.disabledReason;
                    button.appendChild(reason);
                }

                results.appendChild(button);
            });

            panel.hidden = false;
            mountFloatingPanel(panel, anchor, {
                gap: 6,
                flipVertical: true,
                kind: "ad-search",
                matchWidth: true
            });
        };

        const performSearch = async () => {
            const query = queryInput.value.trim();
            queryHidden.value = query;
            clearSelection();

            if (query.length < 1) {
                if (adIsUnavailable) {
                    renderStatusRow(adUnavailableMessage, "error");
                } else {
                    results.innerHTML = "";
                    closePanel();
                }
                return;
            }

            renderStatusRow("Vyhledávám v Active Directory...");

            try {
                const response = await fetch(`${searchUrl}?q=${encodeURIComponent(query)}`, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const payload = parseJsonPayload(await response.text());
                if (!payload || typeof payload !== "object") {
                    throw new Error("INVALID_AD_PAYLOAD");
                }
                if (!payload.available) {
                    adAvailabilityKnown = true;
                    adIsUnavailable = true;
                    renderStatusRow(payload.message || adUnavailableMessage, "error");
                    return;
                }

                adAvailabilityKnown = true;
                adIsUnavailable = false;
                currentResults = Array.isArray(payload.results) ? payload.results.slice(0, 5) : [];
                activeIndex = currentResults.length > 0 ? 0 : -1;
                if (currentResults.length > 0) {
                    renderResults();
                } else {
                    renderStatusRow("Žádná shoda.", "empty");
                }
            } catch {
                adAvailabilityKnown = true;
                adIsUnavailable = true;
                renderStatusRow(adUnavailableMessage, "error");
                reportClientDiagnostic("ad-search-failed", { searchUrl });
            }
        };

        const debouncedAdSearch = debounce(performSearch, 220);

        const probeAvailability = async () => {
            if (adAvailabilityKnown) {
                if (adIsUnavailable) {
                    renderStatusRow(adUnavailableMessage, "error");
                }
                return;
            }

            try {
                const response = await fetch(`${searchUrl}?q=${encodeURIComponent("__pmtracker_probe__")}`, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const payload = parseJsonPayload(await response.text());
                if (!payload || typeof payload !== "object") {
                    throw new Error("INVALID_AD_PROBE_PAYLOAD");
                }
                adAvailabilityKnown = true;
                adIsUnavailable = !payload.available;
                if (adIsUnavailable) {
                    renderStatusRow(payload.message || adUnavailableMessage, "error");
                }
            } catch {
                adAvailabilityKnown = true;
                adIsUnavailable = true;
                renderStatusRow(adUnavailableMessage, "error");
                reportClientDiagnostic("ad-probe-failed", { searchUrl });
            }
        };

        queryInput.addEventListener("input", () => {
            debouncedAdSearch();
        });

        queryInput.addEventListener("focus", () => {
            probeAvailability();
            if (currentResults.length > 0) {
                panel.hidden = false;
                mountFloatingPanel(panel, anchor, {
                    gap: 6,
                    flipVertical: true,
                    kind: "ad-search",
                    matchWidth: true
                });
            }
        });

        queryInput.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();
                closePanel();
                return;
            }

            if (event.key === "ArrowDown") {
                if (currentResults.length === 0) {
                    return;
                }
                event.preventDefault();
                activeIndex = Math.min(activeIndex + 1, currentResults.length - 1);
                renderResults();
                return;
            }

            if (event.key === "ArrowUp") {
                if (currentResults.length === 0) {
                    return;
                }
                event.preventDefault();
                activeIndex = Math.max(activeIndex - 1, 0);
                renderResults();
                return;
            }

            if (event.key === "Enter" && activeIndex >= 0 && currentResults[activeIndex]) {
                event.preventDefault();
                fillFromResult(currentResults[activeIndex]);
            }
        });

        results.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const button = target.closest("[data-index]");
            if (!(button instanceof HTMLElement)) {
                return;
            }

            const index = Number.parseInt(button.dataset.index || "-1", 10);
            if (!Number.isFinite(index) || index < 0 || index >= currentResults.length) {
                return;
            }

            fillFromResult(currentResults[index]);
        });

        if (form instanceof HTMLFormElement) {
            form.addEventListener("submit", (event) => {
                if (guidInput.value && !submitButton.disabled) {
                    return;
                }

                event.preventDefault();
                renderStatusRow("Nejprve vyberte osobu z AD výsledků.", "empty");
                queryInput.focus();
            });
        }

        document.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            if (!isInteractionInsideFloatingControl(target, anchor, panel)) {
                closePanel();
            }
        });
    });
}
