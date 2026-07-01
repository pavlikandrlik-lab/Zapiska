export const msPerDay = 24 * 60 * 60 * 1000;
export const dateMonths = [
    "Leden", "Únor", "Březen", "Duben", "Květen", "Červen",
    "Červenec", "Srpen", "Září", "Říjen", "Listopad", "Prosinec"
];

export function normalizeFilterText(value) {
    if (!value) {
        return "";
    }

    return value
        .toString()
        .trim()
        .toLowerCase()
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "");
}

export function normalizeFilterToken(value) {
    if (value === null || value === undefined) {
        return "";
    }

    return String(value).trim().toUpperCase();
}

export function normalizeSearchText(value) {
    return normalizeFilterText(value);
}

export function containsWordPrefix(text, token) {
    if (!text || !token) {
        return false;
    }

    const parts = text.split(/[\s@._,;:/\\-]+/g).filter(Boolean);
    return parts.some((part) => part.startsWith(token));
}

export function scoreSearchCandidate(query, haystack) {
    const normalizedQuery = normalizeSearchText(query);
    const normalizedHaystack = normalizeSearchText(haystack);

    if (!normalizedQuery) {
        return 1;
    }
    if (!normalizedHaystack) {
        return 0;
    }

    const tokens = normalizedQuery.split(/\s+/g).filter(Boolean);
    let score = 0;

    if (normalizedHaystack === normalizedQuery) {
        score += 1600;
    }
    if (normalizedHaystack.startsWith(normalizedQuery)) {
        score += 1100;
    }
    if (normalizedHaystack.includes(normalizedQuery)) {
        score += 700;
    }

    tokens.forEach((token) => {
        if (containsWordPrefix(normalizedHaystack, token)) {
            score += 180;
        } else if (normalizedHaystack.includes(token)) {
            score += 85;
        }
    });

    return score;
}

export function debounce(callback, waitMs) {
    let timeoutId = 0;
    return (...args) => {
        window.clearTimeout(timeoutId);
        timeoutId = window.setTimeout(() => callback(...args), waitMs);
    };
}

let rainbowMeasureCanvas = null;

export function measureTextWidth(text, fontSpec) {
    const normalized = String(text || "").trim();
    if (!normalized) {
        return 0;
    }

    if (!(rainbowMeasureCanvas instanceof HTMLCanvasElement)) {
        rainbowMeasureCanvas = document.createElement("canvas");
    }

    const context = rainbowMeasureCanvas.getContext("2d");
    if (!context) {
        return normalized.length * 7;
    }

    context.font = fontSpec || "600 11px sans-serif";
    return context.measureText(normalized).width;
}

export function parseColorChannels(value) {
    const normalized = String(value || "").trim();
    if (!normalized) {
        return null;
    }

    if (normalized.startsWith("#")) {
        const hex = normalized.slice(1);
        if (hex.length === 6) {
            const r = Number.parseInt(hex.slice(0, 2), 16);
            const g = Number.parseInt(hex.slice(2, 4), 16);
            const b = Number.parseInt(hex.slice(4, 6), 16);
            if (Number.isFinite(r) && Number.isFinite(g) && Number.isFinite(b)) {
                return { r, g, b };
            }
        }
    }

    const match = normalized.match(/^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)/i);
    if (!match) {
        return null;
    }

    const r = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[1]))));
    const g = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[2]))));
    const b = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[3]))));
    if (!Number.isFinite(r) || !Number.isFinite(g) || !Number.isFinite(b)) {
        return null;
    }

    return { r, g, b };
}

export function getContrastTextColor(backgroundColor) {
    const channels = parseColorChannels(backgroundColor);
    if (!channels) {
        return "#0F172A";
    }

    const toLinear = (channel) => {
        const normalized = channel / 255;
        if (normalized <= 0.03928) {
            return normalized / 12.92;
        }
        return ((normalized + 0.055) / 1.055) ** 2.4;
    };

    const luminance = (0.2126 * toLinear(channels.r))
        + (0.7152 * toLinear(channels.g))
        + (0.0722 * toLinear(channels.b));

    return luminance >= 0.45 ? "#0F172A" : "#F8FAFC";
}

export function pickSegmentLabel(fullLabel, shortLabel, availableWidthPx, fontSpec) {
    const available = Math.max(0, Number(availableWidthPx) || 0);
    if (available < 14) {
        return "";
    }

    const normalizedFull = String(fullLabel || "").trim();
    const normalizedShort = String(shortLabel || "").trim();
    const candidates = [normalizedFull, normalizedShort].filter(Boolean);
    const horizontalPaddingPx = 8;

    for (const candidate of candidates) {
        if (measureTextWidth(candidate, fontSpec) + horizontalPaddingPx <= available) {
            return candidate;
        }
    }

    return "";
}

