using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MultiplyOrRelease
{
    public sealed class SimulationView : IDisposable
    {
        readonly SimulationConfig c;
        readonly SimulationModel model;
        readonly Transform root;
        readonly Transform[] plinkoPanels = new Transform[4];
        Transform defaultParent;
        readonly SpriteRenderer[] cannons = new SpriteRenderer[4];
        readonly float[] firePopRemaining = new float[4];
        readonly Transform[] barrels = new Transform[4];
        readonly SpriteRenderer[][] balls = new SpriteRenderer[4][];
        readonly TrailRenderer[][] ballTrails = new TrailRenderer[4][];
        readonly int[][] ballCycles = new int[4][];
        readonly TextMesh[] ammo = new TextMesh[4], status = new TextMesh[4];
        readonly Dictionary<int, ShotVisual> shotViews = new Dictionary<int, ShotVisual>();
        readonly Stack<ShotVisual> freeShots = new Stack<ShotVisual>();
        readonly List<int> deadIds = new List<int>();
        readonly Vector2[] centers = new Vector2[4];
        readonly HashSet<int> liveIds = new HashSet<int>();
        readonly Texture2D atlas;
        readonly Mesh gridMesh, borderMesh;
        readonly Material gridMaterial;
        readonly Vector2[] gridUV;
        readonly Color[] gridColors;
        readonly int[] flagMinX = new int[4], flagMinY = new int[4], flagMaxX = new int[4], flagMaxY = new int[4];
        int lastBoardVersion = -1;
        TerritoryStyle lastStyle;
        FlagMapping lastMapping;
        readonly bool preview;
        readonly GridImpactVfx gridImpacts;
        float clockSpeed = 1;
        bool clockPaused;
        sealed class ShotVisual
        {
            public SpriteRenderer renderer;
            public TrailRenderer trail;
        }

        public SimulationView(Transform parent, SimulationModel simulation, bool isPreview)
        {
            model = simulation; c = model.config; preview = isPreview;
            root = new GameObject(isPreview ? "Generated Preview" : "Simulation Visuals").transform;
            root.SetParent(parent, false);
            atlas = MakeAtlas();
            gridMaterial = new Material(c.presentation.spriteMaterial) { name = "Territory Atlas (runtime)", mainTexture = atlas };
            gridMesh = MakeGridMesh();
            gridUV = new Vector2[gridMesh.vertexCount]; gridColors = new Color[gridMesh.vertexCount];
            MeshObject("Territory Grid", gridMesh, gridMaterial, 0);
            borderMesh = new Mesh { name = "Ownership Borders" };
            borderMesh.indexFormat = IndexFormat.UInt32;
            MeshObject("Territory Borders", borderMesh, c.presentation.spriteMaterial, 1);
            if (c.plinko.fitTo16By9 && c.presentation.cameraFocus == CameraFocus.Simulation)
                Square("Simulation Frame", Vector2.zero, c.SimulationSize, c.presentation.frameColor, -3);
            Square("Arena Frame", Vector2.zero, new Vector2(c.board.size + c.presentation.frameThickness * 2, c.board.size + c.presentation.frameThickness * 2), c.presentation.frameColor, -2);
            Square("Grid Background", Vector2.zero, Vector2.one * c.board.size, c.board.gridColor, -1);
            for (int t = 0; t < 4; t++) BuildTeam(t);
            gridImpacts = new GridImpactVfx(root, model);
            model.ShotFired += PopCannon;
            Render(true);
        }
        void MeshObject(string name, Mesh mesh, Material material, int order)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        SpriteRenderer Sprite(string name, Vector2 pos, Vector2 size, Sprite sprite, Color color, int order, Transform parent = null)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent != null ? parent : defaultParent != null ? defaultParent : root, false); pivot.localPosition = pos;
            var go = new GameObject("Graphic", typeof(SpriteRenderer)); go.transform.SetParent(pivot, false);
            var r = go.GetComponent<SpriteRenderer>(); r.sprite = sprite;
            r.sharedMaterial = c.presentation.spriteMaterial; r.color = color; r.sortingOrder = order;
            SetSpriteSize(r, sprite, size);
            return r;
        }
        static void SetSpriteSize(SpriteRenderer renderer, Sprite sprite, Vector2 size)
        {
            renderer.sprite = sprite;
            var go = renderer.gameObject;
            Vector2 spriteSize = sprite.bounds.size;
            go.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1);
            // Imported MarbleFlag sprites have an off-centre pivot. Align the graphic,
            // not the asset, so arbitrary user sprites remain centred on the cannon.
            go.transform.localPosition = -Vector3.Scale(sprite.bounds.center, go.transform.localScale);
        }
        SpriteRenderer Square(string name, Vector2 pos, Vector2 size, Color color, int order, Transform parent = null)
            => Sprite(name, pos, size, c.presentation.squareSprite, color, order, parent);
        TextMesh Text(string name, string text, Vector2 pos, float size, Color color, int order, TextAnchor anchor = TextAnchor.MiddleCenter, Font font = null)
        {
            var go = new GameObject(name, typeof(TextMesh)); go.transform.SetParent(defaultParent != null ? defaultParent : root, false); go.transform.localPosition = pos;
            var tm = go.GetComponent<TextMesh>(); tm.font = font != null ? font : c.presentation.font;
            tm.fontSize = 96; tm.characterSize = size / 7.5f; tm.anchor = anchor;
            tm.alignment = TextAlignment.Center; tm.color = color; tm.text = text;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = tm.font.material; mr.sortingOrder = order;
            return tm;
        }
        TrailRenderer Trail(Transform parent, Color color, float time, float width, int order, bool taper)
        {
            var go = new GameObject("Trail", typeof(TrailRenderer)); go.transform.SetParent(parent, false);
            var tr = go.GetComponent<TrailRenderer>(); tr.sharedMaterial = c.presentation.trailMaterial;
            tr.time = time; tr.startWidth = width; tr.endWidth = taper ? 0 : width;
            tr.startColor = color; tr.endColor = new Color(color.r, color.g, color.b, 0);
            tr.minVertexDistance = c.presentation.trailMinVertexDistance; tr.sortingOrder = order; tr.numCapVertices = c.presentation.trailCapVertices;
            tr.emitting = !preview && c.presentation.showTrails; tr.autodestruct = false;
            return tr;
        }
        void BuildTeam(int t)
        {
            var p = c.plinko; var team = c.teams[t]; var s = model.teams[t];
            plinkoPanels[t] = new GameObject(team.name + " Plinko Panel").transform;
            plinkoPanels[t].SetParent(root, false);
            defaultParent = plinkoPanels[t];
            float x = c.board.size * .5f + p.boardGap + p.width * .5f;
            float y = p.height * .5f + p.boardGap * .5f;
            centers[t] = new Vector2(t % 2 == 0 ? -x : x, t < 2 ? y : -y);
            var center = centers[t];
            Square(team.name + " Plinko Frame", center, new Vector2(p.width + c.presentation.frameThickness * 2, p.height + c.presentation.frameThickness * 2), c.presentation.frameColor, -2);
            Square(team.name + " Plinko", center, new Vector2(p.width, p.height), p.backgroundColor, -1);
            foreach (var peg in s.pegs)
                Sprite("Peg", center + peg, Vector2.one * p.pegRadius * 2, c.presentation.circleSprite, p.pegColor, 4);
            bool mirror = p.mirrorRightBoards && t % 2 == 1;
            float multiplyWidth = p.width * p.multiplyRegion;
            float releaseWidth = p.width - multiplyWidth;
            float mx = mirror ? (p.width - multiplyWidth) * .5f : -(p.width - multiplyWidth) * .5f;
            float rx = mirror ? -multiplyWidth * .5f : multiplyWidth * .5f;
            float gy = -p.height * .5f + p.gateHeight * .5f;
            Square("Multiply Gate", center + new Vector2(mx, gy), new Vector2(multiplyWidth, p.gateHeight), p.multiplyColor, 5);
            Square("Release Gate", center + new Vector2(rx, gy), new Vector2(releaseWidth, p.gateHeight), p.releaseColor, 5);
            Text("Multiply Label", "×" + c.cannon.multiplier, center + new Vector2(mx, gy) + p.multiplyTextOffset, p.gateTextSize, p.gateTextColor, 6, font: p.font);
            Text("Release Label", "R", center + new Vector2(rx, gy) + p.releaseTextOffset, p.gateTextSize, p.gateTextColor, 6, font: p.font);
            if (c.presentation.showTeamNames)
                Text("Team Name", team.name.ToUpperInvariant(), center + new Vector2(0, p.height * .5f - p.teamLabelInset) + p.teamNameOffset, c.presentation.labelSize, c.presentation.textColor, 9, font: p.font);
            Color ammoColor = team.ammoTextColor; ammoColor.a = p.ammoTextOpacity;
            ammo[t] = Text("Stored Ammo", "1", center + Vector2.up * .04f + p.ammoTextOffset, c.presentation.ammoTextSize, ammoColor, c.presentation.ammoTextSortingOrder, font: p.font);
            status[t] = Text("Team Status", "READY", center + new Vector2(0, -p.height * .5f + p.gateHeight + .24f) + p.statusTextOffset, p.eventTextSize, team.ammoTextColor, 9, font: p.font);
            var cannonParent = new GameObject(team.name + " Cannon").transform;
            cannonParent.SetParent(root, false); cannonParent.localPosition = s.cannonPosition;
            var barrel = new GameObject("Sweep Pivot").transform; barrel.SetParent(cannonParent, false); barrels[t] = barrel;
            Square("Barrel", new Vector2(c.cannon.muzzleLength * .5f, 0), new Vector2(c.cannon.muzzleLength, c.cannon.barrelWidth), team.barrelColor, 10, barrel);
            Sprite("Cannon Rim", Vector2.zero, Vector2.one * (c.cannon.marbleDiameter + c.cannon.rimWidth), c.presentation.circleSprite, c.presentation.frameColor, 11, cannonParent);
            cannons[t] = Sprite("Cannon Marble", Vector2.zero, Vector2.one * c.cannon.marbleDiameter,
                team.cannonSprite != null ? team.cannonSprite : c.presentation.circleSprite,
                team.cannonSprite != null ? team.cannonTint : team.territoryColor, 12, cannonParent);
            balls[t] = new SpriteRenderer[s.balls.Length]; ballTrails[t] = new TrailRenderer[s.balls.Length]; ballCycles[t] = new int[s.balls.Length];
            for (int b = 0; b < s.balls.Length; b++)
            {
                balls[t][b] = Sprite("Plinko Marble " + (b + 1), center + s.balls[b].position, Vector2.one * p.ballRadius * 2,
                    team.plinkoSprite != null ? team.plinkoSprite : c.presentation.circleSprite,
                    team.plinkoBallColor, 8);
                ballTrails[t][b] = Trail(balls[t][b].transform.parent, team.plinkoTrailColor, p.trailTime, p.trailWidth, 7, p.taperTrail);
            }
            defaultParent = null;
        }
        Texture2D MakeAtlas()
        {
            const int size = 256;
            var tex = new Texture2D(size * 2, size * 2, TextureFormat.RGBA32, false) { name = "Flag Atlas (runtime)", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int t = 0; t < 4; t++)
            {
                var source = c.teams[t].territoryFlag;
                bool readable = source != null && source.isReadable;
                if (source != null && !readable) Debug.LogWarning("Territory flag for " + c.teams[t].name + " must have Read/Write enabled. Using its territory color.");
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    pixels[y * size + x] = readable ? source.GetPixelBilinear((x + .5f) / size, (y + .5f) / size) : c.teams[t].territoryColor;
                tex.SetPixels((t % 2) * size, (t / 2) * size, size, size, pixels);
            }
            tex.Apply(); return tex;
        }
        Mesh MakeGridMesh()
        {
            int count = model.owners.Length;
            var vertices = new Vector3[count * 4]; var triangles = new int[count * 6];
            float half = c.board.size * .5f, cw = model.CellWidth, ch = model.CellHeight;
            for (int y = 0; y < c.board.rows; y++) for (int x = 0; x < c.board.columns; x++)
            {
                int cell = y * c.board.columns + x, v = cell * 4, k = cell * 6;
                float l = -half + x * cw + cw * c.board.gridLineThickness * .5f, b = -half + y * ch + ch * c.board.gridLineThickness * .5f;
                float r = l + cw * (1 - c.board.gridLineThickness), top = b + ch * (1 - c.board.gridLineThickness);
                vertices[v] = new Vector3(l, b); vertices[v + 1] = new Vector3(r, b);
                vertices[v + 2] = new Vector3(r, top); vertices[v + 3] = new Vector3(l, top);
                triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1;
                triangles[k + 3] = v; triangles[k + 4] = v + 3; triangles[k + 5] = v + 2;
            }
            var mesh = new Mesh { name = "Territory Cells", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
        }
        void UpdateGrid()
        {
            bool flag = c.board.style == TerritoryStyle.Flag;
            bool fitTerritory = flag && c.board.flagMapping == FlagMapping.FitOwnedTerritory;
            if (fitTerritory) UpdateFlagBounds();
            for (int y = 0; y < c.board.rows; y++) for (int x = 0; x < c.board.columns; x++)
            {
                int index = y * c.board.columns + x, owner = model.owners[index], v = index * 4;
                Color color = c.teams[owner].territoryColor;
                float variation = ((x + y) % 2 == 0 ? 1 : 1 - c.board.checkerVariation);
                float tint = flag ? variation : c.board.colorBrightness * variation;
                color = flag ? Color.Lerp(color, Color.white, c.board.flagOpacity) : color;
                color = new Color(color.r * tint, color.g * tint, color.b * tint, 1);
                for (int k = 0; k < 4; k++) gridColors[v + k] = color;
                if (flag)
                {
                    float u = x / (float)c.board.columns, uy = y / (float)c.board.rows;
                    float du = 1f / c.board.columns, dv = 1f / c.board.rows;
                    if (fitTerritory)
                    {
                        float width = flagMaxX[owner] - flagMinX[owner] + 1;
                        float height = flagMaxY[owner] - flagMinY[owner] + 1;
                        u = (x - flagMinX[owner]) / width; uy = (y - flagMinY[owner]) / height;
                        du = 1f / width; dv = 1f / height;
                    }
                    else if (c.board.flagMapping == FlagMapping.RepeatStartingQuadrant)
                    { u = Mathf.Repeat(u * 2, 1); uy = Mathf.Repeat(uy * 2, 1); du *= 2; dv *= 2; }
                    Vector2 origin = new Vector2(owner % 2, owner / 2) * .5f;
                    // Half-texel inset avoids sampling another team's atlas quadrant.
                    gridUV[v] = AtlasUV(origin, u, uy);
                    gridUV[v + 1] = AtlasUV(origin, u + du, uy);
                    gridUV[v + 2] = AtlasUV(origin, u + du, uy + dv);
                    gridUV[v + 3] = AtlasUV(origin, u, uy + dv);
                }
                else for (int k = 0; k < 4; k++) gridUV[v + k] = Vector2.zero;
            }
            gridMaterial.mainTexture = flag ? atlas : Texture2D.whiteTexture;
            gridMesh.colors = gridColors; gridMesh.uv = gridUV;
            UpdateBorders(); lastBoardVersion = model.boardVersion; lastStyle = c.board.style; lastMapping = c.board.flagMapping;
        }
        void UpdateFlagBounds()
        {
            for (int t = 0; t < 4; t++)
            {
                flagMinX[t] = c.board.columns; flagMinY[t] = c.board.rows;
                flagMaxX[t] = flagMaxY[t] = -1;
            }
            // A single mapping per team, even if its territory is disconnected.
            // Teams without any cells produce no vertices, so no zero-size UV division.
            for (int y = 0; y < c.board.rows; y++) for (int x = 0; x < c.board.columns; x++)
            {
                int owner = model.owners[y * c.board.columns + x];
                flagMinX[owner] = Mathf.Min(flagMinX[owner], x); flagMaxX[owner] = Mathf.Max(flagMaxX[owner], x);
                flagMinY[owner] = Mathf.Min(flagMinY[owner], y); flagMaxY[owner] = Mathf.Max(flagMaxY[owner], y);
            }
        }
        static Vector2 AtlasUV(Vector2 origin, float u, float v)
            => origin + new Vector2(Mathf.Clamp(u, .002f, .998f), Mathf.Clamp(v, .002f, .998f)) * .5f;
        void UpdateBorders()
        {
            borderMesh.Clear(); if (!c.board.showOwnershipBorders) return;
            var verts = new List<Vector3>(); var tris = new List<int>(); var colors = new List<Color>();
            float half = c.board.size * .5f, cw = model.CellWidth, ch = model.CellHeight, width = c.board.borderThickness * .5f;
            for (int y = 0; y < c.board.rows; y++) for (int x = 0; x < c.board.columns; x++)
            {
                int i = y * c.board.columns + x;
                float l = x * cw - half, b = y * ch - half;
                if (x + 1 < c.board.columns && model.owners[i] != model.owners[i + 1])
                    BorderQuad(verts, tris, colors, l + cw - width, b, l + cw + width, b + ch);
                if (y + 1 < c.board.rows && model.owners[i] != model.owners[i + c.board.columns])
                    BorderQuad(verts, tris, colors, l, b + ch - width, l + cw, b + ch + width);
            }
            borderMesh.SetVertices(verts); borderMesh.SetTriangles(tris, 0); borderMesh.SetColors(colors); borderMesh.RecalculateBounds();
        }
        void BorderQuad(List<Vector3> v, List<int> t, List<Color> colors, float l, float b, float r, float top)
        {
            int i = v.Count; v.Add(new Vector3(l, b)); v.Add(new Vector3(r, b)); v.Add(new Vector3(r, top)); v.Add(new Vector3(l, top));
            t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            for (int k = 0; k < 4; k++) colors.Add(c.board.borderColor);
        }
        public void Render(bool force = false)
        {
            if (force || lastBoardVersion != model.boardVersion || lastStyle != c.board.style || lastMapping != c.board.flagMapping) UpdateGrid();
            for (int t = 0; t < 4; t++)
            {
                var s = model.teams[t]; var team = c.teams[t];
                plinkoPanels[t].gameObject.SetActive(c.presentation.cameraFocus == CameraFocus.Simulation);
                barrels[t].localRotation = Quaternion.Euler(0, 0, s.angle);
                // Hide the whole cannon (rim, marble, and barrel) on elimination.
                barrels[t].parent.gameObject.SetActive(s.alive);
                cannons[t].color = s.alive ? (team.cannonSprite != null ? team.cannonTint : team.territoryColor) : c.cannon.eliminatedTint;
                if (!s.alive || !c.cannon.enableFirePop) ResetFirePop(t);
                long displayAmmo = model.DisplayAmmo(t);
                string number = s.alive ? (c.presentation.compactAmmoNumbers ? ShortNumber(displayAmmo) : displayAmmo.ToString("N0")) : "OUT";
                if (ammo[t].text != number)
                {
                    ammo[t].text = number;
                    ammo[t].transform.localScale = Vector3.one;
                    float width = ammo[t].GetComponent<MeshRenderer>().bounds.size.x;
                    float scale = width > c.plinko.width * .84f ? c.plinko.width * .84f / width : 1;
                    ammo[t].transform.localScale = Vector3.one * scale;
                }
                status[t].text = !s.alive ? "ELIMINATED" : s.queued > 0 ? "FIRING  ·  " + ShortNumber(s.queued) + " QUEUED" : s.lastEvent;
                status[t].gameObject.SetActive(c.presentation.showPlinkoStatus);
                status[t].color = s.alive ? team.ammoTextColor : c.presentation.secondaryTextColor;
                bool plinkoPaused = model.IsPlinkoPaused(t);
                for (int b = 0; b < s.balls.Length; b++)
                {
                    var ball = s.balls[b]; var r = balls[t][b]; var tr = ballTrails[t][b];
                    bool visible = s.alive && (preview || ball.active || plinkoPaused);
                    if (ballCycles[t][b] != ball.cycles || !ball.active) { tr.emitting = false; tr.Clear(); }
                    r.transform.parent.localPosition = centers[t] + ball.position;
                    r.enabled = visible;
                    if (ballCycles[t][b] != ball.cycles) tr.Clear();
                    tr.emitting = !preview && visible && !plinkoPaused && c.presentation.showTrails;
                    tr.time = clockPaused || plinkoPaused ? 1000000 : c.plinko.trailTime / clockSpeed;
                    if (!visible || !c.presentation.showTrails) tr.Clear();
                    ballCycles[t][b] = ball.cycles;
                }
            }
            liveIds.Clear();
            foreach (var s in model.shots)
            {
                liveIds.Add(s.id);
                if (!shotViews.TryGetValue(s.id, out var visual))
                {
                    var team = c.teams[s.team];
                    var flagSprite = c.projectile.useTeamFlagSprite
                        ? (team.projectileSprite != null ? team.projectileSprite : team.cannonSprite) : null;
                    var projectileSprite = flagSprite != null ? flagSprite : c.presentation.circleSprite;
                    float diameter = c.projectile.radius * 2 * c.projectile.visualScale;
                    float trailWidth = c.projectile.matchTrailToSize ? diameter : c.projectile.trailWidth;
                    if (freeShots.Count > 0) visual = freeShots.Pop();
                    else
                    {
                        visual = new ShotVisual();
                        visual.renderer = Sprite("Pooled Projectile", s.position, Vector2.one * diameter,
                            projectileSprite, team.projectileColor, 15);
                        visual.trail = Trail(visual.renderer.transform.parent, team.projectileTrailColor, c.projectile.trailTime, trailWidth, 14, c.projectile.taperTrail);
                    }
                    visual.renderer.transform.parent.gameObject.SetActive(true);
                    // Pool entries can move between teams with different sprite bounds/pivots.
                    SetSpriteSize(visual.renderer, projectileSprite, Vector2.one * diameter);
                    visual.renderer.color = flagSprite != null ? team.projectileSpriteTint : team.projectileColor;
                    visual.trail.startColor = team.projectileTrailColor;
                    // Reapply on pool reuse, including custom width and taper settings.
                    visual.trail.startWidth = trailWidth;
                    visual.trail.endWidth = c.projectile.taperTrail ? 0 : trailWidth;
                    var color = team.projectileTrailColor; color.a = 0; visual.trail.endColor = color;
                    visual.trail.emitting = false;
                    visual.renderer.transform.parent.localPosition = s.position;
                    visual.trail.Clear();
                    shotViews.Add(s.id, visual);
                }
                visual.renderer.transform.parent.localPosition = s.position;
                visual.trail.emitting = c.presentation.showTrails && !preview;
                visual.trail.time = clockPaused ? 1000000 : c.projectile.trailTime / clockSpeed;
                if (!c.presentation.showTrails) visual.trail.Clear();
            }
            deadIds.Clear();
            foreach (var kv in shotViews) if (!liveIds.Contains(kv.Key)) deadIds.Add(kv.Key);
            foreach (int id in deadIds)
            {
                var v = shotViews[id]; v.trail.emitting = false; v.trail.Clear();
                v.renderer.transform.parent.gameObject.SetActive(false); freeShots.Push(v); shotViews.Remove(id);
            }
        }
        public static string ShortNumber(long value)
        {
            if (value < 10000) return value.ToString("N0");
            if (value < 1000000) return (value / 1000.0).ToString("0.#") + "K";
            if (value < 1000000000) return (value / 1000000.0).ToString("0.#") + "M";
            if (value < 1000000000000) return (value / 1000000000.0).ToString("0.#") + "B";
            if (value < 1000000000000000) return (value / 1000000000000.0).ToString("0.#") + "T";
            if (value < 1000000000000000000) return (value / 1000000000000000.0).ToString("0.#") + "Q";
            return (value / 1000000000000000000.0).ToString("0.#") + "E";
        }
        public void SetClock(float speed, bool paused) { clockSpeed = Mathf.Max(.1f, speed); clockPaused = paused; }
        void PopCannon(int team)
        {
            if (!c.cannon.enableFirePop || !model.teams[team].alive) return;
            // Finish the current pulse even when firing every frame; retriggering
            // its peak on every shot would hold the flag permanently enlarged.
            if (firePopRemaining[team] > 0) return;
            firePopRemaining[team] = c.cannon.firePopDuration;
            SetFirePopScale(team);
        }
        void SetFirePopScale(int team)
        {
            float remaining = Mathf.Clamp01(firePopRemaining[team] / c.cannon.firePopDuration);
            float scale = 1 + (c.cannon.firePopScale - 1) * remaining * remaining * remaining;
            // Animate the centred visual pivot, not the imported sprite's
            // off-centre pivot, the cannon body, barrel, or collision geometry.
            cannons[team].transform.parent.localScale = new Vector3(scale, scale, 1);
        }
        void ResetFirePop(int team)
        {
            firePopRemaining[team] = 0;
            cannons[team].transform.parent.localScale = Vector3.one;
        }
        public void AdvanceEffects(float deltaTime)
        {
            gridImpacts.Advance(deltaTime);
            if (deltaTime <= 0) return;
            for (int team = 0; team < 4; team++)
            {
                if (!c.cannon.enableFirePop || !model.teams[team].alive) { ResetFirePop(team); continue; }
                firePopRemaining[team] = Mathf.Max(0, firePopRemaining[team] - deltaTime);
                SetFirePopScale(team);
            }
        }
        public void Dispose()
        {
            model.ShotFired -= PopCannon;
            gridImpacts.Dispose();
            root.gameObject.SetActive(false);
            Destroy(root.gameObject); Destroy(gridMesh); Destroy(borderMesh); Destroy(gridMaterial); Destroy(atlas);
        }
        static void Destroy(UnityEngine.Object obj)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
