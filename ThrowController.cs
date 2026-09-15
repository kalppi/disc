using Godot;

public partial class ThrowController : Node
{
    [Export] public float AimSensitivity { get; set; } = 0.15f;
    [Export] public float PowerSensitivity { get; set; } = 0.005f;
    [Export] public float ReleaseAngleSensitivity { get; set; } = 0.15f;

    [Export] public float MinPitch { get; set; } = -20.0f;
    [Export] public float MaxPitch { get; set; } = 60.0f;
    [Export] public float MaxReleaseAngle { get; set; } = 45.0f;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; } = 10.0f;
    public float Power { get; private set; } = 0.5f;
    public float ReleaseAngle { get; private set; }

    public Vector3 Direction => GetDirection();

    public event System.Action<ThrowParameters>? ThrowRequested;

    private ThrowInputMode _mode = ThrowInputMode.Aiming;
    private Vector2 _powerMouseOrigin;
    private float _powerAtStart;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleMouseMotion(mouseMotion);
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
            _powerMouseOrigin = mouseButton.Position;
            _powerAtStart = Power;
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
                UpdatePower(mouseMotion.Position);
                break;

            case ThrowInputMode.SettingReleaseAngle:
                UpdateReleaseAngle(mouseMotion.Relative);
                break;
        }
    }

    private void UpdateAim(Vector2 mouseDelta)
    {
        Yaw -= mouseDelta.X * AimSensitivity;

        // Pulling the mouse down aims upward, like rotating the
        // aiming line around the throw pivot.
        Pitch += mouseDelta.Y * AimSensitivity;
        Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
    }

    private void UpdatePower(Vector2 mousePosition)
    {
        // Pulling downward from the point where LMB was pressed
        // increases throw power. Moving back up reduces it.
        float pullDistance = mousePosition.Y - _powerMouseOrigin.Y;

        Power = _powerAtStart + pullDistance * PowerSensitivity;
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
        float yawRadians = Mathf.DegToRad(Yaw);
        float pitchRadians = Mathf.DegToRad(Pitch);

        Vector3 direction = new(
            Mathf.Sin(yawRadians) * Mathf.Cos(pitchRadians),
            Mathf.Sin(pitchRadians),
            -Mathf.Cos(yawRadians) * Mathf.Cos(pitchRadians)
        );

        return direction.Normalized();
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