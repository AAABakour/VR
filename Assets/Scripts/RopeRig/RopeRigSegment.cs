using UnityEngine;

public class RopeRigSegment : MonoBehaviour
{
    [HideInInspector]
    public int segmentIndex;

    [HideInInspector]
    public Renderer cachedRenderer;

    private void Awake()
    {
        CacheRenderer();
    }

    public void CacheRenderer()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponentInChildren<Renderer>();
        }
    }
}
