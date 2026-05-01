/* Plán 2 Feature A — /SDConnector/Inspect JS modul.
   Fetch /SDConnector/Load?cislo=XXXXXX, render raw + friendly panelu,
   sessionStorage persistence pro reload, button do chat modalu
   (reuse existing pmChatModal.open). */
(function (global) {
    'use strict';

    const STORAGE_KEY = 'pm.sdc.inspect.lastLoad';
    const CISLO_REGEX = /^\d{6}$/;

    async function load(cislo) {
        const resp = await fetch('/SDConnector/Load?cislo=' + encodeURIComponent(cislo), {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        });
        // 400 BadRequest a 500 InternalServerError obsahují strukturovaný SDConnectorLoadResponse
        // s Error field — controller v něm posílá diagnostiku [stage=...] + exception details.
        // Chceme ji propsat do UI místo generické "HTTP 500".
        if (!resp.ok && resp.status !== 400 && resp.status !== 500) {
            throw new Error('HTTP ' + resp.status);
        }
        const data = await resp.json();
        if (!resp.ok && data && data.error) {
            // Surface server-side diagnostic message (typ exception + stage + stack trace).
            throw new Error(data.error);
        }
        try {
            sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ ts: Date.now(), cislo: cislo, data: data }));
        } catch (_) {
            /* quota full nebo private mode — ignore */
        }
        return data;
    }

    function formatDate(iso) {
        if (!iso) return '—';
        const d = new Date(iso);
        if (isNaN(d.valueOf())) return iso;
        return d.toLocaleString('cs-CZ');
    }

    function escapeHtml(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
        });
    }

    function renderHeader(data) {
        const box = document.querySelector('[data-sdc-header]');
        if (!box) return;
        if (!data.nalezeno || !data.raw) {
            box.hidden = true;
            box.textContent = '';
            return;
        }
        const h = data.raw;
        box.hidden = false;
        box.textContent =
            '#' + h.cislo +
            ' · typ=' + (h.typZaznamu || '?') +
            ' · stav=' + (h.stav || '?') +
            ' · datum=' + formatDate(h.datum) +
            (h.strucne ? ' · ' + h.strucne : '');
    }

    function renderRaw(data) {
        const pre = document.querySelector('[data-sdc-raw]');
        if (!pre) return;
        if (!data.nalezeno || !data.raw) {
            pre.textContent = '';
            return;
        }
        const lines = [];
        lines.push('== Hlavička HOT_ZAZNAMY ==');
        lines.push('id: ' + data.raw.cislo);
        lines.push('typ_zaznamu: ' + (data.raw.typZaznamu || ''));
        lines.push('stav: ' + (data.raw.stav || ''));
        lines.push('datum: ' + formatDate(data.raw.datum));
        lines.push('externi_odkaz_id: ' + (data.raw.externiOdkazId == null ? '—' : data.raw.externiOdkazId));
        lines.push('');
        lines.push('== Stručně ==');
        lines.push(data.raw.strucne || '');
        lines.push('');
        lines.push('== Popis (raw HTML) ==');
        lines.push(data.raw.popisRaw || '');
        lines.push('');
        lines.push('== Vyjádření (HOT_VYJADRENI) ==');
        const bubliny = data.bubliny || [];
        for (let i = 0; i < bubliny.length; i++) {
            const b = bubliny[i];
            lines.push('[id=' + b.hotId + '] typ=' + (b.typ || '') + ' ' + formatDate(b.datum));
            lines.push('  autor (raw login): ' + (b.loginRaw || '—'));
            lines.push('  tým: ' + (b.tym || '—'));
            lines.push('  classified_as: ' + (b.classifiedAs || 'None'));
            lines.push('  popis (raw HTML):');
            const rawPopis = b.popisRaw || '';
            const indented = rawPopis.split('\n').join('\n    ');
            lines.push('    ' + indented);
            lines.push('');
        }
        pre.textContent = lines.join('\n');
    }

    function renderFriendly(data) {
        const container = document.querySelector('[data-sdc-friendly]');
        const btn = document.querySelector('[data-sdc-open-modal]');
        if (!container) return;
        container.innerHTML = '';
        if (btn) btn.hidden = true;
        if (!data.nalezeno) return;

        const bubliny = data.bubliny || [];
        if (bubliny.length === 0) {
            const empty = document.createElement('p');
            empty.textContent = 'Žádná vyjádření nenalezena.';
            container.appendChild(empty);
        }

        for (let i = 0; i < bubliny.length; i++) {
            const b = bubliny[i];
            const div = document.createElement('div');
            div.className = 'sdc-friendly-bubble';
            const classified = b.classifiedAs || 'None';
            div.innerHTML =
                '<div class="sdc-friendly-bubble__header">' +
                '<span>' + escapeHtml(formatDate(b.datum)) + '</span>' +
                '<span>·</span>' +
                '<span>' + escapeHtml(b.autorDisplayName || b.loginRaw || '—') + '</span>' +
                '<span>·</span>' +
                '<span>' + escapeHtml(b.tym || '—') + '</span>' +
                '<span class="sdc-friendly-bubble__classification" data-predicate="' + escapeHtml(classified) + '">' +
                escapeHtml(classified) + '</span>' +
                '</div>' +
                '<div class="sdc-friendly-bubble__plain">' + escapeHtml(b.popisPlainText || '') + '</div>';
            container.appendChild(div);
        }

        // Button „Otevřít v chat modalu" — pouze pokud ticket má vazbu na ZaznamExterniOdkaz.
        if (btn && data.raw && data.raw.externiOdkazId) {
            btn.hidden = false;
            btn.dataset.externiOdkazId = String(data.raw.externiOdkazId);
        }
    }

    function setStatus(msg, kind) {
        const el = document.querySelector('[data-sdc-status]');
        if (!el) return;
        el.textContent = msg || '';
        if (kind) {
            el.dataset.kind = kind;
        } else {
            delete el.dataset.kind;
        }
        // Pro error kind zachovej multi-line formátování (stack trace ze server-side
        // diagnostiky obsahuje newlines). Bez pre-wrap by se vše zploštilo do jednoho řádku.
        if (kind === 'error') {
            el.style.whiteSpace = 'pre-wrap';
            el.style.fontFamily = 'ui-monospace, SFMono-Regular, Menlo, monospace';
            el.style.fontSize = '0.85em';
            el.style.userSelect = 'text';
        } else {
            el.style.whiteSpace = '';
            el.style.fontFamily = '';
            el.style.fontSize = '';
            el.style.userSelect = '';
        }
    }

    function render(data) {
        renderHeader(data);
        renderRaw(data);
        renderFriendly(data);
        const grid = document.querySelector('[data-sdc-grid]');
        if (grid) grid.hidden = !data.nalezeno;
        if (!data.nalezeno) {
            setStatus(data.error || 'Ticket nenalezen.', 'error');
        } else {
            const count = (data.bubliny || []).length;
            setStatus('Načteno: ' + count + ' vyjádření', 'ok');
        }
    }

    async function submitLoad(cislo) {
        if (!CISLO_REGEX.test(cislo)) {
            setStatus('Zadej 6 cifer.', 'error');
            return;
        }
        setStatus('Načítám…', '');
        try {
            const data = await load(cislo);
            render(data);
        } catch (err) {
            setStatus('Chyba: ' + (err && err.message ? err.message : 'unknown'), 'error');
        }
    }

    function init() {
        const form = document.querySelector('[data-sdc-form]');
        const input = document.querySelector('[data-sdc-input]');
        const btnModal = document.querySelector('[data-sdc-open-modal]');

        if (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                const cislo = ((input && input.value) || '').trim();
                submitLoad(cislo);
            });
        }

        if (btnModal) {
            btnModal.addEventListener('click', function () {
                const id = btnModal.dataset.externiOdkazId;
                if (!id) return;
                const parsed = parseInt(id, 10);
                if (isNaN(parsed) || parsed <= 0) return;
                // Reuse chat modal komponenty (A-Q3=c).
                if (global.pmChatModal && typeof global.pmChatModal.open === 'function') {
                    global.pmChatModal.open(parsed);
                } else {
                    // Fallback: navigace na edit záznamu (ExterniOdkaz page nemá přímou URL pro chat).
                    global.location.href = '/Zaznamy/Edit?externiOdkazId=' + parsed + '#chat';
                }
            });
        }

        // Restore z sessionStorage při reload.
        let restoredCislo = null;
        try {
            const raw = sessionStorage.getItem(STORAGE_KEY);
            if (raw) {
                const cached = JSON.parse(raw);
                if (cached && cached.data && input) {
                    if (cached.cislo) {
                        input.value = cached.cislo;
                        restoredCislo = cached.cislo;
                    }
                    render(cached.data);
                }
            }
        } catch (_) {
            /* ignore corrupt cache */
        }

        // Pokud URL má ?cislo= a je validní + liší se od restored, auto-load.
        const params = new URLSearchParams(global.location.search);
        const urlCislo = params.get('cislo');
        if (urlCislo && CISLO_REGEX.test(urlCislo)) {
            if (input) input.value = urlCislo;
            if (urlCislo !== restoredCislo) {
                submitLoad(urlCislo);
            }
        }
    }

    global.pmSdcInspect = { init: init, load: load };
})(window);
