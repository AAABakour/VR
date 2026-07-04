using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[DefaultExecutionOrder(900)]
public class ModernSimulationGameUI : MonoBehaviour
{
    private const string UiRootName = "ModernSimulationGameUI_Runtime";

    [Header("Auto Bootstrap")]
    public bool hideLegacyUI = true;
    public bool showOnStart = true;

    [Header("References")]
    public SimulationManager simulationManager;
    public SimulationPresetApplier presetApplier;
    public PaintEmitter paintEmitter;
    public PaintParticleSimulator particleSimulator;
    public CanvasPainter canvasPainter;
    public PaintImpactEngineV2 impactEngine;
    public PaintSurfaceStateV2 surfaceState;
    public PaintFilmFluidSolverV2 fluidFilmSolver;
    public PaintDripSolverV2 dripSolver;
    public BucketInteriorLiquidSystemV2 bucketLiquid;
    public RigRopeController ropeController;
    public PendulumController pendulumController;
    public ExperimentExporter exporter;

    private Canvas canvas;
    private GameObject contentRoot;
    private TextMeshProUGUI topTitle;
    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI helpText;
    private TextMeshProUGUI statusLineText;
    private TextMeshProUGUI gameStateText;
    private TextMeshProUGUI fpsValue;
    private TextMeshProUGUI paintValue;
    private TextMeshProUGUI particleValue;
    private TextMeshProUGUI fluidValue;
    private TextMeshProUGUI ropeValue;
    private TextMeshProUGUI impactValue;
    private TextMeshProUGUI bucketLiquidValue;
    private Slider paintFillSlider;

    private readonly List<SliderBinding> sliderBindings = new List<SliderBinding>();
    private readonly List<ToggleBinding> toggleBindings = new List<ToggleBinding>();
    private readonly List<DropdownBinding> dropdownBindings = new List<DropdownBinding>();

    private Sprite panelSprite;
    private Sprite buttonSprite;
    private Sprite buttonHoverSprite;
    private Sprite sliderBackgroundSprite;
    private Sprite sliderFillSprite;
    private Sprite handleSprite;

    private float fpsTimer;
    private int fpsFrames;
    private float displayedFps;
    private float refreshTimer;
    private bool isPaused;
    private bool isHidden;
    private bool hasBuilt;
    private float nextSafeButtonTime;
    private string lastUiMessage = "";

    private static readonly Color DarkBackground = new Color(0.025f, 0.032f, 0.050f, 0.82f);
    private static readonly Color PanelColor = new Color(0.055f, 0.068f, 0.095f, 0.90f);
    private static readonly Color PanelColorSoft = new Color(0.075f, 0.090f, 0.125f, 0.86f);
    private static readonly Color AccentCyan = new Color(0.10f, 0.78f, 1.00f, 1f);
    private static readonly Color AccentBlue = new Color(0.15f, 0.38f, 1.00f, 1f);
    private static readonly Color AccentGreen = new Color(0.20f, 1.00f, 0.62f, 1f);
    private static readonly Color AccentOrange = new Color(1.00f, 0.55f, 0.18f, 1f);
    private static readonly Color AccentRed = new Color(1.00f, 0.18f, 0.16f, 1f);
    private static readonly Color TextMain = new Color(0.92f, 0.96f, 1f, 1f);
    private static readonly Color TextMuted = new Color(0.63f, 0.70f, 0.80f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<ModernSimulationGameUI>() != null)
        {
            return;
        }

