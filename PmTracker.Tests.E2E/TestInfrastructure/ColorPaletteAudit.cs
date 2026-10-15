namespace PmTracker.Tests.E2E.TestInfrastructure;

/// <summary>
/// Audit barev na vykreslené stránce (2026-10-15): každá barva aplikačního prvku musí být
/// hodnota gov tokenu aktivního motivu nebo aplikačního --app-* tokenu a text musí splnit
/// WCAG kontrast. Přeskakuje vnitřek gov-* komponent (stylují se samy), zmraženou hlavičku
/// a pruhy harmonogramu s barvami z Excelu (inline --seg-color).
/// </summary>
public static class ColorPaletteAudit
{
    public const string Script = """
() => {
  const probe = document.createElement("div");
  document.body.appendChild(probe);
  const norm = (v) => { probe.style.color = ""; probe.style.color = v; return getComputedStyle(probe).color; };
  const parse = (c) => {
    const m = /rgba?\(([^)]+)\)/.exec(c || "");
    if (!m) return null;
    const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number);
    return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 };
  };
  const rootStyle = getComputedStyle(document.documentElement);
  const palette = [];
  const addToken = (name) => {
    const raw = rootStyle.getPropertyValue(name).trim();
    if (!raw || /^-?[\d.]+(px|rem|em|%|ms|s)?$/.test(raw)) return;
    const c = parse(norm(raw));
    if (c) palette.push(c);
  };
  for (const sheet of document.styleSheets) {
    let rules; try { rules = sheet.cssRules; } catch { continue; }
    const isGov = (sheet.href || "").includes("/assets/gov/styles/");
    const isApp = (sheet.href || "").includes("/css/tokens.css");
    if (!isGov && !isApp) continue;
    const walk = (list) => {
      for (const rule of list) {
        if (rule.cssRules) walk(rule.cssRules);
        if (!rule.style) continue;
        for (let i = 0; i < rule.style.length; i++) {
          const n = rule.style[i];
          if (n.startsWith("--") && (isGov || n.startsWith("--app-"))) addToken(n);
        }
      }
    };
    walk(rules);
  }
  const inPalette = (c) => {
    const p = parse(c);
    if (!p || p.a === 0) return true;
    return palette.some((q) => Math.abs(q.r - p.r) <= 3 && Math.abs(q.g - p.g) <= 3 && Math.abs(q.b - p.b) <= 3 && Math.abs(q.a - p.a) <= 0.02);
  };
  const lum = ({ r, g, b }) => {
    const f = (x) => { x /= 255; return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4); };
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
  };
  const blend = (t, b) => ({ r: t.r * t.a + b.r * (1 - t.a), g: t.g * t.a + b.g * (1 - t.a), b: t.b * t.a + b.b * (1 - t.a), a: 1 });
  const effectiveBg = (el) => {
    const layers = [];
    for (let e = el; e; e = e.parentElement) {
      const p = parse(getComputedStyle(e).backgroundColor);
      if (p && p.a > 0) { layers.push(p); if (p.a >= 0.99) break; }
    }
    let c = parse(getComputedStyle(document.body).backgroundColor) || { r: 255, g: 255, b: 255, a: 1 };
    for (let i = layers.length - 1; i >= 0; i--) c = blend(layers[i], c);
    return c;
  };
  const skipped = (el) => {
    for (let e = el; e; e = e.parentElement) {
      if (e.tagName.startsWith("GOV-") || e.matches("header.gov-header, .gov-header, [style*='--seg-color']")) return true;
    }
    return false;
  };
  const visible = (el) => {
    const r = el.getBoundingClientRect();
    if (r.width < 2 || r.height < 2) return false;
    const cs = getComputedStyle(el);
    return cs.visibility !== "hidden" && cs.display !== "none" && Number(cs.opacity) > 0.05;
  };
  const describe = (el) => el.tagName.toLowerCase() + [...el.classList].slice(0, 2).map((c) => "." + c).join("");
  const off = new Set(), low = new Set();
  for (const el of document.body.querySelectorAll("*")) {
    if (el === probe || ["SCRIPT", "STYLE", "BR", "svg", "path"].includes(el.tagName) || skipped(el) || !visible(el)) continue;
    const cs = getComputedStyle(el);
    const ownText = [...el.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent.trim()).join(" ").trim();
    if (!inPalette(cs.backgroundColor)) off.add(`${describe(el)} background ${cs.backgroundColor}`);
    if (ownText && !inPalette(cs.color)) off.add(`${describe(el)} color ${cs.color} „${ownText.slice(0, 30)}“`);
    for (const side of ["Top", "Right", "Bottom", "Left"]) {
      if (parseFloat(cs[`border${side}Width`]) > 0 && cs[`border${side}Style`] !== "none" && !inPalette(cs[`border${side}Color`])) {
        off.add(`${describe(el)} border ${cs[`border${side}Color`]}`);
        break;
      }
    }
    if (ownText && !el.matches(":disabled, [aria-disabled='true'], .is-disabled")) {
      const fg = parse(cs.color), bg = effectiveBg(el);
      if (!fg) continue;
      const fgc = fg.a < 1 ? blend(fg, bg) : fg;
      const l1 = lum(fgc), l2 = lum(bg);
      const ratio = (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
      const size = parseFloat(cs.fontSize), bold = Number(cs.fontWeight) >= 700;
      const need = size >= 24 || (bold && size >= 18.66) ? 3 : 4.5;
      if (ratio < need) low.add(`${describe(el)} ${ratio.toFixed(2)}:1 (${cs.color} na rgb(${bg.r | 0}, ${bg.g | 0}, ${bg.b | 0})) „${ownText.slice(0, 30)}“`);
    }
  }
  probe.remove();
  return { paletteSize: palette.length, off: [...off], low: [...low] };
}
""";

    /// <summary>Třída s vlastnostmi (ne positional record) — tak ji Playwright .NET spolehlivě naplní z JSON.</summary>
    public sealed class Result
    {
        public int PaletteSize { get; set; }
        public string[] Off { get; set; } = [];
        public string[] Low { get; set; } = [];
    }
}
