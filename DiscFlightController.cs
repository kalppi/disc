using Godot;

public partial class DiscFlightController : RigidBody3D
{
    public enum FlightPhase
    {
        Ready,      // Hovering at stance, ready to throw
        Launch,     // Initial release burst
        Turn,       // High-speed turn
        Glide,      // Cruise speed, maximum lift & line-holding
        Fade,       // Low-speed fade
        Ground,     // Rolling, sliding, or bouncing on surface
        Settled     // Rested on ground, awaiting hover lift
    }

    [ExportGroup("Disc Ratings (Flight Numbers)")]
    [Export(PropertyHint.Range, "1.0, 14.0, 0.5")] public float DiscSpeed { get; set; } = 9.0f;
    [Export(PropertyHint.Range, "1.0, 7.0, 0.5")]  public float DiscGlide { get; set; } = 5.0f;
    [Export(PropertyHint.Range, "-5.0, 1.0, 0.5")] public float DiscTurn { get; set; } = -1.5f;
    [Export(PropertyHint.Range, "0.0, 5.0, 0.5")]  public float DiscFade { get; set; } = 2.5f;

    [ExportGroup("Throw Setup")]
    [Export] public float HoverHeight { get; set; } = 1.5f;
    [Export] public float SettleDelay { get; set; } = 0.6f;
    [Export] public float HoverAnimationDuration { get; set; } = 0.75f;
    [Export] public float MinThrowSpeed { get; set; } = 12.0f;
    [Export] public float MaxThrowSpeed { get; set; } = 34.0f;

    [ExportGroup("Aerodynamics Tuning")]
    [Export] public float TurnMultiplier { get; set; } = 18.0f;
    [Export] public float FadeMultiplier { get; set; } = 20.0f;
    [Export] public float GlideMultiplier { get; set; } = 0.90f;
    [Export] public float BaseFlightGravityScale { get; set; } = 0.75f;
    [Export] public float BaseAirDrag { get; set; } = 0.12f;
    [Export] public float LateralForceMultiplier { get; set; } = 1.35f;
    [Export] public float MaxBankAngle { get; set; } = 60.0f;

    [ExportGroup("Angle of Attack & Stall Aerodynamics")]
    [Export] public float AoALiftFactor { get; set; } = 1.20f;
    [Export] public float AoAInducedDragFactor { get; set; } = 0.80f;
    [Export] public float NoseAnglePitchInfluence { get; set; } = 0.65f;
    [Export] public float StallSpeedRatio { get; set; } = 0.42f;
    [Export] public float StallFadeSinkBoost { get; set; } = 1.35f;

    [ExportGroup("Fairway Air Cushion (Ground Effect)")]
    [Export] public bool EnableGroundEffect { get; set; } = true;
    [Export] public float GroundEffectMaxAltitude { get; set; } = 1.25f;
    [Export] public float GroundEffectLiftBoost { get; set; } = 0.40f;
    [Export] public float GroundEffectDragReduction { get; set; } = 0.35f;

    [ExportGroup("Ground Dynamics: Flare Skips & Rollers")]
    [Export] public bool EnableFlareSkips { get; set; } = true;
    [Export] public float MinSkipSpeed { get; set; } = 7.0f;
    [Export] public float MinSkipBankAngle { get; set; } = 12.0f;
    [Export] public float MaxSkipBankAngle { get; set; } = 52.0f;
    [Export] public float SkipLiftBoost { get; set; } = 0.42f;
    [Export] public float SkipLateralMultiplier { get; set; } = 0.48f;
    [Export] public float SkipForwardRetention { get; set; } = 0.75f;
    [Export] public int MaxSkipCount { get; set; } = 3;
    [Export] public bool EnableRollers { get; set; } = true;
    [Export] public float RollerMinBankAngle { get; set; } = 52.0f;
    [Export] public float RollerSteerRate { get; set; } = 42.0f;
    [Export] public float RollerRollingFriction { get; set; } = 0.16f;
    [Export] public float RollerMaxDuration { get; set; } = 5.5f;

    [ExportGroup("Forehand vs Backhand Dynamics")]
    [Export] public float ForehandTurnTorque { get; set; } = 1.15f;
    [Export] public float ForehandFadeBite { get; set; } = 1.12f;
    [Export] public float BackhandGlideBonus { get; set; } = 1.08f;

    [ExportGroup("Physics Material Tuning")]
    [Export] public float SurfaceFriction { get; set; } = 0.45f;
    [Export] public float SurfaceBounce { get; set; } = 0.35f;
    [Export] public float DiscRadius { get; set; } = 0.35f;
    [Export] public float DiscThickness { get; set; } = 0.04f;

    [ExportGroup("Ground Indicator & Drop Line")]
    [Export] public bool ShowGroundIndicator { get; set; } = true;
    [Export] public Color GroundIndicatorColor { get; set; } = new(0.25f, 0.85f, 1.0f, 0.65f);
    [Export] public Color GroundShadowColor { get; set; } = new(0.0f, 0.0f, 0.0f, 0.35f);
    [Export] public float GroundLineRadius { get; set; } = 0.016f;

