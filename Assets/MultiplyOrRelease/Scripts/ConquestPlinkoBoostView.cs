using UnityEngine;

namespace MultiplyOrRelease
{
    public sealed partial class ConquestView
    {
        Transform[] boostPivots;

        void BuildPlinkoBoosts()
        {
            boostPivots = new Transform[c.plinkoBoosts.Length];
            for (int i = 0; i < boostPivots.Length; i++)
            {
                var boost = c.plinkoBoosts[i]; if (boost == null) continue;
                var graphic = Sprite("Plinko Boost " + (i + 1) + " " + boost.kind, model.PlinkoBoostPosition(i),
                    Vector2.one * boost.radius * 2, c.presentation.circleSprite, boost.color, 3);
                var pivot = graphic.transform.parent; boostPivots[i] = pivot;
                if (c.boostOutlineWidth > 0)
                    Sprite("Boost Outline", Vector2.zero, Vector2.one * (boost.radius + c.boostOutlineWidth) * 2,
                        c.presentation.circleSprite, c.boostOutlineColor, 2, pivot);
                var label = Text("Boost Label", boost.kind == PlinkoBoostKind.MarbleSpeed ? ">>" : "x" + boost.ammoMultiplier,
                    Vector2.zero, c.boostLabelSize, c.boostLabelColor, 5);
                label.transform.SetParent(pivot, false);
                pivot.gameObject.SetActive(model.IsPlinkoBoostAvailable(i));
            }
        }
        void RenderPlinkoBoosts(float dt)
        {
            for (int i = 0; i < boostPivots.Length; i++)
            {
                if (boostPivots[i] == null) continue;
                boostPivots[i].gameObject.SetActive(model.IsPlinkoBoostAvailable(i));
            }
        }
    }
}
