(function () {
    'use strict';

    const form = document.querySelector('[data-global-search]');
    if (!form) return;
    const input = form.querySelector('input[name="q"]');
    const dropdown = form.querySelector('[data-global-search-dropdown]');
    if (!input || !dropdown) return;

    let timer = null;
    let abortController = null;

    function clearDropdown() {
        dropdown.innerHTML = '';
        dropdown.hidden = true;
    }

    function renderHits(hits) {
        if (!hits || hits.length === 0) {
            clearDropdown();
            return;
        }
        const list = document.createElement('ul');
        list.className = 'global-search-list';
        for (const h of hits) {
            const li = document.createElement('li');
            const a = document.createElement('a');
            a.href = h.url || '#';
            a.textContent = h.title || '(bez názvu)';
            const meta = document.createElement('span');
            meta.className = 'global-search-meta';
            meta.textContent = ' · ' + (h.type || '');
            a.appendChild(meta);
            li.appendChild(a);
            list.appendChild(li);
        }
        dropdown.innerHTML = '';
        dropdown.appendChild(list);
        dropdown.hidden = false;
    }

    async function suggest(query) {
        if (abortController) abortController.abort();
        abortController = new AbortController();
        try {
            const res = await fetch('/Search/Suggest?q=' + encodeURIComponent(query), {
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                signal: abortController.signal
            });
            if (!res.ok) { clearDropdown(); return; }
            const data = await res.json();
            renderHits(data.hits || []);
        } catch (e) {
            if (e.name !== 'AbortError') clearDropdown();
        }
    }

    input.addEventListener('input', function () {
        const q = input.value.trim();
        if (timer) clearTimeout(timer);
        if (q.length < 2) { clearDropdown(); return; }
        timer = setTimeout(() => suggest(q), 250);
    });

    input.addEventListener('blur', function () {
        setTimeout(clearDropdown, 150);
    });

    document.addEventListener('click', function (ev) {
        if (!form.contains(ev.target)) clearDropdown();
    });
})();
