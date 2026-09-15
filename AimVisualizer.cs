using Godot;

public partial class AimVisualizer : Node3D
{
    [Export] public ThrowController? ThrowController { get; set; }
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Line Dimensions")]
    [Export] public float MinLineLength { get; set; } = 1.5f;
    [Export] public float MaxLineLength { get; set; } = 6.0f;
    [Export] public float LineRadius { get; set; } = 0.03f;
    [Export] public float ArrowHeadLength { get; set; } = 0.35f;
    [Export] public float ArrowHeadRadius { get; set; } = 0.09f;

    [ExportGroup("Disc Preview Dimensions")]
    [Export] public float DiscRadius { get; set; } = 0.35f;
    [Export] public float DiscThickness { get; set; } = 0.035f;
    [Export] public float HorizonGuideWidth { get; set; } = 1.0f;

    [ExportGroup("Colors")]
    [Export] public Color LowPowerColor { get; set; } = new(0.2f, 0.75f, 1.0f, 0.95f);
    [Export] public Color MidPowerColor { get; set; } = new(0.95f, 0.85f, 0.2f, 0.95f);
    [Export] public Color HighPowerColor { get; set; } = new(1.0f, 0.25f, 0.1f, 0.98f);
    [Export] public Color DiscPreviewColor { get; set; } = new(0.9f, 0.95f, 1.0f, 0.75f);
    [Export] public Color DiscRimColor { get; set; } = new(0.4f, 0.7f, 1.0f, 0.9f);
    [Export] public Color HorizonGuideColor { get; set; } = new(1.0f, 1.0f, 1.0f, 0.35f);
    [Export] public Color PivotMarkerColor { get; set; } = new(0.3f, 0.85f, 1.0f, 0.8f);

    private Node3D _aimRoot = null!;
    private Node3D _discVisualRoot = null!;
    private Node3D _lineRoot = null!;

    private MeshInstance3D _pivotMarker = null!;
    private MeshInstance3D _lineShaft = null!;
    private MeshInstance3D _arrowHead = null!;
    private MeshInstance3D _discBody = null!;
    private MeshInstance3D _discRim = null!;
    private MeshInstance3D _discForwardArrow = null!;
    private MeshInstance3D _horizonGuide = null!;

    private CylinderMesh _shaftMesh = null!;
    private CylinderMesh _arrowHeadMesh = null!;

    private StandardMaterial3D _lineMaterial = null!;
    private StandardMaterial3D _discBodyMaterial = null!;
    private StandardMaterial3D _discRimMaterial = null!;
    private StandardMaterial3D _discArrowMaterial = null!;
    private StandardMaterial3D _horizonMaterial = null!;
    private StandardMaterial3D _pivotMaterial = null!;

    public override void _Ready()
    {
        CreateMaterials();
        BuildVisualHierarchy();
    }

    public override void _Process(double delta)
    {
        if (ThrowController == null)
        {
            return;
        }

        // Hide visualization during active flight
        if (Disc != null && Disc.IsFlying)
        {
            Visible = false;
            return;
        }

        Visible = true;

        if (Disc != null)
        {
            GlobalPosition = Disc.GlobalPosition;
        }

        UpdateAimOrientation();
        UpdatePowerVisuals();
    }

    private void CreateMaterials()
    {
        _lineMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = LowPowerColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _discBodyMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            AlbedoColor = DiscPreviewColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.3f,
            Metallic = 0.1f,
            EmissionEnabled = true,
            Emission = DiscPreviewColor * 0.2f
        };

