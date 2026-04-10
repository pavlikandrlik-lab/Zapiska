const themeStorageKey = "pmtracker.theme.mode";
const themeSwitchSelector = "gov-theme-switch";
const mediaDark = window.matchMedia("(prefers-color-scheme: dark)");
const themeCookieMaxAgeSeconds = 60 * 60 * 24 * 365;

function isThemeMode(value) {
    return value === "dark" || value === "light" || value === "auto";
}

function getStoredThemeMode() {
    const cookieValue = getThemeCookieMode();
    if (isThemeMode(cookieValue)) {
        return cookieValue;
    }

    try {
        const storedValue = localStorage.getItem(themeStorageKey);
        return isThemeMode(storedValue) ? storedValue : null;
    } catch {
        return null;
    }
}

function getThemeCookieMode() {
    const cookies = document.cookie.split(";");
    for (const entry of cookies) {
        const [rawName, rawValue = ""] = entry.split("=");
        if (rawName.trim() !== themeStorageKey) {
            continue;
        }

        const value = decodeURIComponent(rawValue.trim());
        return isThemeMode(value) ? value : null;
    }

    return null;
}

function persistThemeMode(mode) {
    try {
        localStorage.setItem(themeStorageKey, mode);
    } catch {
        // Browser storage is optional. Theme preference still persists via cookie.
    }

    document.cookie = `${themeStorageKey}=${encodeURIComponent(mode)}; Path=/; Max-Age=${themeCookieMaxAgeSeconds}; SameSite=Lax`;
}

function resolveEffectiveTheme(mode) {
    if (mode === "dark" || mode === "light") {
        return mode;
    }

    return mediaDark.matches ? "dark" : "light";
}

function setTheme(mode, persist) {
    const normalized = isThemeMode(mode) ? mode : "auto";
    const effective = resolveEffectiveTheme(normalized);

    document.documentElement.setAttribute("data-theme", effective);
    document.documentElement.setAttribute("data-theme-mode", normalized);

    if (persist) {
        persistThemeMode(normalized);
    }

    syncThemeSwitches(effective);
}

function syncThemeSwitches(effectiveTheme) {
    document.querySelectorAll(themeSwitchSelector).forEach((element) => {
        if (!(element instanceof HTMLElement)) {
            return;
        }

        upgradeThemeSwitch(element);
        syncThemeSwitchElement(element, effectiveTheme);
    });
}

function upgradeThemeSwitch(element) {
    if (element.dataset.themeSwitchInitialized === "true") {
        return;
    }

    element.innerHTML = [
        "<button type=\"button\" class=\"gov-theme-switch-button\" role=\"switch\" aria-checked=\"false\" data-theme-switch-button>",
        "  <span class=\"gov-theme-switch-track\" aria-hidden=\"true\">",
        "    <span class=\"gov-theme-switch-icon gov-theme-switch-icon-sun\">",
        "      <svg viewBox=\"0 0 24 24\" focusable=\"false\" aria-hidden=\"true\">",
        "        <path d=\"M12 4.75a.75.75 0 0 1 .75.75v1.5a.75.75 0 0 1-1.5 0V5.5a.75.75 0 0 1 .75-.75Zm0 11a3.75 3.75 0 1 0 0-7.5 3.75 3.75 0 0 0 0 7.5Zm7.25-4.5a.75.75 0 0 1 0 1.5h-1.5a.75.75 0 0 1 0-1.5h1.5Zm-13 0a.75.75 0 0 1 0 1.5h-1.5a.75.75 0 0 1 0-1.5h1.5Zm9.046-4.296a.75.75 0 0 1 1.061 0l1.061 1.061a.75.75 0 0 1-1.06 1.06l-1.062-1.06a.75.75 0 0 1 0-1.061Zm-8.652 8.652a.75.75 0 0 1 1.06 0l1.061 1.061a.75.75 0 1 1-1.06 1.06l-1.061-1.06a.75.75 0 0 1 0-1.061Zm9.713 1.06a.75.75 0 0 1 1.061 1.061l-1.061 1.061a.75.75 0 0 1-1.06-1.06l1.06-1.062Zm-8.652-8.651a.75.75 0 0 1 0 1.06L6.984 10.14a.75.75 0 0 1-1.06-1.06l1.06-1.062a.75.75 0 0 1 1.061 0ZM12 17a.75.75 0 0 1 .75.75v1.5a.75.75 0 0 1-1.5 0v-1.5A.75.75 0 0 1 12 17Z\" />",
        "      </svg>",
        "    </span>",
        "    <span class=\"gov-theme-switch-icon gov-theme-switch-icon-moon\">",
        "      <svg viewBox=\"0 0 24 24\" focusable=\"false\" aria-hidden=\"true\">",
        "        <path d=\"M14.72 3.78a.75.75 0 0 1 .86.86 7.25 7.25 0 0 0 8.78 8.78.75.75 0 0 1 .86.86A9.25 9.25 0 1 1 14.72 3.78Zm-.91 1.77a7.75 7.75 0 1 0 7.64 7.64 8.75 8.75 0 0 1-7.64-7.64Z\" transform=\"translate(-1.5 -1.5) scale(0.95)\" />",
        "      </svg>",
        "    </span>",
        "    <span class=\"gov-theme-switch-thumb\"></span>",
        "  </span>",
        "  <span class=\"gov-theme-switch-label\" data-theme-switch-label hidden></span>",
        "</button>"
    ].join("");

    const button = element.querySelector("[data-theme-switch-button]");
    if (button instanceof HTMLButtonElement) {
        button.addEventListener("click", () => {
            const currentState = element.getAttribute("data-theme-switch-state") === "dark" ? "dark" : "light";
            const nextMode = currentState === "dark" ? "light" : "dark";

            element.dispatchEvent(new CustomEvent("gov-change", {
                bubbles: true,
                detail: { mode: nextMode }
            }));
        });
    }

    element.dataset.themeSwitchInitialized = "true";
}

