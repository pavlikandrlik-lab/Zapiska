const themeStorageKey = "pmtracker.theme.mode";
const themeSwitchSelector = "[data-theme-switch]";
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

        syncThemeSwitchElement(element, effectiveTheme);
    });
}

function syncThemeSwitchElement(element, effectiveTheme) {
    const input = element.querySelector("[data-theme-switch-input]");
    const label = element.querySelector("[data-theme-switch-label]");
    if (!(input instanceof HTMLInputElement) || !(label instanceof HTMLElement)) {
        return;
    }

    const isDark = effectiveTheme === "dark";
    const displayLabel = shouldDisplayThemeLabel(element);
    const labelLight = getThemeSwitchAttribute(input, "data-label-light", "Světlý mód");
    const labelDark = getThemeSwitchAttribute(input, "data-label-dark", "Tmavý mód");
    const ariaLabelLight = getThemeSwitchAttribute(input, "data-aria-label-light", "Přepnout na tmavý mód");
    const ariaLabelDark = getThemeSwitchAttribute(input, "data-aria-label-dark", "Přepnout na světlý mód");

    element.setAttribute("data-theme-switch-state", effectiveTheme);
    input.checked = isDark;
    label.hidden = !displayLabel;
    label.textContent = isDark ? labelDark : labelLight;

    input.setAttribute("aria-label", isDark ? ariaLabelDark : ariaLabelLight);
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

        if (element.dataset.themeSwitchBound === "true") {
            return;
        }

        const input = element.querySelector("[data-theme-switch-input]");
        if (input instanceof HTMLInputElement) {
            input.addEventListener("change", () => {
                setTheme(input.checked ? "dark" : "light", true);
            });
        }

        element.dataset.themeSwitchBound = "true";
    });
}
