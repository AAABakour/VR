using UnityEngine;

public class CanvasPainter : MonoBehaviour
{
    public enum CanvasSurfaceType
    {
        Paper,
        Canvas,
        Wood,
        Metal
    }

    [Header("Canvas Texture Settings")]
    public int textureSize = 1024;
    public Color backgroundColor = new Color(1f, 0.72f, 0.25f, 1f);

    [Header("Surface Type")]
    public CanvasSurfaceType surfaceType = CanvasSurfaceType.Canvas;

    [Header("Coordinate Mapping")]
    public bool invertX = false;
    public bool invertZ = false;

    [Header("Impact Shape")]
    [Range(0f, 1f)]
    public float edgeIrregularity = 0.5f;

    [Range(0f, 1f)]
    public float paintOpacity = 0.85f;

    [Header("Spray")]
    [Range(0f, 3f)]
    public float sprayAmount = 0.45f;

    [Range(1f, 4f)]
    public float spraySpread = 2.0f;

    [Header("Directional Smear")]
    public bool enableDirectionalSmear = true;

    [Range(0f, 3f)]
    public float smearLength = 1.1f;

    [Range(1, 12)]
    public int smearSteps = 5;

    [Header("Wet Paint Relief Shading")]
    public bool enableWetPaintShading = true;

    [Range(0f, 0.35f)]
    public float wetCenterDarkening = 0.10f;

    [Range(0f, 0.45f)]
    public float raisedRimHighlight = 0.18f;

    [Range(0f, 0.18f)]
    public float pigmentVariation = 0.05f;

    [Range(0f, 1f)]
    public float glossyWetAlphaBoost = 0.18f;

    [Header("Real Fluid Film Texture Rendering")]
    public bool renderFluidFilmFromSurfaceState = true;
    public PaintSurfaceStateV2 fluidSurfaceState;
    public float fluidTextureUpdateInterval = 0.04f;
    public Color thinWetPaintColor = new Color(1f, 0.05f, 0.01f, 1f);
    public Color thickWetPaintColor = new Color(0.55f, 0.0f, 0.0f, 1f);
    public float fluidThicknessVisibility = 0.55f;
    public float fluidWetAlpha = 0.92f;
    public float fluidDryAlpha = 0.42f;
    public float fluidSpecularStrength = 0.18f;
    public float fluidNormalStrength = 3.2f;
    public bool generateFluidNormalMap = true;

    private Texture2D canvasTexture;
    private Renderer canvasRenderer;
    private Color32[] pixelBuffer;
    private Color32[] stainBuffer;
    private Texture2D normalTexture;
    private Color32[] normalBuffer;
    private bool textureDirty;
    private bool normalTextureDirty;
    private float fluidRenderTimer;
    private int lastFluidRevision = -1;

    private struct SurfaceProfile
    {
        public float radiusMultiplier;
        public float opacityMultiplier;
        public float edgeMultiplier;
        public float sprayMultiplier;
        public float spraySpreadMultiplier;
        public float smearMultiplier;
    }

    void Start()
    {
        InitializeCanvas();
    }

    void LateUpdate()
    {
        UpdateFluidCompositeIfNeeded();
        ApplyTextureIfDirty();
    }

    private void InitializeCanvas()
    {
        canvasRenderer = GetComponent<Renderer>();

        canvasTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        canvasTexture.wrapMode = TextureWrapMode.Clamp;

        pixelBuffer = new Color32[textureSize * textureSize];
        stainBuffer = new Color32[textureSize * textureSize];
        normalBuffer = new Color32[textureSize * textureSize];

        FillPixelBuffer(backgroundColor);
        FillNeutralNormalBuffer();

        canvasTexture.SetPixels32(pixelBuffer);
        canvasTexture.Apply(false);

        normalTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, true);
        normalTexture.wrapMode = TextureWrapMode.Clamp;
        normalTexture.SetPixels32(normalBuffer);
        normalTexture.Apply(false);

        if (fluidSurfaceState == null)
        {
            fluidSurfaceState = UnityEngine.Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        }