    [ExportGroup("Nodes")]
    [Export] public Node3D? DiscVisual { get; set; }
    [Export] public CollisionShape3D? DiscCollisionShape { get; set; }

    public bool IsFlying { get; private set; }
    public float FlightProgress { get; private set; }
    public float CurrentBankAngle { get; private set; }
    public float CurrentPitchAngle { get; private set; }
    public FlightPhase CurrentFlightPhase { get; private set; } = FlightPhase.Ready;
    public ThrowTechnique CurrentTechnique { get; private set; } = ThrowTechnique.RHBH;
    public float SpinSign => GetSpinSign(CurrentTechnique);
    public bool IsForehand => IsForehandTechnique(CurrentTechnique);
    public ThrowParameters CurrentThrow { get; private set; }
    public bool IsRolling => _isRolling;
    public int SkipCount => _skipCount;

    public event System.Action<FlightPhase>? FlightPhaseChanged;
    public event System.Action<float>? HoverAnimationStarted;
    public event System.Action? NewTurnStarted;

    private float _initialSpeed;
    private float _initialPitch;
    private float _flightTime;
    private float _restTimer;
    private float _groundTime;
    private float _rollerTimer;
    private int _skipCount;
    private bool _isRolling;
    private bool _hasTouchedGround;
    private bool _hasFloorContact;
    private bool _hasWallContact;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private bool _isEndingFlight;
    private Tween? _hoverTween;
    private float _spinAngle;
    private float _spinRate;
    private Vector3 _lastForward = Vector3.Forward;
    private Vector3 _lastContactNormal = Vector3.Up;
    private float _lastAppliedBankAngle = 999.0f;

    // Ground Drop-Line & Indicator Nodes
    private Node3D? _groundIndicatorRoot;
    private MeshInstance3D? _groundLineInstance;
    private MeshInstance3D? _groundRingInstance;
    private MeshInstance3D? _groundShadowInstance;
    private CylinderMesh? _groundLineMesh;

    public static float GetSpinSign(ThrowTechnique technique) => technique switch
    {
        ThrowTechnique.RHBH => 1.0f,
        ThrowTechnique.LHFH => 1.0f,
        ThrowTechnique.RHFH => -1.0f,
        ThrowTechnique.LHBH => -1.0f,
        _ => 1.0f
    };

    public static bool IsForehandTechnique(ThrowTechnique technique) =>
        technique is ThrowTechnique.RHFH or ThrowTechnique.LHFH;

    public static string GetTechniqueName(ThrowTechnique technique) => technique switch
    {
        ThrowTechnique.RHBH => "Right-Hand Backhand",
        ThrowTechnique.RHFH => "Right-Hand Forehand",
        ThrowTechnique.LHBH => "Left-Hand Backhand",
        ThrowTechnique.LHFH => "Left-Hand Forehand",
        _ => "Right-Hand Backhand"
    };

    public override void _Ready()
    {
        _startPosition = GlobalPosition;
        _startRotation = GlobalTransform.Basis.GetRotationQuaternion();

        DiscCollisionShape ??= GetNodeOrNull<CollisionShape3D>("CollisionShape3D");

        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 6;

        AxisLockAngularX = true;
        AxisLockAngularY = true;
        AxisLockAngularZ = true;

        PhysicsMaterialOverride = new PhysicsMaterial
        {
            Friction = SurfaceFriction,
            Bounce = SurfaceBounce,
            Rough = false
        };

        FreezeMode = FreezeModeEnum.Kinematic;
        Freeze = true;

        if (DiscVisual != null)
        {
            DiscVisual.Visible = true;
        }

        BuildGroundIndicator();
        SetupNewTurnHover(_startPosition);
    }

