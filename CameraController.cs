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

    [ExportGroup("Free-Look Survey Camera (Hold RMB)")]
    [Export] public float FreeLookSensitivity { get; set; } = 0.15f;
    [Export] public float FreeLookReturnDuration { get; set; } = 0.25f;
    [Export] public bool ResetCameraOnFreeLookEnd { get; set; } = true;

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

    public event System.Action<bool>? FreeLookResetToggled;

    private Vector3 _cameraPosition;
    private float _camYaw;
    private float _camPitch;
    private float _flightYaw;
    private float _flightPitch;
    private bool _isFreeLooking;
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
            ThrowController.ThrowRequested += OnThrowRequested;
            ThrowController.FreeLookStarted += OnFreeLookStarted;
            ThrowController.FreeLookEnded += OnFreeLookEnded;
            ThrowController.FreeLookMotion += OnFreeLookMotion;
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
            ThrowController.ThrowRequested -= OnThrowRequested;
            ThrowController.FreeLookStarted -= OnFreeLookStarted;
            ThrowController.FreeLookEnded -= OnFreeLookEnded;
            ThrowController.FreeLookMotion -= OnFreeLookMotion;
        }

        if (Disc != null)
        {
            Disc.HoverAnimationStarted -= OnHoverAnimationStarted;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F || keyEvent.Keycode == Key.C)
            {
                ToggleFreeLookReset();
                return;
            }
        }

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

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (Disc != null && Disc.IsFlying && !_isTweeningToAim)
            {
                // Orbit around the flying disc in air
                _flightYaw -= mouseMotion.Relative.X * FlightOrbitSensitivity;
                _flightPitch -= mouseMotion.Relative.Y * FlightOrbitSensitivity;
                _flightPitch = Mathf.Clamp(_flightPitch, -30.0f, 65.0f);
            }
        }
    }

    public void ToggleFreeLookReset()
    {
        ResetCameraOnFreeLookEnd = !ResetCameraOnFreeLookEnd;
        FreeLookResetToggled?.Invoke(ResetCameraOnFreeLookEnd);
    }

    public override void _Process(double delta)
    {
        if (Camera == null)
        {
            return;
        }

        if (Disc != null && !Disc.IsFlying && !_isTweeningToAim)
        {
            UpdateAimModeCamera();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Camera == null || Disc == null || !Disc.IsFlying || _isTweeningToAim)
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

    private void OnFreeLookStarted()
    {
        _cameraTween?.Kill();
        _cameraTween = null;
        _isTweeningToAim = false;

        _isFreeLooking = true;
    }

    private void OnFreeLookEnded()
    {
        _isFreeLooking = false;

        if (Camera == null || Disc == null || ThrowController == null)
        {
            return;
        }

        if (!ResetCameraOnFreeLookEnd)
        {
            // Do NOT change throw direction, and do NOT reset camera position!
            // Camera remains positioned at its current surveyed viewpoint.
            _cameraPosition = Camera.GlobalPosition;
            return;
        }

        // Smoothly snap back to aim stance
        _camYaw = ThrowController.Yaw;
        _camPitch = ThrowController.Pitch;
        Transform3D targetTransform = GetOrbitTransform(Disc.GlobalPosition, _camYaw, _camPitch);

        _cameraTween?.Kill();
        _cameraTween = CreateTween();
        _cameraTween.SetProcessMode(Tween.TweenProcessMode.Physics);
        _isTweeningToAim = true;

        _cameraTween.TweenProperty(Camera, "global_transform", targetTransform, FreeLookReturnDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        _cameraTween.TweenCallback(Callable.From(() =>
        {
            _isTweeningToAim = false;
            if (Camera != null)
            {
                _cameraPosition = Camera.GlobalPosition;
            }
        }));
    }

    private void OnFreeLookMotion(Vector2 relative)
    {
        if (!_isFreeLooking || Camera == null || Disc == null)
        {
            return;
        }

        _camYaw -= relative.X * FreeLookSensitivity;
        _camPitch += relative.Y * FreeLookSensitivity;
        _camPitch = Mathf.Clamp(_camPitch, -80.0f, 80.0f);

        Transform3D orbitTransform = GetOrbitTransform(Disc.GlobalPosition, _camYaw, _camPitch);
        Camera.GlobalPosition = orbitTransform.Origin;
        Camera.GlobalBasis = orbitTransform.Basis;
        _cameraPosition = Camera.GlobalPosition;
    }

    private void OnHoverAnimationStarted(float duration)
    {
        if (Camera == null || Disc == null)
        {
            return;
        }

        // Preserve the camera viewing direction the player rotated to during flight
        if (ThrowController != null)
        {
            ThrowController.SetAim(_flightYaw, _flightPitch);
        }

        _camYaw = ThrowController?.Yaw ?? _flightYaw;
        _camPitch = ThrowController?.Pitch ?? _flightPitch;

        Vector3 hoverPos = Disc.GetHoverPositionFor(Disc.GlobalPosition);
        Transform3D targetTransform = GetOrbitTransform(hoverPos, _camYaw, _camPitch);

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
        _isFreeLooking = false;

        // Initialize flight orbit rotation directly from current camera rotation
        _flightYaw = _camYaw;
        _flightPitch = _camPitch;

        if (Camera != null)
        {
            _cameraPosition = Camera.GlobalPosition;
        }
    }

    private void OnResetRequested()
    {
        _cameraTween?.Kill();
        _cameraTween = null;
        _isTweeningToAim = false;
        _isFreeLooking = false;

        SnapToAimStance();
    }

    private void SnapToAimStance()
    {
        if (Disc == null || Camera == null)
        {
            return;
        }

        _cameraTween?.Kill();
        _cameraTween = null;
        _isTweeningToAim = false;
        _isFreeLooking = false;

        if (ResetCameraOnFreeLookEnd || ThrowController == null)
        {
            _camYaw = ThrowController?.Yaw ?? 0.0f;
            _camPitch = ThrowController?.Pitch ?? 10.0f;
        }

        Transform3D aimTransform = GetOrbitTransform(Disc.GlobalPosition, _camYaw, _camPitch);
        _cameraPosition = aimTransform.Origin;
        Camera.GlobalPosition = _cameraPosition;
        Camera.GlobalBasis = aimTransform.Basis;
    }

    private Transform3D GetOrbitTransform(Vector3 pivot, float yaw, float pitch)
    {
        Transform3D transform = Transform3D.Identity;
        transform = transform.Rotated(Vector3.Up, Mathf.DegToRad(yaw));
        transform = transform.RotatedLocal(Vector3.Right, Mathf.DegToRad(pitch));

        float height = AimDistance * AimHeightRatio;
        Vector3 cameraOffset = transform.Basis * new Vector3(0.0f, height, AimDistance);
        Vector3 targetPos = pivot + cameraOffset;

        return new Transform3D(transform.Basis, targetPos);
    }

    private void UpdateAimModeCamera()
    {
        if (Disc == null || Camera == null || _isTweeningToAim || _isFreeLooking || ThrowController == null)
        {
            return;
        }

        if (ResetCameraOnFreeLookEnd)
        {
            _camYaw = ThrowController.Yaw;
            _camPitch = ThrowController.Pitch;
        }

        Transform3D aimTransform = GetOrbitTransform(Disc.GlobalPosition, _camYaw, _camPitch);
        Camera.GlobalPosition = aimTransform.Origin;
        Camera.GlobalBasis = aimTransform.Basis;
        _cameraPosition = Camera.GlobalPosition;
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
}
