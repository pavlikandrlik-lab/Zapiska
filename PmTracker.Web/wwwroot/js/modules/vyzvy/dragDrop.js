// dragDrop.js — tažení řádku PNF na dlaždici v railu. Nativní HTML5 drag & drop, bez knihovny.
//
// Drop přijímají jen dlaždice s data-vyzvy-drop-target != "none", tedy buffer a rozpracované
// výzvy; zamčená výzva drop nepřijme (spec §8.3). Posluchače jsou na dokumentu, ne na panelu —
// panel se po každé akci nahrazuje novým elementem a per-panel binding by se musel obnovovat.
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
const DATA_TYPE = "text/x-pmtracker-pnf";

function dropTarget(event) {
    const target = event.target instanceof Element ? event.target : null;
    const tile = target?.closest("[data-vyzvy-drop-target]");
    if (!(tile instanceof HTMLElement) || tile.dataset.vyzvyDropTarget === "none") {
        return null;
    }
    return tile;
}

// Chip u kurzoru (2026-09-08): při tažení ukazuje, kam se PNF pustí. Text si bere
// z dlaždice (data-vyzvy-drop-label), aby česká hláška zůstala v šabloně.
//
// Nativní průsvitný duch dlaždice se potlačí průhledným obrázkem — u kurzoru pak není
// duch i chip naráz. Kdyby setDragImage někde neprošlo, zůstane duch a chip vedle sebe;
// tažení tím netrpí.
const TRANSPARENT_PIXEL = new Image();
TRANSPARENT_PIXEL.src = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

let chipElement = null;

function chip() {
    if (chipElement) {
        return chipElement;
    }
    chipElement = document.createElement("div");
    chipElement.className = "vyzvy-drag-chip";
    chipElement.setAttribute("aria-hidden", "true");
    chipElement.hidden = true;
    document.body.appendChild(chipElement);
    return chipElement;
}

function showChip(text, x, y) {
    const el = chip();
    el.textContent = text;
    // Odsazení od kurzoru, ať chip nepřekrývá místo, na které uživatel míří.
    el.style.left = (x + 16) + "px";
    el.style.top = (y + 16) + "px";
    el.hidden = false;
}

function hideChip() {
    if (chipElement) {
        chipElement.hidden = true;
    }
}

function clearDragOver(scope) {
    (scope || document).querySelectorAll(".vyzvy-tile.is-drag-over")
        .forEach((tile) => tile.classList.remove("is-drag-over"));
}

document.addEventListener("dragstart", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    const row = target?.closest('.vyzvy-pnf[draggable="true"]');
    if (!(row instanceof HTMLElement) || !event.dataTransfer) {
        return;
    }
    event.dataTransfer.setData(DATA_TYPE, row.dataset.externiOdkazId || "");
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setDragImage(TRANSPARENT_PIXEL, 0, 0);
    row.classList.add("is-dragging");
});

document.addEventListener("dragend", (event) => {
    const target = event.target instanceof Element ? event.target : null;
    target?.closest(".vyzvy-pnf")?.classList.remove("is-dragging");
    clearDragOver();
    hideChip();
});

document.addEventListener("dragover", (event) => {
    // types čteme i v dragover (data samotná jsou v protected mode nedostupná) — bez toho
    // by se dlaždice zvýrazňovala i při tažení textu nebo souboru odjinud.
    if (!event.dataTransfer || !Array.from(event.dataTransfer.types).includes(DATA_TYPE)) {
        return;
    }
    const tile = dropTarget(event);
    if (!tile) {
        // Mimo platný cíl chip mizí — zamčená výzva tak rovnou vypadá jako „sem ne".
        hideChip();
        return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
    tile.classList.add("is-drag-over");
    showChip(
        "Přesunout do " + (tile.dataset.vyzvyDropLabel || ""),
        event.clientX,
        event.clientY);
});

document.addEventListener("dragleave", (event) => {
    dropTarget(event)?.classList.remove("is-drag-over");
});

document.addEventListener("drop", (event) => {
    const tile = dropTarget(event);
    if (!tile || !event.dataTransfer) {
        return;
    }
    const externiOdkazId = event.dataTransfer.getData(DATA_TYPE);
    if (!externiOdkazId) {
        return;
    }
    event.preventDefault();
    clearDragOver();
    hideChip();

    const panelElement = tile.closest("[data-vyzvy-panel]");
    window.pmVyzvy?.presunPnf?.(panelElement, externiOdkazId, tile.dataset.vyzvaId || null);
});
