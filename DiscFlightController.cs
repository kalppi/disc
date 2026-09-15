using Godot;

public partial class DiscFlightController : RigidBody3D
{
    public enum FlightPhase
    {
        Ready,      // Hovering at stance, ready to throw
        Launch,     // Initial release burst
        Turn,       // High-speed turn (understable roll right)
        Glide,      // Cruise speed, maximum lift & line-holding
        Fade,       // Low-speed fade (overstable hook left & drop)
        Ground,     // Skipping, rolling, or sliding on surface
        Settled     // Rested on ground, awaiting hover lift
    }

    public enum GroundState
    {
        Airborne,
        Skipping,
        Rolling,
        Sliding,
        Settled
    }

    [ExportGroup("Disc Ratings (Flight Numbers)")]
    [Export(PropertyHint.Range, "1.0, 14.0, 0.5")] public float DiscSpeed { get; set; } = 9.0f;   // Speed requirement (1 = Putter, 7 = Fairway, 13 = Distance Driver)
    [Export(PropertyHint.Range, "1.0, 7.0, 0.5")]  public float DiscGlide { get; set; } = 5.0f;   // Lift retention & loft (1 = brick, 7 = ultra-glide)
    [Export(PropertyHint.Range, "-5.0, 1.0, 0.5")] public float DiscTurn { get; set; } = -1.5f;  // High-speed turn (-5 = very understable, +1 = very overstable)
    [Export(PropertyHint.Range, "0.0, 5.0, 0.5")]  public float DiscFade { get; set; } = 2.5f;   // Low-speed fade (0 = finishes straight, 5 = massive left hook)

    [ExportGroup("Throw Setup")]
    [Export] public float HoverHeight { get; set; } = 1.5f;
    [Export] public float SettleDelay { get; set; } = 1.0f;
    [Export] public float HoverAnimationDuration { get; set; } = 0.8f;
    [Export] public float MinThrowSpeed { get; set; } = 12.0f;
    [Export] public float MaxThrowSpeed { get; set; } = 34.0f;

    [ExportGroup("Arcade Aerodynamics Tuning")]
    [Export] public float TurnMultiplier { get; set; } = 18.0f;        // Base roll deg/s per Turn rating unit
    [Export] public float FadeMultiplier { get; set; } = 20.0f;        // Base hook deg/s per Fade rating unit
    [Export] public float GlideMultiplier { get; set; } = 1.35f;       // Lift force scaling per Glide unit
    [Export] public float BaseFlightGravityScale { get; set; } = 0.30f; // Low gravity in air for floaty arcade feel
    [Export] public float BaseAirDrag { get; set; } = 0.075f;
    [Export] public float LateralForceMultiplier { get; set; } = 1.45f;
    [Export] public float MaxBankAngle { get; set; } = 60.0f;

    [ExportGroup("Ground Physics & Hardness")]
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")] public float SurfaceHardness { get; set; } = 0.5f; // 0.0 = mud, 0.5 = turf/grass, 1.0 = asphalt
    [Export] public float GroundBounciness { get; set; } = 0.50f;
    [Export] public float GroundFriction { get; set; } = 0.38f;
    [Export] public float RollThresholdAngle { get; set; } = 15.0f;  // Minimum bank angle to start rolling on rim
    [Export] public float DiscRadius { get; set; } = 0.35f;
    [Export] public float DiscThickness { get; set; } = 0.04f;

    [ExportGroup("Wall & Obstacle Physics")]
    [Export] public float WallBounciness { get; set; } = 0.68f;
    [Export] public float WallFriction { get; set; } = 0.15f;

    [ExportGroup("Ground Indicator & Drop Line")]
    [Export] public bool ShowGroundIndicator { get; set; } = true;
    [Export] public Color GroundIndicatorColor { get; set; } = new(0.25f, 0.85f, 1.0f, 0.65f);
    [Export] public Color GroundShadowColor { get; set; } = new(0.0f, 0.0f, 0.0f, 0.35f);
    [Export] public float GroundLineRadius { get; set; } = 0.016f;

