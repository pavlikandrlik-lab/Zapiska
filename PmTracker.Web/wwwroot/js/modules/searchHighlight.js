// Dočasné podsvícení hledaného textu po příchodu z výsledků vyhledávání
// (odkaz staví RecordSearchService.BuildDetailUrl, cíl předává data-record-target-highlight).
// Shoda je doslovná, jen bez ohledu na velikost písmen — bez skládání diakritiky
// (rozhodnutí uživatele 2026-10-06: „reseni" nemá podsvítit „řešení").

const FLASH_CLASS = "app-search-flash";
const FADING_CLASS = "app-search-flash--fading";
const DEFAULT_DURATION_MS = 15000;
const FADE_MS = 1000;
// Musí sedět se serverem (SearchQueryText.MinTermLength): krátké slovo vedle delších se
// nehledá, a tak se ani nepodsvítí — jinak spojka „a“ rozsvítí každé „a“ na kartě.
const MIN_TERM_LENGTH = 3;

// Do těchto míst se nesahá: ovládací prvky, skryté formuláře (úprava vyjádření nese
// v <textarea> HTML jako text), editor Quill a už vložené značky.
const SKIP_SELECTOR = [
    "textarea",
    "input",
    "select",
    "option",
    "script",
    "style",
    "[hidden]",
    ".ql-editor",
    ".ql-toolbar",
    `mark.${FLASH_CLASS}`
].join(", ");

function escapeRegExp(text) {
    return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

// Web Components (gov-*, pm-*) si text v hostu spravují samy (Stencil slot relocation) —
// vložená <mark> by se při překreslení rozbila.
function isInsideCustomElement(element, root) {
    for (let node = element; node && node !== root; node = node.parentElement) {
        if (node.localName.includes("-")) {
            return true;
        }
    }

    return false;
}

function unwrap(mark) {
    const parent = mark.parentNode;
    if (!parent) {
        return;
    }

    mark.replaceWith(...mark.childNodes);
    parent.normalize();
}

// Uvozovky fráze — rovné i typografické (české „…“, anglické “…”), jako SearchQueryText.QuoteChars.
const QUOTES = /["\u201E\u201C\u201D]/;

/**
 * Výrazy dotazu k podsvícení, delší první (ať „záloha" nepřebije „zálohování"). Stejný rozklad
 * jako SearchQueryText.SplitTerms na serveru: text v uvozovkách je jedna fráze (i krátká),
 * neuzavřená fráze běží do konce; krátká slova vedle fráze nebo delšího slova vynechá, dotaz
 * jen z krátkých slov („50 %") vrátí celý.
 */
export function highlightTerms(query) {
    const parts = [];
    String(query ?? "").split(QUOTES).forEach((segment, index) => {
        const words = segment.split(/\s+/).filter(Boolean);
        if (index % 2 === 1) {
            if (words.length > 0) {
                parts.push({ text: words.join(" "), phrase: true });
            }
        } else {
            words.forEach((word) => parts.push({ text: word, phrase: false }));
        }
    });

    const hasLongTerm = parts.some((part) => part.phrase || part.text.length >= MIN_TERM_LENGTH);
    const terms = parts
        .filter((part) => part.phrase || !hasLongTerm || part.text.length >= MIN_TERM_LENGTH)
        .map((part) => part.text);
    return [...new Set(terms)].sort((a, b) => b.length - a.length);
}

/**
 * Regulární výraz pro podsvícení. Mezera ve frázi odpovídá libovolné mezeře v textu stránky —
 * zalomení řádku i pevné mezeře.
 */
export function buildHighlightPattern(terms) {
    return new RegExp(terms.map((term) => escapeRegExp(term).replace(/ /g, "\\s+")).join("|"), "giu");
}

/**
 * Obalí výskyty slov z query uvnitř root do <mark class="app-search-flash"> a po durationMs
 * je plynule odstraní. Vrací vložené značky.
 */
export function highlightSearchTerms(root, query, { durationMs = DEFAULT_DURATION_MS } = {}) {
    if (!(root instanceof HTMLElement)) {
        return [];
    }

    const terms = highlightTerms(query);
    if (terms.length === 0) {
        return [];
    }

    const pattern = buildHighlightPattern(terms);
    const textNodes = [];
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        const parent = node.parentElement;
        if (!parent || parent.closest(SKIP_SELECTOR) || isInsideCustomElement(parent, root)) {
            continue;
        }

        pattern.lastIndex = 0;
        if (pattern.test(node.data)) {
            textNodes.push(node);
        }
    }

    const marks = [];
    for (const node of textNodes) {
        const fragment = document.createDocumentFragment();
        let lastIndex = 0;
        for (const match of node.data.matchAll(pattern)) {
            fragment.append(node.data.slice(lastIndex, match.index));
            const mark = document.createElement("mark");
            mark.className = FLASH_CLASS;
            mark.textContent = match[0];
            fragment.append(mark);
            marks.push(mark);
            lastIndex = match.index + match[0].length;
        }

        fragment.append(node.data.slice(lastIndex));
        node.replaceWith(fragment);
    }

    if (marks.length > 0) {
        window.setTimeout(() => {
            marks.forEach((mark) => mark.classList.add(FADING_CLASS));
            window.setTimeout(() => marks.forEach(unwrap), FADE_MS);
        }, durationMs);
    }

    return marks;
}
