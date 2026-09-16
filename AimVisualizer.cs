using Godot;
using System.Collections.Generic;

public partial class AimVisualizer : Node3D
{
    [Export] public ThrowController? ThrowController { get; set; }
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Aim Direction Guide Line (Full Distance)")]
    [Export] public float AimLineLength { get; set; } = 30.0f;
    [Export] public float AimLineRadius { get; set; } = 0.020f;
    [Export] public float AimArrowHeadLength { get; set; } = 0.60f;
    [Export] public float AimArrowHeadRadius { get; set; } = 0.08f;
    [Export] public Color AimGuideColor { get; set; } = new(1.0f, 1.0f, 1.0f, 0.45f);
    [Export] public bool ShowDistanceTicks { get; set; } = true;
    [Export] public float DistanceTickSpacing { get; set; } = 5.0f;

    [ExportGroup("Power Indicator Line (Dynamic Fill)")]
    [Export] public float MinPowerLength { get; set; } = 0.5f;
    [Export] public float PowerLineRadius { get; set; } = 0.038f;
    [Export] public float PowerArrowHeadLength { get; set; } = 0.55f;
    [Export] public float PowerArrowHeadRadius { get; set; } = 0.11f;

    [ExportGroup("Forward Level Reference Line (Pitch = 0)")]
    [Export] public bool ShowHorizonReference { get; set; } = true;
    [Export] public float ForwardReferenceLength { get; set; } = 30.0f;
    [Export] public float ForwardReferenceRadius { get; set; } = 0.018f;
    [Export] public Color ForwardReferenceColor { get; set; } = new(0.85f, 0.90f, 1.0f, 0.35f);
    [Export] public Color HorizonCrossbarColor { get; set; } = new(0.3f, 0.85f, 1.0f, 0.80f);
    [Export] public float HorizonCrossbarWidth { get; set; } = 1.5f;

    [ExportGroup("3D Spin & Technique Ring")]
    [Export] public bool ShowSpinPreview { get; set; } = true;
    [Export] public float SpinRingRadius { get; set; } = 0.46f;
    [Export] public Color ClockwiseColor { get; set; } = new(0.20f, 0.90f, 1.0f, 0.85f);     // Cyan for RHBH/LHFH
    [Export] public Color CounterClockwiseColor { get; set; } = new(1.0f, 0.40f, 0.90f, 0.85f); // Magenta/Pink for RHFH/LHBH

    [ExportGroup("Power Colors")]
    [Export] public Color LowPowerColor { get; set; } = new(0.15f, 0.85f, 1.0f, 1.0f);
    [Export] public Color MidPowerColor { get; set; } = new(1.0f, 0.85f, 0.15f, 1.0f);
    [Export] public Color HighPowerColor { get; set; } = new(1.0f, 0.22f, 0.10f, 1.0f);
    [Export] public Color PivotMarkerColor { get; set; } = new(0.2f, 0.9f, 1.0f, 0.95f);

    // Root nodes
    private Node3D _headingRoot = null!;  // Rotates ONLY with Yaw (Horizontal Level Plane at Pitch = 0)
    private Node3D _aimRoot = null!;      // Rotates with Yaw + Pitch (True 3D Throw Vector)
    private Node3D _discRoot = null!;     // Rotates with Yaw + Pitch + ReleaseAngle (True 3D Disc Plane)

    // 1. Horizon & Heading References (Pitch = 0)
    private MeshInstance3D _forwardRefLine = null!;
    private MeshInstance3D _forwardArrowHead = null!;
    private MeshInstance3D _verticalConnector = null!;
    private CylinderMesh _forwardRefMesh = null!;
    private CylinderMesh _forwardArrowMesh = null!;
    private CylinderMesh _verticalConnectorMesh = null!;

    // 2. Full-Distance Aim Direction Guide Line (Yaw + Pitch)
    private MeshInstance3D _aimGuideShaft = null!;
    private MeshInstance3D _aimGuideArrowHead = null!;
    private CylinderMesh _aimGuideShaftMesh = null!;
    private CylinderMesh _aimGuideArrowMesh = null!;
    private readonly List<MeshInstance3D> _aimTicks = new();

