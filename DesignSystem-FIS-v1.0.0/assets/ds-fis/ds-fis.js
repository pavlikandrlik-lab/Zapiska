/* ==========================================================================
   DS FIS v1.0.0 – chování nadstavby (bez frameworku, klasický skript).
   Načítat s atributem defer, ZA loaderem komponent DS a za scripts.js DS.

   1) Našeptávač vyhledávání – naplnění dat + tlačítko „vymazat"
   2) Mobil/tablet – lupa (rozbalení hledání) a hamburger (rozbalení menu)
   3) Potlačení přeteklé položky „Další" v hlavní navigaci
   4) Sbalení navigace do panelu na úzkém displeji
   5) Zavírání rozbalených menu klikem mimo (levé tlačítko, mimo panel)

   Kompletní popis: CHANGES.md
   ========================================================================== */
(function () {
	"use strict";

	/* ---------- 1) Našeptávač ---------- */

	// Ukázková data pro našeptávač (v reálné aplikaci by přišla z API).
	var SEARCH_ITEMS = [
		"Z-2026-0412 — Ukázkový záznam číslo jedna",
		"Z-2026-0411 — Ukázkový záznam číslo dvě",
		"Z-2026-0408 — Ukázkový záznam číslo tři",
		"Z-2026-0402 — Ukázkový záznam číslo čtyři",
		"Z-2026-0399 — Ukázkový záznam číslo pět",
		"Z-2026-0395 — Ukázkový záznam číslo šest",
		"Z-2026-0390 — Ukázkový záznam číslo sedm",
		"Z-2026-0385 — Ukázkový záznam číslo osm",
		"Z-2026-0380 — Ukázkový záznam číslo devět",
		"Z-2026-0372 — Ukázkový záznam číslo deset"
	];

	function initAutocomplete() {
		var form = document.getElementById("search");
		var ac = form && form.querySelector("gov-form-autocomplete");
		if (!ac || !ac.classList.contains("hydrated")) {
			setTimeout(initAutocomplete, 150);
			return;
		}
		if (ac.__inited) return;
		ac.__inited = true;

		// data přes property `options` (ne atribut)
		ac.options = SEARCH_ITEMS.map(function (name) { return { name: name }; });
		ac.setAttribute("max-options", "6");

		var input = ac.querySelector("input, textarea");
		var clearBtn = form.querySelector(".js-ac-clear");

		function currentValue() { return input ? input.value : ""; }
		function syncClear() {
			if (clearBtn) clearBtn.hidden = currentValue().length === 0;
		}
		syncClear();

		form.addEventListener("input", syncClear);
		form.addEventListener("gov-input", syncClear);

		if (clearBtn) {
			var doClear = function (e) {
				if (e) { e.preventDefault(); e.stopPropagation(); }
				if (typeof ac.clearValue === "function") ac.clearValue();
				if (input) { input.value = ""; input.dispatchEvent(new Event("input", { bubbles: true })); input.focus(); }
				syncClear();
			};
			clearBtn.addEventListener("click", doClear);
			clearBtn.addEventListener("gov-click", doClear);
		}
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", initAutocomplete);
	} else {
		initAutocomplete();
	}

	/* ---------- Mobil: lupa rozbalí / sbalí vyhledávací pole ---------- */
	function toggleMobileSearch(e) {
		var toggle = e.target.closest && e.target.closest(".js-ac-mobile-toggle");
		if (!toggle) return;
		if (e.cancelable) e.preventDefault();
		e.stopPropagation();
		var open = document.body.classList.toggle("app-search-open");
		toggle.setAttribute("aria-expanded", open ? "true" : "false");
		if (open) {
			// zavřít případně otevřenou navigaci
			var nav = document.querySelector(".js-gov-header__navigation");
			if (nav && !nav.hasAttribute("hidden")) {
				nav.setAttribute("hidden", "");
				nav.setAttribute("aria-hidden", "true");
				var ham = document.querySelector(".js-gov-header__navigation-trigger");
				if (ham) {
					ham.setAttribute("aria-expanded", "false");
					var hi = ham.querySelector("gov-icon");
					if (hi) hi.setAttribute("name", "list");
				}
			}
			var input = document.querySelector("#search input, #search textarea");
			if (input) setTimeout(function () { input.focus(); }, 50);
		}
	}
	document.addEventListener("click", toggleMobileSearch, true);
	document.addEventListener("gov-click", toggleMobileSearch, true);

	/* ---------- Mobil: hamburger rozbalí / sbalí hlavní navigaci ----------
	   Vlastní obsluha (capture + stopPropagation), ať se nepere se skriptem DS. */
	function toggleMobileNav(e) {
		var trg = e.target.closest && e.target.closest(".js-gov-header__navigation-trigger");
		if (!trg) return;
		e.stopPropagation();
		if (e.cancelable) e.preventDefault();
		var nav = document.querySelector(".js-gov-header__navigation");
		if (!nav) return;
		var willOpen = nav.hasAttribute("hidden");
		if (willOpen) {
			nav.removeAttribute("hidden");
			nav.removeAttribute("aria-hidden");
			document.body.classList.remove("app-search-open");
		} else {
			nav.setAttribute("hidden", "");
			nav.setAttribute("aria-hidden", "true");
		}
		trg.setAttribute("aria-expanded", willOpen ? "true" : "false");
		var ic = trg.querySelector("gov-icon");
		if (ic) ic.setAttribute("name", willOpen ? "x-lg" : "list");
	}
	document.addEventListener("click", toggleMobileNav, true);
	document.addEventListener("gov-click", toggleMobileNav, true);

	/* ---------- Potlačit přeteklou položku „Další" ----------
	   Skript DS na úzkém displeji přesune položky menu do rozbalovací
	   položky „Další". My chceme všechny položky rovnou v menu, tak je
	   hned vracíme zpět a kontejner mažeme (i při každém dalším vytvoření). */
	function undeferNav() {
		var nav = document.querySelector(".gov-navigation");
		if (!nav) return;
		var mainUl = nav.querySelector(":scope > ul");
		var cont = nav.querySelector(".js-deferred-items-container");
		if (!cont || !mainUl) return;
		var inner = cont.querySelector("ul");
		if (inner) {
			Array.prototype.slice.call(inner.children).forEach(function (li) {
				mainUl.appendChild(li);
			});
		}
		cont.remove();
	}
	var navRoot = document.querySelector(".gov-navigation > ul");
	if (navRoot && "MutationObserver" in window) {
		new MutationObserver(undeferNav).observe(navRoot, { childList: true });
	}
	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", function () { setTimeout(undeferNav, 50); });
	} else {
		setTimeout(undeferNav, 50);
	}
	window.addEventListener("resize", function () { setTimeout(undeferNav, 60); });

	// Na mobilu má být navigace ve výchozím stavu sbalená.
	function collapseNavOnMobile() {
		var nav = document.querySelector(".js-gov-header__navigation");
		if (!nav) return;
		if (window.matchMedia("(max-width: 74.99em)").matches) {
			if (!nav.hasAttribute("hidden")) {
				nav.setAttribute("hidden", "");
				nav.setAttribute("aria-hidden", "true");
			}
		} else {
			nav.removeAttribute("hidden");
			nav.removeAttribute("aria-hidden");
		}
	}
	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", collapseNavOnMobile);
	} else {
		collapseNavOnMobile();
	}
	// skript DS může navigaci po ~500 ms znovu odkrýt – zopakujeme
	setTimeout(collapseNavOnMobile, 300);
	setTimeout(collapseNavOnMobile, 700);
	window.addEventListener("resize", collapseNavOnMobile);

	/* ---------- 2) Zavírání rozbalených menu hlavní navigace ---------- */
	/* Zavře se kliknutím LEVÝM tlačítkem kdekoli MIMO právě otevřený panel.
	   – pravé tlačítko (kontextové menu) nic nezavírá (kvůli „otevřít v nové záložce")
	   – klik na odkaz UVNITŘ panelu nic nezavírá (stránka se stejně načte) */

	function openPanels() {
		return Array.prototype.slice.call(
			document.querySelectorAll(
				".gov-navigation .gov-subnavigation:not([hidden]), .gov-navigation .gov-mega-menu:not([hidden])"
			)
		);
	}

	function closeNavMenu(host) {
		var trg = host.querySelector ? host.querySelector("button") || host : host;
		var id = host.getAttribute("aria-controls") || (trg.getAttribute && trg.getAttribute("aria-controls"));
		var menu = id && document.getElementById(id);
		host.setAttribute("aria-expanded", "false");
		if (trg.setAttribute) trg.setAttribute("aria-expanded", "false");
		if (menu) {
			menu.setAttribute("hidden", "");
			menu.setAttribute("aria-hidden", "true");
		}
		var ic = host.querySelector && host.querySelector("gov-icon");
		if (ic) ic.setAttribute("name", "chevron-down");
	}

	function closeAllNavMenus(exceptHost) {
		document
			.querySelectorAll('.gov-navigation > ul > li > gov-button[aria-expanded="true"], .gov-navigation > ul > li > .gov-button[aria-expanded="true"]')
			.forEach(function (host) {
				if (exceptHost && host === exceptHost) return;
				closeNavMenu(host);
			});
		// pojistka podle otevřených panelů
		openPanels().forEach(function (p) {
			var host = p.closest("li") && p.closest("li").querySelector(":scope > gov-button, :scope > .gov-button");
			if (host && !(exceptHost && host === exceptHost)) closeNavMenu(host);
		});
	}

	document.addEventListener(
		"pointerdown",
		function (e) {
			if (e.button !== 0) return; // jen levé tlačítko
			var panels = openPanels();
			if (!panels.length) return;
			var t = e.target;

			// klik uvnitř otevřeného panelu → nechat být (odkaz může navigovat / otevřít v nové kartě)
			if (panels.some(function (p) { return p.contains(t); })) return;

			// klik na spouštěč položky → zavřít ostatní, tuhle nechá přepnout DS
			var host = t.closest && t.closest(".gov-navigation > ul > li > gov-button, .gov-navigation > ul > li > .gov-button");
			closeAllNavMenus(host || null);
		},
		true
	);

	document.addEventListener("keydown", function (e) {
		if (e.key === "Escape") closeAllNavMenus();
	});
})();
