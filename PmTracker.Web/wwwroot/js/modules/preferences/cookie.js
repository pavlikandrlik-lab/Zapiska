/** preferences/cookie.js — mini cookie helper (Path=/, SameSite=Lax). Sdílí projectMenu + preferences. */

export function readCookie(name) {
    const target = `${name}=`;
    for (const entry of document.cookie.split(";")) {
        const trimmed = entry.trim();
        if (trimmed.startsWith(target)) {
            return decodeURIComponent(trimmed.slice(target.length));
        }
    }
    return null;
}

export function writeCookie(name, value, maxAgeSeconds) {
    document.cookie = `${name}=${encodeURIComponent(value)}; Path=/; Max-Age=${maxAgeSeconds}; SameSite=Lax`;
}

export function deleteCookie(name) {
    document.cookie = `${name}=; Path=/; Max-Age=0; SameSite=Lax`;
}
