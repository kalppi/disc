using Godot;

public partial class DiscFlightController : RigidBody3D
{
    public enum GroundState
    {
        Airborne,
        Skipping,
        Rolling,
        Sliding,
        Settled
    }

    [ExportGroup("Throw Setup")]
    [Export] public float HoverHeight { get; set; } = 1.5f;
    [Export] public float SettleDelay { get; set; } = 1.0f;
    [Export] public float HoverAnimationDuration { get; set; } = 0.8f;
    [Export] public float MinThrowSpeed { get; set; } = 12.0f;
    [Export] public float MaxThrowSpeed { get; set; } = 32.0f;

    [ExportGroup("Arcade Aerodynamics")]
    [Export] public float GlideStrength { get; set; } = 6.8f;
    [Export] public float FlightGravityScale { get; set; } = 0.28f;
    [Export] public float Drag { get; set; } = 0.085f;
    [Export] public float LateralForceMultiplier { get; set; } = 1.4f;

    [ExportGroup("Stability (Turn & Fade)")]
    [Export] public float TurnStrength { get; set; } = 18.0f;       // Degrees per second roll right at high speed
    [Export] public float FadeStrength { get; set; } = 28.0f;       // Degrees per second roll left at low speed
    [Export] public float TurnSpeedThreshold { get; set; } = 18.0f; // Speed above which high-speed turn occurs
    [Export] public float FadeSpeedThreshold { get; set; } = 16.0f; // Speed below which low-speed fade occurs
    [Export] public float MaxBankAngle { get; set; } = 55.0f;

    [ExportGroup("Ground Physics & Hardness")]
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")] public float SurfaceHardness { get; set; } = 0.5f; // 0.0 = mud, 0.5 = turf/grass, 1.0 = asphalt
    [Export] public float GroundBounciness { get; set; } = 0.50f;
    [Export] public float GroundFriction { get; set; } = 0.38f;
    [Export] public float RollThresholdAngle { get; set; } = 15.0f;  // Minimum bank angle to start rolling on edge
    [Export] public float DiscRadius { get; set; } = 0.35f;
    [Export] public float DiscThickness { get; set; } = 0.04f;

    [ExportGroup("Visual")]
    [Export] public Node3D? DiscVisual { get; set; }

    public bool IsFlying { get; private set; }
    public float FlightProgress { get; private set; }
    public float CurrentBankAngle { get; private set; }
    public GroundState CurrentGroundState => _groundState;

    public event System.Action<float>? HoverAnimationStarted;
    public event System.Action? NewTurnStarted;

    private float _initialSpeed;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private bool _isEndingFlight;
    private Tween? _hoverTween;
    private GroundState _groundState = GroundState.Settled;
    private float _groundTimer;
    private float _rollWheelAngle;
    private Vector3 _lastGroundNormal = Vector3.Up;

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