export function parseIsoDate(value) {
    if (!value) {
        return null;
    }

    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value.trim());
    if (!match) {
        return null;
    }

    const year = Number.parseInt(match[1], 10);
    const month = Number.parseInt(match[2], 10);
    const day = Number.parseInt(match[3], 10);
    if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
        return null;
    }

    const date = new Date(year, month - 1, day);
    if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
        return null;
    }

    return date;
}

export function parseDisplayDate(value) {
    if (!value) {
        return null;
    }

    const match = /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/.exec(value.trim());
    if (!match) {
        return null;
    }

    const day = Number.parseInt(match[1], 10);
    const month = Number.parseInt(match[2], 10);
    const year = Number.parseInt(match[3], 10);
    if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
        return null;
    }

    const date = new Date(year, month - 1, day);
    if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
        return null;
    }

    return date;
}

export function formatIsoDate(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

export function formatDisplayDate(date) {
    const day = String(date.getDate()).padStart(2, "0");
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const year = date.getFullYear();
    return `${day}.${month}.${year}`;
}

/**
 * Vrátí "day stamp" pro účely diff kalkulací.
 * Používá lokální datum konzistentně s parseIsoDate a addCalendarDays,
 * čímž se vyhne DST chybám při přechodu letního/zimního času.
 */
export function toUtcDayStamp(date) {
    // Operujeme v local time konzistentně s ostatními date operacemi v aplikaci.
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    return d.getTime();
}

export function diffCalendarDays(a, b) {
    return Math.round((toUtcDayStamp(a) - toUtcDayStamp(b)) / msPerDay);
}

export function addCalendarDays(baseDate, dayCount) {
    const days = Number.isFinite(dayCount) ? Math.trunc(dayCount) : 0;
    const next = new Date(baseDate.getFullYear(), baseDate.getMonth(), baseDate.getDate());
    next.setDate(next.getDate() + days);
    return next;
}

export function formatAxisDayMonth(date) {
    const day = String(date.getDate()).padStart(2, "0");
    const month = String(date.getMonth() + 1).padStart(2, "0");
    return `${day}.${month}.`;
}

export function formatAxisMonthYear(date) {
    const month = String(date.getMonth() + 1).padStart(2, "0");
    return `${month}/${date.getFullYear()}`;
}

export function parseIsoDateTime(value) {
    if (!value) {
        return null;
    }

    const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::\d{2})?$/.exec(value.trim());
    if (!match) {
        return null;
    }

    const year = Number.parseInt(match[1], 10);
    const month = Number.parseInt(match[2], 10);
    const day = Number.parseInt(match[3], 10);
    const hours = Number.parseInt(match[4], 10);
    const minutes = Number.parseInt(match[5], 10);
    if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)
        || !Number.isFinite(hours) || !Number.isFinite(minutes)
        || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
        return null;
    }

    const date = new Date(year, month - 1, day, hours, minutes, 0, 0);
    if (date.getFullYear() !== year
        || date.getMonth() !== month - 1
        || date.getDate() !== day
        || date.getHours() !== hours
        || date.getMinutes() !== minutes) {
        return null;
    }

    return date;
}

export function parseTimeValue(value) {
    if (!value) {
        return null;
    }

    const match = /^(\d{1,2}):(\d{2})$/.exec(value.trim());
    if (!match) {
        return null;
    }

    const hours = Number.parseInt(match[1], 10);
    const minutes = Number.parseInt(match[2], 10);
    if (!Number.isFinite(hours) || !Number.isFinite(minutes) || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
        return null;
    }

    return { hours, minutes };
}

export function formatTime(hours, minutes) {
    return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
}

export function isSameCalendarDate(a, b) {
    return a.getFullYear() === b.getFullYear()
        && a.getMonth() === b.getMonth()
        && a.getDate() === b.getDate();
}

export function parseJsonPayload(rawText) {
    const text = String(rawText || "").trim();
    if (!text) {
        return null;
    }

    try {
        return JSON.parse(text);
    } catch (error) {
        return null;
    }
}

export function isPlainObject(value) {
    return value !== null && typeof value === "object" && !Array.isArray(value);
}

export function resolveAjaxResponseTraceId(response) {
    if (!(response instanceof Response) || !(response.headers instanceof Headers)) {
        return "";
    }

    return response.headers.get("x-trace-id")
        || response.headers.get("trace-id")
        || response.headers.get("request-id")
        || "";
}

export function truncateDiagnosticBody(value, maxLength) {
    const text = String(value || "");
    const limit = Number.isFinite(maxLength) ? Math.max(256, Number(maxLength)) : 12000;
    if (text.length <= limit) {
        return text;
    }

    return `${text.slice(0, limit)}\n...[truncated ${text.length - limit} chars]`;
}

