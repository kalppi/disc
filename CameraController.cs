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
    [Export] public float FlightFollowSpeed { get; set; } = 16.0f;
    [Export] public float FlightRotationSpeed { get; set; } = 14.0f;
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
    private float _flightPitch;
    private bool _isAttachedToDisc = true;
    private bool _isTweeningToAim;
    private Tween? _cameraTween;

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

        if (Disc != null)
        {
            Disc.HoverAnimationStarted += OnHoverAnimationStarted;
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

        if (Disc != null)
        {
            Disc.HoverAnimationStarted -= OnHoverAnimationStarted;
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
            else if (Disc != null && Disc.IsFlying && !_isTweeningToAim)
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
        else if (Disc != null && !Disc.IsFlying && !_isTweeningToAim)
        {
            UpdateAimModeCamera();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Camera == null || Disc == null || IsFreeCam || !Disc.IsFlying || _isTweeningToAim)
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

    private void OnHoverAnimationStarted(float duration)
    {
        if (Camera == null || Disc == null || IsFreeCam)
        {
            return;
        }

        Vector3 hoverPos = Disc.GetHoverPositionFor(Disc.GlobalPosition);
        Transform3D targetTransform = GetAimTransform(hoverPos);

        _cameraTween?.Kill();
        _cameraTween = CreateTween();
        _cameraTween.SetProcessMode(Tween.TweenProcessMode.Physics);
        _isTweeningToAim = true;

        _cameraTween.TweenProperty(Camera, "global_transform", targetTransform, duration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);

        _cameraTween.TweenCallback(Callable.From(() =>
        {
            _isTweeningToAim = false;
            _isAttachedToDisc = true;
            if (Camera != null)
            {
                _cameraPosition = Camera.GlobalPosition;
            }
        }));
    }

    private void OnThrowRequested(ThrowParameters parameters)
    {
        _cameraTween?.Kill();
        _cameraTween = null;
        _isTweeningToAim = false;

        // Initialize flight orbit rotation directly from aim rotation
        if (ThrowController != null)
        {
            _flightYaw = ThrowController.Yaw;
            _flightPitch = ThrowController.Pitch;
        }

        if (Camera != null)
        {
            _cameraPosition = Camera.GlobalPosition;
        }
    }

    private void OnFreeCamToggled(bool active)
    {
        _cameraTween?.Kill();
        _cameraTween = null;
        _isTweeningToAim = false;

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
            // Returning to Aim mode
            if (ThrowController != null)
            {
                // Synchronize throw aim with the view direction from rotate mode
                ThrowController.SetAim(_freeCamYaw, _freeCamPitch);
            }

            if (Disc != null && Camera != null)
            {
                Transform3D targetTransform = GetAimTransform(Disc.GlobalPosition);
                float dist = Camera.GlobalPosition.DistanceTo(targetTransform.Origin);

                if (dist > 0.8f)
                {
                    // If player moved away in free cam, smoothly tween back to aim stance
                    _cameraTween = CreateTween();
                    _cameraTween.SetProcessMode(Tween.TweenProcessMode.Physics);
                    _isTweeningToAim = true;

                    _cameraTween.TweenProperty(Camera, "global_transform", targetTransform, 0.35f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.InOut);

                    _cameraTween.TweenCallback(Callable.From(() =>
                    {
                        _isTweeningToAim = false;
                        _isAttachedToDisc = true;
                        _velocity = Vector3.Zero;
                        if (Camera != null)
                        {
                            _cameraPosition = Camera.GlobalPosition;
                        }
                    }));
                }
                else
                {
                    // Seamless re-attachment without jump
                    _isAttachedToDisc = true;
                    _velocity = Vector3.Zero;
                    Camera.GlobalPosition = targetTransform.Origin;
                    Camera.GlobalBasis = targetTransform.Basis;
                    _cameraPosition = Camera.GlobalPosition;
                }
            }
            else
            {
                _isAttachedToDisc = true;
                _velocity = Vector3.Zero;
            }
        }
    }

    private void OnResetRequested()
    {
        _cameraTween?.Kill();
        _cameraTween = null;
        _isTweeningToAim = false;

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

        _cameraTween?.Kill();
        _cameraTween = null;
        _isTweeningToAim = false;

        _isAttachedToDisc = true;
        _velocity = Vector3.Zero;

        Transform3D aimTransform = GetAimTransform(Disc.GlobalPosition);
        _cameraPosition = aimTransform.Origin;
        Camera.GlobalPosition = _cameraPosition;
        Camera.GlobalBasis = aimTransform.Basis;
    }

    private Transform3D GetAimTransform(Vector3 pivot)
    {
        if (ThrowController == null)
        {
            return Camera?.GlobalTransform ?? Transform3D.Identity;
        }

        Transform3D aimTransform = Transform3D.Identity;
        aimTransform = aimTransform.Rotated(Vector3.Up, Mathf.DegToRad(ThrowController.Yaw));
        aimTransform = aimTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(ThrowController.Pitch));

        float height = AimDistance * AimHeightRatio;
        Vector3 cameraOffset = aimTransform.Basis * new Vector3(0.0f, height, AimDistance);
        Vector3 targetPos = pivot + cameraOffset;

        return new Transform3D(aimTransform.Basis, targetPos);
    }

    private void UpdateAimModeCamera()
    {
        if (Disc == null || ThrowController == null || Camera == null || _isTweeningToAim)
        {
            return;
        }

        if (_isAttachedToDisc)
        {
            Transform3D aimTransform = GetAimTransform(Disc.GlobalPosition);
            Camera.GlobalPosition = aimTransform.Origin;
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
        if (Disc == null || Camera == null)
        {
            return;
        }

        Vector3 discPos = Disc.GlobalPosition;

        // Construct flight camera basis from current flight yaw & pitch
        Transform3D flightTransform = Transform3D.Identity;
        flightTransform = flightTransform.Rotated(Vector3.Up, Mathf.DegToRad(_flightYaw));
        flightTransform = flightTransform.RotatedLocal(Vector3.Right, Mathf.DegToRad(_flightPitch));

        float height = FlightDistance * FlightHeightRatio;
        Vector3 offset = flightTransform.Basis * new Vector3(0.0f, height, FlightDistance);
        Vector3 targetPos = discPos + offset;

        // Position follow
        float posBlend = Mathf.Clamp(FlightFollowSpeed * dt, 0.0f, 1.0f);
        _cameraPosition = _cameraPosition.Lerp(targetPos, posBlend);
        Camera.GlobalPosition = _cameraPosition;

        // Rotation follow
        float rotBlend = Mathf.Clamp(FlightRotationSpeed * dt, 0.0f, 1.0f);
        Quaternion currentQuat = Camera.GlobalBasis.GetRotationQuaternion();
        Quaternion targetQuat = flightTransform.Basis.GetRotationQuaternion();
        Quaternion slerpedQuat = currentQuat.Slerp(targetQuat, rotBlend);

        Camera.GlobalBasis = new Basis(slerpedQuat);
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