function syncThemeSwitchElement(element, effectiveTheme) {
    const button = element.querySelector("[data-theme-switch-button]");
    const label = element.querySelector("[data-theme-switch-label]");
    if (!(button instanceof HTMLButtonElement) || !(label instanceof HTMLElement)) {
        return;
    }

    const isDark = effectiveTheme === "dark";
    const displayLabel = shouldDisplayThemeLabel(element);
    const labelLight = getThemeSwitchAttribute(element, "label-light", "Světlý mód");
    const labelDark = getThemeSwitchAttribute(element, "label-dark", "Tmavý mód");
    const ariaLabelLight = getThemeSwitchAttribute(element, "aria-label-light", "Přepnout na tmavý mód");
    const ariaLabelDark = getThemeSwitchAttribute(element, "aria-label-dark", "Přepnout na světlý mód");

    element.setAttribute("data-theme-switch-state", effectiveTheme);
    label.hidden = !displayLabel;
    label.textContent = isDark ? labelDark : labelLight;

    button.setAttribute("aria-checked", String(isDark));
    button.setAttribute("aria-label", isDark ? ariaLabelDark : ariaLabelLight);
}

function shouldDisplayThemeLabel(element) {
    if (!element.hasAttribute("display-label")) {
        return false;
    }

    const value = element.getAttribute("display-label");
    return value === "" || value === "true";
}

function getThemeSwitchAttribute(element, attributeName, fallback) {
    const value = element.getAttribute(attributeName);
    return value && value.trim().length > 0 ? value.trim() : fallback;
}

export function initTheme() {
    const stored = getStoredThemeMode();
    if (stored && !getThemeCookieMode()) {
        persistThemeMode(stored);
    }

    setTheme(stored || "auto", false);

    const handleMediaChange = () => {
        const mode = getStoredThemeMode() || "auto";
        if (mode === "auto") {
            setTheme("auto", false);
        }
    };

    if (typeof mediaDark.addEventListener === "function") {
        mediaDark.addEventListener("change", handleMediaChange);
    } else if (typeof mediaDark.addListener === "function") {
        mediaDark.addListener(handleMediaChange);
    }

    document.querySelectorAll(themeSwitchSelector).forEach((element) => {
        if (!(element instanceof HTMLElement)) {
            return;
        }

        upgradeThemeSwitch(element);
        if (element.dataset.themeSwitchBound === "true") {
            return;
        }

        element.addEventListener("gov-change", (event) => {
            const nextMode = event instanceof CustomEvent && isThemeMode(event.detail?.mode)
                ? event.detail.mode
                : "auto";
            setTheme(nextMode, true);
        });
        element.dataset.themeSwitchBound = "true";
    });
}
