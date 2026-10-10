# 24 Country Conquest

Open `Scenes/MultiplyOrRelease24.unity` and press Play. The scene can also be opened or created with **Tools → Multiply or Release → Create or Open 24 Country Conquest**.

The board contains 24 permanent cannon slots, arranged as 6 columns × 4 rows. An enemy projectile touching a cannon transfers that slot to the projectile's country. The cannon, its flag, barrel, name, and Plinko balls change country together. Cannon slots never disappear. The captured slot resets its ammo bank to **Ammo After Capture** and cancels its former queued volley. Projectiles belonging to a country with no remaining cannons are removed, preventing an eliminated country from returning after victory.

Each slot has its own ammo bank and configurable number of balls in the shared Plinko panel on the right. A ball entering ×2 multiplies that slot's ammo; R queues its stored ammo for release. Captured slots continue playing for their new country. Rows of pegs rise continuously and wrap from the top to the bottom. Gate hits recycle balls to the top. One ball per slot is the scene default (24 balls); set **Plinko → Ball Count** to 5 for 120 balls.

All 24 cannon slots belonging to the same country ends the match and displays its winner card. Territory ownership does not delay this result.

Select `Config/Conquest24.asset` to edit countries, sprites, colours, grid appearance, flag mapping, simulation speed, seed, cannon sweeps, firing mode, ammo, projectiles, trails, Plinko physics, obstacle rise speed, and winner card. Country slots read left to right, top to bottom. Use **Apply / Restart** on the controller after changing settings. Speed applies immediately; other settings are copied at restart. The controller inspector also provides Pause, Step, New Seed, and Preview Winner.

Grid colours, barrels, and trails default to the main hue sampled from each country's flag catalog, brightened while preserving hue and saturation, with full grid brightness. **Tools → Multiply or Release → 24 Countries → Match Colors to Flags** reapplies these colours to this config. This does not change the shared country presets or the original four-team scene.

The new scene is independent of the original four-team simulation and bracket. Add it to the desired Build Profile scene list when making a standalone player. Its generated visual hierarchy is rebuilt at scene load and is not stored in the scene file.

**Cannon Hit SFX** plays `Audio/explosion.mp3` immediately when an enemy projectile captures a cannon, including the final winning capture. The clip, volume, and enable switch are configurable in `Conquest24.asset` and apply immediately. Friendly projectiles do not trigger this sound. Apply / Restart stops old sounds and keeps a single reusable audio source.

**Boost Collect SFX** plays `Audio/collect.mp3` when a marble successfully collects an available boost. **Winner SFX** plays `Audio/winner.mp3` once when the winning country is determined. Each has its own clip, volume, and enable switch. The winner card is a Canvas overlay parented to the controller in the existing gameplay scene; victory never creates or loads another scene. The arena remains visible behind the card.

**Plinko Boosts** in `Config/Conquest24.asset` controls the trigger zones in the right panel. Cyan `>>` accelerates the collecting marble downward, with configurable speed multiplier, minimum fall speed, and maximum speed. Green `x2` multiplies the stored ammo of the collecting marble's cannon slot, including after that cannon changes country; it respects Max Stored Ammo and leaves an existing Release queue unchanged. Each entry has its own enable switch, normalized position, radius, color, and **Respawn Delay** (default 10 simulation seconds). Once one marble collects a boost, the entire boost hides and cannot be collected by any marble until its timer expires. Other boosts keep their own timers. Zones are stationary and marbles pass through them without bouncing. Pause freezes respawn timers and simulation speed scales them. Use **Apply / Restart** after editing these settings.
