// Rectangular grid textures and an asset manifest for every existing MarbleFlag.
// Existing grid textures are never overwritten. No downloads are required.
const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('sharp');
const countryData = require('flag-icons/country.json');
const projectRoot = path.resolve(__dirname, '../..');
const svgRoot = path.join(path.dirname(require.resolve('flag-icons/package.json')), 'flags/4x3');
const outputFolder = 'Assets/MultiplyOrRelease/Art/TeamFlags';
const normalize = value => value.toLowerCase().normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const byName = new Map(countryData.map(country => [normalize(country.name), country.code]));
const aliases = {
  bolivien: ['bo', 'Bolivia'], bonaire: ['bq', 'Bonaire'], brunei: ['bn', 'Brunei'],
  'cape-verde': ['cv', 'Cape Verde'], 'congo-democratic-republic': ['cd', 'Democratic Republic of the Congo'],
  'congo-republic-of-the': ['cg', 'Republic of the Congo'], 'czech-republic-the': ['cz', 'Czech Republic'],
  'east-timor': ['tl', 'East Timor'], macao: ['mo', 'Macao'], micronesia: ['fm', 'Micronesia'],
  palestine: ['ps', 'Palestine'], 'philippines-the': ['ph', 'Philippines'],
  'seychelles-the': ['sc', 'Seychelles'], 'solomon-islands-the': ['sb', 'Solomon Islands'],
  swaziland: ['sz', 'Eswatini'], turkey: ['tr', 'Turkey'], 'united-states': ['us', 'United States'],
  'vatican-city': ['va', 'Vatican City'], 'st-martin': ['sx', 'Sint Maarten'],
  'st-eustatius': [null, 'Sint Eustatius'], 'st-patrick': [null, 'Saint Patrick']
};
const existing = {
  indonesia: 'Assets/MultiplyOrRelease/Art/FlagIndonesia.png', mexico: 'Assets/MultiplyOrRelease/Art/FlagMexico.png',
  france: 'Assets/MultiplyOrRelease/Art/FlagFrance.png', china: 'Assets/MultiplyOrRelease/Art/FlagChina.png'
};
const title = slug => slug.split('-').map(word => word[0].toUpperCase() + word.slice(1)).join(' ');
async function exists(file) { try { await fs.access(file); return true; } catch { return false; } }

async function palette(input) {
  const { data, info } = await sharp(input).resize(64, 64, { fit: 'fill' }).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const buckets = new Map();
  for (let i = 0; i < data.length; i += info.channels) {
    const r = data[i], g = data[i + 1], b = data[i + 2];
    const max = Math.max(r, g, b), min = Math.min(r, g, b);
    const saturation = max ? (max - min) / max : 0;
    if (data[i + 3] < 240 || saturation < .25 || max < 60) continue;
    const key = `${r >> 5},${g >> 5},${b >> 5}`;
    const entry = buckets.get(key) || { count: 0, r: 0, g: 0, b: 0 };
    entry.count++; entry.r += r; entry.g += g; entry.b += b; buckets.set(key, entry);
  }
  const best = [...buckets.values()].sort((a, b) => b.count - a.count)[0];
  return best ? { r: best.r / best.count / 255, g: best.g / best.count / 255, b: best.b / best.count / 255, a: 1 }
    : { r: .62, g: .64, b: .66, a: 1 };
}

async function generate() {
  await fs.mkdir(path.join(projectRoot, outputFolder), { recursive: true });
  const files = (await fs.readdir(path.join(projectRoot, 'Assets/MarbleFlag'))).filter(file => file.endsWith('_round.png')).sort();
  const flags = [];
  for (const file of files) {
    const slug = file.replace('_round.png', '');
    const alias = aliases[slug];
    const code = alias ? alias[0] : byName.get(slug);
    const displayName = alias ? alias[1] : title(slug);
    const spriteAsset = `Assets/MarbleFlag/${file}`;
    const spriteFile = path.join(projectRoot, spriteAsset);
    const textureAsset = existing[slug] || `${outputFolder}/${slug}.png`;
    const textureFile = path.join(projectRoot, textureAsset);
    let source = code ? `flag-icons:${code}` : 'marble-derived';
    if (slug === 'st-patrick') source = 'saint-patrick-saltire';
    if (!await exists(textureFile)) {
      if (code) {
        await sharp(path.join(svgRoot, `${code}.svg`), { density: 144 }).resize(512, 384)
          .flatten({ background: '#ffffff' }).png().toFile(textureFile);
      } else if (slug === 'st-patrick') {
        const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="512" height="384"><path fill="white" d="M0 0h512v384H0z"/><path stroke="#ce0000" stroke-width="58" d="M0 0l512 384M0 384L512 0"/></svg>';
        await sharp(Buffer.from(svg)).flatten({ background: '#ffffff' }).png().toFile(textureFile);
      } else {
        // Three regional emblems have no exact flag-icons match: retain their
        // original icon artwork on an opaque matching-color background.
        const color = await palette(spriteFile);
        const mask = Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="320" height="320"><circle cx="160" cy="160" r="147" fill="white"/></svg>');
        const icon = await sharp(spriteFile).resize(320, 320).composite([{ input: mask, blend: 'dest-in' }]).png().toBuffer();
        await sharp({ create: { width: 512, height: 384, channels: 4,
          background: { r: Math.round(color.r * 255), g: Math.round(color.g * 255), b: Math.round(color.b * 255), alpha: 1 } } })
          .composite([{ input: icon, left: 96, top: 32 }]).flatten({ background: '#ffffff' }).png().toFile(textureFile);
      }
    }
    const territoryColor = await palette(textureFile);
    flags.push({ slug, displayName, spriteAsset, textureAsset, source, territoryColor });
  }
  const manifest = { schemaVersion: 1, flags };
  await fs.writeFile(path.join(projectRoot, outputFolder, 'MarbleFlagCatalog.json'), JSON.stringify(manifest, null, 2) + '\n');
  console.log(`Prepared ${flags.length} flags; ${flags.filter(flag => flag.source === 'marble-derived').length} regional emblems derived from their existing round artwork.`);
  console.log(`Manifest: ${outputFolder}/MarbleFlagCatalog.json`);
}
generate().catch(error => { console.error(error); process.exitCode = 1; });
