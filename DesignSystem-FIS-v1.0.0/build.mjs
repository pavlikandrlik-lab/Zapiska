/* ==========================================================================
   build.mjs — sestavení lokální kopie Design systému gov.cz do assets/gov/

   Spouští se PO `npm install`. Zkopíruje CSS / písmo / ikony / webové
   komponenty z balíčků @gov-design-system-ce a sbalí skript šablon.
   Výsledek (assets/gov/) je pak samonosný – aplikace běží bez node_modules.

   Použití:  node build.mjs
   ========================================================================== */
import { rm, mkdir, cp, readdir, readFile, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { build as esbuild } from "esbuild";

const root = dirname(fileURLToPath(import.meta.url));
const NM = join(root, "node_modules", "@gov-design-system-ce");
const OUT = join(root, "assets", "gov");

const need = (p) => {
	if (!existsSync(p)) {
		console.error("Chybí:", p, "\nSpusťte nejdřív `npm install`.");
		process.exit(1);
	}
	return p;
};

async function main() {
	need(NM);

	// čistý start
	await rm(OUT, { recursive: true, force: true });
	for (const d of ["styles", "fonts", "icons", "components", "templates"]) {
		await mkdir(join(OUT, d), { recursive: true });
	}

	/* ---- 1) CSS ---------------------------------------------------------- */
	// základ z balíčku styles (obsahuje kompletní tokens.css ~34 kB)
	for (const f of [
		"tokens.css", "styles.css", "layout.css", "components.css",
		"templates.css", "animations.css", "content.css",
	]) {
		await cp(join(NM, "styles/lib", f), join(OUT, "styles", f));
	}
	// doplňky z balíčku templates
	await cp(join(NM, "templates/lib/styles/tokens.css"), join(OUT, "styles/templates-tokens.css"));
	await cp(join(NM, "templates/lib/styles/skip-links.css"), join(OUT, "styles/skip-links.css"));
	await cp(join(NM, "templates/lib/styles/index.css"), join(OUT, "styles/index.css"));

	/* ---- 2) Písmo Roboto (roboto.css + .woff2) ---------------------- */
	{
		const fontSrc = join(NM, "fonts/lib");
		for (const f of await readdir(fontSrc)) {
			if (f === "roboto.css" || f.endsWith(".woff2")) {
				await cp(join(fontSrc, f), join(OUT, "fonts", f));
			}
		}
	}

	/* ---- 3) Ikony (zachovat podadresáře!) ---------------------------- */
	for (const set of ["components", "complex", "colored"]) {
		await cp(join(NM, "icons/lib", set), join(OUT, "icons", set), { recursive: true });
	}
	// některý markup DS používá `type="templates"` – ve v4.7 už tato sada
	// neexistuje samostatně, mapujeme ji na `components` (jinak 404 u ikon)
	await cp(join(NM, "icons/lib/components"), join(OUT, "icons", "templates"), { recursive: true });

	/* ---- 4) Webové komponenty (drop-in loader dist/core) ------------- */
	{
		const compSrc = join(NM, "components/dist/core");
		for (const f of await readdir(compSrc)) {
			if (f.endsWith(".map")) continue; // .map soubory nejsou potřeba
			await cp(join(compSrc, f), join(OUT, "components", f));
		}
	}

	/* ---- 5) Skript šablon DS (rozbalování navigace) – sbalit do IIFE - */
	await esbuild({
		entryPoints: [join(NM, "templates/dist/scripts/scripts.js")],
		bundle: true,
		format: "iife",
		outfile: join(OUT, "templates", "scripts.js"),
		logLevel: "warning",
	});

	// kontrola
	const styles = await readdir(join(OUT, "styles"));
	const icons = await readdir(join(OUT, "icons", "components"));
	console.log("Hotovo → assets/gov/");
	console.log("  styles:    ", styles.join(", "));
	console.log("  icons:     ", icons.length, "komponentních ikon");
	console.log("  components:", "core.esm.js + chunky");
	console.log("  templates: ", "scripts.js (sbaleno)");
}

main().catch((e) => {
	console.error(e);
	process.exit(1);
});
