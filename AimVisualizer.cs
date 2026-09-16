using Godot;
using System.Collections.Generic;

public partial class AimVisualizer : Node3D
{
    [Export] public ThrowController? ThrowController { get; set; }
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Aim Direction Guide Line (Full Distance)")]
    [Export] public float AimLineLength { get; set; } = 30.0f;
    [Export] public float AimLineRadius { get; set; } = 0.045f;
    [Export] public float AimArrowHeadLength { get; set; } = 0.75f;
    [Export] public float AimArrowHeadRadius { get; set; } = 0.12f;
    [Export] public Color AimGuideColor { get; set; } = new(1.0f, 1.0f, 1.0f, 0.45f);
    [Export] public bool ShowDistanceTicks { get; set; } = true;
    [Export] public float DistanceTickSpacing { get; set; } = 5.0f;
    [Export] public float DistanceTickWidth { get; set; } = 0.50f;
    [Export] public float DistanceTickThickness { get; set; } = 0.035f;

    [ExportGroup("Power Indicator Line (Dynamic Fill)")]
    [Export] public float MinPowerLength { get; set; } = 0.5f;
    [Export] public float PowerLineRadius { get; set; } = 0.070f;
    [Export] public float PowerArrowHeadLength { get; set; } = 0.70f;
    [Export] public float PowerArrowHeadRadius { get; set; } = 0.15f;

    [ExportGroup("Forward Level Reference Line (Pitch = 0)")]
    [Export] public bool ShowHorizonReference { get; set; } = true;
    [Export] public float ForwardReferenceLength { get; set; } = 30.0f;
    [Export] public float ForwardReferenceRadius { get; set; } = 0.035f;
    [Export] public float VerticalConnectorRadius { get; set; } = 0.025f;
    [Export] public Color ForwardReferenceColor { get; set; } = new(0.85f, 0.90f, 1.0f, 0.35f);
    [Export] public Color HorizonCrossbarColor { get; set; } = new(0.3f, 0.85f, 1.0f, 0.80f);
    [Export] public float HorizonCrossbarWidth { get; set; } = 1.5f;

    [ExportGroup("3D Spin & Technique Ring")]
    [Export] public bool ShowSpinPreview { get; set; } = true;
    [Export] public float SpinRingRadius { get; set; } = 0.48f;
    [Export] public float SpinRingThickness { get; set; } = 0.035f;
    [Export] public Color ClockwiseColor { get; set; } = new(0.20f, 0.90f, 1.0f, 0.85f);
    [Export] public Color CounterClockwiseColor { get; set; } = new(1.0f, 0.40f, 0.90f, 0.85f);

    [ExportGroup("Training Mode & Trajectory Arc Preview")]
    [Export] public bool TrainingModeRouteEnabled { get; set; } = true;
    [Export] public Key ToggleRouteKey { get; set; } = Key.V;
    [Export] public int TrajectorySteps { get; set; } = 56;
    [Export] public float TrajectoryStepDt { get; set; } = 0.045f;
    [Export] public float TrajectoryThickness { get; set; } = 0.045f;
    [Export] public int TrajectoryRadialSegments { get; set; } = 8;
    [Export] public bool ShowPredictedLandingTarget { get; set; } = true;
    [Export] public Color TrajectoryStartColor { get; set; } = new(0.25f, 0.85f, 1.0f, 0.90f);
    [Export] public Color TrajectoryApexColor { get; set; } = new(1.0f, 0.90f, 0.25f, 0.95f);
    [Export] public Color TrajectoryLandingColor { get; set; } = new(0.95f, 0.35f, 0.15f, 0.90f);

    [ExportGroup("Power Colors")]
    [Export] public Color LowPowerColor { get; set; } = new(0.15f, 0.85f, 1.0f, 1.0f);
    [Export] public Color MidPowerColor { get; set; } = new(1.0f, 0.85f, 0.15f, 1.0f);
    [Export] public Color HighPowerColor { get; set; } = new(1.0f, 0.22f, 0.10f, 1.0f);
    [Export] public Color PivotMarkerColor { get; set; } = new(0.2f, 0.9f, 1.0f, 0.95f);

    // Root nodes
    private Node3D _headingRoot = null!;
    private Node3D _aimRoot = null!;
    private Node3D _discRoot = null!;
    private Node3D _trajectoryRoot = null!;

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

    // 3. Dynamic Power Fill Line
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

