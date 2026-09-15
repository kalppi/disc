using Godot;
using System.Collections.Generic;

public partial class AimVisualizer : Node3D
{
    [Export] public ThrowController? ThrowController { get; set; }
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Aim Preview Line")]
    [Export] public float MinLineLength { get; set; } = 5.0f;
    [Export] public float MaxLineLength { get; set; } = 25.0f;
    [Export] public float LineRadius { get; set; } = 0.035f;
    [Export] public float ArrowHeadLength { get; set; } = 0.60f;
    [Export] public float ArrowHeadRadius { get; set; } = 0.12f;

    [ExportGroup("Forward Level Reference Line")]
    [Export] public float ForwardReferenceLength { get; set; } = 25.0f;
    [Export] public float ForwardReferenceRadius { get; set; } = 0.025f;
    [Export] public Color ForwardReferenceColor { get; set; } = new(1.0f, 1.0f, 1.0f, 0.95f);
    [Export] public Color HorizonCrossbarColor { get; set; } = new(0.3f, 0.85f, 1.0f, 0.90f);
    [Export] public float HorizonCrossbarWidth { get; set; } = 1.5f;

    [ExportGroup("Colors")]
    [Export] public Color LowPowerColor { get; set; } = new(0.15f, 0.85f, 1.0f, 1.0f);
    [Export] public Color MidPowerColor { get; set; } = new(1.0f, 0.85f, 0.15f, 1.0f);
    [Export] public Color HighPowerColor { get; set; } = new(1.0f, 0.22f, 0.10f, 1.0f);
    [Export] public Color PivotMarkerColor { get; set; } = new(0.2f, 0.9f, 1.0f, 0.95f);

    // Root nodes
    private Node3D _headingRoot = null!;  // Rotates ONLY with Yaw (Horizontal Level Plane at Pitch = 0)
    private Node3D _aimRoot = null!;      // Rotates with Yaw + Pitch

    // Forward Level Reference (Always points directly forward horizontally at Pitch = 0)
    private MeshInstance3D _forwardRefLine = null!;
    private MeshInstance3D _forwardArrowHead = null!;
    private MeshInstance3D _horizonCrossbar = null!;
    private MeshInstance3D _verticalConnector = null!;
    private readonly List<MeshInstance3D> _forwardTicks = new();

    private CylinderMesh _forwardRefMesh = null!;
    private CylinderMesh _forwardArrowMesh = null!;
    private CylinderMesh _verticalConnectorMesh = null!;

    // Aim Preview Line
    private MeshInstance3D _pivotMarker = null!;
    private MeshInstance3D _lineShaft = null!;
    private MeshInstance3D _arrowHead = null!;
    private CylinderMesh _shaftMesh = null!;
    private CylinderMesh _arrowHeadMesh = null!;