        GameObject root = new GameObject(UiRootName);
        root.AddComponent<ModernSimulationGameUI>();
    }

    private void Awake()
    {
        if (Object.FindObjectsByType<ModernSimulationGameUI>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        AutoFindReferences();
        EnsureEventSystem();
    }

    private void Start()
    {
        BuildUI();
        SetVisible(showOnStart);
    }

    private void Update()
    {
        AutoFindReferences();
        HandleHotkeys();
        UpdateFpsCounter();
        RefreshBindings();
        RefreshDashboard(false);
    }

    private void AutoFindReferences()
    {
        if (simulationManager == null) simulationManager = Object.FindFirstObjectByType<SimulationManager>();
        if (presetApplier == null) presetApplier = Object.FindFirstObjectByType<SimulationPresetApplier>();
        if (paintEmitter == null) paintEmitter = Object.FindFirstObjectByType<PaintEmitter>();
        if (particleSimulator == null) particleSimulator = Object.FindFirstObjectByType<PaintParticleSimulator>();
        if (canvasPainter == null) canvasPainter = Object.FindFirstObjectByType<CanvasPainter>();
        if (impactEngine == null) impactEngine = Object.FindFirstObjectByType<PaintImpactEngineV2>();
        if (surfaceState == null) surfaceState = Object.FindFirstObjectByType<PaintSurfaceStateV2>();
        if (fluidFilmSolver == null) fluidFilmSolver = Object.FindFirstObjectByType<PaintFilmFluidSolverV2>();
        if (dripSolver == null) dripSolver = Object.FindFirstObjectByType<PaintDripSolverV2>();
        if (bucketLiquid == null) bucketLiquid = Object.FindFirstObjectByType<BucketInteriorLiquidSystemV2>();
        if (ropeController == null) ropeController = Object.FindFirstObjectByType<RigRopeController>();
        if (pendulumController == null) pendulumController = Object.FindFirstObjectByType<PendulumController>();
        if (exporter == null) exporter = Object.FindFirstObjectByType<ExperimentExporter>();
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            return;
        }

        if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void HideOldUIIfNeeded()
    {
        if (!hideLegacyUI)
        {
            return;
        }

        string[] legacyNames = new string[]
        {
            "MainUI",
            "SimulationStats",
            "RopeTypeUI",
            "PresetButtons",
            "StatsPanel"
        };

        for (int i = 0; i < legacyNames.Length; i++)
        {
            GameObject legacy = GameObject.Find(legacyNames[i]);
            if (legacy != null && legacy.name != UiRootName)
            {
                legacy.SetActive(false);
            }
        }
    }

    private void BuildUI()
    {
        if (hasBuilt)
        {
            return;
        }

        hasBuilt = true;
        HideOldUIIfNeeded();
        CreateThemeSprites();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        contentRoot = CreateUIObject("HUD_Content", transform);
        RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
        Stretch(contentRect);

        CreateTopBar(contentRect);
        CreateLeftDashboard(contentRect);
        CreateRightControlDeck(contentRect);
        CreateBottomActionBar(contentRect);
        CreateHelpPill(contentRect);

        RefreshDashboard(true);
    }

    private void CreateTopBar(RectTransform parent)
    {
        RectTransform bar = CreatePanel("Top Command Bar", parent, new Color(0.02f, 0.025f, 0.04f, 0.92f));
        AnchorTop(bar, 24f, 24f, 24f, 90f);

        HorizontalLayoutGroup layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 14, 14);
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        RectTransform titleBlock = CreateUIObject("Title Block", bar).GetComponent<RectTransform>();
        LayoutElement titleLayout = titleBlock.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredWidth = 460f;
        titleLayout.flexibleWidth = 1f;

        VerticalLayoutGroup titleStack = titleBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        titleStack.childAlignment = TextAnchor.MiddleLeft;
        titleStack.childForceExpandHeight = false;
        titleStack.spacing = 1;

        topTitle = CreateText("SWINGING PAINT BUCKET", titleBlock, 28, FontStyles.Bold, TextMain, TextAlignmentOptions.Left);
        subtitleText = CreateText("interactive fluid + rope simulation cockpit", titleBlock, 15, FontStyles.UpperCase, TextMuted, TextAlignmentOptions.Left);

        gameStateText = CreateStatusChip("RUNNING", bar, AccentGreen, 150f);
        fpsValue = CreateStatusChip("FPS --", bar, AccentCyan, 120f);
        paintValue = CreateStatusChip("PAINT --", bar, AccentOrange, 150f);
        fluidValue = CreateStatusChip("FILM --", bar, AccentBlue, 150f);
        ropeValue = CreateStatusChip("ROPE --", bar, AccentCyan, 175f);

        CreateButton("HIDE HUD  H", bar, ToggleHud, 150f, 44f, new Color(0.10f, 0.12f, 0.16f, 0.96f));
    }

    private void CreateLeftDashboard(RectTransform parent)
    {
        RectTransform panel = CreatePanel("Left Telemetry", parent, PanelColor);
        AnchorLeft(panel, 24f, 126f, 360f, 666f);

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 12;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        CreateHeader(panel, "LIVE TELEMETRY", "real-time simulation readout");

        paintFillSlider = CreateReadOnlyMeter(panel, "Paint Tank");

        bucketLiquidValue = CreateMetricCard(panel, "BUCKET LIQUID", "--", AccentBlue);
        particleValue = CreateMetricCard(panel, "PARTICLES", "--", AccentCyan);
        impactValue = CreateMetricCard(panel, "IMPACT", "--", AccentOrange);
        statusLineText = CreateMetricCard(panel, "SURFACE FILM", "--", AccentGreen);

        RectTransform miniControls = CreateUIObject("Mini Controls", panel).GetComponent<RectTransform>();
        GridLayoutGroup grid = miniControls.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150, 42);
        grid.spacing = new Vector2(10, 10);
        LayoutElement gridLayout = miniControls.gameObject.AddComponent<LayoutElement>();
        gridLayout.preferredHeight = 96;

        CreateButton("PAUSE", miniControls, TogglePause, 150, 42, AccentBlue);
        CreateButton("RESET", miniControls, ResetSimulation, 150, 42, AccentRed);
        CreateButton("CLEAR", miniControls, ClearOnlyCanvas, 150, 42, new Color(0.58f, 0.23f, 1f, 1f));
        CreateButton("REFILL", miniControls, RefillPaintOnly, 150, 42, AccentGreen);
    }

    private void CreateRightControlDeck(RectTransform parent)
    {
        RectTransform panel = CreatePanel("Right Control Deck", parent, new Color(0.035f, 0.043f, 0.062f, 0.94f));
        AnchorRight(panel, 24f, 126f, 470f, 820f);

        VerticalLayoutGroup outerLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        outerLayout.padding = new RectOffset(18, 18, 18, 18);
        outerLayout.spacing = 12;
        outerLayout.childControlWidth = true;
        outerLayout.childControlHeight = true;

        CreateHeader(panel, "GAME CONTROL DECK", "tune physics, paint, fluid film and visuals live");

        RectTransform scrollRoot = CreateUIObject("Control Scroll", panel).GetComponent<RectTransform>();
        LayoutElement scrollLayout = scrollRoot.gameObject.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1;
        scrollLayout.preferredHeight = 680;

        Image scrollBackground = scrollRoot.gameObject.AddComponent<Image>();
        scrollBackground.color = new Color(0f, 0f, 0f, 0.05f);

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 36f;

        RectTransform viewport = CreateUIObject("Viewport", scrollRoot).GetComponent<RectTransform>();
        Stretch(viewport);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scroll.viewport = viewport;

        RectTransform content = CreateUIObject("Controls", viewport).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 1600f);
        scroll.content = content;

        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(0, 8, 0, 0);
        contentLayout.spacing = 10;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreatePresetSection(content);
        CreatePaintSourceSection(content);
        CreateBucketInteriorSection(content);
        CreateParticleSection(content);
        CreateFluidFilmSection(content);
        CreateCanvasVisualSection(content);
        CreateMotionSection(content);
        CreateRopeSection(content);
        CreateExportSection(content);
    }

    private void CreateBottomActionBar(RectTransform parent)
    {
        RectTransform bar = CreatePanel("Bottom Action Bar", parent, new Color(0.02f, 0.025f, 0.035f, 0.90f));
        AnchorBottom(bar, 24f, 24f, 24f, 92f);

        HorizontalLayoutGroup layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 14, 14);
        layout.spacing = 12;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        CreateButton("1  CLEAN", bar, () => ApplyPreset(0), 1, 52, AccentCyan, true);
        CreateButton("2  SPLASHY", bar, () => ApplyPreset(1), 1, 52, AccentOrange, true);
        CreateButton("3  DENSE", bar, () => ApplyPreset(2), 1, 52, AccentGreen, true);
        CreateButton("4  DOUBLE", bar, () => ApplyPreset(3), 1, 52, new Color(0.73f, 0.30f, 1f, 1f), true);
        CreateButton("5  METAL", bar, () => ApplyPreset(4), 1, 52, new Color(0.58f, 0.62f, 0.70f, 1f), true);
        CreateButton("SPACE  PAUSE", bar, TogglePause, 1, 52, AccentBlue, true);
        CreateButton("R  RESET", bar, ResetSimulation, 1, 52, AccentRed, true);
    }

    private void CreateHelpPill(RectTransform parent)
    {
        RectTransform pill = CreatePanel("Help Pill", parent, new Color(0.015f, 0.018f, 0.028f, 0.84f));
        pill.anchorMin = new Vector2(0.5f, 0f);
        pill.anchorMax = new Vector2(0.5f, 0f);
        pill.pivot = new Vector2(0.5f, 0f);
        pill.sizeDelta = new Vector2(600f, 38f);
        pill.anchoredPosition = new Vector2(0f, 124f);

        helpText = CreateText("Shortcuts: 1-5 presets   Space pause   R reset   F fluid film   H hide/show HUD", pill, 15, FontStyles.Normal, TextMuted, TextAlignmentOptions.Center);
        Stretch(helpText.rectTransform);
    }

    private void CreatePresetSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "SCENE MODES", "game presets for different physical behaviors");
        RectTransform gridRoot = CreateUIObject("Preset Grid", section).GetComponent<RectTransform>();
        LayoutElement gridRootLayout = gridRoot.gameObject.AddComponent<LayoutElement>();
        gridRootLayout.preferredHeight = 146f;
        GridLayoutGroup grid = CreateGrid(gridRoot, 2, 42f);

        CreateButton("Clean Spiral", gridRoot, () => ApplyPreset(0), 1, 42, AccentCyan, true);
        CreateButton("Splashy Paint", gridRoot, () => ApplyPreset(1), 1, 42, AccentOrange, true);
        CreateButton("Dense Flow", gridRoot, () => ApplyPreset(2), 1, 42, AccentGreen, true);
        CreateButton("Double Hole", gridRoot, () => ApplyPreset(3), 1, 42, new Color(0.73f, 0.30f, 1f, 1f), true);
        CreateButton("Metal Spread", gridRoot, () => ApplyPreset(4), 1, 42, new Color(0.58f, 0.62f, 0.70f, 1f), true);
        CreateButton("Restart Motion", gridRoot, RestartMotionOnly, 1, 42, AccentBlue, true);

        LayoutElement layout = section.GetComponent<LayoutElement>();
        layout.preferredHeight = 222f;
        grid.constraintCount = 2;
    }

    private void CreatePaintSourceSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "PAINT SOURCE", "bucket amount, nozzle geometry, viscosity and color");

        CreateSlider(section, "Paint Capacity", 0.5f, 20f,
            () => paintEmitter != null ? paintEmitter.initialPaintAmount : 5f,
            v => { if (paintEmitter != null) paintEmitter.PreserveFillWhenChangingCapacity(v); }, "0.0 L");

        CreateSlider(section, "Current Fill", 0f, 1f,
            () => paintEmitter != null ? paintEmitter.PaintFill01 : 0f,
            v => { if (paintEmitter != null) paintEmitter.SetFill01(v); if (bucketLiquid != null) bucketLiquid.SetFill01(v); }, "0.00");

        CreateSlider(section, "Flow Rate", 0f, 1.25f,
            () => paintEmitter != null ? paintEmitter.baseFlowRate : 0f,
            v => { if (paintEmitter != null) paintEmitter.baseFlowRate = v; }, "0.00");

        CreateSlider(section, "Hole Diameter", 0.005f, 0.10f,
            () => paintEmitter != null ? paintEmitter.holeDiameter : 0.04f,
            v => { if (paintEmitter != null) paintEmitter.holeDiameter = v; }, "0.000 m");

        CreateSlider(section, "Paint Viscosity", 0.20f, 4.00f,
            () => paintEmitter != null ? paintEmitter.viscosity : 1f,
            v => { if (paintEmitter != null) paintEmitter.viscosity = v; }, "0.00");

        CreateSlider(section, "Particles / Flow", 10f, 220f,
            () => paintEmitter != null ? paintEmitter.particlesPerUnitFlow : 90f,
            v => { if (paintEmitter != null) paintEmitter.particlesPerUnitFlow = v; }, "0", true);

        CreateSlider(section, "Random Spread", 0f, 0.65f,
            () => paintEmitter != null ? paintEmitter.randomSpread : 0.15f,
            v => { if (paintEmitter != null) paintEmitter.randomSpread = v; }, "0.00");

        CreateDropdown(section, "Nozzle Shape", new string[] { "Circular", "Slit", "DoubleHole", "Irregular" },
            () => paintEmitter != null ? (int)paintEmitter.nozzleShape : 0,
            i => { if (paintEmitter != null) paintEmitter.nozzleShape = (PaintEmitter.NozzleShape)Mathf.Clamp(i, 0, 3); });

        CreateToggle(section, "Internal Slosh", () => paintEmitter != null && paintEmitter.enableInternalSlosh,
            v => { if (paintEmitter != null) paintEmitter.enableInternalSlosh = v; });

        CreateColorRow(section);

        LayoutElement layout = section.GetComponent<LayoutElement>();
        layout.preferredHeight = 760f;
    }


    private void CreateBucketInteriorSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "BUCKET INTERNAL PAINT", "visible gravity-level liquid volume, depletion and slosh inside the bucket");

        CreateToggle(section, "Show Interior Paint", () => bucketLiquid != null && bucketLiquid.enableInteriorPaint,
            v => { if (bucketLiquid != null) bucketLiquid.enableInteriorPaint = v; });

        CreateToggle(section, "X-Ray Bucket View (optional)", () => bucketLiquid != null && bucketLiquid.presentationBucketTransparency,
            v => { if (bucketLiquid != null) { bucketLiquid.presentationBucketTransparency = v; bucketLiquid.RequestRebuild(); } });

        CreateToggle(section, "Gravity-Level Surface", () => bucketLiquid != null && bucketLiquid.useGravityAlignedSurface,
            v => { if (bucketLiquid != null) bucketLiquid.useGravityAlignedSurface = v; });

        CreateToggle(section, "Liquid Volume Shell", () => bucketLiquid != null && bucketLiquid.renderLiquidVolume,
            v => { if (bucketLiquid != null) bucketLiquid.renderLiquidVolume = v; });

        CreateToggle(section, "Meniscus / Rim Wetness", () => bucketLiquid != null && bucketLiquid.renderMeniscus,
            v => { if (bucketLiquid != null) bucketLiquid.renderMeniscus = v; });

        CreateToggle(section, "Professional Auto-Fit", () => bucketLiquid != null && bucketLiquid.autoCalibrateFromBucketBody,
            v => { if (bucketLiquid != null) { bucketLiquid.autoCalibrateFromBucketBody = v; if (v) bucketLiquid.RecalibrateNow(); } });

        CreateButton("SAFE RECALIBRATE LIQUID", section, () => { if (bucketLiquid != null) bucketLiquid.RecalibrateNow(); }, 1, 42, AccentCyan, true);

        CreateSlider(section, "Interior Radius", 0.15f, 0.85f,
            () => bucketLiquid != null ? bucketLiquid.innerRadius : 0.455f,
            v => { if (bucketLiquid != null) { bucketLiquid.innerRadius = v; bucketLiquid.RequestRebuild(); } }, "0.000 m");

        CreateSlider(section, "Liquid Center X", -0.55f, 0.55f,
            () => bucketLiquid != null ? bucketLiquid.liquidCenterLocalXZ.x : 0f,
            v => { if (bucketLiquid != null) { bucketLiquid.liquidCenterLocalXZ = new Vector2(v, bucketLiquid.liquidCenterLocalXZ.y); bucketLiquid.RequestRebuild(); } }, "0.000 m");

        CreateSlider(section, "Liquid Center Z", -0.55f, 0.55f,
            () => bucketLiquid != null ? bucketLiquid.liquidCenterLocalXZ.y : 0f,
            v => { if (bucketLiquid != null) { bucketLiquid.liquidCenterLocalXZ = new Vector2(bucketLiquid.liquidCenterLocalXZ.x, v); bucketLiquid.RequestRebuild(); } }, "0.000 m");

        CreateSlider(section, "Bottom Level", -1.35f, 0.0f,
            () => bucketLiquid != null ? bucketLiquid.bottomLocalY : -0.78f,
            v => { if (bucketLiquid != null) { bucketLiquid.bottomLocalY = v; bucketLiquid.RequestRebuild(); } }, "0.000 m");

        CreateSlider(section, "Rim Level", 0.1f, 1.25f,
            () => bucketLiquid != null ? bucketLiquid.rimLocalY : 0.70f,
            v => { if (bucketLiquid != null) { bucketLiquid.rimLocalY = v; bucketLiquid.RequestRebuild(); } }, "0.000 m");

        CreateSlider(section, "Surface Quality", 24f, 96f,
            () => bucketLiquid != null ? bucketLiquid.radialSegments : 96f,
            v => { if (bucketLiquid != null) { bucketLiquid.radialSegments = Mathf.RoundToInt(v); bucketLiquid.RequestRebuild(); } }, "0", true);

        CreateSlider(section, "Surface Rings", 6f, 32f,
            () => bucketLiquid != null ? bucketLiquid.radialRings : 24f,
            v => { if (bucketLiquid != null) { bucketLiquid.radialRings = Mathf.RoundToInt(v); bucketLiquid.RequestRebuild(); } }, "0", true);

        CreateSlider(section, "Slosh Strength", 0f, 1.2f,
            () => bucketLiquid != null ? bucketLiquid.sloshStrength : 0.32f,
            v => { if (bucketLiquid != null) bucketLiquid.sloshStrength = v; }, "0.00");

        CreateSlider(section, "Tilt Response", 0f, 1.2f,
            () => bucketLiquid != null ? bucketLiquid.surfaceTiltResponse : 0.34f,
            v => { if (bucketLiquid != null) bucketLiquid.surfaceTiltResponse = v; }, "0.00");

        CreateSlider(section, "Wave Amplitude", 0f, 0.08f,
            () => bucketLiquid != null ? bucketLiquid.waveAmplitude : 0.020f,
            v => { if (bucketLiquid != null) bucketLiquid.waveAmplitude = v; }, "0.000 m");

        CreateSlider(section, "Wave Frequency", 0f, 12f,
            () => bucketLiquid != null ? bucketLiquid.waveFrequency : 5.2f,
            v => { if (bucketLiquid != null) bucketLiquid.waveFrequency = v; }, "0.00");

        CreateSlider(section, "Liquid Gloss", 0f, 1f,
            () => bucketLiquid != null ? bucketLiquid.smoothness : 0.88f,
            v => { if (bucketLiquid != null) bucketLiquid.smoothness = v; }, "0.00");

        CreateSlider(section, "X-Ray Alpha", 0.15f, 1f,
            () => bucketLiquid != null ? bucketLiquid.bucketTransparencyAlpha : 0.62f,
            v => { if (bucketLiquid != null) { bucketLiquid.bucketTransparencyAlpha = v; } }, "0.00");

        LayoutElement layout = section.GetComponent<LayoutElement>();
        layout.preferredHeight = 1420f;
    }

    private void CreateParticleSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "PARTICLE FLIGHT", "droplet count, air, turbulence and cohesion");

        CreateSlider(section, "Max Particles", 100f, 3000f,
            () => particleSimulator != null ? particleSimulator.maxParticles : 1200f,
            v => { if (particleSimulator != null) particleSimulator.maxParticles = Mathf.RoundToInt(v); }, "0", true);

        CreateSlider(section, "Gravity", 0f, 18f,
            () => particleSimulator != null ? particleSimulator.gravity : 9.81f,
            v => { if (particleSimulator != null) particleSimulator.gravity = v; }, "0.00");

        CreateSlider(section, "Air Drag", 0f, 2.2f,
            () => particleSimulator != null ? particleSimulator.airDrag : 0.25f,
            v => { if (particleSimulator != null) particleSimulator.airDrag = v; }, "0.00");

        CreateSlider(section, "Turbulence", 0f, 1.4f,
            () => particleSimulator != null ? particleSimulator.turbulenceStrength : 0.28f,
            v => { if (particleSimulator != null) particleSimulator.turbulenceStrength = v; }, "0.00");

        CreateSlider(section, "Wind Strength", 0f, 1.2f,
            () => particleSimulator != null ? particleSimulator.windStrength : 0.16f,
            v => { if (particleSimulator != null) particleSimulator.windStrength = v; }, "0.00");

        CreateSlider(section, "Cohesion", 0f, 0.65f,
            () => particleSimulator != null ? particleSimulator.cohesionStrength : 0.14f,
            v => { if (particleSimulator != null) particleSimulator.cohesionStrength = v; }, "0.00");

        CreateSlider(section, "Separation", 0f, 0.50f,
            () => particleSimulator != null ? particleSimulator.separationStrength : 0.07f,
            v => { if (particleSimulator != null) particleSimulator.separationStrength = v; }, "0.00");

        CreateToggle(section, "Air Turbulence", () => particleSimulator != null && particleSimulator.enableAirTurbulence,
            v => { if (particleSimulator != null) particleSimulator.enableAirTurbulence = v; });

        CreateToggle(section, "Visual Droplets", () => particleSimulator != null && particleSimulator.showVisualDroplets,
            v => { if (particleSimulator != null) particleSimulator.showVisualDroplets = v; });
    }

    private void CreateFluidFilmSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "REAL FLUID FILM", "surface liquid behavior: pressure, gravity, adhesion and viscosity");

        CreateToggle(section, "Enable Fluid Film", () => fluidFilmSolver != null && fluidFilmSolver.enableFluidFilm,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.enableFluidFilm = v; });

        CreateToggle(section, "Solve Whole Film", () => fluidFilmSolver != null && fluidFilmSolver.solveWholeFilm,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.solveWholeFilm = v; });

        CreateSlider(section, "Solver Interval", 0.008f, 0.12f,
            () => fluidFilmSolver != null ? fluidFilmSolver.solverInterval : 0.035f,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.solverInterval = v; }, "0.000 s");

        CreateSlider(section, "Substeps", 1f, 6f,
            () => fluidFilmSolver != null ? fluidFilmSolver.subSteps : 3f,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.subSteps = Mathf.RoundToInt(v); }, "0", true);

        CreateSlider(section, "Fluid Viscosity", 0.2f, 16f,
            () => fluidFilmSolver != null ? fluidFilmSolver.viscosity : 5.5f,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.viscosity = v; }, "0.00");

        CreateSlider(section, "Pressure Spread", 0f, 8f,
            () => fluidFilmSolver != null ? fluidFilmSolver.pressureStrength : 2.4f,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.pressureStrength = v; }, "0.00");

        CreateSlider(section, "Surface Gravity", 0f, 10f,
            () => fluidFilmSolver != null ? fluidFilmSolver.gravityStrength : 2.6f,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.gravityStrength = v; }, "0.00");

        CreateSlider(section, "Adhesion", 0f, 1.4f,
            () => fluidFilmSolver != null ? fluidFilmSolver.adhesion : 0.34f,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.adhesion = v; }, "0.00");

        CreateSlider(section, "Surface Tension", 0f, 1.5f,
            () => fluidFilmSolver != null ? fluidFilmSolver.surfaceTension : 0.42f,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.surfaceTension = v; }, "0.00");

        CreateSlider(section, "Wet Friction", 0f, 1.5f,
            () => fluidFilmSolver != null ? fluidFilmSolver.wetFriction : 0.58f,
            v => { if (fluidFilmSolver != null) fluidFilmSolver.wetFriction = v; }, "0.00");
    }

    private void CreateCanvasVisualSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "CANVAS + WET VISUALS", "surface profile, stain, spray, wet highlights and normal map");

        CreateDropdown(section, "Surface Type", new string[] { "Paper", "Canvas", "Wood", "Metal" },
            () => canvasPainter != null ? (int)canvasPainter.surfaceType : 1,
            i => { if (canvasPainter != null) canvasPainter.surfaceType = (CanvasPainter.CanvasSurfaceType)Mathf.Clamp(i, 0, 3); });

        CreateSlider(section, "Paint Opacity", 0.05f, 1f,
            () => canvasPainter != null ? canvasPainter.paintOpacity : 0.85f,
            v => { if (canvasPainter != null) canvasPainter.paintOpacity = v; }, "0.00");

        CreateSlider(section, "Spray Amount", 0f, 3f,
            () => canvasPainter != null ? canvasPainter.sprayAmount : 0.45f,
            v => { if (canvasPainter != null) canvasPainter.sprayAmount = v; }, "0.00");

        CreateSlider(section, "Smear Length", 0f, 3f,
            () => canvasPainter != null ? canvasPainter.smearLength : 1.1f,
            v => { if (canvasPainter != null) canvasPainter.smearLength = v; }, "0.00");

        CreateSlider(section, "Edge Irregularity", 0f, 1f,
            () => canvasPainter != null ? canvasPainter.edgeIrregularity : 0.5f,
            v => { if (canvasPainter != null) canvasPainter.edgeIrregularity = v; }, "0.00");

        CreateSlider(section, "Thickness Visibility", 0f, 1.8f,
            () => canvasPainter != null ? canvasPainter.fluidThicknessVisibility : 0.55f,
            v => { if (canvasPainter != null) canvasPainter.fluidThicknessVisibility = v; }, "0.00");

        CreateSlider(section, "Normal Strength", 0f, 8f,
            () => canvasPainter != null ? canvasPainter.fluidNormalStrength : 3.2f,
            v => { if (canvasPainter != null) canvasPainter.fluidNormalStrength = v; }, "0.00");

        CreateToggle(section, "Directional Smear", () => canvasPainter != null && canvasPainter.enableDirectionalSmear,
            v => { if (canvasPainter != null) canvasPainter.enableDirectionalSmear = v; });

        CreateToggle(section, "Wet Shading", () => canvasPainter != null && canvasPainter.enableWetPaintShading,
            v => { if (canvasPainter != null) canvasPainter.enableWetPaintShading = v; });

        CreateToggle(section, "Dynamic Normal Map", () => canvasPainter != null && canvasPainter.generateFluidNormalMap,
            v => { if (canvasPainter != null) canvasPainter.generateFluidNormalMap = v; });
    }

    private void CreateMotionSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "BUCKET MOTION", "swing amplitude, damping, depth motion and wobble");

        CreateSlider(section, "Rope Length", 1.2f, 4f,
            () => pendulumController != null ? pendulumController.ropeLength : 2.2f,
            v => { if (pendulumController != null) pendulumController.ropeLength = v; if (ropeController != null) ropeController.ropeLength = v; }, "0.00 m");

        CreateSlider(section, "Start Angle X", 0f, 75f,
            () => pendulumController != null ? pendulumController.startAngleDegrees : 35f,
            v => { if (pendulumController != null) pendulumController.startAngleDegrees = v; }, "0°");

        CreateSlider(section, "Start Angle Z", 0f, 55f,
            () => pendulumController != null ? pendulumController.startAngleZDegrees : 18f,
            v => { if (pendulumController != null) pendulumController.startAngleZDegrees = v; }, "0°");

        CreateSlider(section, "Damping", 0f, 0.25f,
            () => pendulumController != null ? pendulumController.damping : 0.05f,
            v => { if (pendulumController != null) pendulumController.damping = v; }, "0.000");

        CreateSlider(section, "Depth Swing", 0f, 1.5f,
            () => pendulumController != null ? pendulumController.zSwingStrength : 0.6f,
            v => { if (pendulumController != null) pendulumController.zSwingStrength = v; }, "0.00");

        CreateSlider(section, "Bucket Wobble", 0f, 24f,
            () => pendulumController != null ? pendulumController.wobbleStrength : 8f,
            v => { if (pendulumController != null) pendulumController.wobbleStrength = v; }, "0.0");

        CreateSlider(section, "Torsion", 0f, 45f,
            () => pendulumController != null ? pendulumController.torsionStrength : 18f,
            v => { if (pendulumController != null) pendulumController.torsionStrength = v; }, "0.0");

        CreateToggle(section, "Depth Swing Enabled", () => pendulumController != null && pendulumController.enableDepthSwing,
            v => { if (pendulumController != null) pendulumController.enableDepthSwing = v; });
    }

    private void CreateRopeSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "ROPE RIG", "tensioned cable visual behavior and material preset");

        CreateDropdown(section, "Rope Type", new string[] { "Cotton", "Nylon", "Steel" },
            () => ropeController != null ? (int)ropeController.ropeType : 1,
            i => { if (ropeController != null) ropeController.SetRopeTypeByIndex(i); });

        CreateSlider(section, "Tautness", 0.80f, 1.0f,
            () => ropeController != null ? ropeController.tautness : 0.97f,
            v => { if (ropeController != null) ropeController.tautness = v; }, "0.000");

        CreateSlider(section, "Max Sag", 0f, 0.16f,
            () => ropeController != null ? ropeController.maxSag : 0.055f,
            v => { if (ropeController != null) ropeController.maxSag = v; }, "0.000 m");

        CreateSlider(section, "Bend Stiffness", 0f, 1f,
            () => ropeController != null ? ropeController.bendStiffness : 0.96f,
            v => { if (ropeController != null) ropeController.bendStiffness = v; }, "0.00");

        CreateSlider(section, "Wave Amplitude", 0f, 0.08f,
            () => ropeController != null ? ropeController.dynamicWaveAmplitude : 0.015f,
            v => { if (ropeController != null) ropeController.dynamicWaveAmplitude = v; }, "0.000");

        CreateSlider(section, "Segments", 8f, 64f,
            () => ropeController != null ? ropeController.segmentCount : 30f,
            v => { if (ropeController != null) { ropeController.segmentCount = Mathf.RoundToInt(v); ropeController.ResetRopeNow(); } }, "0", true);

        CreateToggle(section, "Tension Color", () => ropeController != null && ropeController.enableTensionColor,
            v => { if (ropeController != null) ropeController.enableTensionColor = v; });
    }

    private void CreateExportSection(RectTransform parent)
    {
        RectTransform section = CreateSection(parent, "OUTPUT + DEBUG", "save results and toggle simulation layers");
        RectTransform gridRoot = CreateUIObject("Export Grid", section).GetComponent<RectTransform>();
        LayoutElement gridRootLayout = gridRoot.gameObject.AddComponent<LayoutElement>();
        gridRootLayout.preferredHeight = 96f;
        GridLayoutGroup grid = CreateGrid(gridRoot, 2, 42f);

        CreateButton("Save Image", gridRoot, () => { if (exporter != null) exporter.SaveImage(); }, 1, 42, AccentCyan, true);
        CreateButton("Save Report", gridRoot, () => { if (exporter != null) exporter.SaveReport(); }, 1, 42, AccentGreen, true);
        CreateButton("Save All", gridRoot, () => { if (exporter != null) exporter.SaveImageAndReport(); }, 1, 42, AccentOrange, true);
        CreateButton("Step Film", gridRoot, () =>
        {
            if (fluidFilmSolver != null)
            {
                bool previousWholeFilm = fluidFilmSolver.solveWholeFilm;
                fluidFilmSolver.solveWholeFilm = false;
                fluidFilmSolver.StepFluid(0.015f);
                fluidFilmSolver.solveWholeFilm = previousWholeFilm;
            }
        }, 1, 42, AccentBlue, true);

        LayoutElement layout = section.GetComponent<LayoutElement>();
        layout.preferredHeight = 162f;
        grid.constraintCount = 2;
    }

    private void CreateColorRow(RectTransform parent)
    {
        RectTransform row = CreateUIObject("Paint Color Row", parent).GetComponent<RectTransform>();
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 44f;

        HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = 8;
        group.childForceExpandWidth = true;
        group.childControlHeight = true;
        group.childControlWidth = true;

        CreateColorButton(row, new Color(0.95f, 0.02f, 0.0f, 1f), "RED");
        CreateColorButton(row, new Color(0.05f, 0.25f, 1.0f, 1f), "BLUE");
        CreateColorButton(row, new Color(0.05f, 0.85f, 0.25f, 1f), "GREEN");
        CreateColorButton(row, new Color(1.0f, 0.82f, 0.05f, 1f), "GOLD");
        CreateColorButton(row, new Color(0.75f, 0.18f, 1.0f, 1f), "VIOLET");
    }

    private void CreateColorButton(RectTransform parent, Color color, string label)
    {
        CreateButton(label, parent, () => SetPaintColor(color), 1, 38, color, true);
    }

    private RectTransform CreateSection(RectTransform parent, string title, string subtitle)
    {
        RectTransform section = CreatePanel(title, parent, PanelColorSoft);
        LayoutElement layout = section.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 80f;
        layout.preferredHeight = 560f;

        VerticalLayoutGroup group = section.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(14, 14, 14, 14);
        group.spacing = 8;
        group.childControlHeight = true;
        group.childForceExpandHeight = false;
        group.childControlWidth = true;

        CreateHeader(section, title, subtitle, 18);
        return section;
    }

    private void CreateHeader(RectTransform parent, string title, string subtitle, int titleSize = 20)
    {
        RectTransform header = CreateUIObject(title + " Header", parent).GetComponent<RectTransform>();
        LayoutElement layout = header.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 48f;

        VerticalLayoutGroup group = header.gameObject.AddComponent<VerticalLayoutGroup>();
        group.childAlignment = TextAnchor.MiddleLeft;
        group.childForceExpandHeight = false;
        group.spacing = 0;

        CreateText(title, header, titleSize, FontStyles.Bold, TextMain, TextAlignmentOptions.Left);
        CreateText(subtitle, header, 13, FontStyles.Normal, TextMuted, TextAlignmentOptions.Left);
    }

    private TextMeshProUGUI CreateStatusChip(string text, RectTransform parent, Color accent, float width)
    {
        RectTransform chip = CreatePanel(text + " Chip", parent, new Color(accent.r * 0.16f, accent.g * 0.16f, accent.b * 0.16f, 0.78f));
        LayoutElement layout = chip.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = 46f;

        Image outline = chip.GetComponent<Image>();
        outline.sprite = buttonSprite;
        outline.type = Image.Type.Sliced;

        TextMeshProUGUI label = CreateText(text, chip, 15, FontStyles.Bold, TextMain, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, 8f, 4f, 8f, 4f);
        return label;
    }

    private TextMeshProUGUI CreateMetricCard(RectTransform parent, string label, string value, Color accent)
    {
        RectTransform card = CreatePanel(label + " Card", parent, new Color(accent.r * 0.08f, accent.g * 0.08f, accent.b * 0.08f, 0.55f));
        LayoutElement layout = card.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 88f;

        VerticalLayoutGroup group = card.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(14, 14, 10, 10);
        group.spacing = 2;
        group.childForceExpandHeight = false;
        group.childControlWidth = true;
        group.childAlignment = TextAnchor.MiddleLeft;

        CreateText(label, card, 12, FontStyles.Bold | FontStyles.UpperCase, accent, TextAlignmentOptions.Left);
        TextMeshProUGUI valueText = CreateText(value, card, 23, FontStyles.Bold, TextMain, TextAlignmentOptions.Left);
        return valueText;
    }

    private Slider CreateReadOnlyMeter(RectTransform parent, string label)
    {
        RectTransform root = CreateUIObject(label + " Meter", parent).GetComponent<RectTransform>();
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 66f;

        VerticalLayoutGroup stack = root.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.spacing = 6;
        stack.childControlWidth = true;
        stack.childControlHeight = true;

        CreateText(label, root, 14, FontStyles.Bold, TextMuted, TextAlignmentOptions.Left);
        Slider meter = CreateSliderVisual(root, false);
        meter.interactable = false;
        return meter;
    }

    private void CreateSlider(RectTransform parent, string label, float min, float max, Func<float> getter, Action<float> setter, string format, bool wholeNumbers = false)
    {
        RectTransform root = CreateUIObject(label + " Slider", parent).GetComponent<RectTransform>();
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 70f;

        VerticalLayoutGroup stack = root.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.spacing = 6;
        stack.childControlWidth = true;
        stack.childControlHeight = true;

        RectTransform labelRow = CreateUIObject("Label Row", root).GetComponent<RectTransform>();
        LayoutElement labelLayout = labelRow.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = 22f;
        HorizontalLayoutGroup labelGroup = labelRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        labelGroup.childControlWidth = true;
        labelGroup.childForceExpandWidth = true;

        CreateText(label, labelRow, 14, FontStyles.Bold, TextMain, TextAlignmentOptions.Left);
        TextMeshProUGUI valueText = CreateText("--", labelRow, 14, FontStyles.Bold, AccentCyan, TextAlignmentOptions.Right);

        Slider slider = CreateSliderVisual(root, true);
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.value = Mathf.Clamp(getter != null ? getter() : min, min, max);
        slider.onValueChanged.AddListener(v =>
        {
            if (wholeNumbers)
            {
                v = Mathf.Round(v);
            }

            setter?.Invoke(v);
            valueText.text = FormatValue(v, format);
        });

        sliderBindings.Add(new SliderBinding(slider, valueText, getter, format, wholeNumbers));
    }

    private Slider CreateSliderVisual(RectTransform parent, bool interactable)
    {
        RectTransform root = CreateUIObject("Slider", parent).GetComponent<RectTransform>();
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 28f;

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.interactable = interactable;

        RectTransform background = CreateUIObject("Background", root).GetComponent<RectTransform>();
        Stretch(background, 0f, 8f, 0f, 8f);
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.sprite = sliderBackgroundSprite;
        backgroundImage.type = Image.Type.Sliced;
        backgroundImage.color = new Color(0.16f, 0.18f, 0.23f, 0.86f);
        slider.targetGraphic = backgroundImage;

        RectTransform fillArea = CreateUIObject("Fill Area", root).GetComponent<RectTransform>();
        Stretch(fillArea, 6f, 8f, 6f, 8f);

        RectTransform fill = CreateUIObject("Fill", fillArea).GetComponent<RectTransform>();
        Stretch(fill);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = sliderFillSprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = AccentCyan;
        slider.fillRect = fill;

        RectTransform handleArea = CreateUIObject("Handle Slide Area", root).GetComponent<RectTransform>();
        Stretch(handleArea, 8f, 0f, 8f, 0f);

        RectTransform handle = CreateUIObject("Handle", handleArea).GetComponent<RectTransform>();
        handle.sizeDelta = new Vector2(22f, 22f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = handleSprite;
        handleImage.color = Color.white;
        slider.handleRect = handle;

        return slider;
    }

    private void CreateToggle(RectTransform parent, string label, Func<bool> getter, Action<bool> setter)
    {
        RectTransform root = CreatePanel(label + " Toggle", parent, new Color(0.02f, 0.025f, 0.035f, 0.56f));
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 42f;

        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        toggle.isOn = getter != null && getter();

        HorizontalLayoutGroup group = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.padding = new RectOffset(12, 12, 6, 6);
        group.spacing = 10;
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = false;
        group.childControlHeight = true;

        RectTransform box = CreatePanel("Box", root, toggle.isOn ? AccentGreen : new Color(0.16f, 0.18f, 0.22f, 1f));
        LayoutElement boxLayout = box.gameObject.AddComponent<LayoutElement>();
        boxLayout.preferredWidth = 26f;
        boxLayout.preferredHeight = 26f;
        Image boxImage = box.GetComponent<Image>();
        toggle.targetGraphic = boxImage;

        RectTransform check = CreatePanel("Check", box, AccentGreen);
        Stretch(check, 5, 5, 5, 5);
        toggle.graphic = check.GetComponent<Image>();

        TextMeshProUGUI text = CreateText(label, root, 14, FontStyles.Bold, TextMain, TextAlignmentOptions.Left);
        LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;

        toggle.onValueChanged.AddListener(v =>
        {
            setter?.Invoke(v);
            boxImage.color = v ? AccentGreen : new Color(0.16f, 0.18f, 0.22f, 1f);
        });

        toggleBindings.Add(new ToggleBinding(toggle, getter, boxImage));
    }

    private void CreateDropdown(RectTransform parent, string label, string[] options, Func<int> getter, Action<int> setter)
    {
        RectTransform root = CreateUIObject(label + " Dropdown", parent).GetComponent<RectTransform>();
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 72f;

        VerticalLayoutGroup stack = root.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.spacing = 5;
        stack.childControlWidth = true;
        stack.childControlHeight = true;

        CreateText(label, root, 14, FontStyles.Bold, TextMain, TextAlignmentOptions.Left);

        RectTransform dropdownRoot = CreatePanel("Dropdown Box", root, new Color(0.11f, 0.13f, 0.18f, 0.95f));
        LayoutElement dropdownLayout = dropdownRoot.gameObject.AddComponent<LayoutElement>();
        dropdownLayout.preferredHeight = 38f;

        TMP_Dropdown dropdown = dropdownRoot.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = dropdownRoot.GetComponent<Image>();
        dropdown.options.Clear();
        for (int i = 0; i < options.Length; i++)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData(options[i]));
        }
        dropdown.value = Mathf.Clamp(getter != null ? getter() : 0, 0, options.Length - 1);

        TextMeshProUGUI caption = CreateText(options[Mathf.Clamp(dropdown.value, 0, options.Length - 1)], dropdownRoot, 15, FontStyles.Bold, TextMain, TextAlignmentOptions.Left);
        Stretch(caption.rectTransform, 14f, 4f, 36f, 4f);
        dropdown.captionText = caption;

        TextMeshProUGUI arrow = CreateText("▼", dropdownRoot, 14, FontStyles.Bold, AccentCyan, TextAlignmentOptions.Center);
        arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
        arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
        arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
        arrow.rectTransform.sizeDelta = new Vector2(34f, 0f);
        arrow.rectTransform.anchoredPosition = new Vector2(-5f, 0f);

        RectTransform template = CreateDropdownTemplate(dropdownRoot, options);
        dropdown.template = template;
        dropdown.onValueChanged.AddListener(i => setter?.Invoke(i));

        dropdownBindings.Add(new DropdownBinding(dropdown, getter, options.Length));
    }

    private RectTransform CreateDropdownTemplate(RectTransform parent, string[] options)
    {
        RectTransform template = CreatePanel("Template", parent, new Color(0.06f, 0.07f, 0.10f, 0.98f));
        template.gameObject.SetActive(false);
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = new Vector2(0f, -2f);
        template.sizeDelta = new Vector2(0f, Mathf.Max(120f, options.Length * 34f + 16f));

        ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        RectTransform viewport = CreateUIObject("Viewport", template).GetComponent<RectTransform>();
        Stretch(viewport, 4, 4, 4, 4);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        scroll.viewport = viewport;

        RectTransform content = CreateUIObject("Content", viewport).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, options.Length * 34f);
        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(0, 0, 4, 4);
        contentLayout.spacing = 2;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

        Toggle item = CreateDropdownItem(content, "Item");
        item.gameObject.SetActive(false);
        TMP_Dropdown dropdown = parent.GetComponent<TMP_Dropdown>();
        if (dropdown != null)
        {
            dropdown.itemText = item.GetComponentInChildren<TextMeshProUGUI>();
        }

        return template;
    }

    private Toggle CreateDropdownItem(RectTransform content, string name)
    {
        RectTransform itemRoot = CreateUIObject(name, content).GetComponent<RectTransform>();
        itemRoot.anchorMin = new Vector2(0f, 1f);
        itemRoot.anchorMax = new Vector2(1f, 1f);
        itemRoot.pivot = new Vector2(0.5f, 1f);
        itemRoot.sizeDelta = new Vector2(0f, 32f);
        LayoutElement itemLayout = itemRoot.gameObject.AddComponent<LayoutElement>();
        itemLayout.preferredHeight = 32f;

        Toggle toggle = itemRoot.gameObject.AddComponent<Toggle>();
        Image background = itemRoot.gameObject.AddComponent<Image>();
        background.color = new Color(0.09f, 0.11f, 0.15f, 0.95f);
        toggle.targetGraphic = background;

        TextMeshProUGUI itemText = CreateText("Option", itemRoot, 14, FontStyles.Bold, TextMain, TextAlignmentOptions.Left);
        Stretch(itemText.rectTransform, 12f, 3f, 12f, 3f);
        return toggle;
    }

    private Button CreateButton(string label, RectTransform parent, Action action, float width, float height, Color accent, bool flexibleWidth = false)
    {
        RectTransform root = CreatePanel(label + " Button", parent, new Color(accent.r * 0.62f, accent.g * 0.62f, accent.b * 0.62f, 0.95f));
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        if (flexibleWidth)
        {
            layout.flexibleWidth = width;
        }
        else
        {
            layout.preferredWidth = width;
        }
        layout.preferredHeight = height;

        Button button = root.gameObject.AddComponent<Button>();
        Image image = root.GetComponent<Image>();
        image.sprite = buttonSprite;
        image.type = Image.Type.Sliced;
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(accent.r * 0.62f, accent.g * 0.62f, accent.b * 0.62f, 0.92f);
        colors.highlightedColor = new Color(accent.r * 0.85f, accent.g * 0.85f, accent.b * 0.85f, 1f);
        colors.pressedColor = new Color(accent.r * 0.40f, accent.g * 0.40f, accent.b * 0.40f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TextMeshProUGUI text = CreateText(label, root, 15, FontStyles.Bold, TextMain, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8f, 4f, 8f, 4f);

        button.onClick.AddListener(() => RunUiActionSafely(label, action));
        return button;
    }

    private void RunUiActionSafely(string label, Action action)
    {
        if (Time.unscaledTime < nextSafeButtonTime)
        {
            return;
        }

        nextSafeButtonTime = Time.unscaledTime + 0.12f;

        try
        {
            action?.Invoke();
            lastUiMessage = label + " applied";
        }
        catch (Exception ex)
        {
            lastUiMessage = label + " failed: " + ex.GetType().Name;
            Debug.LogException(ex);
        }

        RefreshDashboard(true);
    }

    private RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateUIObject(name, parent).GetComponent<RectTransform>();
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return rect;
    }

    private TextMeshProUGUI CreateText(string text, Transform parent, int size, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(text + " Text", parent);
        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        return tmp;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private GridLayoutGroup CreateGrid(RectTransform parent, int columns, float rowHeight)
    {
        GridLayoutGroup grid = parent.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(190f, rowHeight);
        grid.spacing = new Vector2(10f, 10f);
        grid.childAlignment = TextAnchor.UpperCenter;
        return grid;
    }

    private void CreateThemeSprites()
    {
        panelSprite = CreateRoundedSprite(64, 64, 16, new Color(1f, 1f, 1f, 1f));
        buttonSprite = CreateRoundedSprite(64, 64, 13, new Color(1f, 1f, 1f, 1f));
        buttonHoverSprite = CreateRoundedSprite(64, 64, 13, new Color(1f, 1f, 1f, 1f));
        sliderBackgroundSprite = CreateRoundedSprite(64, 24, 10, new Color(1f, 1f, 1f, 1f));
        sliderFillSprite = CreateRoundedSprite(64, 24, 10, new Color(1f, 1f, 1f, 1f));
        handleSprite = CreateRoundedSprite(32, 32, 16, new Color(1f, 1f, 1f, 1f));
    }

    private Sprite CreateRoundedSprite(int width, int height, int radius, Color color)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Min(x, width - 1 - x);
                float dy = Mathf.Min(y, height - 1 - y);
                float alpha = 1f;

                if (dx < radius && dy < radius)
                {
                    float cx = radius - dx;
                    float cy = radius - dy;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);
                    alpha = Mathf.Clamp01(radius + 0.5f - dist);
                }

                Color finalColor = Color.Lerp(clear, color, alpha);
                tex.SetPixel(x, y, finalColor);
            }
        }

        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private void HandleHotkeys()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.hKey.wasPressedThisFrame) ToggleHud();
        if (Keyboard.current.rKey.wasPressedThisFrame) ResetSimulation();
        if (Keyboard.current.spaceKey.wasPressedThisFrame) TogglePause();
        if (Keyboard.current.fKey.wasPressedThisFrame && fluidFilmSolver != null) fluidFilmSolver.enableFluidFilm = !fluidFilmSolver.enableFluidFilm;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) ApplyPreset(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) ApplyPreset(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) ApplyPreset(2);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) ApplyPreset(3);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) ApplyPreset(4);
    }

    private void ToggleHud()
    {
        SetVisible(isHidden);
    }

    private void SetVisible(bool visible)
    {
        isHidden = !visible;
        if (contentRoot != null)
        {
            contentRoot.SetActive(visible);
        }
    }

    private void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        RefreshDashboard(true);
    }

    private void ResetSimulation()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (simulationManager != null)
        {
            simulationManager.ResetSimulation();
        }
        else
        {
            if (particleSimulator != null) particleSimulator.ResetParticles();
            if (canvasPainter != null) canvasPainter.ResetCanvas();
            if (surfaceState != null) surfaceState.ResetState();
            if (fluidFilmSolver != null) fluidFilmSolver.ResetSolver();
            if (impactEngine != null) impactEngine.ResetImpactStats();
            if (pendulumController != null) pendulumController.ResetSimulation();
            if (paintEmitter != null) paintEmitter.ResetEmitter();
            if (ropeController != null) ropeController.ResetRopeNow();
        }
        RefreshDashboard(true);
    }

    private void ClearOnlyCanvas()
    {
        if (particleSimulator != null) particleSimulator.ResetParticles();
        if (canvasPainter != null) canvasPainter.ResetCanvas();
        if (surfaceState != null) surfaceState.ResetState();
        if (fluidFilmSolver != null) fluidFilmSolver.ResetSolver();
        if (impactEngine != null) impactEngine.ResetImpactStats();
    }

    private void RefillPaintOnly()
    {
        if (paintEmitter != null)
        {
            paintEmitter.RefillToInitialAmount();
        }

        if (bucketLiquid != null)
        {
            bucketLiquid.ResetLiquidVisual();
        }
    }

    private void RestartMotionOnly()
    {
        if (pendulumController != null)
        {
            pendulumController.ResetSimulation();
        }

        if (ropeController != null)
        {
            ropeController.ResetRopeNow();
        }
    }

    private void ApplyPreset(int index)
    {
        index = Mathf.Clamp(index, 0, 4);
        if (presetApplier != null)
        {
            presetApplier.ApplyPresetByIndex(index);
        }
        else
        {
            ResetSimulation();
        }

        RefreshDashboard(true);
    }

    private void SetPaintColor(Color color)
    {
        if (paintEmitter != null)
        {
            paintEmitter.paintColor = color;
        }

        if (canvasPainter != null)
        {
            canvasPainter.thinWetPaintColor = Color.Lerp(color, Color.white, 0.06f);
            canvasPainter.thickWetPaintColor = Color.Lerp(color, Color.black, 0.45f);
        }

        if (bucketLiquid != null)
        {
            bucketLiquid.paintColor = color;
            bucketLiquid.RequestRebuild();
        }
    }

    private void UpdateFpsCounter()
    {
        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f)
        {
            displayedFps = fpsFrames / Mathf.Max(fpsTimer, 0.0001f);
            fpsFrames = 0;
            fpsTimer = 0f;
        }
    }

    private void RefreshBindings()
    {
        for (int i = 0; i < sliderBindings.Count; i++)
        {
            SliderBinding binding = sliderBindings[i];
            if (binding == null || binding.slider == null || binding.getter == null)
            {
                continue;
            }

            float value = binding.getter();
            if (binding.wholeNumbers)
            {
                value = Mathf.Round(value);
            }

            binding.slider.SetValueWithoutNotify(value);
            if (binding.valueText != null)
            {
                binding.valueText.text = FormatValue(value, binding.format);
            }
        }

        for (int i = 0; i < toggleBindings.Count; i++)
        {
            ToggleBinding binding = toggleBindings[i];
            if (binding == null || binding.toggle == null || binding.getter == null)
            {
                continue;
            }

            bool value = binding.getter();
            binding.toggle.SetIsOnWithoutNotify(value);
            if (binding.boxImage != null)
            {
                binding.boxImage.color = value ? AccentGreen : new Color(0.16f, 0.18f, 0.22f, 1f);
            }
        }

        for (int i = 0; i < dropdownBindings.Count; i++)
        {
            DropdownBinding binding = dropdownBindings[i];
            if (binding == null || binding.dropdown == null || binding.getter == null)
            {
                continue;
            }

            int value = Mathf.Clamp(binding.getter(), 0, Mathf.Max(0, binding.optionCount - 1));
            binding.dropdown.SetValueWithoutNotify(value);
            binding.dropdown.RefreshShownValue();
        }
    }

    private void RefreshDashboard(bool force)
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (!force && refreshTimer < 0.12f)
        {
            return;
        }
        refreshTimer = 0f;

        if (fpsValue != null) fpsValue.text = "FPS " + displayedFps.ToString("0");
        if (gameStateText != null) gameStateText.text = isPaused ? "PAUSED" : "RUNNING";

        if (paintEmitter != null)
        {
            if (paintValue != null) paintValue.text = "PAINT " + (paintEmitter.PaintFill01 * 100f).ToString("0") + "%";
            if (paintFillSlider != null) paintFillSlider.SetValueWithoutNotify(paintEmitter.PaintFill01);
        }

        if (bucketLiquid != null && bucketLiquidValue != null)
        {
            string flow = paintEmitter != null ? paintEmitter.CurrentFlowRate.ToString("0.00") + " L/s" : "--";
            bucketLiquidValue.text =
                bucketLiquid.StatusText +
                " | fill " + (bucketLiquid.Fill01 * 100f).ToString("0") + "%" +
                " | " + bucketLiquid.VisibleLiters.ToString("0.00") + " L" +
                " | flow " + flow +
                " | slosh " + (bucketLiquid.SloshIntensity * 100f).ToString("0") + "%" +
                " | " + bucketLiquid.CalibrationStatus;
        }

        if (particleSimulator != null && particleValue != null)
        {
            particleValue.text = particleSimulator.ActiveParticleCount + " active / " + particleSimulator.ActiveVisualDropletCount + " visible";
        }

        if (fluidFilmSolver != null && fluidValue != null)
        {
            fluidValue.text = "FILM " + (fluidFilmSolver.enableFluidFilm ? "ON" : "OFF") + "  " + fluidFilmSolver.activeFluidCells;
        }

        if (ropeController != null && ropeValue != null)
        {
            ropeValue.text = ropeController.ropeType + "  T " + ropeController.tension.ToString("0.00");
        }

        if (impactEngine != null && impactValue != null)
        {
            impactValue.text = impactEngine.totalImpacts + " impacts | " + impactEngine.lastImpactType + " | " + impactEngine.lastImpactSpeed.ToString("0.0") + " m/s";
        }

        if (surfaceState != null && statusLineText != null)
        {
            statusLineText.text =
                "coverage " + (surfaceState.thickCoverage01 * 100f).ToString("0") + "%" +
                " | wet " + (surfaceState.wetCoverage01 * 100f).ToString("0") + "%" +
                " | max thickness " + surfaceState.maxThickness.ToString("0.000");
        }

        if (statusLineText != null && surfaceState == null)
        {
            statusLineText.text = "surface solver not linked";
        }

        if (helpText != null && !string.IsNullOrEmpty(lastUiMessage))
        {
            helpText.text = lastUiMessage + "   |   Shortcuts: 1-5 presets   Space pause   R reset   F fluid film   H hide/show HUD";
        }
    }

    private string FormatValue(float value, string format)
    {
        if (string.IsNullOrEmpty(format))
        {
            return value.ToString("0.###");
        }

        if (format.Contains("°"))
        {
            return value.ToString(format.Replace("°", string.Empty)) + "°";
        }

        if (format.Contains(" m"))
        {
            return value.ToString(format.Replace(" m", string.Empty)) + " m";
        }

        if (format.Contains(" L"))
        {
            return value.ToString(format.Replace(" L", string.Empty)) + " L";
        }

        if (format.Contains(" s"))
        {
            return value.ToString(format.Replace(" s", string.Empty)) + " s";
        }

        return value.ToString(format);
    }

    private void Stretch(RectTransform rect)
    {
        Stretch(rect, 0f, 0f, 0f, 0f);
    }

    private void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void AnchorTop(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void AnchorBottom(RectTransform rect, float left, float bottom, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
    }

    private void AnchorLeft(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void AnchorRight(RectTransform rect, float right, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private class SliderBinding
    {
        public Slider slider;
        public TextMeshProUGUI valueText;
        public Func<float> getter;
        public string format;
        public bool wholeNumbers;

        public SliderBinding(Slider slider, TextMeshProUGUI valueText, Func<float> getter, string format, bool wholeNumbers)
        {
            this.slider = slider;
            this.valueText = valueText;
            this.getter = getter;
            this.format = format;
            this.wholeNumbers = wholeNumbers;
        }
    }

    private class ToggleBinding
    {
        public Toggle toggle;
        public Func<bool> getter;
        public Image boxImage;

        public ToggleBinding(Toggle toggle, Func<bool> getter, Image boxImage)
        {
            this.toggle = toggle;
            this.getter = getter;
            this.boxImage = boxImage;
        }
    }

    private class DropdownBinding
    {
        public TMP_Dropdown dropdown;
        public Func<int> getter;
        public int optionCount;

        public DropdownBinding(TMP_Dropdown dropdown, Func<int> getter, int optionCount)
        {
            this.dropdown = dropdown;
            this.getter = getter;
            this.optionCount = optionCount;
        }
    }
}
