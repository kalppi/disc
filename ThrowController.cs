using Godot;

public partial class ThrowController : Node
{
    [Export] public DiscFlightController? Disc { get; set; }

    [Export] public float AimSensitivity { get; set; } = 0.15f;
    [Export] public float PowerSensitivity { get; set; } = 0.004f;
    [Export] public float ReleaseAngleSensitivity { get; set; } = 0.15f;

    [Export] public float MinPitch { get; set; } = -80.0f;
    [Export] public float MaxPitch { get; set; } = 80.0f;
    [Export] public float MaxReleaseAngle { get; set; } = 45.0f;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; } = 10.0f;
    public float Power { get; private set; } = 0.5f;
    public float ReleaseAngle { get; private set; }
    public bool IsFreeCam { get; set; }

    public Vector3 Direction => GetDirection();

    public event System.Action<ThrowParameters>? ThrowRequested;
    public event System.Action? ResetRequested;
    public event System.Action<bool>? FreeCamToggled;

    private ThrowInputMode _mode = ThrowInputMode.Aiming;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
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

            if (keyEvent.Keycode == Key.C || keyEvent.Keycode == Key.Tab)
            {
                ToggleFreeCam();
                return;
            }

            if (keyEvent.Keycode == Key.R)
            {
                ResetRequested?.Invoke();
                return;
            }
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }

            // In free cam or during flight, don't trigger throw inputs
            if (IsFreeCam || (Disc != null && Disc.IsFlying))
            {
                return;
            }

            HandleMouseButton(mouseButton);
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            // If in free cam mode, ThrowController does not modify throw aim
            if (IsFreeCam)
            {
                return;
            }

            // If disc is flying, do not alter pre-throw aim
            if (Disc != null && Disc.IsFlying)
            {
                return;
            }

            HandleMouseMotion(mouseMotion);
        }
    }

    public void ToggleFreeCam()
    {
        IsFreeCam = !IsFreeCam;
        _mode = ThrowInputMode.Aiming;
        FreeCamToggled?.Invoke(IsFreeCam);
    }

    public void SetFreeCam(bool active)
    {
        if (IsFreeCam != active)
        {
            IsFreeCam = active;
            _mode = ThrowInputMode.Aiming;
            FreeCamToggled?.Invoke(IsFreeCam);
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            HandleThrowButton(mouseButton);
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            HandleReleaseAngleButton(mouseButton);
        }
    }

    private void HandleThrowButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed && _mode == ThrowInputMode.Aiming)
        {
            _mode = ThrowInputMode.SettingPower;
            return;
        }

        if (!mouseButton.Pressed && _mode == ThrowInputMode.SettingPower)
        {
            RequestThrow();
            _mode = ThrowInputMode.Aiming;
        }
    }

    private void HandleReleaseAngleButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed && _mode == ThrowInputMode.Aiming)
        {
            _mode = ThrowInputMode.SettingReleaseAngle;
            return;
        }

        if (!mouseButton.Pressed && _mode == ThrowInputMode.SettingReleaseAngle)
        {
            _mode = ThrowInputMode.Aiming;
        }
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        switch (_mode)
        {
            case ThrowInputMode.Aiming:
                UpdateAim(mouseMotion.Relative);
                break;

            case ThrowInputMode.SettingPower:
                UpdatePower(mouseMotion.Relative);
                break;

            case ThrowInputMode.SettingReleaseAngle:
                UpdateReleaseAngle(mouseMotion.Relative);
                break;
        }
    }

    private void UpdateAim(Vector2 mouseDelta)
    {
        Yaw -= mouseDelta.X * AimSensitivity;

        // Pulling the mouse down aims upward, moving up aims downward
        Pitch += mouseDelta.Y * AimSensitivity;
        Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
    }

    private void UpdatePower(Vector2 mouseDelta)
    {
        // Pulling downward increases power; moving up decreases power
        Power += mouseDelta.Y * PowerSensitivity;
        Power = Mathf.Clamp(Power, 0.0f, 1.0f);
    }

    private void UpdateReleaseAngle(Vector2 mouseDelta)
    {
        ReleaseAngle += mouseDelta.X * ReleaseAngleSensitivity;
        ReleaseAngle = Mathf.Clamp(
            ReleaseAngle,
            -MaxReleaseAngle,
            MaxReleaseAngle
        );
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

    private enum ThrowInputMode
    {
        Aiming,
        SettingPower,
        SettingReleaseAngle
    }
}