        Material runtimeMaterial = canvasRenderer.material;

        if (runtimeMaterial.HasProperty("_BaseMap"))
        {
            runtimeMaterial.SetTexture("_BaseMap", canvasTexture);
        }

        if (runtimeMaterial.HasProperty("_Smoothness"))
        {
            runtimeMaterial.SetFloat("_Smoothness", 0.42f);
        }

        if (runtimeMaterial.HasProperty("_Metallic"))
        {
            runtimeMaterial.SetFloat("_Metallic", 0f);
        }

        runtimeMaterial.mainTexture = canvasTexture;

        if (runtimeMaterial.HasProperty("_BumpMap"))
        {
            runtimeMaterial.SetTexture("_BumpMap", normalTexture);
            runtimeMaterial.EnableKeyword("_NORMALMAP");
        }

        if (runtimeMaterial.HasProperty("_NormalMap"))
        {
            runtimeMaterial.SetTexture("_NormalMap", normalTexture);
        }

        if (runtimeMaterial.HasProperty("_BumpScale"))
        {
            runtimeMaterial.SetFloat("_BumpScale", 0.85f);
        }
    }

    private void ApplyTextureIfDirty()
    {
        if (!textureDirty || canvasTexture == null || pixelBuffer == null)
        {
            return;
        }

        canvasTexture.SetPixels32(pixelBuffer);
        canvasTexture.Apply(false);

        if (normalTextureDirty && normalTexture != null && normalBuffer != null)
        {
            normalTexture.SetPixels32(normalBuffer);
            normalTexture.Apply(false);
            normalTextureDirty = false;
        }

        textureDirty = false;
    }

    public void ClearCanvas()
    {
        if (pixelBuffer == null || canvasTexture == null)
        {
            return;
        }

        FillPixelBuffer(backgroundColor);
        FillNeutralNormalBuffer();

        canvasTexture.SetPixels32(pixelBuffer);
        canvasTexture.Apply(false);

        if (normalTexture != null && normalBuffer != null)
        {
            normalTexture.SetPixels32(normalBuffer);
            normalTexture.Apply(false);
        }

        textureDirty = false;
        normalTextureDirty = false;
        lastFluidRevision = -1;
    }

    private void FillPixelBuffer(Color color)
    {
        Color32 color32 = color;

        for (int i = 0; i < pixelBuffer.Length; i++)
        {
            pixelBuffer[i] = color32;

            if (stainBuffer != null && i < stainBuffer.Length)
            {
                stainBuffer[i] = color32;
            }
        }
    }

    private void FillNeutralNormalBuffer()
    {
        if (normalBuffer == null)
        {
            return;
        }

        Color32 neutral = new Color32(128, 128, 255, 255);

        for (int i = 0; i < normalBuffer.Length; i++)
        {
            normalBuffer[i] = neutral;
        }
    }

    public void PaintAtWorldPosition(Vector3 worldPosition, Color paintColor, float brushRadiusWorld)
    {
        PaintImpactAtWorldPosition(worldPosition, Vector3.zero, paintColor, brushRadiusWorld);
    }

    public void PaintImpactAtWorldPosition(
        Vector3 worldPosition,
        Vector3 impactVelocity,
        Color paintColor,
        float brushRadiusWorld
    )
    {
        if (pixelBuffer == null)
        {
            return;
        }

        if (!WorldToPixel(worldPosition, out int centerX, out int centerY))
        {
            return;
        }

        SurfaceProfile profile = GetSurfaceProfile();

        float largestCanvasSide = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

        int radiusPixels = Mathf.RoundToInt(
            (brushRadiusWorld / largestCanvasSide) * textureSize * profile.radiusMultiplier
        );

        radiusPixels = Mathf.Max(radiusPixels, 1);

        float effectiveOpacity = Mathf.Clamp01(paintOpacity * profile.opacityMultiplier);
        float effectiveEdge = edgeIrregularity * profile.edgeMultiplier;

        Color32 paintColor32 = paintColor;

        DrawIrregularSplat(centerX, centerY, radiusPixels, paintColor32, effectiveOpacity, effectiveEdge);

        if (enableDirectionalSmear)
        {
            Vector2 smearDirection = GetSmearDirection(impactVelocity);

            if (smearDirection.sqrMagnitude > 0.001f)
            {
                float effectiveSmearLength = smearLength * profile.smearMultiplier;

                DrawDirectionalSmear(
                    centerX,
                    centerY,
                    radiusPixels,
                    smearDirection,
                    paintColor32,
                    effectiveOpacity,
                    effectiveEdge,
                    effectiveSmearLength
                );
            }
        }

        float effectiveSprayAmount = sprayAmount * profile.sprayMultiplier;
        float effectiveSpraySpread = spraySpread * profile.spraySpreadMultiplier;

        DrawSpray(centerX, centerY, radiusPixels, paintColor32, effectiveSprayAmount, effectiveSpraySpread);

        textureDirty = true;
    }

    private SurfaceProfile GetSurfaceProfile()
    {
        SurfaceProfile profile = new SurfaceProfile();

        switch (surfaceType)
        {
            case CanvasSurfaceType.Paper:
                profile.radiusMultiplier = 0.85f;
                profile.opacityMultiplier = 0.75f;
                profile.edgeMultiplier = 1.2f;
                profile.sprayMultiplier = 0.45f;
                profile.spraySpreadMultiplier = 0.9f;
                profile.smearMultiplier = 0.45f;
                break;

            case CanvasSurfaceType.Canvas:
                profile.radiusMultiplier = 1.0f;
                profile.opacityMultiplier = 0.9f;
                profile.edgeMultiplier = 1.45f;
                profile.sprayMultiplier = 0.75f;
                profile.spraySpreadMultiplier = 1.15f;
                profile.smearMultiplier = 0.8f;
                break;

            case CanvasSurfaceType.Wood:
                profile.radiusMultiplier = 0.9f;
                profile.opacityMultiplier = 0.85f;
                profile.edgeMultiplier = 1.7f;
                profile.sprayMultiplier = 0.55f;
                profile.spraySpreadMultiplier = 1.0f;
                profile.smearMultiplier = 0.65f;
                break;

            case CanvasSurfaceType.Metal:
                profile.radiusMultiplier = 1.25f;
                profile.opacityMultiplier = 1.0f;
                profile.edgeMultiplier = 0.8f;
                profile.sprayMultiplier = 1.15f;
                profile.spraySpreadMultiplier = 1.5f;
                profile.smearMultiplier = 1.8f;
                break;
        }

        return profile;
    }

    private bool WorldToPixel(Vector3 worldPosition, out int pixelX, out int pixelY)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPosition);

        float u = invertX ? 0.5f - localPoint.x : localPoint.x + 0.5f;
        float v = invertZ ? 0.5f - localPoint.z : localPoint.z + 0.5f;

        pixelX = 0;
        pixelY = 0;

        if (u < 0f || u > 1f || v < 0f || v > 1f)
        {
            return false;
        }

        pixelX = Mathf.RoundToInt(u * (textureSize - 1));
        pixelY = Mathf.RoundToInt(v * (textureSize - 1));

        return true;
    }

    private Vector2 GetSmearDirection(Vector3 impactVelocity)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(impactVelocity);
        Vector2 direction = new Vector2(localVelocity.x, localVelocity.z);

        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        return direction.normalized;
    }

    private void DrawDirectionalSmear(
        int centerX,
        int centerY,
        int radius,
        Vector2 direction,
        Color32 paintColor,
        float baseOpacity,
        float edgeAmount,
        float effectiveSmearLength
    )
    {
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);

        for (int i = 1; i <= smearSteps; i++)
        {
            float t = i / (float)smearSteps;

            float distance = radius * effectiveSmearLength * t;
            float randomSideShift = Random.Range(-radius * 0.25f, radius * 0.25f);

            int smearX = centerX + Mathf.RoundToInt(direction.x * distance + perpendicular.x * randomSideShift);
            int smearY = centerY + Mathf.RoundToInt(direction.y * distance + perpendicular.y * randomSideShift);

            int smearRadius = Mathf.RoundToInt(radius * Mathf.Lerp(0.85f, 0.25f, t));
            smearRadius = Mathf.Max(smearRadius, 1);

            float smearOpacity = Mathf.Lerp(baseOpacity * 0.55f, baseOpacity * 0.12f, t);

            DrawIrregularSplat(smearX, smearY, smearRadius, paintColor, smearOpacity, edgeAmount);
        }
    }

    private void DrawIrregularSplat(
        int centerX,
        int centerY,
        int radius,
        Color32 paintColor,
        float opacity,
        float edgeAmount
    )
    {
        int seed = Random.Range(0, 10000);

        int minX = Mathf.Max(centerX - radius, 0);
        int maxX = Mathf.Min(centerX + radius, textureSize - 1);
        int minY = Mathf.Max(centerY - radius, 0);
        int maxY = Mathf.Min(centerY + radius, textureSize - 1);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - centerY;

            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;

                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float noise = Mathf.PerlinNoise(
                    (x + seed) * 0.08f,
                    (y + seed) * 0.08f
                );

                float irregularRadius = radius * (1f + (noise - 0.5f) * edgeAmount);

                if (distance > irregularRadius)
                {
                    continue;
                }

                float normalizedDistance = distance / Mathf.Max(irregularRadius, 0.0001f);
                float softness = 1f - normalizedDistance;

                float alpha = Mathf.Clamp01(softness * opacity);

                Color32 shadedColor = enableWetPaintShading
                    ? BuildWetPaintShade(paintColor, normalizedDistance, noise, alpha)
                    : paintColor;

                BlendPixel(x, y, shadedColor, alpha);
            }
        }
    }

    private void DrawSpray(
        int centerX,
        int centerY,
        int radius,
        Color32 paintColor,
        float effectiveSprayAmount,
        float effectiveSpraySpread
    )
    {
        int sprayDots = Mathf.RoundToInt(radius * effectiveSprayAmount);

        for (int i = 0; i < sprayDots; i++)
        {
            Vector2 direction = Random.insideUnitCircle;

            if (direction.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            direction.Normalize();

            float distance = Random.Range(radius * 0.9f, radius * effectiveSpraySpread);

            int sprayX = centerX + Mathf.RoundToInt(direction.x * distance);
            int sprayY = centerY + Mathf.RoundToInt(direction.y * distance);

            int dotRadius = Random.Range(1, Mathf.Max(2, radius / 7));

            DrawSmallDot(sprayX, sprayY, dotRadius, paintColor, Random.Range(0.12f, 0.35f));
        }
    }

    private void DrawSmallDot(
        int centerX,
        int centerY,
        int radius,
        Color32 paintColor,
        float opacity
    )
    {
        int minX = Mathf.Max(centerX - radius, 0);
        int maxX = Mathf.Min(centerX + radius, textureSize - 1);
        int minY = Mathf.Max(centerY - radius, 0);
        int maxY = Mathf.Min(centerY + radius, textureSize - 1);

        int radiusSquared = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - centerY;

            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                int distanceSquared = dx * dx + dy * dy;

                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared) / Mathf.Max(radius, 1);
                float alpha = Mathf.Clamp01((1f - distance) * opacity);

                Color32 shadedColor = enableWetPaintShading
                    ? BuildWetPaintShade(paintColor, distance, Random.value, alpha * 0.65f)
                    : paintColor;

                BlendPixel(x, y, shadedColor, alpha);
            }
        }
    }

    private Color32 BuildWetPaintShade(
        Color32 paintColor,
        float normalizedDistance,
        float noise,
        float alpha01
    )
    {
        Color color = paintColor;

        float center = Mathf.Clamp01(1f - normalizedDistance);
        float rim = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.58f, 0.95f, normalizedDistance));
        float noiseVariation = (noise - 0.5f) * pigmentVariation;

        float brightness = 1f;
        brightness -= wetCenterDarkening * center * Mathf.Clamp01(alpha01 + 0.25f);
        brightness += raisedRimHighlight * rim * Mathf.Clamp01(alpha01 + 0.15f);
        brightness += noiseVariation;

        color.r = Mathf.Clamp01(color.r * brightness);
        color.g = Mathf.Clamp01(color.g * brightness);
        color.b = Mathf.Clamp01(color.b * brightness);
        color.a = Mathf.Clamp01(color.a + glossyWetAlphaBoost * alpha01);

        return color;
    }

    private void BlendPixel(int x, int y, Color32 paintColor, float alpha01)
    {
        if (alpha01 <= 0f)
        {
            return;
        }

        int index = y * textureSize + x;

        Color32[] targetBuffer = stainBuffer != null ? stainBuffer : pixelBuffer;
        Color32 current = targetBuffer[index];

        int colorAlpha = paintColor.a;
        int alpha = Mathf.Clamp(Mathf.RoundToInt(alpha01 * colorAlpha), 0, 255);
        int inverseAlpha = 255 - alpha;

        byte r = (byte)((current.r * inverseAlpha + paintColor.r * alpha) / 255);
        byte g = (byte)((current.g * inverseAlpha + paintColor.g * alpha) / 255);
        byte b = (byte)((current.b * inverseAlpha + paintColor.b * alpha) / 255);

        targetBuffer[index] = new Color32(r, g, b, 255);
    }


    private void UpdateFluidCompositeIfNeeded()
    {
        if (!renderFluidFilmFromSurfaceState || fluidSurfaceState == null || !fluidSurfaceState.HasValidMaps)
        {
            if (textureDirty && stainBuffer != null && pixelBuffer != null)
            {
                System.Array.Copy(stainBuffer, pixelBuffer, pixelBuffer.Length);
            }

            return;
        }

        fluidRenderTimer += Time.deltaTime;

        bool revisionChanged = fluidSurfaceState.dataRevision != lastFluidRevision;
        bool intervalElapsed = fluidRenderTimer >= fluidTextureUpdateInterval;

        if (!revisionChanged && !textureDirty && !intervalElapsed)
        {
            return;
        }

        fluidRenderTimer = 0f;
        lastFluidRevision = fluidSurfaceState.dataRevision;

        ComposeFluidFilmTexture();
        textureDirty = true;
        normalTextureDirty = generateFluidNormalMap;
    }

    private void ComposeFluidFilmTexture()
    {
        if (pixelBuffer == null || stainBuffer == null || fluidSurfaceState == null)
        {
            return;
        }

        float[] thickness = fluidSurfaceState.ThicknessMapRaw;
        float[] wetness = fluidSurfaceState.WetnessMapRaw;
        int resolution = fluidSurfaceState.Resolution;

        if (thickness == null || wetness == null || resolution <= 1)
        {
            System.Array.Copy(stainBuffer, pixelBuffer, pixelBuffer.Length);
            return;
        }

        Vector2 lightDirection = new Vector2(-0.45f, 0.75f).normalized;

        for (int py = 0; py < textureSize; py++)
        {
            float v = py / (float)(textureSize - 1);
            float sampleV = invertZ ? 1f - v : v;
            int sy = Mathf.Clamp(Mathf.RoundToInt(sampleV * (resolution - 1)), 0, resolution - 1);

            for (int px = 0; px < textureSize; px++)
            {
                int pixelIndex = py * textureSize + px;
                Color32 baseColor = stainBuffer[pixelIndex];

                float u = px / (float)(textureSize - 1);
                float sampleU = invertX ? 1f - u : u;
                int sx = Mathf.Clamp(Mathf.RoundToInt(sampleU * (resolution - 1)), 0, resolution - 1);
                int mapIndex = sy * resolution + sx;

                float h = Mathf.Max(0f, thickness[mapIndex]);
                float w = Mathf.Clamp01(wetness[mapIndex]);

                if (h <= 0.0001f && w <= 0.001f)
                {
                    pixelBuffer[pixelIndex] = baseColor;

                    if (generateFluidNormalMap && normalBuffer != null)
                    {
                        normalBuffer[pixelIndex] = new Color32(128, 128, 255, 255);
                    }

                    continue;
                }

                float h01 = 1f - Mathf.Exp(-h * fluidThicknessVisibility);
                float alpha = Mathf.Lerp(fluidDryAlpha, fluidWetAlpha, w) * h01;
                alpha = Mathf.Clamp01(alpha);

                float left = thickness[sy * resolution + Mathf.Max(0, sx - 1)];
                float right = thickness[sy * resolution + Mathf.Min(resolution - 1, sx + 1)];
                float down = thickness[Mathf.Max(0, sy - 1) * resolution + sx];
                float up = thickness[Mathf.Min(resolution - 1, sy + 1) * resolution + sx];

                Vector2 gradient = new Vector2(right - left, up - down);
                float slope = Mathf.Clamp01(gradient.magnitude * 0.45f);
                float shine = Mathf.Pow(Mathf.Clamp01(Vector2.Dot(-gradient.normalized, lightDirection) * 0.5f + 0.5f), 10f);
                shine *= w * fluidSpecularStrength * Mathf.Clamp01(h01 + 0.25f);

                Color fluidColor = Color.Lerp(thinWetPaintColor, thickWetPaintColor, Mathf.Clamp01(h01 * 1.35f));
                fluidColor.r = Mathf.Clamp01(fluidColor.r + shine + slope * 0.035f);
                fluidColor.g = Mathf.Clamp01(fluidColor.g + shine * 0.45f);
                fluidColor.b = Mathf.Clamp01(fluidColor.b + shine * 0.35f);

                pixelBuffer[pixelIndex] = AlphaBlend(baseColor, fluidColor, alpha);

                if (generateFluidNormalMap && normalBuffer != null)
                {
                    Vector3 normal = new Vector3(
                        -gradient.x * fluidNormalStrength,
                        -gradient.y * fluidNormalStrength,
                        1f
                    ).normalized;

                    normalBuffer[pixelIndex] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt((normal.x * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((normal.y * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((normal.z * 0.5f + 0.5f) * 255f), 0, 255),
                        255
                    );
                }
            }
        }
    }

    private Color32 AlphaBlend(Color32 baseColor, Color overlayColor, float alpha)
    {
        alpha = Mathf.Clamp01(alpha * overlayColor.a);
        float inverse = 1f - alpha;

        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(baseColor.r * inverse + overlayColor.r * 255f * alpha), 0, 255);
        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(baseColor.g * inverse + overlayColor.g * 255f * alpha), 0, 255);
        byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(baseColor.b * inverse + overlayColor.b * 255f * alpha), 0, 255);

        return new Color32(r, g, b, 255);
    }

    public Texture2D GetCanvasTexture()
    {
        return canvasTexture;
    }

    public void ForceApplyTexture()
    {
        ApplyTextureIfDirty();
    }

    public int CountPaintedPixels(float threshold = 0.03f)
    {
        if (pixelBuffer == null)
        {
            return 0;
        }

        Color32 background = backgroundColor;
        int paintedPixels = 0;
        int thresholdValue = Mathf.RoundToInt(threshold * 765f);

        for (int i = 0; i < pixelBuffer.Length; i++)
        {
            Color32 pixel = pixelBuffer[i];

            int difference =
                Mathf.Abs(pixel.r - background.r) +
                Mathf.Abs(pixel.g - background.g) +
                Mathf.Abs(pixel.b - background.b);

            if (difference > thresholdValue)
            {
                paintedPixels++;
            }
        }

        return paintedPixels;
    }

    public float GetPaintCoverage01(float threshold = 0.03f)
    {
        if (pixelBuffer == null || pixelBuffer.Length == 0)
        {
            return 0f;
        }

        return CountPaintedPixels(threshold) / (float)pixelBuffer.Length;
    }

    public float GetApproxPaintedArea(float threshold = 0.03f)
    {
        float canvasArea = transform.lossyScale.x * transform.lossyScale.z;
        return GetPaintCoverage01(threshold) * canvasArea;
    }

    public void ResetCanvas()
    {
        ClearCanvas();
    }
}