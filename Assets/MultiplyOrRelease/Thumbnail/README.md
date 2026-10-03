# YouTube thumbnail scene

Open `Assets/MultiplyOrRelease/Scenes/Thumbnail.unity`, or use **Tools > Multiply or Release > Create or Open Thumbnail**. Select **Thumbnail Board** in the Hierarchy. It displays in Edit mode; Play is optional.

- **Teams**: drag any number of assets from `Assets/MultiplyOrRelease/TeamPresets` into this list. Reorder to arrange them from left to right, top to bottom. Each panel uses the preset's `territoryFlag`, with `territoryColor` as its fallback. Duplicate teams are allowed; empty slots use `emptyColor`.
- **Columns**: set the number of columns, or use `0` for automatic layout. Rows grow with the team count. **Center Last Row** centers incomplete rows. The example uses 18 teams in 6 columns.
- **Margin**, **Panel Gap**, **Corner Radius**, and the background colors control the board shape.
- **Cells Across / Down**, **Grid Line Thickness / Color**, and **Checker Variation** control the territory grid. The flag image remains smooth across cells, matching the simulation scene. Down = `0` keeps cells approximately square. **Flag Fit** stretches, contains, or crops the flag to its panel. Defaults use 28 × 28 cells, no grid gaps, and 0.08 checker variation, with no margin or rounded corners.
- **Image Width / Height**: default 1920 × 1080; 1280 × 720 also works. Use a 16:9 Game view to see the matching framing.
- **Rebuild Preview** refreshes after editing a referenced team preset. Scene settings and list changes refresh automatically.
- **Export PNG...** saves exactly the generated image, without editor gizmos, text, or dependence on Game view resolution. `Thumbnail.png` is the provided example export.

Save the scene to preserve its list and settings. The preview image is regenerated when the scene opens. This scene contains only a board, camera, and light; it does not reference simulation or bracket configuration and is not added to Build Settings.
