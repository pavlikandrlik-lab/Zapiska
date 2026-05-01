/**
 * pm-tabs / pm-tab-list / pm-tab / pm-tab-panel
 * ----------------------------------------------
 * Vanilla custom elements (no shadow DOM) implementující WAI-ARIA tabs pattern.
 * Visualy navazuje sync-tabs pattern: tab line tvoří horní okraj prvního panelu,
 * aktivní tab vizuálně přechází do panelu.
 *
 * HTML použití (light DOM, CSS scoping přes element selectory):
 *
 *   <pm-tabs persist="hash" persist-key="record-edit" initial="basic"
 *            sync-input="EditorTab">
 *     <pm-tab-list>
 *       <pm-tab key="basic">Základní údaje</pm-tab>
 *       <pm-tab key="external">Externí vazby</pm-tab>
 *     </pm-tab-list>
 *     <pm-tab-panel key="basic">…</pm-tab-panel>
 *     <pm-tab-panel key="external">…</pm-tab-panel>
 *   </pm-tabs>
 *
 * Atributy <pm-tabs>:
 *   initial      — klíč iniciálního tabu (jen pokud persist storage/hash neuloží jiný)
 *   persist      — none|hash|storage  (default: none)
 *   persist-key  — unikátní identifikátor pro hash/storage namespace
 *   sync-input   — name= hidden inputu uvnitř pm-tabs, do kterého se zrcadlí active key
 *
 * Eventy:
 *   pm-tab-change — bubbles, detail: { key, previousKey }
 *
 * API:
 *   element.activeKey                 — getter/setter
 *   element.setActive(key, options)   — options.silent = true → nedispatchovat event
 */

const TAB_NAME = "pm-tab";
const TAB_LEFT_NAME = "pm-tab-left";
const TAB_RIGHT_NAME = "pm-tab-right";
const TAB_LAST_IN_ROW_NAME = "pm-tab-last-in-row";
const TABS_NAME = "pm-tabs";
const TAB_LIST_NAME = "pm-tab-list";
const TAB_PANEL_NAME = "pm-tab-panel";

/** CSS-tag selector zahrnuje base + dědicné varianty. */
const ANY_TAB_SELECTOR = `${TAB_NAME}, ${TAB_LEFT_NAME}, ${TAB_RIGHT_NAME}, ${TAB_LAST_IN_ROW_NAME}`;

const STORAGE_PREFIX = "pmtracker.tabs.";

class PmTabs extends HTMLElement {
    constructor() {
        super();
        this._activeKey = null;
        this._handleClick = this._handleClick.bind(this);
        this._handleKeydown = this._handleKeydown.bind(this);
        this._handleHashChange = this._handleHashChange.bind(this);
    }

    connectedCallback() {
        if (this._initialized) return;
        this._initialized = true;
        // Wait one microtask so child custom elements upgrade first.
        queueMicrotask(() => this._init());
    }

    disconnectedCallback() {
        this.removeEventListener("click", this._handleClick);
        this.removeEventListener("keydown", this._handleKeydown);
        if (this.persistMode === "hash") {
            window.removeEventListener("hashchange", this._handleHashChange);
        }
    }

    get persistMode() {
        const v = (this.getAttribute("persist") || "none").toLowerCase();
        return v === "hash" || v === "storage" ? v : "none";
    }

    get persistKey() {
        return this.getAttribute("persist-key") || "";
    }

    get syncInputName() {
        return this.getAttribute("sync-input") || "";
    }

    get activeKey() {
        return this._activeKey;
    }

    set activeKey(key) {
        this.setActive(key);
    }

    _init() {
        const tabs = this._tabs();
        if (tabs.length === 0) return;

        const initial = this._resolveInitialKey(tabs);
        this._applyState(initial);

        this.addEventListener("click", this._handleClick);
        this.addEventListener("keydown", this._handleKeydown);
        if (this.persistMode === "hash") {
            window.addEventListener("hashchange", this._handleHashChange);
        }
    }

    _tabs() {
        return Array.from(this.querySelectorAll(ANY_TAB_SELECTOR))
            .filter((t) => t.parentElement && t.parentElement.tagName.toLowerCase() === TAB_LIST_NAME);
    }

