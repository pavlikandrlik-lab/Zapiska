/**
 * Spec 2026-10-14 (R5): volba „Ručně / Z vyjádření“ u kroků 2/5/8/9 byla mrtvý kód — automat
 * tyto kroky nevyplňuje (HarmonogramKrokDatumMapping je nemapuje), takže se nikdy nevykreslila.
 *
 * Soubor zůstává PRÁZDNÝ jen proto, že bootstrap.js z předchozí verze v mezipaměti prohlížeče
 * ho importuje a chybějící modul by shodil celý ESM graf (memory project_bundle_sync).
 * Smazat ve verzi po příští.
 */
