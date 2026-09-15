using Godot;

public partial class DiscFlightController : RigidBody3D
{
    [ExportGroup("Throw Setup")]
    [Export] public float HoverHeight { get; set; } = 1.5f;
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

    [ExportGroup("Visual")]
    [Export] public Node3D? DiscVisual { get; set; }

    public bool IsFlying { get; private set; }
    public float FlightProgress { get; private set; }
    public float CurrentBankAngle { get; private set; }

    public event System.Action? NewTurnStarted;

    private float _initialSpeed;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private bool _hasTouchedGround;
    private float _groundContactTimer;

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
        if (!IsFlying)
        {
            return;
        }

        float dt = (float)delta;

        if (_hasTouchedGround)
        {
            _groundContactTimer += dt;
            if (_groundContactTimer > 0.8f || LinearVelocity.Length() < 0.6f)
            {
                EndFlight();
                return;
            }
        }
        else
        {
            ApplyArcadeFlight(dt);
        }

        UpdateFlightProgress();
        UpdateVisualOrientation(dt);
    }

    public void Throw(ThrowParameters parameters)
    {
        Freeze = false;
        Sleeping = false;
        _hasTouchedGround = false;
        _groundContactTimer = 0.0f;

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

        UpdateVisualOrientation(0.016f);
    }

    public void ResetPosition()
    {
        SetupNewTurnHover(_startPosition);
    }

    public void SetupNewTurnHover(Vector3 basePosition)
    {
        IsFlying = false;
        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GravityScale = 0.0f;
        _hasTouchedGround = false;
        _groundContactTimer = 0.0f;

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

    private void UpdateVisualOrientation(float dt)
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

    private void OnBodyEntered(Node body)
    {
        if (IsFlying && !_hasTouchedGround)
        {
            _hasTouchedGround = true;
            GravityScale = 1.0f;
        }
    }

    private void EndFlight()
    {
        SetupNewTurnHover(GlobalPosition);
    }

    private float GetGroundHeightAt(Vector3 position)
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
