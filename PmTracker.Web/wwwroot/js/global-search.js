(function () {
    'use strict';

    var form = document.querySelector('[data-global-search]');
    if (!form) return;
    var input = form.querySelector('input[name="q"]');
    var dropdown = form.querySelector('[data-global-search-dropdown]');
    if (!input || !dropdown) return;

    var timer = null;
    var abortController = null;
    var activeIndex = -1;
    var currentHits = [];

    // ARIA housekeeping
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-expanded', 'false');
    input.setAttribute('aria-haspopup', 'listbox');
    input.setAttribute('aria-controls', 'global-search-listbox');

    dropdown.setAttribute('id', 'global-search-listbox');
    dropdown.setAttribute('role', 'listbox');

    function clearDropdown() {
        dropdown.innerHTML = '';
        dropdown.hidden = true;
        dropdown.removeAttribute('aria-activedescendant');
        input.setAttribute('aria-expanded', 'false');
        activeIndex = -1;
        currentHits = [];
    }

    function getOptions() {
        return dropdown.querySelectorAll('[role="option"]');
    }

    function setActiveOption(index) {
        var options = getOptions();
        options.forEach(function (el, i) {
            if (i === index) {
                el.setAttribute('aria-selected', 'true');
                el.classList.add('is-active');
                input.setAttribute('aria-activedescendant', el.id);
                el.scrollIntoView({ block: 'nearest' });
            } else {
                el.removeAttribute('aria-selected');
                el.classList.remove('is-active');
            }
        });
        activeIndex = index;
    }

    function renderHits(hits) {
        dropdown.innerHTML = '';
        activeIndex = -1;
        currentHits = hits || [];

        if (!currentHits.length) {
            var empty = document.createElement('div');
            empty.className = 'global-search-empty';
            empty.textContent = 'Nenalezeno';
            dropdown.appendChild(empty);
            dropdown.hidden = false;
            input.setAttribute('aria-expanded', 'true');
            return;
        }

        var list = document.createElement('ul');
        list.className = 'global-search-list';

        currentHits.forEach(function (h, i) {
            var li = document.createElement('li');
            li.setAttribute('role', 'option');
            li.setAttribute('id', 'gs-option-' + i);
            li.setAttribute('aria-selected', 'false');
            li.className = 'global-search-item';

            var a = document.createElement('a');
            a.href = h.url || '#';
            a.className = 'global-search-item__link';
            a.tabIndex = -1;

            var header = document.createElement('div');
            header.className = 'global-search-item__header';

            var title = document.createElement('span');
            title.className = 'global-search-item__title';
            title.textContent = h.title || '(bez názvu)';

            var badge = document.createElement('span');
            badge.className = 'global-search-item__badge global-search-item__badge--' + (h.type || 'zaznam');
            badge.textContent = h.type === 'vyjadreni' ? 'Vyjádření' : 'Záznam';

            header.appendChild(title);
            header.appendChild(badge);
            a.appendChild(header);

            if (h.snippet) {
                var snippet = document.createElement('div');
                snippet.className = 'global-search-item__snippet';
                snippet.textContent = h.snippet;
                a.appendChild(snippet);
            }

            if (h.projektNazev) {
                var tag = document.createElement('div');
                tag.className = 'global-search-item__projekt';
                tag.textContent = h.projektNazev;
                a.appendChild(tag);
            }

            li.appendChild(a);
            list.appendChild(li);

            li.addEventListener('click', function (ev) {
                ev.preventDefault();
                var url = a.href;
                if (url && url !== window.location.origin + '/#' && url !== '#') {
                    window.location.href = url;
                }
                clearDropdown();
            });
        });

        dropdown.appendChild(list);
        dropdown.hidden = false;
        input.setAttribute('aria-expanded', 'true');
    }

    async function suggest(query) {
        if (abortController) {
            abortController.abort();
        }
        abortController = new AbortController();
        try {
            var res = await fetch('/Search/Suggest?q=' + encodeURIComponent(query), {
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                signal: abortController.signal
            });
            if (!res.ok) { clearDropdown(); return; }
            var data = await res.json();
            renderHits(data.hits || []);
        } catch (e) {
            if (e.name !== 'AbortError') {
                clearDropdown();
            }
        }
    }

    input.addEventListener('input', function () {
        var q = input.value.trim();
        if (timer) clearTimeout(timer);
        if (q.length < 2) { clearDropdown(); return; }
        timer = setTimeout(function () { suggest(q); }, 300);
    });

    input.addEventListener('keydown', function (ev) {
        var options = getOptions();
        var count = options.length;

        if (ev.key === 'Escape') {
            clearDropdown();
            ev.preventDefault();
            return;
        }

        if (dropdown.hidden || count === 0) return;

        if (ev.key === 'ArrowDown') {
            ev.preventDefault();
            var next = activeIndex < count - 1 ? activeIndex + 1 : 0;
            setActiveOption(next);
            return;
        }

        if (ev.key === 'ArrowUp') {
            ev.preventDefault();
            var prev = activeIndex > 0 ? activeIndex - 1 : count - 1;
            setActiveOption(prev);
            return;
        }

        if (ev.key === 'Enter' && activeIndex >= 0 && activeIndex < currentHits.length) {
            ev.preventDefault();
            var hit = currentHits[activeIndex];
            if (hit && hit.url && hit.url !== '#') {
                window.location.href = hit.url;
            }
            clearDropdown();
        }
    });

    document.addEventListener('click', function (ev) {
        if (!form.contains(ev.target)) {
            clearDropdown();
        }
    });

    form.addEventListener('submit', function () {
        clearDropdown();
    });
})();
