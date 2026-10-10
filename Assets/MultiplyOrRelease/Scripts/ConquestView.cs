using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MultiplyOrRelease
{
    public sealed partial class ConquestView : IDisposable
    {
        sealed class Marble { public SpriteRenderer sprite; public TrailRenderer trail; public int cycle = -1; }
        readonly ConquestConfig c;
        readonly ConquestModel model;
        readonly Transform root;
        readonly SpriteRenderer[] cannonSprites = new SpriteRenderer[24], barrelSprites = new SpriteRenderer[24];
        readonly Transform[] barrels = new Transform[24];
        readonly TextMesh[] ammo = new TextMesh[24], names = new TextMesh[24];
        readonly TextMesh[][] ammoOutlines = new TextMesh[24][];
        readonly int[] displayedOwner = new int[24];
        readonly float[] pops = new float[24], flashes = new float[24];
        readonly SpriteRenderer[] captureRings = new SpriteRenderer[24];
        readonly Marble[][] balls = new Marble[24][];
        readonly SpriteRenderer[] pegs;
        readonly Dictionary<int, Marble> shotViews = new Dictionary<int, Marble>();
        readonly Stack<Marble> freeViews = new Stack<Marble>();
        readonly HashSet<int> liveIds = new HashSet<int>();
        readonly List<int> deadIds = new List<int>();
        readonly Mesh gridMesh;
        readonly Mesh borderMesh;
        readonly List<Vector3> borderVertices = new List<Vector3>();
        readonly List<int> borderIndices = new List<int>();
        readonly List<Color> borderColors = new List<Color>();
        readonly Material gridMaterial;
        readonly Texture2D atlas;
        readonly Color[] gridColors;
        readonly Vector2[] gridUvs;
        readonly int[] minX = new int[24], minY = new int[24], maxX = new int[24], maxY = new int[24];
        int lastVersion = -1;
        TerritoryStyle lastStyle;
        FlagMapping lastMapping;
        bool disposed;

        public ConquestView(Transform parent, ConquestConfig config, ConquestModel state)
        {
            c = config; model = state;
            root = new GameObject("Conquest Visuals").transform; root.SetParent(parent, false);
            root.gameObject.hideFlags = HideFlags.DontSave;
            float frame = c.presentation.frameThickness;
            Square("Arena Frame", Vector2.zero, c.arenaSize + Vector2.one * frame * 2, c.presentation.frameColor, -3);
            Square("Grid Background", Vector2.zero, c.arenaSize, c.grid.gridColor, -2);
            float center = c.PlinkoLeft + c.plinko.width * .5f;
            Square("Plinko Frame", new Vector2(center, 0), new Vector2(c.plinko.width + frame * 2, c.arenaSize.y + frame * 2), c.presentation.frameColor, -3);
            Square("Shared Plinko", new Vector2(center, 0), new Vector2(c.plinko.width, c.arenaSize.y), c.plinko.backgroundColor, -1);
            float gateY = -c.arenaSize.y * .5f + c.plinko.gateHeight * .5f;
            float multiplyWidth = c.plinko.width * c.plinko.multiplyRegion, releaseWidth = c.plinko.width - multiplyWidth;
            float mx = c.PlinkoLeft + multiplyWidth * .5f, rx = c.PlinkoLeft + multiplyWidth + releaseWidth * .5f;
            Square("Multiply Gate", new Vector2(mx, gateY), new Vector2(multiplyWidth, c.plinko.gateHeight), c.plinko.multiplyColor, 5);
            Square("Release Gate", new Vector2(rx, gateY), new Vector2(releaseWidth, c.plinko.gateHeight), c.plinko.releaseColor, 5);
            Text("Multiply", "×" + c.cannon.multiplier, new Vector2(mx, gateY) + c.plinko.multiplyTextOffset, c.plinko.gateTextSize, c.plinko.gateTextColor, 6);
            Text("Release", "R", new Vector2(rx, gateY) + c.plinko.releaseTextOffset, c.plinko.gateTextSize, c.plinko.gateTextColor, 6);
            pegs = new SpriteRenderer[model.pegs.Length];
            for (int i = 0; i < pegs.Length; i++) pegs[i] = Sprite("Moving Peg " + i, model.pegs[i], Vector2.one * c.plinko.pegRadius * 2,
                c.presentation.circleSprite, c.plinko.pegColor, 4);
            BuildPlinkoBoosts();
            atlas = BuildAtlas();
            gridMaterial = new Material(c.presentation.spriteMaterial) { name = "Conquest Territory Material", mainTexture = Texture2D.whiteTexture };
            gridMesh = new Mesh { name = "24 Country Territory Grid", indexFormat = IndexFormat.UInt32 };
            gridMesh.MarkDynamic();
            int cells = model.owners.Length;
            var vertices = new Vector3[cells * 4]; var indices = new int[cells * 6];
            gridColors = new Color[cells * 4]; gridUvs = new Vector2[cells * 4];
            float w = c.arenaSize.x / c.GridColumns, h = c.arenaSize.y / c.GridRows;
            for (int y = 0; y < c.GridRows; y++) for (int x = 0; x < c.GridColumns; x++)
            {
                int index = y * c.GridColumns + x, v = index * 4, t = index * 6;
                float left = -c.arenaSize.x * .5f + x * w + w * c.grid.gridLineThickness * .5f;
                float bottom = -c.arenaSize.y * .5f + y * h + h * c.grid.gridLineThickness * .5f;
                float right = left + w * (1 - c.grid.gridLineThickness), top = bottom + h * (1 - c.grid.gridLineThickness);
                vertices[v] = new Vector3(left, bottom); vertices[v + 1] = new Vector3(right, bottom);
                vertices[v + 2] = new Vector3(right, top); vertices[v + 3] = new Vector3(left, top);
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v; indices[t + 4] = v + 3; indices[t + 5] = v + 2;
            }
            gridMesh.vertices = vertices; gridMesh.triangles = indices; gridMesh.RecalculateBounds();
            var grid = new GameObject("Territory Grid", typeof(MeshFilter), typeof(MeshRenderer)); grid.transform.SetParent(root, false);
            grid.GetComponent<MeshFilter>().sharedMesh = gridMesh; grid.GetComponent<MeshRenderer>().sharedMaterial = gridMaterial;
            borderMesh = new Mesh { name = "Conquest Ownership Borders", indexFormat = IndexFormat.UInt32 }; borderMesh.MarkDynamic();
            var border = new GameObject("Ownership Borders", typeof(MeshFilter), typeof(MeshRenderer)); border.transform.SetParent(root, false);
            border.GetComponent<MeshFilter>().sharedMesh = borderMesh;
            border.GetComponent<MeshRenderer>().sharedMaterial = c.presentation.spriteMaterial; border.GetComponent<MeshRenderer>().sortingOrder = 1;
            for (int i = 0; i < 24; i++) BuildCannon(i);
            model.ShotFired += Pop; model.CannonCaptured += Capture;
            Render(0, 1, true);
        }
        SpriteRenderer Sprite(string label, Vector2 position, Vector2 size, Sprite sprite, Color color, int order, Transform parent = null)
        {
            var pivot = new GameObject(label).transform; pivot.SetParent(parent != null ? parent : root, false); pivot.localPosition = position;
            var graphic = new GameObject("Graphic", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>(); graphic.transform.SetParent(pivot, false);
            graphic.sharedMaterial = c.presentation.spriteMaterial; graphic.sortingOrder = order; graphic.color = color;
            SetSize(graphic, sprite, size); return graphic;
        }
        static void SetSize(SpriteRenderer renderer, Sprite sprite, Vector2 size)
        {
            renderer.sprite = sprite; if (sprite == null) return;
            var bounds = sprite.bounds;
            renderer.transform.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, 1);
            renderer.transform.localPosition = -Vector3.Scale(bounds.center, renderer.transform.localScale);
        }
        SpriteRenderer Square(string label, Vector2 p, Vector2 size, Color color, int order, Transform parent = null)
            => Sprite(label, p, size, c.presentation.squareSprite, color, order, parent);
        TextMesh Text(string label, string text, Vector2 p, float size, Color color, int order)
        {
            var go = new GameObject(label, typeof(TextMesh)); go.transform.SetParent(root, false); go.transform.localPosition = p;
            var result = go.GetComponent<TextMesh>(); result.font = c.plinko.font != null ? c.plinko.font : c.presentation.font;
            result.fontSize = 96; result.characterSize = size / 7.5f; result.anchor = TextAnchor.MiddleCenter;
            result.alignment = TextAlignment.Center; result.color = color; result.text = text;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = result.font.material; renderer.sortingOrder = order; return result;
        }
        Marble NewMarble(string label, float diameter, int order)
        {
            var result = new Marble { sprite = Sprite(label, Vector2.zero, Vector2.one * diameter, c.presentation.circleSprite, Color.white, order) };
            var go = new GameObject("Trail", typeof(TrailRenderer)); go.transform.SetParent(result.sprite.transform.parent, false);
            result.trail = go.GetComponent<TrailRenderer>(); result.trail.sharedMaterial = c.presentation.trailMaterial;
            result.trail.minVertexDistance = c.presentation.trailMinVertexDistance; result.trail.numCapVertices = c.presentation.trailCapVertices;
            result.trail.sortingOrder = order - 1; result.trail.emitting = false; return result;
        }
        void ConfigureMarble(Marble v, int owner, bool projectile)
        {
            var team = c.countries[owner];
            Sprite sprite = projectile ? (c.projectile.useTeamFlagSprite ? team.projectileSprite != null ? team.projectileSprite : team.cannonSprite : null) : team.plinkoSprite;
            Color tint = sprite == null ? projectile ? team.projectileColor : team.plinkoBallColor : projectile ? team.projectileSpriteTint : team.plinkoBallColor;
            float diameter = projectile ? c.projectile.radius * 2 * c.projectile.visualScale : c.plinko.ballRadius * 2;
            SetSize(v.sprite, sprite != null ? sprite : c.presentation.circleSprite, Vector2.one * diameter); v.sprite.color = tint;
            Color color = projectile ? team.projectileTrailColor : team.plinkoTrailColor;
            v.trail.startColor = color; color.a = 0; v.trail.endColor = color;
            v.trail.startWidth = projectile ? c.projectile.matchTrailToSize ? diameter : c.projectile.trailWidth : c.plinko.trailWidth;
            v.trail.endWidth = (projectile ? c.projectile.taperTrail : c.plinko.taperTrail) ? 0 : v.trail.startWidth;
        }
        void BuildCannon(int slot)
        {
            var state = model.cannons[slot];
            var pivot = new GameObject("Cannon Slot " + (slot + 1)).transform; pivot.SetParent(root, false); pivot.localPosition = state.position;
            var barrel = new GameObject("Sweep Pivot").transform; barrel.SetParent(pivot, false); barrels[slot] = barrel;
            float outline = c.cannon.barrelOutlineWidth;
            if (outline > 0) Square("Barrel Outline", Vector2.right * c.cannon.muzzleLength * .5f,
                new Vector2(c.cannon.muzzleLength + outline * 2, c.cannon.barrelWidth + outline * 2), c.cannon.barrelOutlineColor, 9, barrel);
            barrelSprites[slot] = Square("Barrel", Vector2.right * c.cannon.muzzleLength * .5f,
                new Vector2(c.cannon.muzzleLength, c.cannon.barrelWidth), Color.white, 10, barrel);
            captureRings[slot] = Sprite("Capture Pulse", Vector2.zero, Vector2.one, c.presentation.circleSprite, Color.clear, 10, pivot);
            Sprite("Cannon Rim", Vector2.zero, Vector2.one * (c.cannon.marbleDiameter + c.cannon.rimWidth), c.presentation.circleSprite, Color.black, 11, pivot);
            cannonSprites[slot] = Sprite("Country Flag", Vector2.zero, Vector2.one * c.cannon.marbleDiameter, c.presentation.circleSprite, Color.white, 12, pivot);
            ammo[slot] = Text("Ammo Slot " + (slot + 1), "1", AmmoPosition(slot),
                c.presentation.ammoTextSize, Color.white, c.presentation.ammoTextSortingOrder);
            if (c.enableAmmoTextOutline && c.ammoTextOutlineWidth > 0)
            {
                ammoOutlines[slot] = new TextMesh[8];
                for (int edgeIndex = 0; edgeIndex < 8; edgeIndex++)
                {
                    float angle = edgeIndex * Mathf.PI * .25f;
                    var edge = Text("Ammo Outline " + edgeIndex, "1", Vector2.zero, c.presentation.ammoTextSize,
                        c.ammoTextOutlineColor, Mathf.Max(-32768, c.presentation.ammoTextSortingOrder - 1));
                    edge.transform.SetParent(ammo[slot].transform, false);
                    edge.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * c.ammoTextOutlineWidth;
                    ammoOutlines[slot][edgeIndex] = edge;
                }
            }
            names[slot] = Text("Country Slot " + (slot + 1), "", state.position + Vector2.down * 1.03f + c.plinko.teamNameOffset,
                c.presentation.labelSize, c.presentation.textColor, 21);
            balls[slot] = new Marble[state.balls.Length]; displayedOwner[slot] = -1;
            for (int b = 0; b < balls[slot].Length; b++) balls[slot][b] = NewMarble("Plinko Slot " + (slot + 1) + " Ball " + b, c.plinko.ballRadius * 2, 8);
        }
        Texture2D BuildAtlas()
        {
            const int width = 256, height = 128;
            var result = new Texture2D(width * 6, height * 4, TextureFormat.RGBA32, false) { name = "24 Country Flag Atlas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var previous = RenderTexture.active;
            var tile = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            try
            {
                for (int t = 0; t < 24; t++)
                {
                    var flag = c.countries[t].territoryFlag;
                    Graphics.Blit(flag != null ? flag : Texture2D.whiteTexture, rt); RenderTexture.active = rt;
                    tile.ReadPixels(new Rect(0, 0, width, height), 0, 0); tile.Apply();
                    result.SetPixels(t % 6 * width, t / 6 * height, width, height, tile.GetPixels());
                }
                result.Apply(); return result;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Destroy(tile); }
        }
        void RenderGrid()
        {
            if (lastVersion == model.boardVersion && lastStyle == c.grid.style && lastMapping == c.grid.flagMapping) return;
            lastVersion = model.boardVersion; lastStyle = c.grid.style; lastMapping = c.grid.flagMapping;
            bool flag = c.grid.style == TerritoryStyle.Flag;
            for (int t = 0; t < 24; t++) { minX[t] = c.GridColumns; minY[t] = c.GridRows; maxX[t] = maxY[t] = -1; }
            if (flag && c.grid.flagMapping == FlagMapping.FitOwnedTerritory)
                for (int y = 0; y < c.GridRows; y++) for (int x = 0; x < c.GridColumns; x++)
                { int t = model.owners[y * c.GridColumns + x]; minX[t] = Math.Min(minX[t], x); minY[t] = Math.Min(minY[t], y); maxX[t] = Math.Max(maxX[t], x); maxY[t] = Math.Max(maxY[t], y); }
            for (int y = 0; y < c.GridRows; y++) for (int x = 0; x < c.GridColumns; x++)
            {
                int index = y * c.GridColumns + x, t = model.owners[index], v = index * 4;
                float variation = (x + y) % 2 == 0 ? 1 : 1 - c.grid.checkerVariation;
                Color color = c.countries[t].territoryColor * (c.grid.colorBrightness * variation); color.a = 1;
                if (flag && c.countries[t].territoryFlag != null) color = Color.Lerp(color, Color.white, c.grid.flagOpacity);
                // Mesh vertex colours need explicit sRGB conversion in Linear projects.
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
                for (int j = 0; j < 4; j++) gridColors[v + j] = color;
                float u0, u1, v0, v1;
                if (c.grid.flagMapping == FlagMapping.FitOwnedTerritory)
                {
                    float width = Math.Max(1, maxX[t] - minX[t] + 1), height = Math.Max(1, maxY[t] - minY[t] + 1);
                    u0 = (x - minX[t]) / width; u1 = (x - minX[t] + 1) / width;
                    v0 = (y - minY[t]) / height; v1 = (y - minY[t] + 1) / height;
                }
                else if (c.grid.flagMapping == FlagMapping.RepeatStartingQuadrant)
                {
                    u0 = x % c.cellsPerCountry / (float)c.cellsPerCountry; u1 = (x % c.cellsPerCountry + 1) / (float)c.cellsPerCountry;
                    v0 = y % c.cellsPerCountry / (float)c.cellsPerCountry; v1 = (y % c.cellsPerCountry + 1) / (float)c.cellsPerCountry;
                }
                else { u0 = x / (float)c.GridColumns; u1 = (x + 1) / (float)c.GridColumns;
                    v0 = y / (float)c.GridRows; v1 = (y + 1) / (float)c.GridRows; }
                gridUvs[v] = AtlasUv(t, u0, v0); gridUvs[v + 1] = AtlasUv(t, u1, v0);
                gridUvs[v + 2] = AtlasUv(t, u1, v1); gridUvs[v + 3] = AtlasUv(t, u0, v1);
            }
            gridMaterial.mainTexture = flag ? atlas : Texture2D.whiteTexture;
            gridMesh.colors = gridColors; gridMesh.uv = gridUvs;
            RenderBorders();
        }
        void RenderBorders()
        {
            borderVertices.Clear(); borderIndices.Clear(); borderColors.Clear(); borderMesh.Clear();
            if (!c.grid.showOwnershipBorders) return;
            float w = c.arenaSize.x / c.GridColumns, h = c.arenaSize.y / c.GridRows;
            float thickness = c.grid.borderThickness;
            for (int y = 0; y < c.GridRows; y++) for (int x = 0; x < c.GridColumns; x++)
            {
                int i = y * c.GridColumns + x;
                float left = -c.arenaSize.x * .5f + x * w, bottom = -c.arenaSize.y * .5f + y * h;
                if (x + 1 < c.GridColumns && model.owners[i] != model.owners[i + 1]) BorderQuad(new Vector2(left + w, bottom + h * .5f), new Vector2(thickness, h));
                if (y + 1 < c.GridRows && model.owners[i] != model.owners[i + c.GridColumns]) BorderQuad(new Vector2(left + w * .5f, bottom + h), new Vector2(w, thickness));
            }
            borderMesh.SetVertices(borderVertices); borderMesh.SetTriangles(borderIndices, 0); borderMesh.SetColors(borderColors); borderMesh.RecalculateBounds();
        }
        void BorderQuad(Vector2 p, Vector2 size)
        {
            int index = borderVertices.Count; Vector2 half = size * .5f;
            borderVertices.Add(p - half); borderVertices.Add(p + new Vector2(half.x, -half.y));
            borderVertices.Add(p + half); borderVertices.Add(p + new Vector2(-half.x, half.y));
            borderIndices.Add(index); borderIndices.Add(index + 2); borderIndices.Add(index + 1);
            borderIndices.Add(index); borderIndices.Add(index + 3); borderIndices.Add(index + 2);
            Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.grid.borderColor.linear : c.grid.borderColor;
            for (int i = 0; i < 4; i++) borderColors.Add(color);
        }
        static Vector2 AtlasUv(int t, float u, float v) => new Vector2((t % 6 + Mathf.Lerp(.002f, .998f, Mathf.Clamp01(u))) / 6,
            (t / 6 + Mathf.Lerp(.004f, .996f, Mathf.Clamp01(v))) / 4);
        void Pop(int slot) { if (c.cannon.enableFirePop && pops[slot] <= 0) pops[slot] = c.cannon.firePopDuration; }
        Vector2 AmmoPosition(int slot) => model.cannons[slot].position + Vector2.down * .72f
            + c.plinko.ammoTextOffset + c.ammoTextOffset;
        void Capture(int slot, int previous, int next) { flashes[slot] = .32f; pops[slot] = 0; }
        void MoveMarble(Marble visual, Vector2 p, bool active, bool paused, float time, float speed)
        {
            var pivot = visual.sprite.transform.parent;
            pivot.gameObject.SetActive(active); pivot.localPosition = p;
            visual.trail.time = paused ? 100000 : Mathf.Max(.01f, time) / Mathf.Max(.1f, speed);
            visual.trail.emitting = Application.isPlaying && active && !paused && c.presentation.showTrails;
            if (!active || !c.presentation.showTrails) visual.trail.Clear();
        }
        public void Render(float dt, float speed, bool paused)
        {
            RenderGrid();
            RenderPlinkoBoosts(dt);
            for (int i = 0; i < pegs.Length; i++) pegs[i].transform.parent.localPosition = model.pegs[i];
            for (int i = 0; i < 24; i++)
            {
                var s = model.cannons[i]; var team = c.countries[s.owner];
                ammo[i].transform.localPosition = AmmoPosition(i);
                if (displayedOwner[i] != s.owner)
                {
                    displayedOwner[i] = s.owner;
                    SetSize(cannonSprites[i], team.cannonSprite != null ? team.cannonSprite : c.presentation.circleSprite, Vector2.one * c.cannon.marbleDiameter);
                    cannonSprites[i].color = team.cannonSprite != null ? team.cannonTint : team.territoryColor;
                    barrelSprites[i].color = team.barrelColor; names[i].text = team.name.ToUpperInvariant(); ammo[i].color = team.ammoTextColor;
                    for (int b = 0; b < balls[i].Length; b++) { ConfigureMarble(balls[i][b], s.owner, false); balls[i][b].trail.Clear(); }
                }
                barrels[i].localRotation = Quaternion.Euler(0, 0, s.angle);
                long value = model.DisplayAmmo(i);
                string number = c.presentation.compactAmmoNumbers ? SimulationView.ShortNumber(value) : value.ToString("N0");
                if (ammo[i].text != number)
                {
                    ammo[i].text = number;
                    if (ammoOutlines[i] != null) foreach (var outline in ammoOutlines[i]) outline.text = number;
                }
                names[i].gameObject.SetActive(c.presentation.showTeamNames);
                pops[i] = Mathf.Max(0, pops[i] - dt); flashes[i] = Mathf.Max(0, flashes[i] - dt);
                float pop = 1 + (c.cannon.firePopScale - 1) * Mathf.Pow(pops[i] / c.cannon.firePopDuration, 3);
                cannonSprites[i].transform.parent.localScale = Vector3.one * pop;
                Color ringColor = team.barrelColor; ringColor.a = flashes[i] / .32f * .65f;
                captureRings[i].color = ringColor;
                captureRings[i].transform.parent.localScale = Vector3.one * (c.cannon.marbleDiameter + .15f + (.32f - flashes[i]) * 2);
                for (int b = 0; b < balls[i].Length; b++)
                {
                    var ball = s.balls[b]; var v = balls[i][b];
                    if (v.cycle != ball.cycles) { v.cycle = ball.cycles; v.trail.Clear(); }
                    MoveMarble(v, ball.position, ball.active || !Application.isPlaying, paused || c.plinko.pauseWhileReleasing && s.queued > 0,
                        c.plinko.trailTime, speed);
                }
            }
            liveIds.Clear();
            foreach (var shot in model.shots)
            {
                liveIds.Add(shot.id);
                if (!shotViews.TryGetValue(shot.id, out var visual))
                {
                    visual = freeViews.Count > 0 ? freeViews.Pop() : NewMarble("Pooled Projectile", c.projectile.radius * 2, 16);
                    ConfigureMarble(visual, shot.team, true); visual.trail.Clear(); shotViews.Add(shot.id, visual);
                }
                MoveMarble(visual, shot.position, true, paused, c.projectile.trailTime, speed);
            }
            deadIds.Clear(); foreach (var pair in shotViews) if (!liveIds.Contains(pair.Key)) deadIds.Add(pair.Key);
            foreach (int id in deadIds)
            {
                var v = shotViews[id]; v.trail.emitting = false; v.trail.Clear(); v.sprite.transform.parent.gameObject.SetActive(false);
                freeViews.Push(v); shotViews.Remove(id);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            model.ShotFired -= Pop; model.CannonCaptured -= Capture;
            root.gameObject.SetActive(false); Destroy(root.gameObject); Destroy(gridMesh); Destroy(borderMesh); Destroy(gridMaterial); Destroy(atlas);
        }
        static void Destroy(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