    // Materials
    private StandardMaterial3D _lineMaterial = null!;
    private StandardMaterial3D _forwardRefMaterial = null!;
    private StandardMaterial3D _horizonCrossbarMaterial = null!;
    private StandardMaterial3D _connectorMaterial = null!;
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
        // 1. Aim Preview Line Material
        _lineMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = LowPowerColor,
            RenderPriority = 2,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        // 2. High-contrast Forward Level Reference Material (Crisp White with NoDepthTest so it never gets hidden)
        _forwardRefMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = ForwardReferenceColor,
            RenderPriority = 1,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _horizonCrossbarMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = HorizonCrossbarColor,
            RenderPriority = 1,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _connectorMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.70f),
            RenderPriority = 1,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _pivotMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = PivotMarkerColor
        };
    }

    private void BuildVisualHierarchy()
    {
        // 1. Pivot point marker at disc center
        _pivotMarker = new MeshInstance3D
        {
            Name = "PivotMarker",
            Mesh = new SphereMesh
            {
                Radius = LineRadius * 2.2f,
                Height = LineRadius * 4.4f
            },
            MaterialOverride = _pivotMaterial
        };
        AddChild(_pivotMarker);

        // 2. Heading Root (Yaw only - stays strictly horizontal at pitch = 0)
        _headingRoot = new Node3D { Name = "HeadingRoot" };
        AddChild(_headingRoot);

        BuildForwardReference();

        // 3. Aim Root (Yaw + Pitch)
        _aimRoot = new Node3D { Name = "AimRoot" };
        AddChild(_aimRoot);

        BuildAimPreviewLine();
    }

    private void BuildForwardReference()
    {
        // A. Horizontal Level Crossbar across origin
        _horizonCrossbar = new MeshInstance3D
        {
            Name = "HorizonCrossbar",
            Mesh = new BoxMesh
            {
                Size = new Vector3(HorizonCrossbarWidth, 0.012f, 0.02f)
            },
            MaterialOverride = _horizonCrossbarMaterial
        };
        _headingRoot.AddChild(_horizonCrossbar);

        // B. Straight Forward Reference Line Shaft
        _forwardRefMesh = new CylinderMesh
        {
            TopRadius = ForwardReferenceRadius,
            BottomRadius = ForwardReferenceRadius,
            Height = ForwardReferenceLength,
            RadialSegments = 12
        };
        _forwardRefLine = new MeshInstance3D
        {
            Name = "ForwardReferenceLine",
            Mesh = _forwardRefMesh,
            MaterialOverride = _forwardRefMaterial,
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
            Position = new Vector3(0.0f, 0.0f, -ForwardReferenceLength * 0.5f)
        };
        _headingRoot.AddChild(_forwardRefLine);

        // C. Forward Level Arrow Head
        _forwardArrowMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = ForwardReferenceRadius * 3.0f,
            Height = ArrowHeadLength * 0.7f,
            RadialSegments = 12
        };
        _forwardArrowHead = new MeshInstance3D
        {
            Name = "ForwardArrowHead",
            Mesh = _forwardArrowMesh,
            MaterialOverride = _forwardRefMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Position = new Vector3(0.0f, 0.0f, -ForwardReferenceLength - ArrowHeadLength * 0.35f)
        };
        _headingRoot.AddChild(_forwardArrowHead);

        // D. Distance tick marks along the forward horizontal line (at 5m, 10m, 15m, 20m)
        _forwardTicks.Clear();
        for (float dist = 5.0f; dist < ForwardReferenceLength; dist += 5.0f)
        {
            var tick = new MeshInstance3D
            {
                Name = $"ForwardTick_{dist:F0}m",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(0.40f, 0.01f, 0.04f)
                },
                MaterialOverride = _forwardRefMaterial,
                Position = new Vector3(0.0f, 0.0f, -dist)
            };
            _headingRoot.AddChild(tick);
            _forwardTicks.Add(tick);
        }

        // E. Vertical connector line between aim tip and horizontal forward plane
        _verticalConnectorMesh = new CylinderMesh
        {
            TopRadius = 0.015f,
            BottomRadius = 0.015f,
            Height = 1.0f,
            RadialSegments = 8
        };
        _verticalConnector = new MeshInstance3D
        {
            Name = "VerticalConnector",
            Mesh = _verticalConnectorMesh,
            MaterialOverride = _connectorMaterial,
            Visible = false
        };
        _headingRoot.AddChild(_verticalConnector);
    }

    private void BuildAimPreviewLine()
    {
        // A. Aim Line Shaft
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
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f)
        };
        _aimRoot.AddChild(_lineShaft);

        // B. Aim Arrow Head
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
        _aimRoot.AddChild(_arrowHead);
    }

    private void UpdateAimOrientation()
    {
        // 1. Heading root stays strictly horizontal (Pitch = 0, Roll = 0) pointing directly forward
        _headingRoot.Transform = Transform3D.Identity.Rotated(Vector3.Up, Mathf.DegToRad(ThrowController!.Yaw));

        // 2. Aim root rotates with both Yaw and Pitch
        Transform3D aimTransform = Transform3D.Identity;
        aimTransform = aimTransform.Rotated(Vector3.Up, Mathf.DegToRad(ThrowController.Yaw));
        aimTransform = aimTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(ThrowController.Pitch));
        _aimRoot.Transform = aimTransform;

        // 3. Orient the actual DiscVisual node with Yaw, Pitch, and ReleaseAngle (Hyzer/Anhyzer)
        if (Disc?.DiscVisual != null)
        {
            Transform3D discTransform = aimTransform.RotatedLocal(
                Vector3.Forward,
                Mathf.DegToRad(ThrowController.ReleaseAngle)
            );
            Disc.DiscVisual.Transform = discTransform;
        }
    }

    private void UpdatePowerVisuals()
    {
        float power = Mathf.Clamp(ThrowController!.Power, 0.0f, 1.0f);
        float lineLength = Mathf.Lerp(MinLineLength, MaxLineLength, power);
        float pitch = ThrowController.Pitch;

        // Update aim shaft length and position along -Z
        _shaftMesh.Height = lineLength;
        _lineShaft.Position = new Vector3(0.0f, 0.0f, -lineLength * 0.5f);

        // Update arrow head position at the end of the shaft
        _arrowHead.Position = new Vector3(0.0f, 0.0f, -lineLength - ArrowHeadLength * 0.5f);

        // Update vertical elevation connector between aim tip and horizontal line
        UpdateVerticalConnector(pitch, lineLength);

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
        _pivotMaterial.AlbedoColor = powerColor;
    }

    private void UpdateVerticalConnector(float pitchDeg, float lineLength)
    {
        if (Mathf.Abs(pitchDeg) < 1.0f)
        {
            _verticalConnector.Visible = false;
            return;
        }

        _verticalConnector.Visible = true;

        float pitchRad = Mathf.DegToRad(pitchDeg);
        // Tip position in heading root coordinates:
        // In heading root, -Z is forward, +Y is up.
        // Pitch rotates around +X (Right). Positive pitch points upward (+Y), negative downward (-Y).
        float tipY = Mathf.Sin(pitchRad) * lineLength;
        float tipZ = -Mathf.Cos(pitchRad) * lineLength;

        float height = Mathf.Abs(tipY);
        _verticalConnectorMesh.Height = Mathf.Max(height, 0.02f);
        _verticalConnector.Position = new Vector3(0.0f, tipY * 0.5f, tipZ);
    }
}