    public override void _Process(double delta)
    {
        if (ShowGroundIndicator)
        {
            UpdateGroundIndicator((float)delta);
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        _hasFloorContact = false;
        _hasWallContact = false;

        int count = state.GetContactCount();
        for (int i = 0; i < count; i++)
        {
            Vector3 normal = state.GetContactLocalNormal(i);
            _lastContactNormal = normal;

            if (normal.Y >= 0.60f)
            {
                _hasFloorContact = true;
            }
            else
            {
                _hasWallContact = true;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsFlying || _isEndingFlight)
        {
            return;
        }

        float dt = (float)delta;
        _flightTime += dt;

        // Safety watchdog: stop after 18 seconds max
        if (_flightTime > 18.0f)
        {
            EndFlight();
            return;
        }

        // 1. High-speed obstacle & wall collision sweep guard (prevents tunneling through walls & terrain)
        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState != null && LinearVelocity.LengthSquared() > 0.5f)
        {
            var sweepQuery = PhysicsRayQueryParameters3D.Create(
                GlobalPosition,
                GlobalPosition + LinearVelocity * dt * 1.35f
            );
            sweepQuery.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            var hit = spaceState.IntersectRay(sweepQuery);
            if (hit.Count > 0 && hit.TryGetValue("normal", out var normalVar))
            {
                Vector3 hitNormal = normalVar.AsVector3().Normalized();
                _lastContactNormal = hitNormal;

                if (hitNormal.Y < 0.60f)
                {
                    // Hitting a vertical wall or obstacle in the air
                    Vector3 incomingHoriz = new Vector3(LinearVelocity.X, 0.0f, LinearVelocity.Z);
                    LinearVelocity = LinearVelocity.Bounce(hitNormal) * SurfaceBounce;
                    Vector3 outgoingHoriz = new Vector3(LinearVelocity.X, 0.0f, LinearVelocity.Z);

                    if (incomingHoriz.LengthSquared() > 0.01f && outgoingHoriz.LengthSquared() > 0.01f)
                    {
                        Vector3 inDir = incomingHoriz.Normalized();
                        Vector3 outDir = outgoingHoriz.Normalized();
                        float normalDotIn = inDir.Dot(hitNormal);

                        // Reflect bank angle sign according to bounce deflection, keeping hyzer/anhyzer tilt!
                        CurrentBankAngle = -CurrentBankAngle * Mathf.Sign(normalDotIn);
                        _lastForward = outDir;
                    }
                    // Walls NEVER mark ground contact. The disc remains in the air!
                }
                else
                {
                    // Floor / surface impact detected via sweep
                    if (HandleGroundImpact(dt))
                    {
                        return;
                    }
                }
            }
        }

        // Floor contact detection via direct body state contacts
        if (_hasFloorContact)
        {
            if (HandleGroundImpact(dt))
            {
                return;
            }
        }

        if (!_hasTouchedGround)
        {
            // --- AIRBORNE AERODYNAMICS ---
            _restTimer = 0.0f;
            _groundTime = 0.0f;
            _isRolling = false;
            _rollerTimer = 0.0f;
            ApplyAirborneAerodynamics(dt);
        }
        else
        {
            // --- GROUND / SURFACE INTERACTION (ROLLERS, SLIDES & SETTLING) ---
            SetFlightPhase(FlightPhase.Ground);
            _groundTime += dt;

            float speed = LinearVelocity.Length();
            Vector3 horiz = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);

            if (_isRolling)
            {
                // Cut Roller state: Disc rolls on its edge curving in bank direction
                _rollerTimer += dt;
                float steerSign = Mathf.Sign(CurrentBankAngle);
                float steerAngleDelta = -steerSign * Mathf.DegToRad(RollerSteerRate) * dt;

                _lastForward = _lastForward.Rotated(Vector3.Up, steerAngleDelta).Normalized();

                // Apply roller rolling drag
                float rollDrag = Mathf.Max(0.0f, 1.0f - RollerRollingFriction * dt);
                LinearVelocity = new Vector3(
                    _lastForward.X * horiz.Length() * rollDrag,
                    LinearVelocity.Y,
                    _lastForward.Z * horiz.Length() * rollDrag
                );

                // Spin decays slowly while rolling
                _spinRate = Mathf.MoveToward(_spinRate, 0.0f, 25.0f * dt);
                _spinAngle += _spinRate * dt;

                // Destabilize roller into flat slide as speed drops or timer ends
                if (speed < 2.5f || _rollerTimer >= RollerMaxDuration)
                {
                    float wobbleRate = Mathf.Lerp(120.0f, 60.0f, Mathf.Clamp(speed / 2.5f, 0.0f, 1.0f));
                    CurrentBankAngle = Mathf.MoveToward(CurrentBankAngle, 0.0f, wobbleRate * dt);
                    if (Mathf.Abs(CurrentBankAngle) < 2.0f)
                    {
                        CurrentBankAngle = 0.0f;
                        _isRolling = false;
                    }
                }
            }
            else
            {
                // Standard Ground Slide: disc levels out flat as kinetic energy drops
                float fallRate = Mathf.Lerp(160.0f, 90.0f, Mathf.Clamp(speed / 5.0f, 0.0f, 1.0f));
                CurrentBankAngle = Mathf.MoveToward(CurrentBankAngle, 0.0f, fallRate * dt);
                if (Mathf.Abs(CurrentBankAngle) < 1.0f)
                {
                    CurrentBankAngle = 0.0f;
                }

                // Spin slows down on surface contact
                _spinRate = Mathf.MoveToward(_spinRate, 0.0f, 40.0f * dt);
                _spinAngle += _spinRate * dt;
            }

            // Rest detection: purely based on physical movement stopping in 3D (X, Y, Z)
            if (speed < 0.18f || Sleeping)
            {
                _restTimer += dt;
                if (_restTimer >= 0.35f)
                {
                    EndFlight();
                    return;
                }
            }
            else
            {
                _restTimer = 0.0f;
            }
        }

        // Update visual model and collision shape orientation stably
        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        Vector3 forward = _lastForward;
        if (horizVel.LengthSquared() > 0.15f)
        {
            forward = horizVel.Normalized();
            _lastForward = forward;
        }

        float verticalPitch = _hasTouchedGround ? 0.0f : CurrentPitchAngle;
        UpdateOrientation(forward, CurrentBankAngle, verticalPitch, dt, _hasTouchedGround ? _lastContactNormal : Vector3.Up);
    }

