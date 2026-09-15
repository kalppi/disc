using Godot;

public partial class DiscFlightController : RigidBody3D
{
    [ExportGroup("Throw")]
    [Export] public float MinThrowSpeed { get; set; } = 8.0f;
    [Export] public float MaxThrowSpeed { get; set; } = 28.0f;

    [ExportGroup("Flight")]
    [Export] public float TurnStrength { get; set; } = 1.5f;
    [Export] public float FadeStrength { get; set; } = 2.5f;
    [Export] public float GlideStrength { get; set; } = 8.0f;
    [Export] public float FlightGravityScale { get; set; } = 0.35f;
    [Export] public float Drag { get; set; } = 0.15f;

    [ExportGroup("Flight Phases")]
    [Export(PropertyHint.Range, "0,1")]
    public float TurnEnd { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0,1")]
    public float FadeStart { get; set; } = 0.65f;

    [ExportGroup("Visual")]
    [Export] public Node3D? DiscVisual { get; set; }

    public bool IsFlying { get; private set; }
    public float FlightProgress { get; private set; }

    private float _initialSpeed;
    private float _releaseAngle;

    public override void _Ready()
    {
        GravityScale = 1.0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsFlying)
        {
            return;
        }

        float dt = (float)delta;

        UpdateFlightProgress();
        ApplyArcadeFlight(dt);
        UpdateVisualOrientation();
        CheckForEndOfFlight();
    }

    public void Throw(ThrowParameters parameters)
    {
        float speed = Mathf.Lerp(
            MinThrowSpeed,
            MaxThrowSpeed,
            parameters.Power
        );

        LinearVelocity = parameters.Direction.Normalized() * speed;
        AngularVelocity = Vector3.Zero;

        _initialSpeed = speed;
        _releaseAngle = parameters.ReleaseAngle;

        FlightProgress = 0.0f;
        GravityScale = FlightGravityScale;
        IsFlying = true;

        Sleeping = false;

        UpdateVisualOrientation();
    }

    private void UpdateFlightProgress()
    {
        float currentSpeed = LinearVelocity.Length();

        if (_initialSpeed <= 0.0f)
        {
            FlightProgress = 1.0f;
            return;
        }

        // Flight progress is intentionally based on lost speed rather
        // than elapsed time. This makes stronger throws stay in their
        // high-speed flight phase longer.
        FlightProgress = 1.0f - currentSpeed / _initialSpeed;
        FlightProgress = Mathf.Clamp(FlightProgress, 0.0f, 1.0f);
    }

    private void ApplyArcadeFlight(float delta)
    {
        Vector3 horizontalVelocity = new(
            LinearVelocity.X,
            0.0f,
            LinearVelocity.Z
        );

        float horizontalSpeed = horizontalVelocity.Length();

        if (horizontalSpeed < 0.1f)
        {
            return;
        }

        Vector3 forward = horizontalVelocity.Normalized();
        Vector3 right = Vector3.Up.Cross(forward).Normalized();

        ApplyTurn(right, delta);
        ApplyGlide(delta);
        ApplyFade(right, delta);
        ApplyDrag(delta);
    }

    private void ApplyTurn(Vector3 right, float delta)
    {
        if (FlightProgress >= TurnEnd)
        {
            return;
        }

        float phase = 1.0f - FlightProgress / TurnEnd;

        // Positive lateral acceleration represents the high-speed
        // turn portion of the flight.
        LinearVelocity += right * TurnStrength * phase * delta;
    }

    private void ApplyGlide(float delta)
    {
        float verticalSpeed = LinearVelocity.Y;

        if (verticalSpeed >= 0.0f)
        {
            return;
        }

        // Glide opposes some downward velocity. This is deliberately
        // simple and exists to create long, readable flights rather
        // than model aerodynamic lift.
        float glide = Mathf.Min(
            -verticalSpeed,
            GlideStrength * delta
        );

        LinearVelocity += Vector3.Up * glide;
    }

    private void ApplyFade(Vector3 right, float delta)
    {
        if (FlightProgress <= FadeStart)
        {
            return;
        }

        float fadeRange = 1.0f - FadeStart;

        if (fadeRange <= 0.0f)
        {
            return;
        }

        float phase = (FlightProgress - FadeStart) / fadeRange;
        phase = Mathf.Clamp(phase, 0.0f, 1.0f);

        // Fade acts opposite to turn and gets progressively stronger
        // toward the end of the flight.
        LinearVelocity -= right * FadeStrength * phase * delta;
    }

    private void ApplyDrag(float delta)
    {
        float multiplier = Mathf.Max(0.0f, 1.0f - Drag * delta);
        LinearVelocity *= multiplier;
    }

    private void UpdateVisualOrientation()
    {
        if (DiscVisual == null)
        {
            return;
        }

        Vector3 horizontalDirection = new(
            LinearVelocity.X,
            0.0f,
            LinearVelocity.Z
        );

        if (horizontalDirection.LengthSquared() < 0.001f)
        {
            return;
        }

        horizontalDirection = horizontalDirection.Normalized();

        DiscVisual.LookAt(
            DiscVisual.GlobalPosition + horizontalDirection,
            Vector3.Up
        );

        DiscVisual.RotateObjectLocal(
            Vector3.Forward,
            Mathf.DegToRad(_releaseAngle)
        );
    }

    private void CheckForEndOfFlight()
    {
        if (LinearVelocity.Length() > 0.5f)
        {
            return;
        }

        IsFlying = false;
        GravityScale = 1.0f;
    }
}