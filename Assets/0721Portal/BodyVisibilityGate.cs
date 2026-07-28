using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides the first-person avatar body while it is fully inside the MR /
/// passthrough cube, and shows it as soon as ANY part of the body pokes
/// outside that cube.
///
/// It watches every skeleton bone of the avatar. If even one bone is outside
/// the MR volume, the whole body becomes visible. Once every bone is back
/// inside, it hides again.
///
/// This does NOT touch layers, so it won't interfere with the stencil-portal
/// Render Objects features (which filter by layer). It only toggles the
/// Renderers on/off. The Character Retargeter drives bones, not renderer
/// enable state, so this toggle is stable frame-to-frame.
///
/// Setup:
///  - bodyRoot : the "body" GameObject (its Renderers are toggled, its bones are watched)
///  - mrVolume : a BoxCollider (Is Trigger) sized to the MR play area.
///               Leave empty = body stays hidden (nothing can be "outside" of nothing).
/// </summary>
public class BodyVisibilityGate : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root of the avatar mesh. Its Renderers are toggled and its bones are watched.")]
    [SerializeField] private GameObject bodyRoot;

    [Tooltip("BoxCollider marking the MR cube. Body shows the moment any bone leaves it. Empty = always hidden.")]
    [SerializeField] private Collider mrVolume;

    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Transform> testPoints = new List<Transform>();
    private bool currentlyVisible = true;

    void Awake()
    {
        if (bodyRoot != null)
        {
            bodyRoot.GetComponentsInChildren(true, renderers);

            // Watch the skeleton bones of every skinned mesh under the body.
            var skinned = new List<SkinnedMeshRenderer>();
            bodyRoot.GetComponentsInChildren(true, skinned);
            foreach (var smr in skinned)
                if (smr.bones != null)
                    foreach (var bone in smr.bones)
                        if (bone != null && !testPoints.Contains(bone))
                            testPoints.Add(bone);

            // Fallback: no bones found -> just watch every child transform.
            if (testPoints.Count == 0)
            {
                var all = new List<Transform>();
                bodyRoot.GetComponentsInChildren(true, all);
                testPoints.AddRange(all);
            }
        }

        // Start hidden — the participant begins inside the MR cube.
        SetVisible(false);
    }

    void Update()
    {
        SetVisible(AnyPartOutside());
    }

    private bool AnyPartOutside()
    {
        // No volume assigned -> nothing to be "outside" of -> stay hidden.
        if (mrVolume == null)
            return false;

        for (int i = 0; i < testPoints.Count; i++)
        {
            Transform t = testPoints[i];
            if (t == null) continue;

            Vector3 p = t.position;
            // ClosestPoint returns the point itself only when it's inside/on the collider.
            // If it differs, this bone is outside the cube.
            if (mrVolume.ClosestPoint(p) != p)
                return true;
        }
        return false;
    }

    /// <summary>Force the body visible or hidden, overriding the automatic gate.</summary>
    public void SetVisible(bool visible)
    {
        if (visible == currentlyVisible) return;
        currentlyVisible = visible;
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i] != null)
                renderers[i].enabled = visible;
    }
}
