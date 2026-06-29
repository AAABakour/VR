using UnityEngine;

public static class GpuSphBufferUtility
{
    public const int ParticleStrideBytes = 48;
    public const int ForceStrideBytes = 12;
    public const int IntStrideBytes = 4;
    public const int IndirectArgsStrideBytes = 4;

    public static void ReleaseBuffer(ref ComputeBuffer buffer)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Release();
        buffer = null;
    }

    public static int DispatchGroups(int count, int threadGroupSize)
    {
        return Mathf.Max(1, Mathf.CeilToInt(count / (float)Mathf.Max(1, threadGroupSize)));
    }

    public static float BytesToMegabytes(long bytes)
    {
        return bytes / (1024f * 1024f);
    }
}