    private bool HandleGroundImpact(float dt)
    {
        float speed = LinearVelocity.Length();
        float absBank = Mathf.Abs(CurrentBankAngle);

        // 1. Flare Skips: Low/Medium bank angle (12°-52°) at high speed skips/kicks off surface
        if (EnableFlareSkips && _skipCount < MaxSkipCount && speed >= MinSkipSpeed && absBank >= MinSkipBankAngle && absBank <= MaxSkipBankAngle)
        {
            _skipCount++;
            _hasTouchedGround = false;
            ContinuousCd = true;
            GravityScale = BaseFlightGravityScale;

            Vector3 horizVel = new Vector3(LinearVelocity.X, 0.0f, LinearVelocity.Z);
            Vector3 fwd = horizVel.LengthSquared() > 0.01f ? horizVel.Normalized() : _lastForward;
            Vector3 right = fwd.Cross(Vector3.Up).Normalized();

            float bankRad = Mathf.DegToRad(CurrentBankAngle);
            float skipY = Mathf.Max(2.2f, speed * SkipLiftBoost * Mathf.Abs(Mathf.Sin(bankRad)));
            Vector3 latKick = right * Mathf.Sin(bankRad) * speed * SkipLateralMultiplier;
            Vector3 forwardVel = fwd * (speed * SkipForwardRetention);

            LinearVelocity = forwardVel + latKick + Vector3.Up * skipY;

            // Bank angle partially flattens after flare skip impact
            CurrentBankAngle *= 0.68f;

            SetFlightPhase(FlightPhase.Fade);
            return false; // Continues airborne flight!
        }

        // 2. Cut Rollers: Steep bank angle (>52°) at decent speed rolls on ground edge
        if (EnableRollers && absBank >= RollerMinBankAngle && speed >= 3.0f && !_hasTouchedGround)
        {
            _hasTouchedGround = true;
            _isRolling = true;
            _rollerTimer = 0.0f;
            ContinuousCd = false;
            GravityScale = 1.0f;
            SetFlightPhase(FlightPhase.Ground);
            return false;
        }

        // 3. Flat / Standard Surface Impact
        _hasTouchedGround = true;
        ContinuousCd = false;
        GravityScale = 1.0f;
        return false;
    }

    public void Throw(ThrowParameters parameters)
    {
        _hoverTween?.Kill();
        _hoverTween = null;
        _isEndingFlight = false;
        _flightTime = 0.0f;
        _restTimer = 0.0f;
        _groundTime = 0.0f;
        _rollerTimer = 0.0f;
        _skipCount = 0;
        _isRolling = false;
        _hasTouchedGround = false;
        _hasFloorContact = false;
        _hasWallContact = false;
        _lastAppliedBankAngle = 999.0f;
        CurrentTechnique = parameters.Technique;
        CurrentThrow = parameters;

        FreezeMode = FreezeModeEnum.Kinematic;
        Freeze = false;
        Sleeping = false;
        ContinuousCd = true;

        float speed = Mathf.Lerp(
            MinThrowSpeed,
            MaxThrowSpeed,
            parameters.Power
        );

        Vector3 launchDirection = parameters.Direction.Normalized();
        _lastForward = new Vector3(launchDirection.X, 0.0f, launchDirection.Z).Normalized();
        if (_lastForward.LengthSquared() < 0.001f)
        {
            _lastForward = Vector3.Forward;
        }

        LinearVelocity = launchDirection * speed;
        AngularVelocity = Vector3.Zero;

        _initialSpeed = speed;
        CurrentBankAngle = parameters.ReleaseAngle;

        float horizLen = new Vector2(launchDirection.X, launchDirection.Z).Length();
        _initialPitch = Mathf.Atan2(launchDirection.Y, Mathf.Max(0.01f, horizLen));
        CurrentPitchAngle = _initialPitch;

        _spinRate = 45.0f * (speed / MinThrowSpeed);
        _spinAngle = 0.0f;

        FlightProgress = 0.0f;
        GravityScale = BaseFlightGravityScale;
        IsFlying = true;

        SetFlightPhase(FlightPhase.Launch);
        UpdateOrientation(_lastForward, CurrentBankAngle, CurrentPitchAngle, 0.016f, Vector3.Up);
    }

    public void ResetPosition()
    {
        _hoverTween?.Kill();
        _hoverTween = null;
        _isEndingFlight = false;
        SetupNewTurnHover(_startPosition);
    }

    public void SetupNewTurnHover(Vector3 basePosition)
    {
        _hoverTween?.Kill();
        _hoverTween = null;
        _isEndingFlight = false;
        _flightTime = 0.0f;
        _restTimer = 0.0f;
        _groundTime = 0.0f;
        _rollerTimer = 0.0f;
        _skipCount = 0;
        _isRolling = false;
        _hasTouchedGround = false;
        _hasFloorContact = false;
        _hasWallContact = false;
        _spinAngle = 0.0f;
        _spinRate = 0.0f;
        _lastAppliedBankAngle = 999.0f;
        IsFlying = false;

        FreezeMode = FreezeModeEnum.Kinematic;
        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GravityScale = 0.0f;

        Vector3 hoverPosition = new(basePosition.X, basePosition.Y + HoverHeight, basePosition.Z);

        GlobalPosition = hoverPosition;
        GlobalTransform = new Transform3D(new Basis(_startRotation), hoverPosition);
        CurrentBankAngle = 0.0f;
        CurrentPitchAngle = 0.0f;

        if (DiscCollisionShape != null)
        {
            DiscCollisionShape.Position = Vector3.Zero;
            DiscCollisionShape.Quaternion = Quaternion.Identity;
            DiscCollisionShape.Transform = Transform3D.Identity;
        }

        if (DiscVisual != null)
        {
            DiscVisual.Position = Vector3.Zero;
            DiscVisual.Quaternion = Quaternion.Identity;
            DiscVisual.Transform = Transform3D.Identity;
            DiscVisual.Visible = true;
        }

        SetFlightPhase(FlightPhase.Ready);
        NewTurnStarted?.Invoke();
    }

