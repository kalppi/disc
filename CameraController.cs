using Godot;

public partial class CameraController : Node3D
{
    [Export] public Camera3D? Camera { get; set; }
    [Export] public ThrowController? ThrowController { get; set; }
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Aim Stance Camera")]
    [Export] public float AimDistance { get; set; } = 2.8f;
    [Export] public float AimHeight { get; set; } = 0.85f;

    [ExportGroup("Flight Chase Camera")]
    [Export] public float FlightDistance { get; set; } = 4.0f;
    [Export] public float FlightHeight { get; set; } = 1.6f;
    [Export] public float FlightFollowSpeed { get; set; } = 10.0f;
    [Export] public float FlightRotationSpeed { get; set; } = 7.0f;

    [ExportGroup("Free Movement")]
    [Export] public float MoveSpeed { get; set; } = 14.0f;
    [Export] public float FastMoveMultiplier { get; set; } = 2.5f;
    [Export] public float SlowMoveMultiplier { get; set; } = 0.4f;
    [Export] public float FreeCamSensitivity { get; set; } = 0.15f;
    [Export] public float Acceleration { get; set; } = 16.0f;

    public bool IsFreeCam { get; private set; }

    private Vector3 _velocity;
    private Vector3 _cameraPosition;
    private Vector3 _currentLookTarget;

    private float _freeCamYaw;
    private float _freeCamPitch;
    private bool _isAttachedToDisc = true;

    public override void _Ready()
    {
        if (Camera == null)
        {
            Camera = GetNodeOrNull<Camera3D>("Camera3D");
        }

        if (ThrowController != null)
        {
            ThrowController.ResetRequested += OnResetRequested;
            ThrowController.FreeCamToggled += OnFreeCamToggled;
        }

        SnapToAimStance();
    }

    public override void _ExitTree()
    {
        if (ThrowController != null)
        {
            ThrowController.ResetRequested -= OnResetRequested;
            ThrowController.FreeCamToggled -= OnFreeCamToggled;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.F)
            {
                if (IsFreeCam && ThrowController != null)
                {
                    ThrowController.SetFreeCam(false);
                }
                SnapToAimStance();
                return;
            }
        }

        if (IsFreeCam && @event is InputEventMouseMotion mouseMotion)
        {
            _freeCamYaw -= mouseMotion.Relative.X * FreeCamSensitivity;
            _freeCamPitch -= mouseMotion.Relative.Y * FreeCamSensitivity;
            _freeCamPitch = Mathf.Clamp(_freeCamPitch, -85.0f, 85.0f);
        }
    }

    public override void _Process(double delta)
    {
        if (Camera == null)
        {
            return;
        }

        float dt = (float)delta;

        if (IsFreeCam)
        {
            UpdateFreeCam(dt);
        }
        else if (Disc != null && !Disc.IsFlying)
        {
            UpdateAimModeCamera();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Camera == null || Disc == null || IsFreeCam || !Disc.IsFlying)
        {
            return;
        }

        UpdateFlightChaseCamera((float)delta);
    }

    private void OnFreeCamToggled(bool active)
    {
        IsFreeCam = active;
        if (IsFreeCam)
        {
            _isAttachedToDisc = false;
            if (ThrowController != null)
            {
                _freeCamYaw = ThrowController.Yaw;
                _freeCamPitch = ThrowController.Pitch;
            }
            if (Camera != null)
            {
                _cameraPosition = Camera.GlobalPosition;
            }
        }
        else
        {
            // Keep camera exactly where it is - no snap/teleport
            if (Camera != null)
            {
                _cameraPosition = Camera.GlobalPosition;
            }
        }
    }

    private void OnResetRequested()
    {
        if (ThrowController != null)
        {
            ThrowController.SetFreeCam(false);
        }
        SnapToAimStance();
    }

    private void SnapToAimStance()
    {
        if (Disc == null || ThrowController == null || Camera == null)
        {
            return;
        }

        _isAttachedToDisc = true;
        _velocity = Vector3.Zero;

        Vector3 pivot = Disc.GlobalPosition;
        Transform3D aimTransform = Transform3D.Identity;
        aimTransform = aimTransform.Rotated(Vector3.Up, Mathf.DegToRad(ThrowController.Yaw));
        aimTransform = aimTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(ThrowController.Pitch));

        Vector3 cameraOffset = aimTransform.Basis * new Vector3(0.0f, AimHeight, AimDistance);
        _cameraPosition = pivot + cameraOffset;

        Camera.GlobalPosition = _cameraPosition;
        Camera.GlobalBasis = aimTransform.Basis;
    }

    private void UpdateAimModeCamera()
    {
        if (Disc == null || ThrowController == null || Camera == null)
        {
            return;
        }

        if (_isAttachedToDisc)
        {
            Vector3 pivot = Disc.GlobalPosition;
            Transform3D aimTransform = Transform3D.Identity;
            aimTransform = aimTransform.Rotated(Vector3.Up, Mathf.DegToRad(ThrowController.Yaw));
            aimTransform = aimTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(ThrowController.Pitch));

            Vector3 cameraOffset = aimTransform.Basis * new Vector3(0.0f, AimHeight, AimDistance);
            Camera.GlobalPosition = pivot + cameraOffset;
            Camera.GlobalBasis = aimTransform.Basis;
            _cameraPosition = Camera.GlobalPosition;
        }
        else
        {
            // Camera stays at its chosen free position without jumping
            Camera.GlobalPosition = _cameraPosition;
        }
    }

    private void UpdateFlightChaseCamera(float dt)
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

        _cameraPosition = _cameraPosition.Lerp(targetPos, posBlend);
        _currentLookTarget = _currentLookTarget.Lerp(lookTarget, lookBlend);

        Camera!.GlobalPosition = _cameraPosition;
        Camera.LookAt(_currentLookTarget, Vector3.Up);
    }

    private void UpdateFreeCam(float dt)
    {
        // 1. Update Free Cam Rotation
        Transform3D camTransform = Transform3D.Identity;
        camTransform = camTransform.Rotated(Vector3.Up, Mathf.DegToRad(_freeCamYaw));
        camTransform = camTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(_freeCamPitch));
        Camera!.GlobalBasis = camTransform.Basis;

        // 2. Update Free Cam Translation (WASD)
        Vector3 inputDir = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) inputDir -= Camera.GlobalTransform.Basis.Z;
        if (Input.IsKeyPressed(Key.S)) inputDir += Camera.GlobalTransform.Basis.Z;
        if (Input.IsKeyPressed(Key.A)) inputDir -= Camera.GlobalTransform.Basis.X;
        if (Input.IsKeyPressed(Key.D)) inputDir += Camera.GlobalTransform.Basis.X;
        if (Input.IsKeyPressed(Key.E) || Input.IsKeyPressed(Key.Space)) inputDir += Vector3.Up;
        if (Input.IsKeyPressed(Key.Q) || Input.IsKeyPressed(Key.C)) inputDir -= Vector3.Up;

        if (inputDir.LengthSquared() > 0.001f)
        {
            inputDir = inputDir.Normalized();
        }

        float speed = MoveSpeed;
        if (Input.IsKeyPressed(Key.Shift))
        {
            speed *= FastMoveMultiplier;
        }
        else if (Input.IsKeyPressed(Key.Ctrl))
        {
            speed *= SlowMoveMultiplier;
        }

        Vector3 targetVelocity = inputDir * speed;
        _velocity = _velocity.Lerp(targetVelocity, Mathf.Clamp(Acceleration * dt, 0.0f, 1.0f));

        _cameraPosition += _velocity * dt;
        Camera.GlobalPosition = _cameraPosition;
    }
}