    [ExportGroup("Visual")]
    [Export] public Node3D? DiscVisual { get; set; }

    public bool IsFlying { get; private set; }
    public float FlightProgress { get; private set; }
    public float CurrentBankAngle { get; private set; }
    public FlightPhase CurrentFlightPhase { get; private set; } = FlightPhase.Ready;
    public GroundState CurrentGroundState => _groundState;

    public event System.Action<FlightPhase>? FlightPhaseChanged;
    public event System.Action<float>? HoverAnimationStarted;
    public event System.Action? NewTurnStarted;

    private float _initialSpeed;
    private float _flightTime;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private bool _isEndingFlight;
    private Tween? _hoverTween;
    private GroundState _groundState = GroundState.Settled;
    private float _groundTimer;
    private float _spinAngle;
    private float _spinRate;
    private Vector3 _lastForward = Vector3.Forward;

    // Ground Drop-Line & Indicator Nodes
    private Node3D? _groundIndicatorRoot;
    private MeshInstance3D? _groundLineInstance;
    private MeshInstance3D? _groundRingInstance;
    private MeshInstance3D? _groundShadowInstance;
    private CylinderMesh? _groundLineMesh;

    public override void _Ready()
    {
        _startPosition = GlobalPosition;
        _startRotation = GlobalTransform.Basis.GetRotationQuaternion();

        // Lock rigid body physics rotation to prevent physics engine tumbling/jitter
        AxisLockAngularX = true;
        AxisLockAngularY = true;
        AxisLockAngularZ = true;

        ContactMonitor = true;
        MaxContactsReported = 4;
        BodyEntered += OnBodyEntered;

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

    public override void _PhysicsProcess(double delta)
    {
        if (!IsFlying || _isEndingFlight)
        {
            return;
        }

        float dt = (float)delta;
        _flightTime += dt;

        float groundY = GetGroundHeightAt(GlobalPosition);
        float minCenterHeight = GetMinCenterHeight(CurrentBankAngle);
        float groundSurfaceY = groundY + minCenterHeight;

        switch (_groundState)
        {
            case GroundState.Airborne:
                ProcessAirborne(dt, groundSurfaceY);
                break;

            case GroundState.Skipping:
                ProcessSkipping(dt, groundSurfaceY);
                break;

            case GroundState.Rolling:
                ProcessRolling(dt, groundSurfaceY);
                break;

            case GroundState.Sliding:
                ProcessSliding(dt, groundSurfaceY);
                break;

            case GroundState.Settled:
                break;
        }
    }

    public void Throw(ThrowParameters parameters)
    {
        _hoverTween?.Kill();
        _hoverTween = null;
        _isEndingFlight = false;
        _groundState = GroundState.Airborne;
        _groundTimer = 0.0f;
        _flightTime = 0.0f;

        Freeze = false;
        Sleeping = false;

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

        _spinRate = 45.0f * (speed / MinThrowSpeed);
        _spinAngle = 0.0f;

        FlightProgress = 0.0f;
        GravityScale = BaseFlightGravityScale;
        IsFlying = true;

        SetFlightPhase(FlightPhase.Launch);
        UpdateVisualOrientation(_lastForward, CurrentBankAngle, 0.0f, 0.016f);
    }

    public void ResetPosition()
    {
        _hoverTween?.Kill();
        _hoverTween = null;
        _isEndingFlight = false;
        _groundState = GroundState.Settled;
        SetupNewTurnHover(_startPosition);
    }

    public void SetupNewTurnHover(Vector3 basePosition)
    {
        _hoverTween?.Kill();
        _hoverTween = null;
        _isEndingFlight = false;
        _groundState = GroundState.Settled;
        _groundTimer = 0.0f;
        _spinAngle = 0.0f;
        _spinRate = 0.0f;
        _flightTime = 0.0f;
        IsFlying = false;
        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GravityScale = 0.0f;

        float groundY = GetGroundHeightAt(basePosition);
        Vector3 hoverPosition = new(basePosition.X, groundY + HoverHeight, basePosition.Z);

        GlobalPosition = hoverPosition;
        GlobalTransform = new Transform3D(new Basis(_startRotation), hoverPosition);
        CurrentBankAngle = 0.0f;

        if (DiscVisual != null)
        {
            DiscVisual.Transform = Transform3D.Identity;
        }

        SetFlightPhase(FlightPhase.Ready);
        NewTurnStarted?.Invoke();
    }

    public Vector3 GetHoverPositionFor(Vector3 position)
    {
        float groundY = GetGroundHeightAt(position);
        return new Vector3(position.X, groundY + HoverHeight, position.Z);
    }

    public float GetMinCenterHeight(float bankAngleDeg)
    {
        float bankRad = Mathf.DegToRad(Mathf.Abs(bankAngleDeg));
        return DiscRadius * Mathf.Sin(bankRad) + (DiscThickness * 0.5f) * Mathf.Cos(bankRad) + 0.003f;
    }

    public float GetRequiredCruiseSpeed()
    {
        // Maps DiscSpeed 1..14 to ~13.5 m/s .. ~31.0 m/s
        return Mathf.Lerp(13.5f, 31.0f, (DiscSpeed - 1.0f) / 13.0f);
    }

    public FlightTendency EstimateFlightTendency(ThrowParameters parameters)
    {
        float speed = Mathf.Lerp(MinThrowSpeed, MaxThrowSpeed, parameters.Power);
        float cruiseSpeed = GetRequiredCruiseSpeed();
        float speedRatio = speed / Mathf.Max(cruiseSpeed, 1.0f);

        float turnSpeedThreshold = cruiseSpeed * 0.85f;
        float fadeSpeedThreshold = cruiseSpeed * 0.60f;

        float launchWeight = 0.12f;
        float turnWeight = 0.0f;
        float glideWeight = 0.0f;
        float fadeWeight = 0.0f;

        if (speed > turnSpeedThreshold && DiscTurn < 0.0f)
        {
            float turnPotential = Mathf.Clamp((speed - turnSpeedThreshold) / (cruiseSpeed * 0.4f), 0.0f, 1.5f);
            turnWeight = Mathf.Clamp(turnPotential * (Mathf.Abs(DiscTurn) / 5.0f) * 0.40f, 0.0f, 0.45f);
        }

        if (speed > fadeSpeedThreshold)
        {
            glideWeight = Mathf.Clamp((DiscGlide / 7.0f) * 0.45f, 0.15f, 0.50f);
        }

        fadeWeight = Mathf.Clamp((DiscFade / 5.0f) * 0.35f + Mathf.Max(0.0f, 1.0f - speedRatio) * 0.30f, 0.10f, 0.60f);

        float totalWeight = launchWeight + turnWeight + glideWeight + fadeWeight;
        if (totalWeight > 0.001f)
        {
            launchWeight /= totalWeight;
            turnWeight /= totalWeight;
            glideWeight /= totalWeight;
            fadeWeight /= totalWeight;
        }

        float estimatedAirTime = Mathf.Lerp(1.8f, 5.2f, parameters.Power * (DiscGlide / 6.0f));
        float estimatedTurnAngle = parameters.ReleaseAngle + (turnWeight > 0.05f ? -DiscTurn * 12.0f * speedRatio : 0.0f);
        float estimatedFinishAngle = estimatedTurnAngle - (DiscFade * 8.5f);

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

    private void ProcessAirborne(float dt, float groundSurfaceY)
    {
        // Check for wall or obstacle impacts along trajectory
        if (CheckObstacleCollision(dt))
        {
            return;
        }

        if (GlobalPosition.Y <= groundSurfaceY)
        {
            HandleGroundImpact();
            return;
        }

        ApplyArcadeFlight(dt);
        UpdateFlightProgress();

        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        Vector3 forward = horizVel.LengthSquared() > 0.01f ? horizVel.Normalized() : _lastForward;
        float verticalPitch = Mathf.Clamp(LinearVelocity.Y / (horizVel.Length() + 0.1f), -0.5f, 0.5f) * 0.35f;

        _spinRate = Mathf.MoveToward(_spinRate, 10.0f, 2.5f * dt);
        _spinAngle += _spinRate * dt;

        UpdateVisualOrientation(forward, CurrentBankAngle, verticalPitch, dt);
    }

    private void ProcessSkipping(float dt, float groundSurfaceY)
    {
        SetFlightPhase(FlightPhase.Ground);

        // Check for wall / obstacle collisions while skipping
        if (CheckObstacleCollision(dt))
        {
            return;
        }

        LinearVelocity += Vector3.Down * 9.8f * BaseFlightGravityScale * 2.5f * dt;
        LinearVelocity *= Mathf.Max(0.0f, 1.0f - BaseAirDrag * dt);

        if (GlobalPosition.Y <= groundSurfaceY && LinearVelocity.Y <= 0.0f)
        {
            HandleGroundImpact();
            return;
        }

        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        Vector3 forward = horizVel.LengthSquared() > 0.01f ? horizVel.Normalized() : _lastForward;
        float verticalPitch = Mathf.Clamp(LinearVelocity.Y / (horizVel.Length() + 0.1f), -0.5f, 0.5f) * 0.35f;

        _spinRate = Mathf.MoveToward(_spinRate, 10.0f, 3.0f * dt);
        _spinAngle += _spinRate * dt;

        UpdateVisualOrientation(forward, CurrentBankAngle, verticalPitch, dt);
    }

    private void ProcessRolling(float dt, float groundSurfaceY)
    {
        SetFlightPhase(FlightPhase.Ground);
        _groundTimer += dt;

        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        float currentSpeed = horizVel.Length();

        if (currentSpeed < 0.15f)
        {
            _groundState = GroundState.Sliding;
            return;
        }

        Vector3 forward = horizVel.Normalized();
        Vector3 right = forward.Cross(Vector3.Up).Normalized();

        // Check for obstacle collision along rolling path
        if (CheckObstacleCollision(dt))
        {
            return;
        }

        // Curvature turn: disc curves naturally towards the side it leans on
        float leanFactor = Mathf.Sin(Mathf.DegToRad(CurrentBankAngle));
        Vector3 curveAcc = right * leanFactor * (4.0f + currentSpeed * 0.20f);
        LinearVelocity += curveAcc * dt;

        // Rolling resistance
        float rollFriction = (0.22f + 0.32f * (1.0f - SurfaceHardness)) * 9.8f;
        horizVel = new Vector3(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        currentSpeed = Mathf.MoveToward(horizVel.Length(), 0.0f, rollFriction * dt);
        LinearVelocity = horizVel.Normalized() * currentSpeed;

        // Step position manually while frozen on ground
        GlobalPosition += LinearVelocity * dt;
        GlobalPosition = new Vector3(GlobalPosition.X, groundSurfaceY, GlobalPosition.Z);

        // Disc rolls along rim by rotating around its face normal axis (local Y)
        _spinRate = Mathf.MoveToward(_spinRate, (currentSpeed / DiscRadius), 25.0f * dt);
        _spinAngle += _spinRate * dt;

        // Bank angle wobble and decay towards flat as speed drops
        float decayRate = 14.0f + Mathf.Max(0.0f, 3.0f - currentSpeed) * 28.0f;
        CurrentBankAngle = Mathf.MoveToward(CurrentBankAngle, 0.0f, decayRate * dt);

        UpdateVisualOrientation(forward, CurrentBankAngle, 0.0f, dt);

        if (Mathf.Abs(CurrentBankAngle) < 2.0f || currentSpeed < 0.35f || _groundTimer > 4.0f)
        {
            _groundState = GroundState.Sliding;
        }
    }

    private void ProcessSliding(float dt, float groundSurfaceY)
    {
        SetFlightPhase(FlightPhase.Ground);
        _groundTimer += dt;

        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        float currentSpeed = horizVel.Length();

        // Level out remaining bank angle
        CurrentBankAngle = Mathf.MoveToward(CurrentBankAngle, 0.0f, 70.0f * dt);

        // Sliding friction
        float slideFriction = (GroundFriction * (0.6f + 0.8f * (1.0f - SurfaceHardness))) * 9.8f;
        currentSpeed = Mathf.MoveToward(currentSpeed, 0.0f, slideFriction * dt);

        Vector3 forward = horizVel.LengthSquared() > 0.001f ? horizVel.Normalized() : _lastForward;
        LinearVelocity = forward * currentSpeed;

        // Check for obstacle collision along slide path
        if (CheckObstacleCollision(dt))
        {
            return;
        }

        // Step position manually while frozen on ground
        GlobalPosition += LinearVelocity * dt;
        GlobalPosition = new Vector3(GlobalPosition.X, groundSurfaceY, GlobalPosition.Z);

        // Spin slows down on grass
        _spinRate = Mathf.MoveToward(_spinRate, 0.0f, 20.0f * dt);
        _spinAngle += _spinRate * dt;

        UpdateVisualOrientation(forward, CurrentBankAngle, 0.0f, dt);

        if (currentSpeed < 0.04f && Mathf.Abs(CurrentBankAngle) < 1.0f)
        {
            EndFlight();
        }
        else if (_groundTimer > 3.5f)
        {
            EndFlight();
        }
    }

    private bool CheckObstacleCollision(float dt)
    {
        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null)
        {
            return false;
        }

        Vector3 moveStep = LinearVelocity * dt;
        float stepLen = moveStep.Length();
        if (stepLen < 0.0001f)
        {
            return false;
        }

        // Cast forward to detect walls / trees / obstacles before penetration
        Vector3 from = GlobalPosition;
        Vector3 to = from + moveStep + moveStep.Normalized() * (DiscRadius + 0.05f);

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0 && result.TryGetValue("normal", out var normalVar))
        {
            Vector3 normal = normalVar.AsVector3();
            // If surface is mostly vertical (wall/obstacle)
            if (normal.Y < 0.65f)
            {
                if (result.TryGetValue("position", out var hitPosVar))
                {
                    Vector3 hitPos = hitPosVar.AsVector3();
                    GlobalPosition = hitPos + normal * (DiscRadius + 0.03f);
                }
                HandleWallImpact(normal);
                return true;
            }
        }

        return false;
    }

    private void HandleWallImpact(Vector3 wallNormal)
    {
        Vector3 incomingVel = LinearVelocity;
        float incomingSpeed = incomingVel.Length();

        if (incomingSpeed < 0.1f)
        {
            return;
        }

        wallNormal = wallNormal.Normalized();

        // Standard reflection vector
        Vector3 reflectedDir = incomingVel.Bounce(wallNormal).Normalized();

        // Arcade bounce restitution & wall drag
        float restitution = WallBounciness * (0.80f + 0.20f * SurfaceHardness);
        float reboundSpeed = incomingSpeed * restitution;

        // Apply bounced velocity with slight gravity kick
        LinearVelocity = reflectedDir * reboundSpeed + Vector3.Down * 0.5f;

        // Deflect bank angle on wall strike
        CurrentBankAngle = Mathf.Clamp(-CurrentBankAngle * 0.5f, -MaxBankAngle, MaxBankAngle);

        // Update forward direction for orientation
        Vector3 horizBounce = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        if (horizBounce.LengthSquared() > 0.001f)
        {
            _lastForward = horizBounce.Normalized();
        }

        // Return to airborne flight so the disc continues to fly and drop after bouncing
        _groundState = GroundState.Airborne;
        Freeze = false;
        Sleeping = false;
        GravityScale = BaseFlightGravityScale * 1.5f;

        UpdateVisualOrientation(_lastForward, CurrentBankAngle, 0.0f, 0.016f);
    }

    private void HandleGroundImpact()
    {
        float groundY = GetGroundHeightAt(GlobalPosition);
        float minCenterHeight = GetMinCenterHeight(CurrentBankAngle);
        float groundSurfaceY = groundY + minCenterHeight;
        GlobalPosition = new Vector3(GlobalPosition.X, groundSurfaceY, GlobalPosition.Z);

        float downwardSpeed = -LinearVelocity.Y;
        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        float horizSpeed = horizVel.Length();

        // 1. Roll on rim if banked
        if (Mathf.Abs(CurrentBankAngle) >= RollThresholdAngle && horizSpeed > 1.2f)
        {
            _groundState = GroundState.Rolling;
            Freeze = true;
            float rollPreservation = 0.78f + 0.18f * SurfaceHardness;
            LinearVelocity = horizVel * rollPreservation;
            return;
        }

        // 2. Skip / Bounce if fast & hard surface
        float restitution = GroundBounciness * (0.35f + 0.65f * SurfaceHardness);
        float reboundSpeedY = downwardSpeed * restitution;

        if (reboundSpeedY > 0.6f && horizSpeed > 1.8f)
        {
            _groundState = GroundState.Skipping;
            Freeze = false;
            float skipForwardRetention = 0.78f + 0.18f * SurfaceHardness;
            LinearVelocity = new Vector3(
                horizVel.X * skipForwardRetention,
                reboundSpeedY,
                horizVel.Z * skipForwardRetention
            );
            return;
        }

        // 3. Slide on grass
        _groundState = GroundState.Sliding;
        Freeze = true;
        float slideRetention = 0.58f + 0.32f * SurfaceHardness;
        LinearVelocity = horizVel * slideRetention;
    }

    private void ApplyArcadeFlight(float dt)
    {
        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        float currentSpeed = horizVel.Length();

        if (currentSpeed < 0.2f)
        {
            return;
        }

        Vector3 forward = horizVel.Normalized();
        Vector3 right = forward.Cross(Vector3.Up).Normalized();

        float cruiseSpeed = GetRequiredCruiseSpeed();
        float turnThreshold = cruiseSpeed * 0.85f;
        float fadeThreshold = cruiseSpeed * 0.60f;

        // 1. Flight Phase & Aerodynamic Turn/Fade Roll Adjustments
        if (_flightTime < 0.20f)
        {
            SetFlightPhase(FlightPhase.Launch);
        }
        else if (currentSpeed > turnThreshold)
        {
            SetFlightPhase(FlightPhase.Turn);

            // High-Speed Turn (RHBH: negative turn rating causes roll to the right)
            float speedFactor = (currentSpeed - turnThreshold) / Mathf.Max(MaxThrowSpeed - turnThreshold, 2.0f);
            float turnRate = (-DiscTurn) * TurnMultiplier * (0.4f + 0.6f * speedFactor);
            CurrentBankAngle += turnRate * dt;
        }
        else if (currentSpeed > fadeThreshold)
        {
            SetFlightPhase(FlightPhase.Glide);

            // Glide Phase: Holds line and gently stabilizes extreme tilt
            CurrentBankAngle = Mathf.MoveToward(CurrentBankAngle, 0.0f, 2.5f * dt);
        }
        else
        {
            SetFlightPhase(FlightPhase.Fade);

            // Low-Speed Fade (RHBH: fade rating causes roll to the left as speed decays)
            float fadeProgress = 1.0f - (currentSpeed / Mathf.Max(fadeThreshold, 1.0f));
            float fadeRate = DiscFade * FadeMultiplier * (0.35f + 0.65f * fadeProgress);
            CurrentBankAngle -= fadeRate * dt;
        }

        CurrentBankAngle = Mathf.Clamp(CurrentBankAngle, -MaxBankAngle, MaxBankAngle);

        // 2. Lateral Carving Force from Bank Angle
        float bankRad = Mathf.DegToRad(CurrentBankAngle);
        Vector3 lateralAcc = right * Mathf.Sin(bankRad) * currentSpeed * LateralForceMultiplier;
        LinearVelocity += lateralAcc * dt;

        // 3. Lift & Glide Force
        float baseLift = currentSpeed * (DiscGlide * 0.16f * GlideMultiplier) * 0.05f;
        float currentVerticalVel = LinearVelocity.Y;

        float bankLiftDamping = Mathf.Cos(bankRad);
        float effectiveLift = baseLift * Mathf.Max(0.25f, bankLiftDamping);

        if (currentVerticalVel < 1.5f)
        {
            float liftForce = Mathf.Min(effectiveLift, (1.5f - currentVerticalVel) * 3.0f);
            LinearVelocity += Vector3.Up * liftForce * dt;
        }

        // In fade phase, disc loses lift and fades downward
        if (CurrentFlightPhase == FlightPhase.Fade)
        {
            LinearVelocity += Vector3.Down * (DiscFade * 0.5f) * dt;
        }

        // 4. Air Drag
        float inducedDragFactor = 1.0f + Mathf.Abs(Mathf.Sin(bankRad)) * 0.5f;
        float effectiveDrag = BaseAirDrag * inducedDragFactor;
        float dragFactor = Mathf.Max(0.0f, 1.0f - effectiveDrag * dt);

        LinearVelocity = new Vector3(
            LinearVelocity.X * dragFactor,
            LinearVelocity.Y,
            LinearVelocity.Z * dragFactor
        );
    }

    private void UpdateFlightProgress()
    {
        float currentSpeed = LinearVelocity.Length();

        if (_initialSpeed <= 0.0f)
        {
            FlightProgress = 1.0f;
            return;
        }

        FlightProgress = 1.0f - Mathf.Clamp(currentSpeed / _initialSpeed, 0.0f, 1.0f);
    }

    private void SetFlightPhase(FlightPhase newPhase)
    {
        if (CurrentFlightPhase != newPhase)
        {
            CurrentFlightPhase = newPhase;
            FlightPhaseChanged?.Invoke(newPhase);
        }
    }

    private void UpdateVisualOrientation(Vector3 forward, float bankAngle, float pitchAngle, float dt)
    {
        if (DiscVisual == null)
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

        Vector3 right = _lastForward.Cross(Vector3.Up).Normalized();
        if (right.LengthSquared() < 0.001f)
        {
            right = Vector3.Right;
        }
        Vector3 up = right.Cross(_lastForward).Normalized();

        // 1. Base alignment with travel direction
        Basis basis = new(right, up, -_lastForward);

        // 2. Bank tilt (roll around travel vector -Z)
        basis = basis.Rotated(basis.Z, Mathf.DegToRad(bankAngle));

        // 3. Pitch along flight angle (pitch around local right X)
        if (Mathf.Abs(pitchAngle) > 0.001f)
        {
            basis = basis.Rotated(basis.X, pitchAngle);
        }

        // 4. Spin around disc face normal (local Y axis)
        basis = basis.Rotated(basis.Y, _spinAngle);

        DiscVisual.GlobalBasis = basis;
    }

    private void OnBodyEntered(Node body)
    {
        if (!IsFlying || _isEndingFlight)
        {
            return;
        }

        // Check if collision contact was with a wall/obstacle
        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState != null && LinearVelocity.LengthSquared() > 0.01f)
        {
            var query = PhysicsRayQueryParameters3D.Create(
                GlobalPosition - LinearVelocity.Normalized() * 0.2f,
                GlobalPosition + LinearVelocity.Normalized() * (DiscRadius + 0.2f)
            );
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            var result = spaceState.IntersectRay(query);
            if (result.Count > 0 && result.TryGetValue("normal", out var normalVar))
            {
                Vector3 normal = normalVar.AsVector3();
                if (normal.Y < 0.65f)
                {
                    HandleWallImpact(normal);
                    return;
                }
            }
        }

        if (_groundState == GroundState.Airborne || _groundState == GroundState.Skipping)
        {
            HandleGroundImpact();
        }
    }