    _panels() {
        return Array.from(this.querySelectorAll(TAB_PANEL_NAME))
            .filter((p) => p.parentElement === this);
    }

    _resolveInitialKey(tabs) {
        // Priority: hash > storage > initial attribute > first non-disabled tab.
        const hashKey = this._readHashKey();
        if (hashKey && tabs.some((t) => t.tabKey === hashKey && !t.disabled)) {
            return hashKey;
        }
        const storedKey = this._readStorageKey();
        if (storedKey && tabs.some((t) => t.tabKey === storedKey && !t.disabled)) {
            return storedKey;
        }
        const initialAttr = this.getAttribute("initial");
        if (initialAttr && tabs.some((t) => t.tabKey === initialAttr && !t.disabled)) {
            return initialAttr;
        }
        const firstUsable = tabs.find((t) => !t.disabled && !t.hidden);
        return firstUsable ? firstUsable.tabKey : null;
    }

    _readHashKey() {
        if (this.persistMode !== "hash" || !this.persistKey) return null;
        const hash = window.location.hash || "";
        // Format: #pmtab-{persistKey}={value} (multiple components in one hash can coexist)
        const re = new RegExp(`(?:^#|[#&])pmtab-${this._escapeRegex(this.persistKey)}=([^&]+)`);
        const match = hash.match(re);
        return match ? decodeURIComponent(match[1]) : null;
    }

    _readStorageKey() {
        if (this.persistMode !== "storage" || !this.persistKey) return null;
        try {
            return window.localStorage.getItem(STORAGE_PREFIX + this.persistKey);
        } catch {
            return null;
        }
    }

    _writePersistence(key) {
        if (!this.persistKey) return;
        if (this.persistMode === "hash") {
            const hash = window.location.hash || "";
            const re = new RegExp(`pmtab-${this._escapeRegex(this.persistKey)}=[^&]*`);
            let next;
            if (re.test(hash)) {
                next = hash.replace(re, `pmtab-${this.persistKey}=${encodeURIComponent(key)}`);
            } else {
                next = (hash ? hash + "&" : "#") + `pmtab-${this.persistKey}=${encodeURIComponent(key)}`;
            }
            // Use replaceState to avoid polluting history with every tab click.
            try {
                history.replaceState(null, "", `${window.location.pathname}${window.location.search}${next}`);
            } catch {
                window.location.hash = next.startsWith("#") ? next.slice(1) : next;
            }
        } else if (this.persistMode === "storage") {
            try {
                window.localStorage.setItem(STORAGE_PREFIX + this.persistKey, key);
            } catch { /* quota or disabled — silent */ }
        }
    }

    _escapeRegex(s) {
        return String(s).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    }

    _handleClick(event) {
        const tab = event.target.closest(ANY_TAB_SELECTOR);
        if (!(tab instanceof PmTab)) return;
        if (!this.contains(tab)) return;
        if (tab.disabled) return;
        event.preventDefault();
        this.setActive(tab.tabKey);
    }

    _handleKeydown(event) {
        const tab = event.target.closest(ANY_TAB_SELECTOR);
        if (!(tab instanceof PmTab)) return;
        if (!this.contains(tab)) return;

        const tabs = this._tabs().filter((t) => !t.disabled && !t.hidden);
        const idx = tabs.indexOf(tab);
        if (idx < 0) return;

        let nextIdx = -1;
        switch (event.key) {
            case "ArrowRight":
                nextIdx = (idx + 1) % tabs.length; break;
            case "ArrowLeft":
                nextIdx = (idx - 1 + tabs.length) % tabs.length; break;
            case "Home":
                nextIdx = 0; break;
            case "End":
                nextIdx = tabs.length - 1; break;
            default:
                return;
        }
        event.preventDefault();
        const next = tabs[nextIdx];
        next.focus();
        this.setActive(next.tabKey);
    }

    _handleHashChange() {
        const hashKey = this._readHashKey();
        if (hashKey && hashKey !== this._activeKey) {
            this._applyState(hashKey, { silent: true });
        }
    }

