(function () {
    'use strict';

    /** Popisek druhého řádku podle toho, proč se záznam našel. */
    var MATCH_BADGE = {
        Nazev: '',
        Popis: 'popis',
        Vyjadreni: '',
        ExterniOdkaz: 'Ext. záz.'
    };

    var form = document.querySelector('[data-global-search]');
    if (!form) return;
    var input = form.querySelector('input[name="q"]');
    var dropdown = form.querySelector('[data-global-search-dropdown]');
    if (!input || !dropdown) return;

    var timer = null;
    var abortController = null;
    var activeIndex = -1;
    var currentItems = [];

    // ARIA housekeeping
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-expanded', 'false');
    input.setAttribute('aria-haspopup', 'listbox');
    input.setAttribute('aria-controls', 'global-search-listbox');

    dropdown.setAttribute('id', 'global-search-listbox');
    dropdown.setAttribute('role', 'listbox');

    // Erase (křížek) tlačítko — zobrazuje se jen když je v inputu text.
    // gov-form-search má slot="button-erase" ale bez built-in logiky viditelnosti.
    var eraseButton = form.querySelector('[data-global-search-erase]');

    function syncEraseVisibility() {
        if (!eraseButton) return;
        eraseButton.hidden = !(input.value && input.value.length > 0);
    }

    if (eraseButton) {
        eraseButton.addEventListener('click', function (ev) {
            ev.preventDefault();
            input.value = '';
            syncEraseVisibility();
            clearDropdown();
            input.focus();
            input.dispatchEvent(new Event('input', { bubbles: true }));
        });
    }

    input.addEventListener('input', syncEraseVisibility);
    syncEraseVisibility();

    function clearDropdown() {
        dropdown.replaceChildren();
        dropdown.hidden = true;
        dropdown.removeAttribute('aria-activedescendant');
        input.setAttribute('aria-expanded', 'false');
        activeIndex = -1;
        currentItems = [];
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

    function navigateTo(url) {
        if (url && url !== '#') {
            window.location.href = url;
        }
    }

    /** Druhý řádek: výřez okolí shody se zvýrazněnou shodou.
        Skládá se z DOM uzlů přes textContent — text pochází z databáze a nesmí se
        vkládat jako HTML. */
    function buildSnippetNode(item) {
        var wrap = document.createElement('span');
        wrap.className = 'app-search-item__snippet';

        var snippet = item.snippet;
        if (!snippet) {
            wrap.textContent = item.nazev;
            return wrap;
        }

        wrap.appendChild(document.createTextNode(snippet.before || ''));

        var hl = document.createElement('mark');
        hl.className = 'app-search-hl';
        hl.textContent = snippet.match || '';
        wrap.appendChild(hl);

        wrap.appendChild(document.createTextNode(snippet.after || ''));
        return wrap;
    }

    /** Text vpravo dole: číslo jednání u vyjádření, jinak popisek typu shody. */
    function buildBadgeText(item) {
        if (item.matchKind === 'Vyjadreni' && item.cisloJednani) {
            return String(item.cisloJednani);
        }
        return MATCH_BADGE[item.matchKind] || '';
    }

    function renderItem(item, index) {
        var li = document.createElement('li');
        li.className = 'app-search-item';
        li.setAttribute('role', 'option');
        li.setAttribute('id', 'gs-option-' + index);
        li.setAttribute('aria-selected', 'false');
        li.dataset.url = item.detailUrl || '';

        // První řádek: název (a číslo) záznamu vlevo, zkratka subsystému vpravo.
        var rowTitle = document.createElement('span');
        rowTitle.className = 'app-search-item__row';

        var title = document.createElement('span');
        title.className = 'app-search-item__title';
        title.textContent = item.cisloViditelne
            ? item.cisloViditelne + ' — ' + item.nazev
            : item.nazev;
        rowTitle.appendChild(title);

        var subsystem = document.createElement('span');
        subsystem.className = 'app-search-item__meta';
        subsystem.textContent = item.subsystemKod || '';
        rowTitle.appendChild(subsystem);

        // Druhý řádek: proč se záznam našel, vpravo číslo jednání / popisek.
        var rowSnippet = document.createElement('span');
        rowSnippet.className = 'app-search-item__row';
        rowSnippet.appendChild(buildSnippetNode(item));

        var badge = document.createElement('span');
        badge.className = 'app-search-item__badge';
        badge.textContent = buildBadgeText(item);
        rowSnippet.appendChild(badge);

        li.appendChild(rowTitle);
        li.appendChild(rowSnippet);

        li.addEventListener('click', function (ev) {
            ev.preventDefault();
            navigateTo(li.dataset.url);
            clearDropdown();
        });

        return li;
    }

    function renderResults(payload) {
        activeIndex = -1;

        // Zploští kategorie na jeden seznam položek — dnes je kategorie jediná,
        // ale klávesová navigace i tak pracuje nad plochým seznamem.
        currentItems = [];
        (payload.categories || []).forEach(function (category) {
            (category.items || []).forEach(function (item) {
                currentItems.push(item);
            });
        });

        if (!currentItems.length) {
            var empty = document.createElement('div');
            empty.className = 'global-search-empty';
            empty.textContent = 'Nenalezeno';
            dropdown.replaceChildren(empty);
            dropdown.hidden = false;
            input.setAttribute('aria-expanded', 'true');
            return;
        }

        var list = document.createElement('ul');
        list.className = 'app-search-list';
        currentItems.forEach(function (item, i) {
            list.appendChild(renderItem(item, i));
        });

        dropdown.replaceChildren(list);
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
            renderResults(data);
        } catch (e) {
            if (e.name !== 'AbortError') {
                clearDropdown();
            }
        }
    }

    function fireSuggest() {
        var q = input.value.trim();
        // práh 3 znaky — musí sedět se serverem (SearchQueryText.MinQueryLength)
        if (q.length < 3) { clearDropdown(); return; }
        suggest(q);
    }

    input.addEventListener('input', function () {
        var q = input.value.trim();
        if (timer) clearTimeout(timer);
        if (q.length < 3) { clearDropdown(); return; }
        // debounce 200 ms — bez něj by každý úhoz poslal dotaz a narazil na rate limit
        timer = setTimeout(fireSuggest, 200);
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

        if (ev.key === 'Enter' && activeIndex >= 0 && activeIndex < currentItems.length) {
            ev.preventDefault();
            navigateTo(currentItems[activeIndex].detailUrl);
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
