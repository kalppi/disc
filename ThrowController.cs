using Godot;

public partial class ThrowController : Node
{
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Aim Sensitivity")]
    [Export] public float AimSensitivity { get; set; } = 0.15f;
    [Export] public float PowerSensitivity { get; set; } = 0.004f;
    [Export] public float TiltStep { get; set; } = 5.0f;

    [ExportGroup("Limits")]
    [Export] public float MinPitch { get; set; } = -80.0f;
    [Export] public float MaxPitch { get; set; } = 80.0f;
    [Export] public float MaxReleaseAngle { get; set; } = 45.0f;
    [Export] public float DefaultPower { get; set; } = 0.5f;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; } = 10.0f;
    public float Power { get; private set; } = 0.5f;
    public float ReleaseAngle { get; private set; }

    public bool IsHoldingLmb => _isHoldingLmb;
    public bool IsHoldingRmb => _isHoldingRmb;

    public Vector3 Direction => GetDirection();

    public event System.Action<ThrowParameters>? ThrowRequested;
    public event System.Action? ResetRequested;
    public event System.Action? FreeLookStarted;
    public event System.Action? FreeLookEnded;
    public event System.Action<Vector2>? FreeLookMotion;

    private bool _isHoldingLmb;
    private bool _isHoldingRmb;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Power = DefaultPower;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Escape)
            {
                Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                    ? Input.MouseModeEnum.Visible
                    : Input.MouseModeEnum.Captured;
                return;
            }

            if (keyEvent.Keycode == Key.R)
            {
                ResetRequested?.Invoke();
                return;
            }

            // Q & E keys adjust Hyzer/Anhyzer release angle tilt
            if (keyEvent.Keycode == Key.Q)
            {
                ReleaseAngle = Mathf.Clamp(ReleaseAngle - TiltStep, -MaxReleaseAngle, MaxReleaseAngle);
                return;
            }

            if (keyEvent.Keycode == Key.E)
            {
                ReleaseAngle = Mathf.Clamp(ReleaseAngle + TiltStep, -MaxReleaseAngle, MaxReleaseAngle);
                return;
            }
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }

            if (Disc != null && Disc.IsFlying)
            {
                return;
            }

            HandleMouseButton(mouseButton);
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (Disc != null && Disc.IsFlying)
            {
                return;
            }

            HandleMouseMotion(mouseMotion);
        }
    }

    public void SetAim(float yaw, float pitch)
    {
        Yaw = yaw;
        Pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        // 1. Right Mouse Button: Free-Look Orbit
        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            if (mouseButton.Pressed)
            {
                _isHoldingRmb = true;
                FreeLookStarted?.Invoke();
            }
            else
            {
                _isHoldingRmb = false;
                FreeLookEnded?.Invoke();
            }
            return;
        }

        // 2. Left Mouse Button: Power Pull-back & Launch
        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (_isHoldingRmb)
            {
                return;
            }

            if (mouseButton.Pressed)
            {
                _isHoldingLmb = true;
            }
            else if (_isHoldingLmb)
            {
                _isHoldingLmb = false;
                RequestThrow();
                Power = DefaultPower;
            }
        }
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        // A. If holding RMB: Survey / Free-Look orbit around the hole
        if (_isHoldingRmb)
        {
            FreeLookMotion?.Invoke(mouseMotion.Relative);
            return;
        }

        // B. If holding LMB: Pull back power (aim heading stays firmly locked)
        if (_isHoldingLmb)
        {
            Power += mouseMotion.Relative.Y * PowerSensitivity;
            Power = Mathf.Clamp(Power, 0.05f, 1.0f);
            return;
        }

        // C. Default (No buttons): Mouse directly rotates Aim direction & Camera
        Yaw -= mouseMotion.Relative.X * AimSensitivity;
        Pitch += mouseMotion.Relative.Y * AimSensitivity;
        Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
    }

    private Vector3 GetDirection()
    {
        Transform3D transform = Transform3D.Identity;
        transform = transform.Rotated(Vector3.Up, Mathf.DegToRad(Yaw));
        transform = transform.RotatedLocal(Vector3.Right, Mathf.DegToRad(Pitch));

        return -transform.Basis.Z.Normalized();
    }

    private void RequestThrow()
    {
        var parameters = new ThrowParameters(
            Direction,
            Power,
            ReleaseAngle
        );

        ThrowRequested?.Invoke(parameters);
    }
}
