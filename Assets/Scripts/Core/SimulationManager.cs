using UnityEngine;
using UnityEngine.InputSystem;

public class SimulationManager : MonoBehaviour
{
    [Header("Simulation References")]
    public PendulumController pendulumController;
    public PaintEmitter paintEmitter;
    public CanvasPainter canvasPainter;

    [Header("Keyboard Controls")]
    public Key resetKey = Key.R;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[resetKey].wasPressedThisFrame)
        {
            ResetSimulation();
        }
    }

    public void ResetSimulation()
    {
        if (canvasPainter != null)
        {
            canvasPainter.ResetCanvas();
        }

        if (pendulumController != null)
        {
            pendulumController.ResetSimulation();
        }

        if (paintEmitter != null)
        {
            paintEmitter.ResetEmitter();
        }
    }
}