        SetupNewTurnHover(_startPosition);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsFlying || _isEndingFlight)
        {
            return;
        }

        float dt = (float)delta;
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
        _rollWheelAngle = 0.0f;

        Freeze = false;
        Sleeping = false;

        float speed = Mathf.Lerp(
            MinThrowSpeed,
            MaxThrowSpeed,
            parameters.Power
        );

        Vector3 launchDirection = parameters.Direction.Normalized();
        LinearVelocity = launchDirection * speed;
        AngularVelocity = Vector3.Zero;

        _initialSpeed = speed;
        CurrentBankAngle = parameters.ReleaseAngle;

        FlightProgress = 0.0f;
        GravityScale = FlightGravityScale;
        IsFlying = true;

        UpdateVisualOrientationAirborne(0.016f);
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
        _rollWheelAngle = 0.0f;
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

    private void ProcessAirborne(float dt, float groundSurfaceY)
    {
        if (GlobalPosition.Y <= groundSurfaceY)
        {
            HandleGroundImpact();
            return;
        }

        ApplyArcadeFlight(dt);
        UpdateFlightProgress();
        UpdateVisualOrientationAirborne(dt);
    }

    private void ProcessSkipping(float dt, float groundSurfaceY)
    {
        // In skipping state, disc has bounced off ground with ballistic arc
        LinearVelocity += Vector3.Down * 9.8f * FlightGravityScale * 2.5f * dt;
        LinearVelocity *= Mathf.Max(0.0f, 1.0f - Drag * dt);

        if (GlobalPosition.Y <= groundSurfaceY && LinearVelocity.Y <= 0.0f)
        {
            HandleGroundImpact();
            return;
        }

        UpdateVisualOrientationAirborne(dt);
    }

    private void ProcessRolling(float dt, float groundSurfaceY)
    {
        _groundTimer += dt;

        // Clamp to prevent disc sinking below ground
        GlobalPosition = new Vector3(GlobalPosition.X, groundSurfaceY, GlobalPosition.Z);

        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        float currentSpeed = horizVel.Length();

        if (currentSpeed < 0.1f)
        {
            _groundState = GroundState.Sliding;
            return;
        }

        Vector3 forward = horizVel.Normalized();
        Vector3 right = forward.Cross(Vector3.Up).Normalized();

        // 1. Curvature turn: disc curves naturally towards the side it is leaning on
        float leanFactor = Mathf.Sin(Mathf.DegToRad(CurrentBankAngle));
        Vector3 curveAcc = right * leanFactor * (4.5f + currentSpeed * 0.25f);
        LinearVelocity += curveAcc * dt;

        // 2. Rolling resistance
        float rollFriction = (0.20f + 0.35f * (1.0f - SurfaceHardness)) * 9.8f;
        horizVel = new Vector3(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        currentSpeed = Mathf.MoveToward(horizVel.Length(), 0.0f, rollFriction * dt);
        LinearVelocity = horizVel.Normalized() * currentSpeed;

        // 3. Wheel spin accumulation along rolling rim
        _rollWheelAngle += (currentSpeed / DiscRadius) * dt;

        // 4. Bank angle wobble and decay towards flat as speed drops
        float decayRate = 12.0f + Mathf.Max(0.0f, 3.5f - currentSpeed) * 30.0f;
        CurrentBankAngle = Mathf.MoveToward(CurrentBankAngle, 0.0f, decayRate * dt);

        UpdateVisualOrientationRolling(forward, dt);

        // Check if rolling mode ends
        if (Mathf.Abs(CurrentBankAngle) < 2.0f || currentSpeed < 0.35f || _groundTimer > 4.0f)
        {
            _groundState = GroundState.Sliding;
        }
    }

    private void ProcessSliding(float dt, float groundSurfaceY)
    {
        _groundTimer += dt;

        // Clamp to ensure it rests flat on grass surface without sinking
        GlobalPosition = new Vector3(GlobalPosition.X, groundSurfaceY, GlobalPosition.Z);

        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        float currentSpeed = horizVel.Length();

        // Level out remaining bank angle
        CurrentBankAngle = Mathf.MoveToward(CurrentBankAngle, 0.0f, 65.0f * dt);

        // Sliding friction
        float slideFriction = (GroundFriction * (0.6f + 0.9f * (1.0f - SurfaceHardness))) * 9.8f;
        currentSpeed = Mathf.MoveToward(currentSpeed, 0.0f, slideFriction * dt);

        Vector3 forward = horizVel.LengthSquared() > 0.001f ? horizVel.Normalized() : -DiscVisual?.GlobalBasis.Z ?? Vector3.Forward;
        LinearVelocity = forward * currentSpeed;

        UpdateVisualOrientationSliding(forward, dt);

        if (currentSpeed < 0.04f && Mathf.Abs(CurrentBankAngle) < 1.0f)
        {
            EndFlight();
        }
        else if (_groundTimer > 3.5f)
        {
            EndFlight();
        }
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

        // Check if landing angle causes a Roll on rim
        if (Mathf.Abs(CurrentBankAngle) >= RollThresholdAngle && horizSpeed > 1.2f)
        {
            _groundState = GroundState.Rolling;
            float rollPreservation = 0.75f + 0.20f * SurfaceHardness;
            LinearVelocity = horizVel * rollPreservation;
            _rollWheelAngle = 0.0f;
            return;
        }

        // Check if landing velocity causes a Skip / Bounce
        float restitution = GroundBounciness * (0.35f + 0.65f * SurfaceHardness);
        float reboundSpeedY = downwardSpeed * restitution;

        if (reboundSpeedY > 0.6f && horizSpeed > 1.8f)
        {
            _groundState = GroundState.Skipping;
            float skipForwardRetention = 0.76f + 0.20f * SurfaceHardness;
            LinearVelocity = new Vector3(
                horizVel.X * skipForwardRetention,
                reboundSpeedY,
                horizVel.Z * skipForwardRetention
            );
            return;
        }

        // Otherwise slide on the ground
        _groundState = GroundState.Sliding;
        float slideRetention = 0.55f + 0.35f * SurfaceHardness;
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

        // 1. Aerodynamic Bank Angle Adjustment (Turn & Fade)
        if (currentSpeed > TurnSpeedThreshold)
        {
            float turnFactor = (currentSpeed - TurnSpeedThreshold) / (MaxThrowSpeed - TurnSpeedThreshold + 0.01f);
            CurrentBankAngle += TurnStrength * turnFactor * dt;
        }

        if (currentSpeed < FadeSpeedThreshold)
        {
            float fadeFactor = 1.0f - (currentSpeed / FadeSpeedThreshold);
            CurrentBankAngle -= FadeStrength * fadeFactor * dt;
        }

        CurrentBankAngle = Mathf.Clamp(CurrentBankAngle, -MaxBankAngle, MaxBankAngle);

        // 2. Lateral Force from Bank Angle
        float bankRad = Mathf.DegToRad(CurrentBankAngle);
        Vector3 lateralAcc = right * Mathf.Sin(bankRad) * currentSpeed * LateralForceMultiplier;
        LinearVelocity += lateralAcc * dt;

        // 3. Lift & Glide
        float targetLift = currentSpeed * GlideStrength * 0.05f;
        float currentVerticalVel = LinearVelocity.Y;

        if (currentVerticalVel < 1.0f)
        {
            float liftForce = Mathf.Min(targetLift, (1.0f - currentVerticalVel) * 3.0f);
            LinearVelocity += Vector3.Up * liftForce * dt;
        }

        // 4. Air Drag
        float dragFactor = Mathf.Max(0.0f, 1.0f - Drag * dt);
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

    private void UpdateVisualOrientationAirborne(float dt)
    {
        if (DiscVisual == null)
        {
            return;
        }

        Vector3 horizVel = new(LinearVelocity.X, 0.0f, LinearVelocity.Z);
        if (horizVel.LengthSquared() < 0.01f)
        {
            return;
        }

        Vector3 forward = horizVel.Normalized();
        Vector3 right = forward.Cross(Vector3.Up).Normalized();
        Vector3 up = right.Cross(forward).Normalized();

        // Construct stable forward basis
        Basis orientationBasis = new(right, up, -forward);

        // Apply roll (bank angle) around local forward (-Z) axis
        orientationBasis = orientationBasis.Rotated(orientationBasis.Z, Mathf.DegToRad(CurrentBankAngle));

        // Apply slight pitch along flight velocity vector
        float verticalPitch = Mathf.Clamp(LinearVelocity.Y / (horizVel.Length() + 0.1f), -0.5f, 0.5f);
        orientationBasis = orientationBasis.Rotated(orientationBasis.X, verticalPitch * 0.4f);

        // Update disc visual globally with stable basis
        DiscVisual.GlobalBasis = orientationBasis;
    }

    private void UpdateVisualOrientationRolling(Vector3 forward, float dt)
    {
        if (DiscVisual == null)
        {
            return;
        }

        Vector3 right = forward.Cross(Vector3.Up).Normalized();
        Vector3 up = right.Cross(forward).Normalized();
        Basis orientationBasis = new(right, up, -forward);

        // Apply bank angle roll around forward (-Z)
        orientationBasis = orientationBasis.Rotated(orientationBasis.Z, Mathf.DegToRad(CurrentBankAngle));

        // Apply wheel rotation around right (X) axis
        orientationBasis = orientationBasis.Rotated(orientationBasis.X, _rollWheelAngle);

        DiscVisual.GlobalBasis = orientationBasis;
    }

    private void UpdateVisualOrientationSliding(Vector3 forward, float dt)
    {
        if (DiscVisual == null)
        {
            return;
        }

        Vector3 right = forward.Cross(Vector3.Up).Normalized();
        Vector3 up = Vector3.Up;
        Basis orientationBasis = new(right, up, -forward);

        if (Mathf.Abs(CurrentBankAngle) > 0.05f)
        {
            orientationBasis = orientationBasis.Rotated(orientationBasis.Z, Mathf.DegToRad(CurrentBankAngle));
        }

        DiscVisual.GlobalBasis = orientationBasis;
    }

    private void OnBodyEntered(Node body)
    {
        if (IsFlying && !_isEndingFlight)
        {
            if (_groundState == GroundState.Airborne || _groundState == GroundState.Skipping)
            {
                HandleGroundImpact();
            }
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
        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GravityScale = 0.0f;

        Vector3 groundPos = GlobalPosition;
        float groundY = GetGroundHeightAt(groundPos);
        // Ensure settled disc rests cleanly on the surface
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

            if (DiscVisual != null)
            {
                DiscVisual.Transform = Transform3D.Identity;
            }

            _isEndingFlight = false;
            IsFlying = false;
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
}