    public Vector3 GetHoverPositionFor(Vector3 position)
    {
        return new Vector3(position.X, position.Y + HoverHeight, position.Z);
    }

    public float GetRequiredCruiseSpeed()
    {
        return Mathf.Lerp(13.5f, 31.0f, (DiscSpeed - 1.0f) / 13.0f);
    }

    public FlightTendency EstimateFlightTendency(ThrowParameters parameters)
    {
        float speed = Mathf.Lerp(MinThrowSpeed, MaxThrowSpeed, parameters.Power);
        float cruiseSpeed = GetRequiredCruiseSpeed();
        float speedRatio = speed / Mathf.Max(cruiseSpeed, 1.0f);
        float spinSign = GetSpinSign(CurrentTechnique);
        bool isForehand = IsForehandTechnique(CurrentTechnique);

        float launchWeight = 0.12f;
        float turnWeight = 0.0f;
        float glideWeight = 0.0f;
        float fadeWeight = 0.0f;

        // Turn weight
        float turnMultiplier = isForehand ? ForehandTurnTorque : 1.0f;
        if (speedRatio > 0.82f && DiscTurn < 0.0f)
        {
            float turnPotential = Mathf.Clamp((speedRatio - 0.82f) / 0.40f, 0.0f, 1.5f);
            turnWeight = Mathf.Clamp(turnPotential * turnPotential * (Mathf.Abs(DiscTurn) / 5.0f) * 0.45f * turnMultiplier, 0.0f, 0.55f);
        }

        // Fade weight
        float fadeMultiplier = isForehand ? ForehandFadeBite : 1.0f;
        float fadePotential = Mathf.Clamp((0.80f - speedRatio) / 0.50f, 0.0f, 1.0f);
        fadeWeight = Mathf.Clamp(((DiscFade / 5.0f) * 0.35f + fadePotential * fadePotential * 0.35f) * fadeMultiplier, 0.10f, 0.68f);

        // Glide weight
        float glideBonus = !isForehand ? BackhandGlideBonus : 1.0f;
        glideWeight = Mathf.Clamp((DiscGlide / 7.0f) * 0.45f * glideBonus, 0.15f, 0.52f);

        float totalWeight = launchWeight + turnWeight + glideWeight + fadeWeight;
        if (totalWeight > 0.001f)
        {
            launchWeight /= totalWeight;
            turnWeight /= totalWeight;
            glideWeight /= totalWeight;
            fadeWeight /= totalWeight;
        }

        float estimatedAirTime = Mathf.Lerp(1.8f, 5.2f, parameters.Power * (DiscGlide / 6.0f) * (isForehand ? 0.95f : 1.05f));
        float estimatedTurnAngle = parameters.ReleaseAngle + (turnWeight > 0.05f ? spinSign * (-DiscTurn * 12.0f * speedRatio * turnMultiplier) : 0.0f);
        float estimatedFinishAngle = estimatedTurnAngle - (spinSign * DiscFade * 8.5f * fadeMultiplier);

        return new FlightTendency(
            launchWeight,
            turnWeight,
            glideWeight,
            fadeWeight,
            estimatedAirTime,
            estimatedTurnAngle,
            estimatedFinishAngle
        );
    }

