using UnityEngine;

[DefaultExecutionOrder(25)]
public class FluidBoxMotionController : MonoBehaviour
{
    public enum MotionMode
    {
        Static,
        TiltLeftRight,
        OscillatingTilt,
        HorizontalShake,
        CircularMotion,
        ManualTestMotion
    }

    public MotionMode motionMode = MotionMode.OscillatingTilt;
    public bool motionEnabled = true;
    public float tiltAngle = 18f;
    public float motionAmplitude = 0.35f;
    public float motionFrequency = 0.6f;
    public Vector3 manualEulerAngles = new Vector3(0f, 0f, 12f);
    public Vector3 manualOffset = Vector3.zero;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private float motionTime;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        if (!motionEnabled)
        {
            return;
        }

        motionTime += Time.deltaTime;
        float wave = Mathf.Sin(motionTime * Mathf.PI * 2f * Mathf.Max(0.01f, motionFrequency));

        switch (motionMode)
        {
            case MotionMode.Static:
                transform.SetPositionAndRotation(startPosition, startRotation);
                break;
            case MotionMode.TiltLeftRight:
                transform.SetPositionAndRotation(startPosition, startRotation * Quaternion.Euler(0f, 0f, tiltAngle));
                break;
            case MotionMode.OscillatingTilt:
                transform.SetPositionAndRotation(startPosition, startRotation * Quaternion.Euler(0f, 0f, wave * tiltAngle));
                break;
            case MotionMode.HorizontalShake:
                transform.SetPositionAndRotation(startPosition + Vector3.right * (wave * motionAmplitude), startRotation);
                break;
            case MotionMode.CircularMotion:
                Vector3 offset = new Vector3(Mathf.Cos(motionTime * motionFrequency), 0f, Mathf.Sin(motionTime * motionFrequency)) * motionAmplitude;
                transform.SetPositionAndRotation(startPosition + offset, startRotation * Quaternion.Euler(wave * tiltAngle * 0.4f, 0f, wave * tiltAngle));
                break;
            case MotionMode.ManualTestMotion:
                transform.SetPositionAndRotation(startPosition + manualOffset, startRotation * Quaternion.Euler(manualEulerAngles));
                break;
        }
    }

    public void ToggleMotion()
    {
        motionEnabled = !motionEnabled;
    }

    public void CycleMotionMode()
    {
        int count = System.Enum.GetValues(typeof(MotionMode)).Length;
        motionMode = (MotionMode)(((int)motionMode + 1) % count);
    }

    public void ResetMotion()
    {
        motionTime = 0f;
        transform.SetPositionAndRotation(startPosition, startRotation);
    }
}