    setActive(key, options) {
        if (!key || key === this._activeKey) return;
        const tabs = this._tabs();
        if (!tabs.some((t) => t.tabKey === key && !t.disabled)) return;

        const previous = this._activeKey;
        this._applyState(key);
        this._writePersistence(key);

        const silent = options && options.silent === true;
        if (!silent) {
            this.dispatchEvent(new CustomEvent("pm-tab-change", {
                bubbles: true,
                detail: { key, previousKey: previous }
            }));
        }
    }

    _applyState(key, options) {
        this._activeKey = key;

        const tabs = this._tabs();
        const panels = this._panels();

        tabs.forEach((tab) => {
            const active = tab.tabKey === key;
            tab.toggleAttribute("active", active);
            tab.setAttribute("aria-selected", active ? "true" : "false");
            tab.setAttribute("tabindex", active ? "0" : "-1");
        });

        panels.forEach((panel) => {
            const active = panel.tabKey === key;
            panel.toggleAttribute("active", active);
            panel.hidden = !active;
        });

        // Sync hidden input (server-side form binding compatibility).
        const inputName = this.syncInputName;
        if (inputName) {
            const input = this.querySelector(`input[name="${CSS.escape(inputName)}"]`);
            if (input instanceof HTMLInputElement) {
                input.value = key;
            }
        }
    }
}

class PmTabList extends HTMLElement {
    connectedCallback() {
        this.setAttribute("role", "tablist");
    }
}

class PmTab extends HTMLElement {
    static get observedAttributes() { return ["disabled"]; }

    connectedCallback() {
        this.setAttribute("role", "tab");
        if (!this.hasAttribute("tabindex")) {
            this.setAttribute("tabindex", "-1");
        }
        // Wrap text content into <span> so we can use 4 pseudo-elements
        // (li:before/:after + a:before/:after = pm-tab:before/:after + span:before/:after)
        // per CSS-tricks "Tabs With Round Out Borders" technique by Lea Verou.
        if (!this.querySelector(":scope > span[data-pm-tab-inner]")) {
            const span = document.createElement("span");
            span.setAttribute("data-pm-tab-inner", "");
            while (this.firstChild) span.appendChild(this.firstChild);
            this.appendChild(span);
        }
    }

    attributeChangedCallback(name) {
        if (name === "disabled") {
            this.setAttribute("aria-disabled", this.disabled ? "true" : "false");
        }
    }

    get tabKey() { return this.getAttribute("key"); }
    get disabled() { return this.hasAttribute("disabled"); }
}

class PmTabPanel extends HTMLElement {
    connectedCallback() {
        this.setAttribute("role", "tabpanel");
        if (!this.hasAttribute("tabindex")) {
            this.setAttribute("tabindex", "0");
        }
    }
    get tabKey() { return this.getAttribute("key"); }
}

function defineOnce(name, ctor) {
    if (!customElements.get(name)) {
        customElements.define(name, ctor);
    }
}

/** Děděné varianty pm-tab. Sdílejí všechno chování (instanceof PmTab je true).
 *  Liší se jen tag name → různé CSS targety pro outer round-out per pozice.
 *  - pm-tab: default middle, oba outer rohy active-only
 *  - pm-tab-left: vlevo na kraji, vždy LEFT outer round-out (pro page-bg edge)
 *  - pm-tab-right: vpravo na kraji, vždy RIGHT outer round-out
 *  - pm-tab-last-in-row: alias k pm-tab-right pro wrapped rows
 */
class PmTabLeft extends PmTab {}
class PmTabRight extends PmTab {}
class PmTabLastInRow extends PmTab {}

defineOnce(TABS_NAME, PmTabs);
defineOnce(TAB_LIST_NAME, PmTabList);
defineOnce(TAB_NAME, PmTab);
defineOnce(TAB_LEFT_NAME, PmTabLeft);
defineOnce(TAB_RIGHT_NAME, PmTabRight);
defineOnce(TAB_LAST_IN_ROW_NAME, PmTabLastInRow);
defineOnce(TAB_PANEL_NAME, PmTabPanel);

export { PmTabs, PmTab, PmTabLeft, PmTabRight, PmTabLastInRow, PmTabList, PmTabPanel };