    private void ApplyAirborneAerodynamics(float dt)
    {
        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        float currentSpeed = horizVel.Length();
        float totalSpeed = LinearVelocity.Length();

        if (currentSpeed < 0.2f)
        {
            return;
        }

        Vector3 forward = horizVel.Normalized();
        Vector3 right = forward.Cross(Vector3.Up).Normalized();

        float cruiseSpeed = GetRequiredCruiseSpeed();
        float speedRatio = totalSpeed / Mathf.Max(cruiseSpeed, 1.0f);
        float spinSign = GetSpinSign(CurrentTechnique);
        bool isForehand = IsForehandTechnique(CurrentTechnique);

        // 1. Angle of Attack (AoA) & Dynamic Pitch Calculation
        float velocityPitch = Mathf.Atan2(LinearVelocity.Y, Mathf.Max(0.01f, currentSpeed));
        // Disc pitch tracks initial release nose angle blended smoothly with flight path
        float targetPitch = Mathf.Lerp(velocityPitch, _initialPitch, NoseAnglePitchInfluence);
        CurrentPitchAngle = Mathf.MoveToward(CurrentPitchAngle, targetPitch, 0.75f * dt);

        float aoa = CurrentPitchAngle - velocityPitch; // Angle of attack
        float aoaLiftDelta = Mathf.Sin(aoa) * totalSpeed * AoALiftFactor * 0.45f;
        float aoaInducedDrag = Mathf.Sin(aoa) * Mathf.Sin(aoa) * AoAInducedDragFactor;

        // 2. High-Speed Turn & Low-Speed Fade Aerodynamics
        float turnFactor = 0.0f;
        float turnRate = 0.0f;
        float turnTorque = isForehand ? ForehandTurnTorque : 1.0f;
        if (speedRatio > 0.82f && DiscTurn < 0.0f)
        {
            float rawTurn = Mathf.Clamp((speedRatio - 0.82f) / 0.40f, 0.0f, 1.5f);
            turnFactor = rawTurn * rawTurn;
            turnRate = (-DiscTurn) * TurnMultiplier * turnFactor * turnTorque;
        }

        float fadeFactor = 0.0f;
        float fadeRate = 0.0f;
        float fadeBite = isForehand ? ForehandFadeBite : 1.0f;
        if (speedRatio < 0.78f && DiscFade > 0.0f)
        {
            float rawFade = Mathf.Clamp((0.78f - speedRatio) / 0.50f, 0.0f, 1.0f);
            fadeFactor = rawFade * rawFade;
            fadeRate = DiscFade * FadeMultiplier * fadeFactor * fadeBite;
        }

        // Glide line-holding stability
        float glideWeight = Mathf.Clamp(1.0f - (turnFactor * 1.5f + fadeFactor * 1.5f), 0.0f, 1.0f);
        if (glideWeight > 0.05f)
        {
            CurrentBankAngle = Mathf.MoveToward(CurrentBankAngle, 0.0f, 2.0f * glideWeight * dt);
        }

        // Net aerodynamic roll angle
        CurrentBankAngle += spinSign * (turnRate - fadeRate) * dt;
        CurrentBankAngle = Mathf.Clamp(CurrentBankAngle, -MaxBankAngle, MaxBankAngle);

        // 3. Flight Phase Transitions
        if (_flightTime < 0.20f)
        {
            SetFlightPhase(FlightPhase.Launch);
        }
        else if (turnRate > 1.5f && turnFactor > 0.10f)
        {
            SetFlightPhase(FlightPhase.Turn);
        }
        else if (fadeRate > 2.5f && fadeFactor > 0.12f)
        {
            SetFlightPhase(FlightPhase.Fade);
        }
        else
        {
            SetFlightPhase(FlightPhase.Glide);
        }

        // 4. Wing Normal Lift Vector & Fairway Air Cushion (Ground Effect)
        float bankRad = Mathf.DegToRad(CurrentBankAngle);
        float glideBonus = !isForehand ? BackhandGlideBonus : 1.0f;
        float baseLift = totalSpeed * (DiscGlide * 0.12f * GlideMultiplier * glideBonus) * 0.05f;

        // Add Angle of Attack Lift
        float netLiftMagnitude = Mathf.Max(0.0f, baseLift + aoaLiftDelta);

        // Stall check: If airspeed drops below stall ratio, lift drops and sink accelerates
        if (speedRatio < StallSpeedRatio)
        {
            float stallSeverity = (StallSpeedRatio - speedRatio) / StallSpeedRatio;
            netLiftMagnitude *= (1.0f - stallSeverity * 0.65f);
            fadeFactor = Mathf.Max(fadeFactor, stallSeverity * StallFadeSinkBoost);
        }

        // Fairway Air Cushion (Ground Effect): Near ground altitude increases lift & reduces drag
        float groundEffectCushion = 0.0f;
        if (EnableGroundEffect)
        {
            float groundY = GetGroundHeightAt(GlobalPosition);
            float altitude = GlobalPosition.Y - (groundY + DiscThickness * 0.5f);
            if (altitude < GroundEffectMaxAltitude && altitude > 0.05f)
            {
                float groundFactor = 1.0f - Mathf.Clamp(altitude / GroundEffectMaxAltitude, 0.0f, 1.0f);
                groundEffectCushion = groundFactor * groundFactor;
                netLiftMagnitude *= (1.0f + GroundEffectLiftBoost * groundEffectCushion);
            }
        }

        // Lift vector tilts with the disc's wing normal:
        // Vertical lift = netLift * cos(bank)
        // Lateral carve force = netLift * sin(bank) * LateralForceMultiplier
        float verticalLift = netLiftMagnitude * Mathf.Cos(bankRad);
        Vector3 lateralAcc = right * Mathf.Sin(bankRad) * (netLiftMagnitude * LateralForceMultiplier + totalSpeed * 0.85f);

        LinearVelocity += (Vector3.Up * verticalLift + lateralAcc) * dt;

        if (fadeFactor > 0.02f)
        {
            LinearVelocity += Vector3.Down * (DiscFade * 0.90f * fadeFactor * fadeBite) * dt;
        }

        // 5. Total Drag (Base Drag + Induced Bank Drag + AoA Drag - Ground Effect Reduction)
        float bankInducedDrag = Mathf.Abs(Mathf.Sin(bankRad)) * 0.55f;
        float effectiveDrag = (BaseAirDrag + bankInducedDrag + aoaInducedDrag) * (1.0f - GroundEffectDragReduction * groundEffectCushion);
        float dragFactor = Mathf.Max(0.0f, 1.0f - effectiveDrag * dt);

        LinearVelocity = new Vector3(
            LinearVelocity.X * dragFactor,
            LinearVelocity.Y,
            LinearVelocity.Z * dragFactor
        );

        // Spin Decay
        _spinRate = Mathf.MoveToward(_spinRate, 10.0f, 2.5f * dt);
        _spinAngle += _spinRate * dt;

        // Flight progress
        if (_initialSpeed > 0.0f)
        {
            FlightProgress = 1.0f - Mathf.Clamp(totalSpeed / _initialSpeed, 0.0f, 1.0f);
        }
    }

