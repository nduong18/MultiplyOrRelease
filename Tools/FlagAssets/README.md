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
# Full MarbleFlag team library

Run `npm run generate:teams`, refresh Unity, then use `Tools > Multiply or Release > Generate Missing MarbleFlag Team Presets`. The generator covers every `Assets/MarbleFlag/*_round.png`, keeps existing presets, and never overwrites existing grid textures. Round sprites are used by cannon, Plinko and bullets; readable opaque rectangular textures are used by the grid.

The existing four grid textures are reused. Country/name aliases are resolved through flag-icons 7.5.0. Saint Patrick uses its red saltire. Arab League, Saba and Sint Eustatius have no matching flag-icons asset, so their existing emblems are placed on an opaque matching-color rectangular background; these entries are marked `marble-derived` in `MarbleFlagCatalog.json` (not claimed to be exact rectangular national flags).
