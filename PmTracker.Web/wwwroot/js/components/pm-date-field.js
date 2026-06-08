/**
 * <pm-date-field> Custom Element — sjednocená komponenta pro datumové vstupy.
 *
 * Účel: nahrazuje historické inline markup z _AppDateField.cshtml partialu jednou
 * web komponentou používanou na všech místech (základní údaje, harmonogram plán
 * + skutečnost, jednání, …). Razor partial se zachovává jako thin wrapper, který
 * pouze renderuje tento element s typovaným ViewModelem.
 *
 * Atributy (stringy, observed):
 *   - name                 — name pro hidden input (povinný pro form POST)
 *   - iso-value            — počáteční hodnota yyyy-MM-dd (může být prázdná)
 *   - display-value        — počáteční zobrazená hodnota dd.MM.yyyy
 *   - locked               — "true" / "false" (default false)
 *   - aria-label           — text pro otvírací tlačítko ("Otevřít kalendář")
 *   - container-css-class  — extra CSS třídy přidané na vnitřní wrapper
 *
 * Light DOM (ne Shadow DOM): hidden input musí být submitnutelný v parent <form>,
 * což shadow root znemožňuje. Light DOM zachovává plnou kompatibilitu s existujícím
 * pickers/date.js modulem (delegated listeners, querySelector na document).
 *
 * Auto-init: po connectedCallback element renderuje markup a volá
 * initCustomDatePickers(this) — instance kalendářní logiky se nabinduje
 * automaticky, žádný explicit init pattern z volajícího kódu nepotřebuje.
 *
 * Forwarding extra data-* atributů:
 *   - data-* atributy přidané na <pm-date-field> jsou propsány na hidden input
 *     (= pattern dříve realizovaný přes ExtraDataAttributes ViewModelu).
 */
import { initCustomDatePickers } from "../modules/pickers/date.js";

const ATTR_NAME = "name";
const ATTR_ISO = "iso-value";
const ATTR_DISPLAY = "display-value";
const ATTR_LOCKED = "locked";
const ATTR_ARIA = "aria-label";
const ATTR_CONTAINER_CLASS = "container-css-class";
const ATTR_CLEARABLE = "clearable";