    private void EndFlight()
    {
        if (_isEndingFlight)
        {
            return;
        }

        _isEndingFlight = true;
        _groundState = GroundState.Settled;
        SetFlightPhase(FlightPhase.Settled);

        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GravityScale = 0.0f;

        Vector3 groundPos = GlobalPosition;
        float groundY = GetGroundHeightAt(groundPos);
        GlobalPosition = new Vector3(groundPos.X, groundY + DiscThickness * 0.5f + 0.003f, groundPos.Z);

        Vector3 hoverPosition = new(groundPos.X, groundY + HoverHeight, groundPos.Z);
        Basis targetBasis = new(_startRotation);

        _hoverTween?.Kill();
        _hoverTween = CreateTween();
        _hoverTween.SetProcessMode(Tween.TweenProcessMode.Physics);

        // Settle on the ground for a second before animating to hover
        _hoverTween.TweenInterval(SettleDelay);

        // Notify camera and other listeners that hover animation is beginning
        _hoverTween.TweenCallback(Callable.From(() =>
        {
            HoverAnimationStarted?.Invoke(HoverAnimationDuration);
        }));

        // Smoothly lift disc to hover height
        _hoverTween.TweenProperty(this, "global_position", hoverPosition, HoverAnimationDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);

        // Smoothly level out visual rotation
        if (DiscVisual != null)
        {
            _hoverTween.Parallel()
                .TweenProperty(DiscVisual, "transform", Transform3D.Identity, HoverAnimationDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
        }

        _hoverTween.TweenCallback(Callable.From(() =>
        {
            GlobalTransform = new Transform3D(targetBasis, hoverPosition);
            CurrentBankAngle = 0.0f;
            _spinAngle = 0.0f;
            _spinRate = 0.0f;

            if (DiscVisual != null)
            {
                DiscVisual.Transform = Transform3D.Identity;
            }

            _isEndingFlight = false;
            IsFlying = false;
            SetFlightPhase(FlightPhase.Ready);
            NewTurnStarted?.Invoke();
        }));
    }

    public float GetGroundHeightAt(Vector3 position)
    {
        var directSpaceState = GetWorld3D()?.DirectSpaceState;
        if (directSpaceState == null)
        {
            return 0.0f;
        }

        var query = PhysicsRayQueryParameters3D.Create(
            position + Vector3.Up * 20.0f,
            position + Vector3.Down * 100.0f
        );
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = directSpaceState.IntersectRay(query);
        if (result.Count > 0 && result.TryGetValue("position", out var hitPosVariant))
        {
            return hitPosVariant.AsVector3().Y;
        }

        return 0.0f;
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
        if (altitude < 0.05f || (_groundState == GroundState.Settled && !_isEndingFlight && CurrentFlightPhase != FlightPhase.Ready))
        {
            _groundIndicatorRoot.Visible = false;
            return;
        }

        _groundIndicatorRoot.Visible = true;

        // Position ground target ring & shadow flat on terrain
        Vector3 groundCenter = new(discPos.X, groundY + 0.012f, discPos.Z);
        _groundRingInstance.GlobalPosition = groundCenter;
        _groundShadowInstance.GlobalPosition = groundCenter;

        // Update vertical drop line height and center
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
