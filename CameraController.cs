using Godot;

public partial class CameraController : Node3D
{
    [Export] public Camera3D? Camera { get; set; }
    [Export] public ThrowController? ThrowController { get; set; }
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Aim Stance Camera")]
    [Export] public float AimDistance { get; set; } = 3.6f;
    [Export] public float AimHeightRatio { get; set; } = 0.32f;
    [Export] public float MinAimDistance { get; set; } = 1.5f;
    [Export] public float MaxAimDistance { get; set; } = 15.0f;

    [ExportGroup("Flight Orbit Camera")]
    [Export] public float FlightDistance { get; set; } = 5.5f;
    [Export] public float FlightHeightRatio { get; set; } = 0.30f;
    [Export] public float MinFlightDistance { get; set; } = 2.5f;
    [Export] public float MaxFlightDistance { get; set; } = 22.0f;
    [Export] public float FlightFollowSpeed { get; set; } = 14.0f;
    [Export] public float FlightOrbitSensitivity { get; set; } = 0.15f;

    [ExportGroup("Zoom Controls")]
    [Export] public float ZoomStep { get; set; } = 0.5f;

    [ExportGroup("Free Movement")]
    [Export] public float MoveSpeed { get; set; } = 14.0f;
    [Export] public float FastMoveMultiplier { get; set; } = 2.5f;
    [Export] public float SlowMoveMultiplier { get; set; } = 0.4f;
    [Export] public float FreeCamSensitivity { get; set; } = 0.15f;
    [Export] public float Acceleration { get; set; } = 16.0f;

    public bool IsFreeCam { get; private set; }

    private Vector3 _velocity;
    private Vector3 _cameraPosition;

    private float _freeCamYaw;
    private float _freeCamPitch;
    private float _flightYaw;
    private float _flightPitch = 12.0f;
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
            ThrowController.ThrowRequested += OnThrowRequested;
        }

        SnapToAimStance();
    }

    public override void _ExitTree()
    {
        if (ThrowController != null)
        {
            ThrowController.ResetRequested -= OnResetRequested;
            ThrowController.FreeCamToggled -= OnFreeCamToggled;
            ThrowController.ThrowRequested -= OnThrowRequested;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                AdjustZoom(-ZoomStep);
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                AdjustZoom(ZoomStep);
                return;
            }
        }

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

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (IsFreeCam)
            {
                _freeCamYaw -= mouseMotion.Relative.X * FreeCamSensitivity;
                _freeCamPitch -= mouseMotion.Relative.Y * FreeCamSensitivity;
                _freeCamPitch = Mathf.Clamp(_freeCamPitch, -85.0f, 85.0f);
            }
            else if (Disc != null && Disc.IsFlying)
            {
                // Orbit around the flying disc in air
                _flightYaw -= mouseMotion.Relative.X * FlightOrbitSensitivity;
                _flightPitch -= mouseMotion.Relative.Y * FlightOrbitSensitivity;
                _flightPitch = Mathf.Clamp(_flightPitch, -30.0f, 65.0f);
            }
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

        UpdateFlightOrbitCamera((float)delta);
    }

    private void AdjustZoom(float delta)
    {
        AimDistance = Mathf.Clamp(AimDistance + delta, MinAimDistance, MaxAimDistance);
        FlightDistance = Mathf.Clamp(FlightDistance + delta * 1.3f, MinFlightDistance, MaxFlightDistance);
    }

    private void OnThrowRequested(ThrowParameters parameters)
    {
        // Start flight orbit facing from behind the throw direction
        if (ThrowController != null)
        {
            _flightYaw = ThrowController.Yaw;
            _flightPitch = Mathf.Clamp(ThrowController.Pitch * 0.5f + 10.0f, 5.0f, 35.0f);
        }
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

        float height = AimDistance * AimHeightRatio;
        Vector3 cameraOffset = aimTransform.Basis * new Vector3(0.0f, height, AimDistance);
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

            float height = AimDistance * AimHeightRatio;
            Vector3 cameraOffset = aimTransform.Basis * new Vector3(0.0f, height, AimDistance);
            Camera.GlobalPosition = pivot + cameraOffset;
            Camera.GlobalBasis = aimTransform.Basis;
            _cameraPosition = Camera.GlobalPosition;
        }
        else
        {
            Camera.GlobalPosition = _cameraPosition;
        }
    }

    private void UpdateFlightOrbitCamera(float dt)
    {
        Vector3 discPos = Disc!.GlobalPosition;

        float yawRad = Mathf.DegToRad(_flightYaw);
        float pitchRad = Mathf.DegToRad(_flightPitch);

        float horizDistance = FlightDistance * Mathf.Cos(pitchRad * 0.4f);
        float height = FlightDistance * FlightHeightRatio - FlightDistance * Mathf.Sin(pitchRad * 0.4f);

        Vector3 offset = new(
            -Mathf.Sin(yawRad) * horizDistance,
            height,
            Mathf.Cos(yawRad) * horizDistance
        );

        Vector3 targetPos = discPos + offset;
        Vector3 lookTarget = discPos;

        float posBlend = Mathf.Clamp(FlightFollowSpeed * dt, 0.0f, 1.0f);
        _cameraPosition = _cameraPosition.Lerp(targetPos, posBlend);

        Camera!.GlobalPosition = _cameraPosition;
        Camera.LookAt(lookTarget, Vector3.Up);
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
