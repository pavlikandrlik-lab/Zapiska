const themeStorageKey = "pmtracker.theme.mode";
const themeSwitchSelector = "[data-theme-switch]";
const govThemeSwitchTag = "gov-theme-switch";
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
    // Synchronizace gov-theme-switch Web Component
    const govSwitch = element.querySelector(govThemeSwitchTag);
    if (govSwitch instanceof HTMLElement) {
        // Nastavíme theme property/atribut dle aktuálního efektivního tématu.
        // gov-theme-switch přijímá "light" | "dark" | "auto".
        govSwitch.setAttribute("theme", effectiveTheme);
        return;
    }

    // Fallback: původní custom input (pro případné další instance)
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

/**
 * Připojí gov-change listener na gov-theme-switch Web Component.
 * gov-theme-switch emituje CustomEvent "gov-change" s detail.state = "light" | "dark".
 * My zachytíme event, uložíme volbu naší cookies a synchronizujeme html atributy.
 * Auto-mód gov-theme-switch neemituje — při kliknutí vždy přepíná na light nebo dark.
 */
function bindGovThemeSwitch(element) {
    const govSwitch = element.querySelector(govThemeSwitchTag);
    if (!(govSwitch instanceof HTMLElement)) {
        return false;
    }

    if (element.dataset.themeSwitchBound === "true") {
        return true;
    }

    govSwitch.addEventListener("gov-change", (event) => {
        const detail = event.detail;
        const newMode = detail && isThemeMode(detail.state) ? detail.state : null;
        if (!newMode) {
            return;
        }

        // gov-theme-switch již nastavil data-theme na <html> interně.
        // My navíc uložíme do naší cookie a nastavíme data-theme-mode.
        persistThemeMode(newMode);
        document.documentElement.setAttribute("data-theme-mode", newMode);
        // gov-theme-switch sám nastaví data-theme, ale pro jistotu synchronizujeme
        document.documentElement.setAttribute("data-theme", resolveEffectiveTheme(newMode));
        element.setAttribute("data-theme-switch-state", newMode);
    });

    element.dataset.themeSwitchBound = "true";
    return true;
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

        // Pokus o binding gov-theme-switch Web Component
        if (bindGovThemeSwitch(element)) {
            return;
        }

        // Fallback: původní custom input
        const input = element.querySelector("[data-theme-switch-input]");
        if (input instanceof HTMLInputElement) {
            input.addEventListener("change", () => {
                setTheme(input.checked ? "dark" : "light", true);
            });
        }

        element.dataset.themeSwitchBound = "true";
    });
}
