using Godot;

public partial class CameraController : Node3D
{
    [Export] public Camera3D? Camera { get; set; }
    [Export] public ThrowController? ThrowController { get; set; }
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Aiming Camera")]
    [Export] public float AimDistance { get; set; } = 3.2f;
    [Export] public float AimHeight { get; set; } = 1.0f;
    [Export] public float AimFollowSpeed { get; set; } = 20.0f;

    [ExportGroup("Flight Chase Camera")]
    [Export] public float FlightDistance { get; set; } = 4.0f;
    [Export] public float FlightHeight { get; set; } = 1.6f;
    [Export] public float FlightFollowSpeed { get; set; } = 8.0f;
    [Export] public float FlightRotationSpeed { get; set; } = 6.0f;

    private Vector3 _currentCameraPos;
    private Vector3 _currentLookTarget;
    private bool _initialized;

    public override void _Ready()
    {
        if (Camera == null)
        {
            Camera = GetNodeOrNull<Camera3D>("Camera3D");
        }

        if (Disc != null && ThrowController != null)
        {
            _currentCameraPos = CalculateAimCameraPosition();
            _currentLookTarget = Disc.GlobalPosition + ThrowController.Direction * 2.0f;
            _initialized = true;

            if (Camera != null)
            {
                Camera.GlobalPosition = _currentCameraPos;
                Camera.LookAt(_currentLookTarget, Vector3.Up);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (Camera == null || Disc == null || ThrowController == null)
        {
            return;
        }

        float dt = (float)delta;

        if (!_initialized)
        {
            _currentCameraPos = CalculateAimCameraPosition();
            _currentLookTarget = Disc.GlobalPosition + ThrowController.Direction * 2.0f;
            _initialized = true;
        }

        if (Disc.IsFlying)
        {
            UpdateFlightCamera(dt);
        }
        else
        {
            UpdateAimCamera(dt);
        }
    }

    private void UpdateAimCamera(float dt)
    {
        Vector3 targetPos = CalculateAimCameraPosition();
        Vector3 lookTarget = Disc!.GlobalPosition + ThrowController!.Direction * 2.5f;

        float blend = Mathf.Clamp(AimFollowSpeed * dt, 0.0f, 1.0f);
        _currentCameraPos = _currentCameraPos.Lerp(targetPos, blend);
        _currentLookTarget = _currentLookTarget.Lerp(lookTarget, blend);

        Camera!.GlobalPosition = _currentCameraPos;
        Camera.LookAt(_currentLookTarget, Vector3.Up);
    }

    private Vector3 CalculateAimCameraPosition()
    {
        Vector3 pivot = Disc!.GlobalPosition;
        float yawRad = Mathf.DegToRad(ThrowController!.Yaw);
        float pitchRad = Mathf.DegToRad(ThrowController.Pitch);

        // Position camera behind the disc along the aiming line
        float horizDistance = AimDistance * Mathf.Cos(pitchRad * 0.35f);
        float vertOffset = AimHeight - AimDistance * Mathf.Sin(pitchRad * 0.35f);

        Vector3 offset = new(
            -Mathf.Sin(yawRad) * horizDistance,
            vertOffset,
            Mathf.Cos(yawRad) * horizDistance
        );

        return pivot + offset;
    }

    private void UpdateFlightCamera(float dt)
    {
        Vector3 discPos = Disc!.GlobalPosition;
        Vector3 velocity = Disc.LinearVelocity;

        Vector3 forward;
        if (velocity.LengthSquared() > 0.5f)
        {
            forward = velocity.Normalized();
        }
        else
        {
            forward = -Camera!.GlobalTransform.Basis.Z;
        }

        Vector3 targetPos = discPos - forward * FlightDistance + Vector3.Up * FlightHeight;
        Vector3 lookTarget = discPos + forward * 1.5f;

        float posBlend = Mathf.Clamp(FlightFollowSpeed * dt, 0.0f, 1.0f);
        float lookBlend = Mathf.Clamp(FlightRotationSpeed * dt, 0.0f, 1.0f);

        _currentCameraPos = _currentCameraPos.Lerp(targetPos, posBlend);
        _currentLookTarget = _currentLookTarget.Lerp(lookTarget, lookBlend);

        Camera!.GlobalPosition = _currentCameraPos;
        Camera.LookAt(_currentLookTarget, Vector3.Up);
    }
}
