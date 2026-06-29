using UnityEngine;

public class LegacySystemTag : MonoBehaviour
{
    [Header("Legacy System")]
    public string systemName;
    [TextArea]
    public string reasonKept;
    [TextArea]
    public string plannedReplacement;
    public string targetPhase;
}