    private void SetFlightPhase(FlightPhase newPhase)
    {
        if (CurrentFlightPhase != newPhase)
        {
            CurrentFlightPhase = newPhase;
            FlightPhaseChanged?.Invoke(newPhase);
        }
    }

    private void UpdateOrientation(Vector3 forward, float bankAngle, float pitchAngle, float dt, Vector3 surfaceNormal)
    {
        if (DiscVisual == null && DiscCollisionShape == null)
        {
            return;
        }

        if (forward.LengthSquared() < 0.001f)
        {
            forward = _lastForward;
        }
        else
        {
            _lastForward = forward.Normalized();
        }

        Vector3 normalUp = surfaceNormal.Normalized();
        Vector3 right = _lastForward.Cross(normalUp).Normalized();
        if (right.LengthSquared() < 0.001f)
        {
            right = Vector3.Right;
        }
        Vector3 up = right.Cross(_lastForward).Normalized();

        // 1. Base alignment with travel direction
        Basis baseBasis = new(right, up, -_lastForward);

        // 2. Bank tilt (roll around forward travel vector)
        Basis tiltedBasis = baseBasis.Rotated(_lastForward, Mathf.DegToRad(bankAngle));

        // 3. Pitch along flight angle / nose angle
        if (Mathf.Abs(pitchAngle) > 0.001f)
        {
            tiltedBasis = tiltedBasis.Rotated(right, pitchAngle);
        }

        // Only update physical collision shape if bank angle changed noticeably (prevents micro-jitter pushes against floor)
        if (DiscCollisionShape != null)
        {
            if (Mathf.Abs(bankAngle - _lastAppliedBankAngle) > 0.4f || !_hasTouchedGround)
            {
                DiscCollisionShape.GlobalBasis = tiltedBasis;
                _lastAppliedBankAngle = bankAngle;
            }
        }

        // Apply tilted basis + spin to DiscVisual
        if (DiscVisual != null)
        {
            float spinSign = GetSpinSign(CurrentTechnique);
            Basis visualBasis = tiltedBasis.Rotated(tiltedBasis.Y, spinSign * _spinAngle);
            DiscVisual.GlobalBasis = visualBasis;
        }
    }

