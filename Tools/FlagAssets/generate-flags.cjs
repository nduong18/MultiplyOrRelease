const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('sharp');

const sourceRoot = path.dirname(require.resolve('flag-icons/package.json'));
const outputRoot = path.resolve(__dirname, '../../Assets/MultiplyOrRelease/Art');
const flags = { id: 'Indonesia', mx: 'Mexico', fr: 'France', cn: 'China' };

async function generate() {
  await fs.mkdir(outputRoot, { recursive: true });
  for (const [code, name] of Object.entries(flags)) {
    const source = path.join(sourceRoot, 'flags', '4x3', `${code}.svg`);
    const destination = path.join(outputRoot, `Flag${name}.png`);
    await sharp(source, { density: 144 })
      .resize(1024, 768)
      .flatten({ background: '#ffffff' })
      .png()
      .toFile(destination);
    console.log(`${code}: ${destination} (1024 x 768)`);
  }
  await fs.copyFile(path.join(sourceRoot, 'LICENSE'), path.join(outputRoot, 'FlagIcons-LICENSE.txt'));
}

generate().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
