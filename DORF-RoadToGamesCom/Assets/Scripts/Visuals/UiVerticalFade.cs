using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fades a UI graphic out towards its lower edge, so it blends into whatever is drawn behind it
/// instead of ending on its own outline. Used to melt the animated Sauerteig frame into the dough
/// sprite underneath.
///
/// A UI quad has four corners, so a plain vertex colour would only give a ramp across the whole
/// rect - it could not hold the upper part opaque. This splits the quad into horizontal slices and
/// gives each one its own alpha, which keeps the fade inside the band between
/// <see cref="fadeStart"/> and <see cref="fadeEnd"/>.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UiVerticalFade : BaseMeshEffect
{
    [Header("Settings")]
    [Tooltip("Height inside the rect where the graphic is fully transparent, 0 is the bottom edge " +
             "and 1 the top. Everything below this stays invisible.")]
    [Range(0f, 1f)] [SerializeField] private float fadeStart = 0.2194f;
    [Tooltip("Height inside the rect where the graphic is back to full opacity.")]
    [Range(0f, 1f)] [SerializeField] private float fadeEnd = 0.4119f;
    [Tooltip("How many slices the quad is cut into. More is smoother and costs a few more vertices.")]
    [Range(2, 64)] [SerializeField] private int slices = 16;

    private readonly List<UIVertex> corners = new();

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount == 0)
            return;

        var rect = ((RectTransform)transform).rect;

        if (rect.height <= 0f || fadeEnd <= fadeStart)
            return;

        // anything but the plain four corner quad of a Simple image gets the cheap treatment:
        // per-vertex alpha, no extra geometry
        if (vertexHelper.currentVertCount != 4)
        {
            FadeExistingVertices(vertexHelper, rect);
            return;
        }

        corners.Clear();

        for (var i = 0; i < 4; i++)
        {
            var corner = new UIVertex();
            vertexHelper.PopulateUIVertex(ref corner, i);
            corners.Add(corner);
        }

        // Image builds its quad bottom left, top left, top right, bottom right
        var bottomLeft = corners[0];
        var topLeft = corners[1];
        var topRight = corners[2];
        var bottomRight = corners[3];

        vertexHelper.Clear();

        for (var slice = 0; slice <= slices; slice++)
        {
            var t = (float)slice / slices;

            var left = Blend(bottomLeft, topLeft, t, rect);
            var right = Blend(bottomRight, topRight, t, rect);

            vertexHelper.AddVert(left);
            vertexHelper.AddVert(right);

            if (slice == 0)
                continue;

            var bottomIndex = (slice - 1) * 2;
            vertexHelper.AddTriangle(bottomIndex, bottomIndex + 1, bottomIndex + 3);
            vertexHelper.AddTriangle(bottomIndex, bottomIndex + 3, bottomIndex + 2);
        }
    }

    private void FadeExistingVertices(VertexHelper vertexHelper, Rect rect)
    {
        var vertex = new UIVertex();

        for (var i = 0; i < vertexHelper.currentVertCount; i++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, i);
            vertex.color = ApplyAlpha(vertex.color, HeightOf(vertex.position.y, rect));
            vertexHelper.SetUIVertex(vertex, i);
        }
    }

    private UIVertex Blend(UIVertex bottom, UIVertex top, float t, Rect rect)
    {
        var vertex = bottom;
        vertex.position = Vector3.Lerp(bottom.position, top.position, t);
        vertex.uv0 = Vector4.Lerp(bottom.uv0, top.uv0, t);
        vertex.color = ApplyAlpha(Color32.Lerp(bottom.color, top.color, t), HeightOf(vertex.position.y, rect));

        return vertex;
    }

    private static float HeightOf(float localY, Rect rect)
    {
        return Mathf.InverseLerp(rect.yMin, rect.yMax, localY);
    }

    private Color32 ApplyAlpha(Color32 color, float height)
    {
        var fade = Mathf.InverseLerp(fadeStart, fadeEnd, height);
        color.a = (byte)Mathf.RoundToInt(color.a * fade);

        return color;
    }
}