        _discRimMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = DiscRimColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        _discArrowMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.9f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        _horizonMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = HorizonGuideColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        _pivotMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = PivotMarkerColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
    }

    private void BuildVisualHierarchy()
    {
        // 1. Pivot point marker at origin
        _pivotMarker = new MeshInstance3D
        {
            Name = "PivotMarker",
            Mesh = new SphereMesh
            {
                Radius = LineRadius * 2.5f,
                Height = LineRadius * 5.0f
            },
            MaterialOverride = _pivotMaterial
        };
        AddChild(_pivotMarker);

        // 2. Aim Root node (handles Yaw & Pitch)
        _aimRoot = new Node3D { Name = "AimRoot" };
        AddChild(_aimRoot);

        // 2a. Horizon guide bar (stays horizontal relative to roll so player sees tilt angle against level)
        _horizonGuide = new MeshInstance3D
        {
            Name = "HorizonGuide",
            Mesh = new BoxMesh
            {
                Size = new Vector3(HorizonGuideWidth, 0.008f, 0.008f)
            },
            MaterialOverride = _horizonMaterial
        };
        _aimRoot.AddChild(_horizonGuide);

        // 2b. Disc visual root (rolls around local launch forward axis with ReleaseAngle)
        _discVisualRoot = new Node3D { Name = "DiscVisualRoot" };
        _aimRoot.AddChild(_discVisualRoot);

        BuildDiscPreviewMesh();

        // 2c. Direction line root (extends along local -Z / forward vector)
        _lineRoot = new Node3D { Name = "LineRoot" };
        _aimRoot.AddChild(_lineRoot);

        BuildDirectionLineMesh();
    }

    private void BuildDiscPreviewMesh()
    {
        // Flat cylinder representing disc body
        _discBody = new MeshInstance3D
        {
            Name = "DiscBody",
            Mesh = new CylinderMesh
            {
                TopRadius = DiscRadius,
                BottomRadius = DiscRadius,
                Height = DiscThickness,
                RadialSegments = 32
            },
            MaterialOverride = _discBodyMaterial
        };
        _discVisualRoot.AddChild(_discBody);

        // Outer rim / halo ring for clear edge silhouette
        _discRim = new MeshInstance3D
        {
            Name = "DiscRim",
            Mesh = new TorusMesh
            {
                InnerRadius = DiscRadius - 0.025f,
                OuterRadius = DiscRadius + 0.01f,
                Rings = 32,
                RingSegments = 16
            },
            MaterialOverride = _discRimMaterial
        };
        _discVisualRoot.AddChild(_discRim);

        // Direction pointer on disc top (forward notch/arrow)
        _discForwardArrow = new MeshInstance3D
        {
            Name = "DiscForwardIndicator",
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.04f, DiscThickness + 0.005f, DiscRadius * 0.7f)
            },
            Position = new Vector3(0.0f, 0.0f, -DiscRadius * 0.4f),
            MaterialOverride = _discArrowMaterial
        };
        _discVisualRoot.AddChild(_discForwardArrow);
    }

    private void BuildDirectionLineMesh()
    {
        // Line shaft pointing along local -Z
        _shaftMesh = new CylinderMesh
        {
            TopRadius = LineRadius,
            BottomRadius = LineRadius,
            Height = MinLineLength,
            RadialSegments = 12
        };

        _lineShaft = new MeshInstance3D
        {
            Name = "LineShaft",
            Mesh = _shaftMesh,
            MaterialOverride = _lineMaterial,
            // Rotate cylinder from Y-axis to Z-axis
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f)
        };
        _lineRoot.AddChild(_lineShaft);

        // Arrow tip cone at the end of the shaft
        _arrowHeadMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = ArrowHeadRadius,
            Height = ArrowHeadLength,
            RadialSegments = 12
        };

        _arrowHead = new MeshInstance3D
        {
            Name = "ArrowHead",
            Mesh = _arrowHeadMesh,
            MaterialOverride = _lineMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f)
        };
        _lineRoot.AddChild(_arrowHead);
    }

    private void UpdateAimOrientation()
    {
        // Rotate aim root to match throw Yaw and Pitch
        Transform3D aimTransform = Transform3D.Identity;
        aimTransform = aimTransform.Rotated(Vector3.Up, Mathf.DegToRad(ThrowController!.Yaw));
        aimTransform = aimTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(ThrowController.Pitch));
        _aimRoot.Transform = aimTransform;

        // Roll disc visual root around local Forward (-Z) by ReleaseAngle (Hyzer / Anhyzer)
        _discVisualRoot.Transform = Transform3D.Identity.Rotated(
            Vector3.Forward,
            Mathf.DegToRad(ThrowController.ReleaseAngle)
        );
    }

    private void UpdatePowerVisuals()
    {
        float power = Mathf.Clamp(ThrowController!.Power, 0.0f, 1.0f);
        float lineLength = Mathf.Lerp(MinLineLength, MaxLineLength, power);

        // Update shaft length and position along forward (-Z)
        _shaftMesh.Height = lineLength;
        _lineShaft.Position = new Vector3(0.0f, 0.0f, -lineLength * 0.5f);

        // Update arrow head position at the end of the shaft
        _arrowHead.Position = new Vector3(0.0f, 0.0f, -lineLength - ArrowHeadLength * 0.5f);

        // Calculate power gradient color
        Color powerColor;
        if (power < 0.5f)
        {
            float t = power / 0.5f;
            powerColor = LowPowerColor.Lerp(MidPowerColor, t);
        }
        else
        {
            float t = (power - 0.5f) / 0.5f;
            powerColor = MidPowerColor.Lerp(HighPowerColor, t);
        }

        _lineMaterial.AlbedoColor = powerColor;
        _discRimMaterial.AlbedoColor = powerColor;
        _pivotMaterial.AlbedoColor = powerColor;
    }
}