export function reportClientDiagnostic(type, detail = {}) {
    const name = String(type || "").trim();
    if (!name) {
        return;
    }

    window.dispatchEvent(new CustomEvent("pmtracker:client-diagnostic", {
        detail: {
            type: name,
            ...detail
        }
    }));
}

export function buildResponseHeadersSnapshot(response, maxHeaders) {
    if (!(response instanceof Response) || !(response.headers instanceof Headers)) {
        return "<none>";
    }

    const limit = Number.isFinite(maxHeaders) ? Math.max(5, Number(maxHeaders)) : 80;
    const entries = Array.from(response.headers.entries());
    if (entries.length === 0) {
        return "<none>";
    }

    return entries
        .slice(0, limit)
        .map(([key, value]) => `${key}: ${value}`)
        .join("\n");
}

export function buildFormDataSnapshot(formData, maxFields) {
    if (!(formData instanceof FormData)) {
        return "<unavailable>";
    }

    const limit = Number.isFinite(maxFields) ? Math.max(5, Number(maxFields)) : 120;
    const lines = [];
    let count = 0;
    for (const [key, rawValue] of formData.entries()) {
        count += 1;
        if (count > limit) {
            break;
        }

        if (rawValue instanceof File) {
            lines.push(`${key}=<file:${rawValue.name};size=${rawValue.size}>`);
            continue;
        }

        const value = truncateDiagnosticBody(String(rawValue || ""), 300);
        lines.push(`${key}=${value}`);
    }

    if (count === 0) {
        return "<empty>";
    }

    if (count > limit) {
        lines.push(`...[truncated ${count - limit} fields]`);
    }

    return lines.join("\n");
}

/**
 * Vrací true pro DOM elementy, které reprezentují "button-like" UI kontrolu:
 * native <button>, <input type="submit|button|reset">, nebo gov-button Web Component.
 * Používej místo instanceof HTMLButtonElement tam, kde může vstupovat gov-button
 * z pm-button wrapperu (Fáze 2D).
 */
export function isButtonLike(el) {
    if (!(el instanceof HTMLElement)) return false;
    if (el instanceof HTMLButtonElement) return true;
    if (el instanceof HTMLInputElement) {
        const t = el.type;
        return t === "submit" || t === "button" || t === "reset";
    }
    return el.tagName.toLowerCase() === "gov-button";
}

/**
 * Přečte form-override atribut (formaction/formmethod) z submitteru formuláře.
 * gov-button (pm-button) se renderuje jako web komponenta: formaction/formmethod
 * zůstanou na host elementu <gov-button>, ale event.submitter je vnitřní nativní
 * <button>, do kterého komponenta tyto atributy NEKOPÍRUJE. Bez tohoto vystoupání
 * na host by se formaction ztratil → POST míří na prázdnou form action ("/" → 405).
 * Viz docs/known-issues/proposal-decision-buttons-formaction-405.md.
 *
 * @param {{getAttribute: (name: string) => (string|null)}|null} submitter
 * @param {string} attrName "formaction" | "formmethod"
 * @param {{getAttribute: (name: string) => (string|null)}|null} closestGovButton
 *   Nejbližší gov-button host (submitter.closest("gov-button")) nebo null.
 * @returns {string} hodnota atributu, jinak "".
 */
export function resolveFormSubmitterAttr(submitter, attrName, closestGovButton) {
    if (!submitter) return "";
    const own = submitter.getAttribute(attrName);
    if (own) return own;
    if (closestGovButton) {
        return closestGovButton.getAttribute(attrName) || "";
    }
    return "";
}

/**
 * Nastaví disabled stav pro button-like element.
 * Pro <gov-button> musí nastavit DOM atribut (custom element ho sleduje),
 * .disabled property na JS instanci nemusí mít efekt.
 */
export function setButtonDisabled(el, disabled) {
    if (!(el instanceof HTMLElement)) return;
    if (disabled) {
        el.setAttribute("disabled", "disabled");
        if ("disabled" in el) el.disabled = true;
    } else {
        el.removeAttribute("disabled");
        if ("disabled" in el) el.disabled = false;
    }
}

/**
 * Vrátí selektor pro všechny submit tlačítka ve formuláři, včetně gov-button.
 */
export const SUBMIT_SELECTOR =
    'button[type="submit"], input[type="submit"], gov-button[native-type="submit"]';

export async function copyTextToClipboard(text) {
    const value = String(text || "");
    if (!value) {
        return false;
    }

    if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") {
        try {
            await navigator.clipboard.writeText(value);
            return true;
        } catch (error) {
            // Fallback below.
        }
    }

    const helper = document.createElement("textarea");
    helper.value = value;
    helper.setAttribute("readonly", "readonly");
    helper.style.position = "fixed";
    helper.style.opacity = "0";
    document.body.appendChild(helper);
    helper.select();
    const copied = document.execCommand("copy");
    document.body.removeChild(helper);
    return copied;
}