    // 3. Dynamic Power Fill Line (Scales along Aim Guide with Power)
    private MeshInstance3D _pivotMarker = null!;
    private MeshInstance3D _powerShaft = null!;
    private MeshInstance3D _powerArrowHead = null!;
    private CylinderMesh _powerShaftMesh = null!;
    private CylinderMesh _powerArrowHeadMesh = null!;

    // 4. 3D Spin Direction Orbital Ring & Direction Indicators
    private Node3D _spinOrbitRoot = null!;
    private MeshInstance3D _spinTorus = null!;
    private MeshInstance3D _spinArrowHead1 = null!;
    private MeshInstance3D _spinArrowHead2 = null!;
    private StandardMaterial3D _spinMaterial = null!;
    private float _spinAnimPulse;

    // Materials
    private StandardMaterial3D _aimGuideMaterial = null!;
    private StandardMaterial3D _powerMaterial = null!;
    private StandardMaterial3D _forwardRefMaterial = null!;
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
        UpdateSpinPreview((float)delta);
    }

    private void CreateMaterials()
    {
        // 1. Full-Length Aim Guide Material
        _aimGuideMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = AimGuideColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 1,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        // 2. Dynamic Power Fill Material
        _powerMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = LowPowerColor,
            RenderPriority = 2,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        // 3. Level Reference Material (Pitch = 0)
        _forwardRefMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = ForwardReferenceColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 0,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _connectorMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.50f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 1,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _pivotMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = PivotMarkerColor
        };

        // 4. Spin Preview Material
        _spinMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = ClockwiseColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 3,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
    }

    private void BuildVisualHierarchy()
    {
        // Pivot point marker at disc center
        _pivotMarker = new MeshInstance3D
        {
            Name = "PivotMarker",
            Mesh = new SphereMesh
            {
                Radius = PowerLineRadius * 1.8f,
                Height = PowerLineRadius * 3.6f
            },
            MaterialOverride = _pivotMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_pivotMarker);

        // Heading Root (Yaw only - stays strictly horizontal at pitch = 0)
        _headingRoot = new Node3D { Name = "HeadingRoot" };
        AddChild(_headingRoot);
        BuildForwardReference();

        // Aim Root (Yaw + Pitch - points along true 3D throw direction)
        _aimRoot = new Node3D { Name = "AimRoot" };
        AddChild(_aimRoot);

        BuildAimDirectionGuide();
        BuildPowerFillLine();

        // Disc Root (Yaw + Pitch + ReleaseAngle - coplanar with the disc)
        _discRoot = new Node3D { Name = "DiscRoot" };
        AddChild(_discRoot);
        BuildSpinPreview();
    }

    private void BuildForwardReference()
    {
        // Forward Level Reference Shaft
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
            Position = new Vector3(0.0f, 0.0f, -ForwardReferenceLength * 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _headingRoot.AddChild(_forwardRefLine);

        // Forward Arrow Head
        _forwardArrowMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = ForwardReferenceRadius * 2.5f,
            Height = AimArrowHeadLength * 0.7f,
            RadialSegments = 12
        };
        _forwardArrowHead = new MeshInstance3D
        {
            Name = "ForwardArrowHead",
            Mesh = _forwardArrowMesh,
            MaterialOverride = _forwardRefMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Position = new Vector3(0.0f, 0.0f, -ForwardReferenceLength - AimArrowHeadLength * 0.35f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _headingRoot.AddChild(_forwardArrowHead);

        // Vertical elevation connector between aim power tip and horizontal plane
        _verticalConnectorMesh = new CylinderMesh
        {
            TopRadius = 0.012f,
            BottomRadius = 0.012f,
            Height = 1.0f,
            RadialSegments = 8
        };
        _verticalConnector = new MeshInstance3D
        {
            Name = "VerticalConnector",
            Mesh = _verticalConnectorMesh,
            MaterialOverride = _connectorMaterial,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _headingRoot.AddChild(_verticalConnector);
    }

    private void BuildAimDirectionGuide()
    {
        // 1. Full-Length Direction Shaft
        _aimGuideShaftMesh = new CylinderMesh
        {
            TopRadius = AimLineRadius,
            BottomRadius = AimLineRadius,
            Height = AimLineLength,
            RadialSegments = 12
        };
        _aimGuideShaft = new MeshInstance3D
        {
            Name = "AimGuideShaft",
            Mesh = _aimGuideShaftMesh,
            MaterialOverride = _aimGuideMaterial,
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
            Position = new Vector3(0.0f, 0.0f, -AimLineLength * 0.5f)
        };
        _aimRoot.AddChild(_aimGuideShaft);

        // 2. Aim Guide Arrow Head at distant target tip
        _aimGuideArrowMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = AimArrowHeadRadius,
            Height = AimArrowHeadLength,
            RadialSegments = 12
        };
        _aimGuideArrowHead = new MeshInstance3D
        {
            Name = "AimGuideArrowHead",
            Mesh = _aimGuideArrowMesh,
            MaterialOverride = _aimGuideMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Position = new Vector3(0.0f, 0.0f, -AimLineLength - AimArrowHeadLength * 0.5f)
        };
        _aimRoot.AddChild(_aimGuideArrowHead);

        // 3. Distance tick marks along the 3D aim vector
        _aimTicks.Clear();
        if (ShowDistanceTicks && DistanceTickSpacing > 0.5f)
        {
            for (float dist = DistanceTickSpacing; dist < AimLineLength; dist += DistanceTickSpacing)
            {
                var tick = new MeshInstance3D
                {
                    Name = $"AimTick_{dist:F0}m",
                    Mesh = new BoxMesh
                    {
                        Size = new Vector3(0.35f, 0.01f, 0.03f)
                    },
                    MaterialOverride = _aimGuideMaterial,
                    Position = new Vector3(0.0f, 0.0f, -dist)
                };
                _aimRoot.AddChild(tick);
                _aimTicks.Add(tick);
            }
        }
    }

    private void BuildPowerFillLine()
    {
        // 1. Dynamic Power Fill Shaft
        _powerShaftMesh = new CylinderMesh
        {
            TopRadius = PowerLineRadius,
            BottomRadius = PowerLineRadius,
            Height = MinPowerLength,
            RadialSegments = 12
        };
        _powerShaft = new MeshInstance3D
        {
            Name = "PowerShaft",
            Mesh = _powerShaftMesh,
            MaterialOverride = _powerMaterial,
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f)
        };
        _aimRoot.AddChild(_powerShaft);

        // 2. Power Level Tip Arrow Head
        _powerArrowHeadMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = PowerArrowHeadRadius,
            Height = PowerArrowHeadLength,
            RadialSegments = 12
        };
        _powerArrowHead = new MeshInstance3D
        {
            Name = "PowerArrowHead",
            Mesh = _powerArrowHeadMesh,
            MaterialOverride = _powerMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f)
        };
        _aimRoot.AddChild(_powerArrowHead);
    }

    private void BuildSpinPreview()
    {
        _spinOrbitRoot = new Node3D { Name = "SpinOrbitRoot" };
        _discRoot.AddChild(_spinOrbitRoot);

        // 1. Thin orbital ring encircling disc
        _spinTorus = new MeshInstance3D
        {
            Name = "SpinTorus",
            Mesh = new TorusMesh
            {
                InnerRadius = SpinRingRadius * 0.94f,
                OuterRadius = SpinRingRadius,
                Rings = 32,
                RingSegments = 12
            },
            MaterialOverride = _spinMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _spinOrbitRoot.AddChild(_spinTorus);

        // 2. Tangential directional arrow heads on front and back of ring
        var arrowMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = 0.045f,
            Height = 0.12f,
            RadialSegments = 10
        };

        _spinArrowHead1 = new MeshInstance3D
        {
            Name = "SpinArrow1",
            Mesh = arrowMesh,
            MaterialOverride = _spinMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _spinOrbitRoot.AddChild(_spinArrowHead1);

        _spinArrowHead2 = new MeshInstance3D
        {
            Name = "SpinArrow2",
            Mesh = arrowMesh,
            MaterialOverride = _spinMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _spinOrbitRoot.AddChild(_spinArrowHead2);
    }

    private void UpdateAimOrientation()
    {
        // 1. Heading root stays strictly horizontal (Pitch = 0) pointing forward along Yaw
        _headingRoot.Transform = Transform3D.Identity.Rotated(Vector3.Up, Mathf.DegToRad(ThrowController!.Yaw));
        _headingRoot.Visible = ShowHorizonReference;

        // 2. Aim root rotates with both Yaw and Pitch (Full 3D Aim Vector)
        Transform3D aimTransform = Transform3D.Identity;
        aimTransform = aimTransform.Rotated(Vector3.Up, Mathf.DegToRad(ThrowController.Yaw));
        aimTransform = aimTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(ThrowController.Pitch));
        _aimRoot.Transform = aimTransform;

        // 3. Disc root rotates with Yaw, Pitch, AND ReleaseAngle (Hyzer/Anhyzer)
        Transform3D discTransform = aimTransform.RotatedLocal(
            Vector3.Forward,
            Mathf.DegToRad(ThrowController.ReleaseAngle)
        );
        _discRoot.Transform = discTransform;

        // 4. Orient the actual DiscVisual node with the exact discTransform
        if (Disc?.DiscVisual != null)
        {
            Disc.DiscVisual.Transform = discTransform;
        }
    }

    private void UpdatePowerVisuals()
    {
        float power = Mathf.Clamp(ThrowController!.Power, 0.0f, 1.0f);
        float powerLength = Mathf.Lerp(MinPowerLength, AimLineLength, power);
        float pitch = ThrowController.Pitch;

        // 1. Update dynamic power shaft length and position along -Z
        _powerShaftMesh.Height = powerLength;
        _powerShaft.Position = new Vector3(0.0f, 0.0f, -powerLength * 0.5f);

        // 2. Update power arrow head at current charge tip
        _powerArrowHead.Position = new Vector3(0.0f, 0.0f, -powerLength - PowerArrowHeadLength * 0.5f);

        // 3. Update vertical connector between power charge tip and horizon plane
        UpdateVerticalConnector(pitch, powerLength);

        // 4. Calculate power gradient color
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

        _powerMaterial.AlbedoColor = powerColor;
        _pivotMaterial.AlbedoColor = powerColor;
    }

    private void UpdateSpinPreview(float dt)
    {
        if (!ShowSpinPreview || ThrowController == null)
        {
            _spinOrbitRoot.Visible = false;
            return;
        }

        _spinOrbitRoot.Visible = true;
        ThrowTechnique technique = ThrowController.Technique;
        float spinSign = DiscFlightController.GetSpinSign(technique);

        // Rotate arrow indicators smoothly around local Y (Disc normal)
        _spinAnimPulse += dt * 3.5f * spinSign;
        float r = SpinRingRadius;

        // Position arrow head 1 at front edge (-Z), pointing tangentially
        // For Clockwise (spinSign = +1): front moves toward +X (Right)
        // For Counter-Clockwise (spinSign = -1): front moves toward -X (Left)
        _spinArrowHead1.Position = new Vector3(0.0f, 0.0f, -r);
        _spinArrowHead1.RotationDegrees = new Vector3(0.0f, spinSign > 0 ? -90.0f : 90.0f, 90.0f);

        // Position arrow head 2 at back edge (+Z), pointing in opposing tangent
        _spinArrowHead2.Position = new Vector3(0.0f, 0.0f, r);
        _spinArrowHead2.RotationDegrees = new Vector3(0.0f, spinSign > 0 ? 90.0f : -90.0f, 90.0f);

        // Color and pulse opacity
        Color baseColor = spinSign > 0 ? ClockwiseColor : CounterClockwiseColor;
        float pulseAlpha = 0.70f + 0.25f * Mathf.Sin(_spinAnimPulse * 2.0f);
        _spinMaterial.AlbedoColor = new Color(baseColor.R, baseColor.G, baseColor.B, pulseAlpha);
    }

    private void UpdateVerticalConnector(float pitchDeg, float powerLength)
    {
        if (!ShowHorizonReference || Mathf.Abs(pitchDeg) < 1.0f)
        {
            _verticalConnector.Visible = false;
            return;
        }

        _verticalConnector.Visible = true;

        float pitchRad = Mathf.DegToRad(pitchDeg);
        float tipY = Mathf.Sin(pitchRad) * powerLength;
        float tipZ = -Mathf.Cos(pitchRad) * powerLength;

        float height = Mathf.Abs(tipY);
        _verticalConnectorMesh.Height = Mathf.Max(height, 0.02f);
        _verticalConnector.Position = new Vector3(0.0f, tipY * 0.5f, tipZ);
    }
}