function escapeHtml(value) {
    if (value === null || value === undefined) return "";
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

class PmDateFieldElement extends HTMLElement {
    static get observedAttributes() {
        return [ATTR_NAME, ATTR_ISO, ATTR_DISPLAY, ATTR_LOCKED, ATTR_ARIA, ATTR_CONTAINER_CLASS, ATTR_CLEARABLE];
    }

    connectedCallback() {
        if (!this._rendered) {
            this.render();
        }
        // Auto-init kalendářové logiky pro tuto instanci.
        initCustomDatePickers(this);
    }

    attributeChangedCallback(_name, oldValue, newValue) {
        if (!this._rendered) return;
        if (oldValue === newValue) return;
        // Re-render is overkill — pickers/date.js drží references na DOM nodes.
        // Update jen relevantní atributy in-place.
        this._applyAttributeChange(_name);
    }

    _applyAttributeChange(name) {
        const wrapper = this.querySelector("[data-app-date-field]");
        if (!wrapper) return;
        const display = wrapper.querySelector("[data-app-date-display]");
        const hidden = wrapper.querySelector("[data-app-date-value]");
        const trigger = wrapper.querySelector("[data-app-date-open]");
        switch (name) {
            case ATTR_ISO:
                if (hidden) hidden.value = this.getAttribute(ATTR_ISO) || "";
                break;
            case ATTR_DISPLAY:
                if (display) display.value = this.getAttribute(ATTR_DISPLAY) || "";
                break;
            case ATTR_LOCKED: {
                const locked = this.getAttribute(ATTR_LOCKED) === "true";
                wrapper.dataset.appDateLocked = locked ? "true" : "false";
                if (trigger) {
                    if (locked) trigger.setAttribute("disabled", "disabled");
                    else trigger.removeAttribute("disabled");
                }
                break;
            }
            case ATTR_ARIA: {
                const label = this.getAttribute(ATTR_ARIA) || "Otevřít kalendář";
                if (trigger) trigger.setAttribute("aria-label", label);
                break;
            }
            case ATTR_NAME:
                if (hidden) hidden.setAttribute("name", this.getAttribute(ATTR_NAME) || "");
                break;
            case ATTR_CONTAINER_CLASS: {
                const extra = this.getAttribute(ATTR_CONTAINER_CLASS) || "";
                wrapper.className = ("app-date-field " + extra).trim();
                break;
            }
        }
    }

    render() {
        const name = this.getAttribute(ATTR_NAME) || "";
        const isoValue = this.getAttribute(ATTR_ISO) || "";
        const displayValue = this.getAttribute(ATTR_DISPLAY) || "";
        const locked = this.getAttribute(ATTR_LOCKED) === "true";
        const ariaLabel = this.getAttribute(ATTR_ARIA) || "Otevřít kalendář";
        const containerClass = this.getAttribute(ATTR_CONTAINER_CLASS) || "";
        const clearable = this.getAttribute(ATTR_CLEARABLE) === "true";
        const fullContainerClass = ("app-date-field " + containerClass).trim();

        // Forward data-* atributy z elementu na hidden input (= dříve ExtraDataAttributes).
        const forwardedDataAttrs = Array.from(this.attributes)
            .filter((a) => a.name.startsWith("data-"))
            .map((a) => `${a.name}="${escapeHtml(a.value)}"`)
            .join(" ");

        // FIX 2026-05-05: clear button (✕) opt-in přes clearable="true". Viditelný jen když
        // hidden input má hodnotu (CSS rule na data-has-value="true" wrapper). Klik handler je
        // v pickers/date.js (data-app-date-clear).
        const hasValue = isoValue.length > 0 ? "true" : "false";
        const clearButtonMarkup = clearable
            ? `<button class="app-date-clear" type="button" data-app-date-clear aria-label="Smazat datum"${locked ? " disabled=\"disabled\"" : ""}>✕</button>`
            : "";

        this.innerHTML = `
<div class="${escapeHtml(fullContainerClass)}" data-app-date-field data-app-date-locked="${locked ? "true" : "false"}" data-app-date-clearable="${clearable ? "true" : "false"}" data-app-date-has-value="${hasValue}" data-floating-anchor>
    <input class="app-date-display-input" type="text" data-app-date-display value="${escapeHtml(displayValue)}" readonly />
    <input type="hidden" name="${escapeHtml(name)}" value="${escapeHtml(isoValue)}" data-app-date-value ${forwardedDataAttrs} />
    ${clearButtonMarkup}
    <button class="app-date-trigger" type="button" data-app-date-open aria-label="${escapeHtml(ariaLabel)}"${locked ? " disabled=\"disabled\"" : ""}>
        <gov-icon size="s" name="calendar3" type="components" aria-hidden="true"></gov-icon>
    </button>
    <div class="app-date-panel" data-app-date-panel data-floating-panel data-floating-kind="date" hidden>
        <div class="app-date-nav">
            <button class="app-date-nav-btn" type="button" data-app-date-prev aria-label="Předchozí měsíc">‹</button>
            <select class="app-date-select app-date-month" data-app-date-month aria-label="Měsíc"></select>
            <select class="app-date-select app-date-year" data-app-date-year aria-label="Rok"></select>
            <button class="app-date-nav-btn" type="button" data-app-date-next aria-label="Další měsíc">›</button>
        </div>
        <div class="app-date-weekdays" aria-hidden="true">
            <span>Po</span><span>Út</span><span>St</span><span>Čt</span><span>Pá</span><span>So</span><span>Ne</span>
        </div>
        <div class="app-date-grid" data-app-date-grid role="grid"></div>
    </div>
</div>`.trim();
        this._rendered = true;
    }
}

if (!customElements.get("pm-date-field")) {
    customElements.define("pm-date-field", PmDateFieldElement);
}
