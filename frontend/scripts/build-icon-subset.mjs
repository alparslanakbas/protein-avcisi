/**
 * Kullanılan Phosphor ikonlarını içeren daraltılmış bir kopya üretir.
 *
 * Neden: paket 1530 ikon taşıyor, biz onlarcasını kullanıyoruz. Tam paket
 * iki font dosyasında ~279 KB, iki CSS dosyasında ~164 KB yer kaplıyordu --
 * derlenen stil paketinin dörtte üçü tek başına bu kurallardı.
 *
 * Kaynak her zaman node_modules'taki tam paket; bu betik src/vendor/phosphor
 * altındaki kopyaları sıfırdan üretir. Yeni bir ikon eklendiğinde betiği
 * yeniden çalıştırmak yeterli, elle bir şey düzenlenmiyor.
 *
 *   npm run build:icons
 */
import { readFile, writeFile, readdir, stat } from 'node:fs/promises';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import subsetFont from 'subset-font';

const ROOT = dirname(dirname(fileURLToPath(import.meta.url)));
const SRC = join(ROOT, 'src');
const UPSTREAM = join(ROOT, 'node_modules', '@phosphor-icons', 'web', 'src');
const OUT = join(SRC, 'vendor', 'phosphor');

/** ph-fill gibi stil sınıfları bir ikon adı değil. */
const STYLE_CLASSES = new Set(['ph-fill', 'ph-bold', 'ph-duotone', 'ph-thin', 'ph-light', 'ph-regular']);

async function walk(dir) {
  const out = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    if (entry.name === 'vendor') continue;
    const full = join(dir, entry.name);
    if (entry.isDirectory()) out.push(...(await walk(full)));
    else if (/\.(html|ts)$/.test(entry.name)) out.push(full);
  }
  return out;
}

async function scanSources() {
  const used = new Set();
  const fillUsed = new Set();
  for (const file of await walk(SRC)) {
    const text = await readFile(file, 'utf8');
    for (const m of text.matchAll(/\bph-[a-z0-9]+(?:-[a-z0-9]+)*/g)) used.add(m[0]);
    for (const m of text.matchAll(/ph-fill[ '"]+(ph-[a-z0-9-]+)/g)) fillUsed.add(m[1]);
  }
  for (const s of STYLE_CLASSES) {
    used.delete(s);
    fillUsed.delete(s);
  }
  return { icons: [...used].sort(), fillIcons: [...fillUsed].sort() };
}

/** İkon adı -> kod noktası eşlemesini upstream CSS'ten okur. */
function parseCodepoints(css) {
  const map = new Map();
  const re = /\.(ph-[a-z0-9-]+):before\s*\{\s*content:\s*"\\([0-9a-fA-F]+)"/g;
  for (const m of css.matchAll(re)) {
    map.set(m[1], parseInt(m[2], 16));
  }
  return map;
}

async function build({ kind, folder, fontFile, familyName, selectorPrefix, wanted }) {
  const upstreamCss = await readFile(join(UPSTREAM, folder, 'style.css'), 'utf8');
  const codepoints = parseCodepoints(upstreamCss);

  const missing = wanted.filter((name) => !codepoints.has(name));
  if (missing.length) throw new Error(`Bu ikonlar pakette yok: ${missing.join(', ')}`);
  if (!wanted.length) throw new Error(`${kind} için hiç ikon bulunamadı`);

  const glyphText = wanted.map((name) => String.fromCodePoint(codepoints.get(name))).join('');
  const original = await readFile(join(UPSTREAM, folder, fontFile));
  const subset = await subsetFont(original, glyphText, { targetFormat: 'woff2' });
  await writeFile(join(OUT, fontFile), subset);

  // Taban kural (.ph { font-family: ... }) upstream'den aynen alınıyor;
  // ikon adlarının hangi sınıfla eşleştiği orada tanımlı.
  const baseRule = upstreamCss.match(/\.ph\b[^{]*\{[^}]*\}/)?.[0] ?? '';

  const backslash = String.fromCharCode(92);
  const rules = wanted
    .map((name) => {
      const hex = codepoints.get(name).toString(16);
      return `.${selectorPrefix}.${name}:before { content: "${backslash}${hex}"; }`;
    })
    .join('\n');

  // font-display bilinçli olarak swap: paketin kendi değeri "block" idi ve
  // font inene kadar bu yazı tipini kullanan her öğeyi görünmez bırakıyordu.
  const css = `/* Bu dosya scripts/build-icon-subset.mjs tarafından üretiliyor, elle
   düzenlenmemeli. Yeni bir ikon eklendiğinde: npm run build:icons */
@font-face {
  font-family: "${familyName}";
  src: url("./${fontFile}") format("woff2");
  font-weight: 400;
  font-style: normal;
  font-display: swap;
}

${baseRule}

${rules}
`;
  await writeFile(join(OUT, `${kind}.css`), css);

  const fontSize = (await stat(join(OUT, fontFile))).size;
  const cssSize = (await stat(join(OUT, `${kind}.css`))).size;
  return { count: wanted.length, fontSize, cssSize, originalFont: original.length };
}

const { icons, fillIcons } = await scanSources();

const regular = await build({
  kind: 'regular',
  folder: 'regular',
  fontFile: 'Phosphor.woff2',
  familyName: 'Phosphor',
  selectorPrefix: 'ph',
  wanted: icons,
});
const fill = await build({
  kind: 'fill',
  folder: 'fill',
  fontFile: 'Phosphor-Fill.woff2',
  familyName: 'Phosphor-Fill',
  selectorPrefix: 'ph-fill',
  wanted: fillIcons,
});

for (const [label, r] of [
  ['regular', regular],
  ['fill', fill],
]) {
  console.log(
    `${label.padEnd(8)} ${String(r.count).padStart(3)} ikon | font ${r.originalFont} -> ${r.fontSize} byte | css ${r.cssSize} byte`,
  );
}
