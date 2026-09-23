// Tenký most mezi nativním gov-theme-switch a serverem (spec 2026-09-23 §9.1, §12.4).
//
// Přepínání obstarává gov-theme-switch sám: nastaví data-theme na <html> a volbu si drží
// v session cookie „data-theme". Aplikace přidává jen trvalou cookie pmtracker.theme.mode,
// ze které _Layout vykreslí data-theme už na serveru — i po zavření prohlížeče tak stránka
// naběhne rovnou ve zvoleném motivu, bez probliknutí. Bez uložené volby dosadí motiv podle
// systému inline skript v <head> _Layout.
const themeCookieName = "pmtracker.theme.mode";
const themeCookieMaxAgeSeconds = 60 * 60 * 24 * 365;
const govThemeSwitchComponent = "gov-theme-switch";

let bound = false;

function persistTheme(theme) {
    document.cookie = `${themeCookieName}=${theme}; Path=/; Max-Age=${themeCookieMaxAgeSeconds}; SameSite=Lax`;
}

export function initTheme() {
    if (bound) {
        return;
    }

    bound = true;

    // gov-change bublá (Stencil: bubbles + composed) a posílají ho i jiné gov komponenty
    // (gov-dropdown, formuláře) — proto filtr na detail.component.
    document.addEventListener("gov-change", (event) => {
        const detail = event.detail;
        if (!detail || detail.component !== govThemeSwitchComponent) {
            return;
        }

        if (detail.state === "dark" || detail.state === "light") {
            persistTheme(detail.state);
        }
    });
}
