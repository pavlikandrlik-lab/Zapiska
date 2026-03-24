const themeStorageKey = "pmtracker.theme.mode";
const mediaDark = window.matchMedia("(prefers-color-scheme: dark)");

function getStoredThemeMode() {
    return localStorage.getItem(themeStorageKey);
}

function resolveEffectiveTheme(mode) {
    if (mode === "dark" || mode === "light") {
        return mode;
    }
    return mediaDark.matches ? "dark" : "light";
}

function setTheme(mode, persist) {
    const normalized = mode === "dark" || mode === "light" || mode === "auto" ? mode : "auto";
    const effective = resolveEffectiveTheme(normalized);
    document.documentElement.setAttribute("data-theme", effective);
    document.documentElement.setAttribute("data-theme-mode", normalized);

    if (persist) {
        localStorage.setItem(themeStorageKey, normalized);
    }

    document.querySelectorAll("[data-theme-switch]").forEach((element) => {
        if (element instanceof HTMLInputElement) {
            element.checked = effective === "dark";
        }
    });
}

export function initTheme() {
    const stored = getStoredThemeMode();
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

    document.querySelectorAll("[data-theme-switch]").forEach((element) => {
        if (!(element instanceof HTMLInputElement)) {
            return;
        }
        element.addEventListener("change", () => {
            setTheme(element.checked ? "dark" : "light", true);
        });
    });
}
