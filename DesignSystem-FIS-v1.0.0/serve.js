// Minimální statický server (jen vestavěné moduly Node.js, žádná závislost).
// Spuštění:  node serve.js       →  http://localhost:8000
const http = require("http");
const fs = require("fs");
const path = require("path");

const ROOT = __dirname;
const PORT = process.env.PORT || 8000;

const TYPES = {
	".html": "text/html; charset=utf-8",
	".css": "text/css; charset=utf-8",
	".js": "text/javascript; charset=utf-8",
	".mjs": "text/javascript; charset=utf-8",
	".json": "application/json; charset=utf-8",
	".svg": "image/svg+xml",
	".woff2": "font/woff2",
	".woff": "font/woff",
	".png": "image/png",
	".jpg": "image/jpeg",
	".webp": "image/webp",
	".ico": "image/x-icon",
	".map": "application/json",
};

http
	.createServer((req, res) => {
		let urlPath = decodeURIComponent(req.url.split("?")[0]);
		if (urlPath === "/") urlPath = "/index.html";

		const filePath = path.join(ROOT, path.normalize(urlPath));
		if (!filePath.startsWith(ROOT)) {
			res.writeHead(403);
			return res.end("Forbidden");
		}

		fs.readFile(filePath, (err, data) => {
			if (err) {
				res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
				return res.end("404 – " + urlPath);
			}
			const type = TYPES[path.extname(filePath).toLowerCase()] || "application/octet-stream";
			res.writeHead(200, {
				"Content-Type": type,
				// vždy čerstvé soubory – ať stačí refresh v prohlížeči
				"Cache-Control": "no-store, must-revalidate",
			});
			res.end(data);
		});
	})
	.listen(PORT, () => {
		console.log("Reporty — Finanční informační systém");
		console.log("Server běží na  http://localhost:" + PORT + "/");
		console.log("Ukončení: Ctrl+C");
	});