    // 5. Procedural 3D Trajectory Tube Mesh & Landing Target
    private MeshInstance3D _trajectoryTube = null!;
    private ArrayMesh _trajectoryMesh = null!;
    private MeshInstance3D _predictedLandingTarget = null!;
    private StandardMaterial3D _trajectoryMaterial = null!;
    private StandardMaterial3D _landingMaterial = null!;

    // Materials
    private StandardMaterial3D _aimGuideMaterial = null!;
    private StandardMaterial3D _powerMaterial = null!;
    private StandardMaterial3D _forwardRefMaterial = null!;
    private StandardMaterial3D _connectorMaterial = null!;
    private StandardMaterial3D _pivotMaterial = null!;

    // Reusable tube mesh builder & flight simulation buffer
    private TubeMeshBuilder _tubeMeshBuilder = null!;
    private readonly List<Vector3> _simPoints = new(64);

    // Caching & Dirty flag optimization state
    private bool _needsInitialBuild = true;
    private Vector3 _cachedDiscPos = Vector3.Zero;
    private float _cachedYaw = float.NaN;
    private float _cachedPitch = float.NaN;
    private float _cachedPower = float.NaN;
    private float _cachedReleaseAngle = float.NaN;
    private ThrowTechnique _cachedTechnique = (ThrowTechnique)(-1);
    private bool _cachedRouteEnabled;
    private bool _cachedShowHorizon;

