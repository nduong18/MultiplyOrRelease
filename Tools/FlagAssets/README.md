# Flag textures for Unity

Source: [flag-icons](https://github.com/lipis/flag-icons), MIT license.
The exact package versions are pinned in package.json/package-lock.json.

From this directory, run:

```powershell
npm ci --ignore-scripts
npm run generate
```

The generator converts the package's 4:3 SVGs for Indonesia (`id`), Mexico (`mx`),
France (`fr`), and China (`cn`) to opaque 1024 x 768 PNGs under
`Assets/MultiplyOrRelease/Art`. It also copies the upstream MIT license there.
Existing PNG `.meta` files are retained so Unity references keep their GUIDs.
The complete SVG flag catalog remains available in `node_modules/flag-icons/flags`.
Add country-code/name pairs to `flags` in `generate-flags.cjs` to export more countries.

Unity needs Read/Write enabled on these textures because SimulationView builds
a texture atlas from them. The committed PNGs work without Node or npm at runtime.