    private void EndFlight()
    {
        if (_isEndingFlight)
        {
            return;
        }

        _isEndingFlight = true;
        SetFlightPhase(FlightPhase.Settled);

        FreezeMode = FreezeModeEnum.Kinematic;
        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GravityScale = 0.0f;

        Vector3 restingPos = GlobalPosition;
        Vector3 hoverPosition = restingPos + Vector3.Up * HoverHeight;
        Basis targetBasis = new(_startRotation);

        CurrentBankAngle = 0.0f;
        CurrentPitchAngle = 0.0f;
        UpdateOrientation(_lastForward, 0.0f, 0.0f, 0.016f, _lastContactNormal);

        _hoverTween?.Kill();
        _hoverTween = CreateTween();
        _hoverTween.SetProcessMode(Tween.TweenProcessMode.Physics);

        // 1. Rest naturally on the surface where it stopped for SettleDelay before animating to hover
        _hoverTween.TweenInterval((double)SettleDelay);

        _hoverTween.TweenCallback(Callable.From(() =>
        {
            HoverAnimationStarted?.Invoke(HoverAnimationDuration);
        }));

        // 2. Tween smoothly straight up to hover height
        _hoverTween.TweenProperty(this, "global_position", hoverPosition, (double)HoverAnimationDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);

        if (DiscVisual != null)
        {
            _hoverTween.Parallel().TweenProperty(DiscVisual, "position", Vector3.Zero, (double)HoverAnimationDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);

            _hoverTween.Parallel().TweenProperty(DiscVisual, "quaternion", Quaternion.Identity, (double)HoverAnimationDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
        }

        if (DiscCollisionShape != null)
        {
            _hoverTween.Parallel().TweenProperty(DiscCollisionShape, "position", Vector3.Zero, (double)HoverAnimationDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);

            _hoverTween.Parallel().TweenProperty(DiscCollisionShape, "quaternion", Quaternion.Identity, (double)HoverAnimationDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
        }

        _hoverTween.TweenCallback(Callable.From(() =>
        {
            FinishEndFlight(hoverPosition, targetBasis);
        }));
    }

    private void FinishEndFlight(Vector3 hoverPosition, Basis targetBasis)
    {
        GlobalPosition = hoverPosition;
        GlobalTransform = new Transform3D(targetBasis, hoverPosition);
        CurrentBankAngle = 0.0f;
        CurrentPitchAngle = 0.0f;
        _spinAngle = 0.0f;
        _spinRate = 0.0f;
        _skipCount = 0;
        _isRolling = false;
        _hasTouchedGround = false;
        _hasFloorContact = false;
        _hasWallContact = false;

        if (DiscCollisionShape != null)
        {
            DiscCollisionShape.Position = Vector3.Zero;
            DiscCollisionShape.Quaternion = Quaternion.Identity;
            DiscCollisionShape.Transform = Transform3D.Identity;
        }

        if (DiscVisual != null)
        {
            DiscVisual.Position = Vector3.Zero;
            DiscVisual.Quaternion = Quaternion.Identity;
            DiscVisual.Transform = Transform3D.Identity;
        }

        _isEndingFlight = false;
        IsFlying = false;
        SetFlightPhase(FlightPhase.Ready);
        NewTurnStarted?.Invoke();
    }

    public float GetGroundHeightAt(Vector3 position)
    {
        var directSpaceState = GetWorld3D()?.DirectSpaceState;
        if (directSpaceState == null)
        {
            return position.Y;
        }

        var query = PhysicsRayQueryParameters3D.Create(
            position + Vector3.Up * 1.0f,
            position + Vector3.Down * 20.0f
        );
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = directSpaceState.IntersectRay(query);
        if (result.Count > 0 && result.TryGetValue("position", out var hitPosVariant))
        {
            return hitPosVariant.AsVector3().Y;
        }

        return position.Y;
    }

    private void BuildGroundIndicator()
    {
        _groundIndicatorRoot = new Node3D
        {
            Name = "GroundIndicatorRoot",
            TopLevel = true
        };
        AddChild(_groundIndicatorRoot);

        // 1. Vertical Drop Line
        _groundLineMesh = new CylinderMesh
        {
            TopRadius = GroundLineRadius,
            BottomRadius = GroundLineRadius,
            Height = 1.0f,
            RadialSegments = 10
        };

        var lineMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = GroundIndicatorColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _groundLineInstance = new MeshInstance3D
        {
            Name = "DropLine",
            Mesh = _groundLineMesh,
            MaterialOverride = lineMat
        };
        _groundIndicatorRoot.AddChild(_groundLineInstance);

        // 2. Ground Ring Projection
        var ringMesh = new TorusMesh
        {
            InnerRadius = DiscRadius * 0.85f,
            OuterRadius = DiscRadius,
            Rings = 24,
            RingSegments = 12
        };

        var ringMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(GroundIndicatorColor.R, GroundIndicatorColor.G, GroundIndicatorColor.B, 0.85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        _groundRingInstance = new MeshInstance3D
        {
            Name = "GroundRing",
            Mesh = ringMesh,
            MaterialOverride = ringMat
        };
        _groundIndicatorRoot.AddChild(_groundRingInstance);

        // 3. Ground Soft Shadow Disc
        var shadowMesh = new CylinderMesh
        {
            TopRadius = DiscRadius * 0.85f,
            BottomRadius = DiscRadius * 0.85f,
            Height = 0.002f,
            RadialSegments = 24
        };

        var shadowMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = GroundShadowColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        _groundShadowInstance = new MeshInstance3D
        {
            Name = "GroundShadow",
            Mesh = shadowMesh,
            MaterialOverride = shadowMat
        };
        _groundIndicatorRoot.AddChild(_groundShadowInstance);
    }

    private void UpdateGroundIndicator(float dt)
    {
        if (_groundIndicatorRoot == null || _groundLineMesh == null || _groundLineInstance == null || _groundRingInstance == null || _groundShadowInstance == null)
        {
            return;
        }

        Vector3 discPos = GlobalPosition;
        float groundY = GetGroundHeightAt(discPos);
        float altitude = discPos.Y - (groundY + DiscThickness * 0.5f);

        // Hide when settled flat on ground
        if (altitude < 0.05f || (!IsFlying && CurrentFlightPhase != FlightPhase.Ready))
        {
            _groundIndicatorRoot.Visible = false;
            return;
        }

        _groundIndicatorRoot.Visible = true;

        Vector3 groundCenter = new(discPos.X, groundY + 0.012f, discPos.Z);
        _groundRingInstance.GlobalPosition = groundCenter;
        _groundShadowInstance.GlobalPosition = groundCenter;

        float lineHeight = Mathf.Max(0.01f, discPos.Y - groundY);
        _groundLineMesh.Height = lineHeight;
        _groundLineInstance.GlobalPosition = new Vector3(discPos.X, groundY + lineHeight * 0.5f, discPos.Z);
    }
}

public readonly record struct FlightTendency(
    float LaunchWeight,
    float TurnWeight,
    float GlideWeight,
    float FadeWeight,
    float EstimatedAirTime,
    float PeakTurnAngle,
    float ExpectedFinishAngle
);