    public override void _Ready()
    {
        _tubeMeshBuilder = new TubeMeshBuilder();
        CreateMaterials();
        BuildVisualHierarchy();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == ToggleRouteKey)
            {
                TrainingModeRouteEnabled = !TrainingModeRouteEnabled;
                GetViewport()?.SetInputAsHandled();
            }
        }
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
            if (Visible)
            {
                Visible = false;
            }
            return;
        }

        if (!Visible)
        {
            Visible = true;
            _needsInitialBuild = true;
        }

        Vector3 currentDiscPos = Disc != null ? Disc.GlobalPosition : GlobalPosition;
        bool posChanged = _needsInitialBuild || _cachedDiscPos.DistanceSquaredTo(currentDiscPos) > 0.0001f;
        if (posChanged)
        {
            GlobalPosition = currentDiscPos;
            _cachedDiscPos = currentDiscPos;
        }

        float curYaw = ThrowController.Yaw;
        float curPitch = ThrowController.Pitch;
        float curPower = ThrowController.Power;
        float curAngle = ThrowController.ReleaseAngle;
        ThrowTechnique curTechnique = ThrowController.Technique;

        bool orientationDirty = _needsInitialBuild ||
                                Mathf.Abs(_cachedYaw - curYaw) > 0.001f ||
                                Mathf.Abs(_cachedPitch - curPitch) > 0.001f ||
                                Mathf.Abs(_cachedReleaseAngle - curAngle) > 0.001f ||
                                _cachedShowHorizon != ShowHorizonReference;

        bool powerDirty = _needsInitialBuild ||
                         Mathf.Abs(_cachedPower - curPower) > 0.0005f ||
                         Mathf.Abs(_cachedPitch - curPitch) > 0.001f;

        bool trajectoryDirty = _needsInitialBuild ||
                               posChanged ||
                               orientationDirty ||
                               powerDirty ||
                               _cachedRouteEnabled != TrainingModeRouteEnabled ||
                               _cachedTechnique != curTechnique;

        // 1. Update aim orientation only when angles change
        if (orientationDirty)
        {
            UpdateAimOrientation(curYaw, curPitch, curAngle);
            _cachedYaw = curYaw;
            _cachedPitch = curPitch;
            _cachedReleaseAngle = curAngle;
            _cachedShowHorizon = ShowHorizonReference;
        }

        // 2. Update power indicator only when power or pitch changes
        if (powerDirty)
        {
            UpdatePowerVisuals(curPower, curPitch);
            _cachedPower = curPower;
        }

        // 3. Update spin preview
        UpdateSpinPreview((float)delta, curTechnique);

        // 4. Rebuild trajectory 3D tube mesh ONLY when inputs change
        if (trajectoryDirty)
        {
            UpdateTrajectoryArc(curPower, curAngle, curTechnique);
            _cachedRouteEnabled = TrainingModeRouteEnabled;
            _cachedTechnique = curTechnique;
        }

        _needsInitialBuild = false;
    }

    private void CreateMaterials()
    {
        _aimGuideMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = AimGuideColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 1,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _powerMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = LowPowerColor,
            RenderPriority = 2,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

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
            AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 1,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _pivotMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = PivotMarkerColor
        };

        _spinMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = ClockwiseColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 3,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _trajectoryMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 3,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _landingMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = TrajectoryLandingColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            RenderPriority = 2,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
    }

    private void BuildVisualHierarchy()
    {
        _pivotMarker = new MeshInstance3D
        {
            Name = "PivotMarker",
            Mesh = new SphereMesh
            {
                Radius = PowerLineRadius * 1.5f,
                Height = PowerLineRadius * 3.0f
            },
            MaterialOverride = _pivotMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_pivotMarker);

        _headingRoot = new Node3D { Name = "HeadingRoot" };
        AddChild(_headingRoot);
        BuildForwardReference();

        _aimRoot = new Node3D { Name = "AimRoot" };
        AddChild(_aimRoot);
        BuildAimDirectionGuide();
        BuildPowerFillLine();

        _discRoot = new Node3D { Name = "DiscRoot" };
        AddChild(_discRoot);
        BuildSpinPreview();

        _trajectoryRoot = new Node3D
        {
            Name = "TrajectoryRoot",
            TopLevel = true
        };
        AddChild(_trajectoryRoot);
        BuildTrajectoryVisuals();
    }

    private void BuildForwardReference()
    {
        _forwardRefMesh = new CylinderMesh
        {
            TopRadius = ForwardReferenceRadius,
            BottomRadius = ForwardReferenceRadius,
            Height = ForwardReferenceLength,
            RadialSegments = 16
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

        _forwardArrowMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = ForwardReferenceRadius * 2.5f,
            Height = AimArrowHeadLength * 0.75f,
            RadialSegments = 16
        };
        _forwardArrowHead = new MeshInstance3D
        {
            Name = "ForwardArrowHead",
            Mesh = _forwardArrowMesh,
            MaterialOverride = _forwardRefMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Position = new Vector3(0.0f, 0.0f, -ForwardReferenceLength - AimArrowHeadLength * 0.375f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _headingRoot.AddChild(_forwardArrowHead);

        _verticalConnectorMesh = new CylinderMesh
        {
            TopRadius = VerticalConnectorRadius,
            BottomRadius = VerticalConnectorRadius,
            Height = 1.0f,
            RadialSegments = 12
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
        _aimGuideShaftMesh = new CylinderMesh
        {
            TopRadius = AimLineRadius,
            BottomRadius = AimLineRadius,
            Height = AimLineLength,
            RadialSegments = 16
        };
        _aimGuideShaft = new MeshInstance3D
        {
            Name = "AimGuideShaft",
            Mesh = _aimGuideShaftMesh,
            MaterialOverride = _aimGuideMaterial,
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
            Position = new Vector3(0.0f, 0.0f, -AimLineLength * 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _aimRoot.AddChild(_aimGuideShaft);

        _aimGuideArrowMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = AimArrowHeadRadius,
            Height = AimArrowHeadLength,
            RadialSegments = 16
        };
        _aimGuideArrowHead = new MeshInstance3D
        {
            Name = "AimGuideArrowHead",
            Mesh = _aimGuideArrowMesh,
            MaterialOverride = _aimGuideMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Position = new Vector3(0.0f, 0.0f, -AimLineLength - AimArrowHeadLength * 0.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _aimRoot.AddChild(_aimGuideArrowHead);

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
                        Size = new Vector3(DistanceTickWidth, DistanceTickThickness, DistanceTickThickness * 1.5f)
                    },
                    MaterialOverride = _aimGuideMaterial,
                    Position = new Vector3(0.0f, 0.0f, -dist),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                };
                _aimRoot.AddChild(tick);
                _aimTicks.Add(tick);
            }
        }
    }

    private void BuildPowerFillLine()
    {
        _powerShaftMesh = new CylinderMesh
        {
            TopRadius = PowerLineRadius,
            BottomRadius = PowerLineRadius,
            Height = MinPowerLength,
            RadialSegments = 16
        };
        _powerShaft = new MeshInstance3D
        {
            Name = "PowerShaft",
            Mesh = _powerShaftMesh,
            MaterialOverride = _powerMaterial,
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _aimRoot.AddChild(_powerShaft);

        _powerArrowHeadMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = PowerArrowHeadRadius,
            Height = PowerArrowHeadLength,
            RadialSegments = 16
        };
        _powerArrowHead = new MeshInstance3D
        {
            Name = "PowerArrowHead",
            Mesh = _powerArrowHeadMesh,
            MaterialOverride = _powerMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _aimRoot.AddChild(_powerArrowHead);
    }

    private void BuildSpinPreview()
    {
        _spinOrbitRoot = new Node3D { Name = "SpinOrbitRoot" };
        _discRoot.AddChild(_spinOrbitRoot);

        _spinTorus = new MeshInstance3D
        {
            Name = "SpinTorus",
            Mesh = new TorusMesh
            {
                InnerRadius = Mathf.Max(0.1f, SpinRingRadius - SpinRingThickness),
                OuterRadius = SpinRingRadius + SpinRingThickness,
                Rings = 32,
                RingSegments = 16
            },
            MaterialOverride = _spinMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _spinOrbitRoot.AddChild(_spinTorus);

        var arrowMesh = new CylinderMesh
        {
            TopRadius = 0.0f,
            BottomRadius = 0.055f,
            Height = 0.15f,
            RadialSegments = 12
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

    private void BuildTrajectoryVisuals()
    {
        _trajectoryMesh = new ArrayMesh();
        _trajectoryTube = new MeshInstance3D
        {
            Name = "TrajectoryTube",
            Mesh = _trajectoryMesh,
            MaterialOverride = _trajectoryMaterial,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _trajectoryRoot.AddChild(_trajectoryTube);

        var ringMesh = new TorusMesh
        {
            InnerRadius = 0.50f,
            OuterRadius = 0.70f,
            Rings = 28,
            RingSegments = 16
        };

        _predictedLandingTarget = new MeshInstance3D
        {
            Name = "PredictedLandingTarget",
            Mesh = ringMesh,
            MaterialOverride = _landingMaterial,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _trajectoryRoot.AddChild(_predictedLandingTarget);
    }

    private void UpdateAimOrientation(float yaw, float pitch, float releaseAngle)
    {
        _headingRoot.Transform = Transform3D.Identity.Rotated(Vector3.Up, Mathf.DegToRad(yaw));
        _headingRoot.Visible = ShowHorizonReference;

        Transform3D aimTransform = Transform3D.Identity;
        aimTransform = aimTransform.Rotated(Vector3.Up, Mathf.DegToRad(yaw));
        aimTransform = aimTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(pitch));
        _aimRoot.Transform = aimTransform;

        Transform3D discTransform = aimTransform.RotatedLocal(
            Vector3.Forward,
            Mathf.DegToRad(releaseAngle)
        );
        _discRoot.Transform = discTransform;

        if (Disc?.DiscVisual != null)
        {
            Disc.DiscVisual.Transform = discTransform;
        }
    }

    private void UpdatePowerVisuals(float power, float pitch)
    {
        float clampedPower = Mathf.Clamp(power, 0.0f, 1.0f);
        float powerLength = Mathf.Lerp(MinPowerLength, AimLineLength, clampedPower);

        _powerShaftMesh.Height = powerLength;
        _powerShaft.Position = new Vector3(0.0f, 0.0f, -powerLength * 0.5f);
        _powerArrowHead.Position = new Vector3(0.0f, 0.0f, -powerLength - PowerArrowHeadLength * 0.5f);

        UpdateVerticalConnector(pitch, powerLength);

        Color powerColor;
        if (clampedPower < 0.5f)
        {
            float t = clampedPower / 0.5f;
            powerColor = LowPowerColor.Lerp(MidPowerColor, t);
        }
        else
        {
            float t = (clampedPower - 0.5f) / 0.5f;
            powerColor = MidPowerColor.Lerp(HighPowerColor, t);
        }

        _powerMaterial.AlbedoColor = powerColor;
        _pivotMaterial.AlbedoColor = powerColor;
    }

    private void UpdateSpinPreview(float dt, ThrowTechnique technique)
    {
        if (!ShowSpinPreview)
        {
            if (_spinOrbitRoot.Visible)
            {
                _spinOrbitRoot.Visible = false;
            }
            return;
        }

        if (!_spinOrbitRoot.Visible)
        {
            _spinOrbitRoot.Visible = true;
        }

        float spinSign = DiscFlightController.GetSpinSign(technique);
        _spinAnimPulse += dt * 3.5f * spinSign;
        float r = SpinRingRadius;

        _spinArrowHead1.Position = new Vector3(0.0f, 0.0f, -r);
        _spinArrowHead1.RotationDegrees = new Vector3(0.0f, spinSign > 0 ? -90.0f : 90.0f, 90.0f);

        _spinArrowHead2.Position = new Vector3(0.0f, 0.0f, r);
        _spinArrowHead2.RotationDegrees = new Vector3(0.0f, spinSign > 0 ? 90.0f : -90.0f, 90.0f);

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

    private void UpdateTrajectoryArc(float power, float releaseAngle, ThrowTechnique technique)
    {
        if (!TrainingModeRouteEnabled || Disc == null || ThrowController == null)
        {
            _trajectoryRoot.Visible = false;
            return;
        }

        _trajectoryRoot.Visible = true;

        _simPoints.Clear();

        Vector3 currentPos = Disc.GlobalPosition;
        float speed = Mathf.Lerp(Disc.MinThrowSpeed, Disc.MaxThrowSpeed, power);
        Vector3 launchDir = ThrowController.Direction.Normalized();
        Vector3 simVelocity = launchDir * speed;
        float bankAngle = releaseAngle;

        float horizLen = new Vector2(launchDir.X, launchDir.Z).Length();
        float initialPitch = Mathf.Atan2(launchDir.Y, Mathf.Max(0.01f, horizLen));
        float currentPitch = initialPitch;

        float cruiseSpeed = Disc.GetRequiredCruiseSpeed();
        float spinSign = DiscFlightController.GetSpinSign(technique);
        bool isForehand = DiscFlightController.IsForehandTechnique(technique);
        float turnTorque = isForehand ? Disc.ForehandTurnTorque : 1.0f;
        float fadeBite = isForehand ? Disc.ForehandFadeBite : 1.0f;
        float glideBonus = !isForehand ? Disc.BackhandGlideBonus : 1.0f;

        float dt = TrajectoryStepDt;
        int maxSteps = Mathf.Clamp(TrajectorySteps, 8, 120);
        bool hitGround = false;
        Vector3 landingPos = Vector3.Zero;

        _simPoints.Add(currentPos);

        for (int i = 0; i < maxSteps && !hitGround; i++)
        {
            Vector3 horizVel = new(simVelocity.X, 0.0f, simVelocity.Z);
            float hSpeed = horizVel.Length();
            float tSpeed = simVelocity.Length();

            if (hSpeed < 0.2f)
            {
                break;
            }

            Vector3 fwd = horizVel.Normalized();
            Vector3 right = fwd.Cross(Vector3.Up).Normalized();

            float speedRatio = tSpeed / Mathf.Max(cruiseSpeed, 1.0f);

            // AoA & pitch
            float velPitch = Mathf.Atan2(simVelocity.Y, Mathf.Max(0.01f, hSpeed));
            float targetPitch = Mathf.Lerp(velPitch, initialPitch, Disc.NoseAnglePitchInfluence);
            currentPitch = Mathf.MoveToward(currentPitch, targetPitch, 0.75f * dt);
            float aoa = currentPitch - velPitch;
            float aoaLift = Mathf.Sin(aoa) * tSpeed * Disc.AoALiftFactor * 0.45f;
            float aoaDrag = Mathf.Sin(aoa) * Mathf.Sin(aoa) * Disc.AoAInducedDragFactor;

            // Turn & Fade
            float turnFactor = 0.0f;
            float turnRate = 0.0f;
            if (speedRatio > 0.82f && Disc.DiscTurn < 0.0f)
            {
                float rawTurn = Mathf.Clamp((speedRatio - 0.82f) / 0.40f, 0.0f, 1.5f);
                turnFactor = rawTurn * rawTurn;
                turnRate = (-Disc.DiscTurn) * Disc.TurnMultiplier * turnFactor * turnTorque;
            }

            float fadeFactor = 0.0f;
            float fadeRate = 0.0f;
            if (speedRatio < 0.78f && Disc.DiscFade > 0.0f)
            {
                float rawFade = Mathf.Clamp((0.78f - speedRatio) / 0.50f, 0.0f, 1.0f);
                fadeFactor = rawFade * rawFade;
                fadeRate = Disc.DiscFade * Disc.FadeMultiplier * fadeFactor * fadeBite;
            }

            float glideWeight = Mathf.Clamp(1.0f - (turnFactor * 1.5f + fadeFactor * 1.5f), 0.0f, 1.0f);
            if (glideWeight > 0.05f)
            {
                bankAngle = Mathf.MoveToward(bankAngle, 0.0f, 2.0f * glideWeight * dt);
            }

            bankAngle += spinSign * (turnRate - fadeRate) * dt;
            bankAngle = Mathf.Clamp(bankAngle, -Disc.MaxBankAngle, Disc.MaxBankAngle);

            // Lift & Air Cushion
            float bankRad = Mathf.DegToRad(bankAngle);
            float baseLift = tSpeed * (Disc.DiscGlide * 0.12f * Disc.GlideMultiplier * glideBonus) * 0.05f;
            float netLift = Mathf.Max(0.0f, baseLift + aoaLift);

            if (speedRatio < Disc.StallSpeedRatio)
            {
                float stallSeverity = (Disc.StallSpeedRatio - speedRatio) / Disc.StallSpeedRatio;
                netLift *= (1.0f - stallSeverity * 0.65f);
                fadeFactor = Mathf.Max(fadeFactor, stallSeverity * Disc.StallFadeSinkBoost);
            }

            float groundEffectCushion = 0.0f;
            if (Disc.EnableGroundEffect)
            {
                float groundY = Disc.GetGroundHeightAt(currentPos);
                float altitude = currentPos.Y - (groundY + Disc.DiscThickness * 0.5f);
                if (altitude < Disc.GroundEffectMaxAltitude && altitude > 0.05f)
                {
                    float gFactor = 1.0f - Mathf.Clamp(altitude / Disc.GroundEffectMaxAltitude, 0.0f, 1.0f);
                    groundEffectCushion = gFactor * gFactor;
                    netLift *= (1.0f + Disc.GroundEffectLiftBoost * groundEffectCushion);
                }
            }

            float vertLift = netLift * Mathf.Cos(bankRad);
            Vector3 latAcc = right * Mathf.Sin(bankRad) * (netLift * Disc.LateralForceMultiplier + tSpeed * 0.85f);

            simVelocity += (Vector3.Up * vertLift + latAcc) * dt;
            simVelocity += Vector3.Down * (9.8f * Disc.BaseFlightGravityScale) * dt;

            if (fadeFactor > 0.02f)
            {
                simVelocity += Vector3.Down * (Disc.DiscFade * 0.90f * fadeFactor * fadeBite) * dt;
            }

            float bankDrag = Mathf.Abs(Mathf.Sin(bankRad)) * 0.55f;
            float effDrag = (Disc.BaseAirDrag + bankDrag + aoaDrag) * (1.0f - Disc.GroundEffectDragReduction * groundEffectCushion);
            float dFactor = Mathf.Max(0.0f, 1.0f - effDrag * dt);
            simVelocity = new Vector3(simVelocity.X * dFactor, simVelocity.Y, simVelocity.Z * dFactor);

            Vector3 prevPos = currentPos;
            currentPos += simVelocity * dt;

            float terrainY = Disc.GetGroundHeightAt(currentPos);
            float groundThreshold = terrainY + Disc.DiscThickness * 0.5f;
            if (currentPos.Y <= groundThreshold)
            {
                hitGround = true;
                float alpha = Mathf.Clamp((prevPos.Y - groundThreshold) / Mathf.Max(0.001f, prevPos.Y - currentPos.Y), 0.0f, 1.0f);
                Vector3 exactTouchdown = prevPos.Lerp(currentPos, alpha);
                exactTouchdown.Y = terrainY;
                _simPoints.Add(exactTouchdown);
                landingPos = exactTouchdown;
                break;
            }

            _simPoints.Add(currentPos);
        }

        // Build / update 3D Tube Mesh with thickness via reusable TubeMeshBuilder
        bool generated = _tubeMeshBuilder.BuildTube(
            _trajectoryMesh,
            _simPoints,
            radius: TrajectoryThickness,
            radialSegments: TrajectoryRadialSegments,
            startColor: TrajectoryStartColor,
            apexColor: TrajectoryApexColor,
            endColor: TrajectoryLandingColor
        );
        _trajectoryTube.Visible = generated;

        if (hitGround && ShowPredictedLandingTarget)
        {
            _predictedLandingTarget.Visible = true;
            _predictedLandingTarget.GlobalPosition = new Vector3(landingPos.X, landingPos.Y + 0.025f, landingPos.Z);
        }
        else
        {
            _predictedLandingTarget.Visible = false;
        }
    }
}
