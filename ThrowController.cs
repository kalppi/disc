using Godot;

public partial class ThrowController : Node
{
    [Export] public DiscFlightController? Disc { get; set; }

    [ExportGroup("Throw Technique")]
    [Export] public ThrowTechnique Technique { get; set; } = ThrowTechnique.RHBH;

    [ExportGroup("Power & Auto-Charge Control")]
    [Export] public bool AutoChargePower { get; set; } = true;
    [Export] public float ChargeDuration { get; set; } = 1.25f; // Seconds to reach 100% power
    [Export] public bool PingPongCharge { get; set; } = true;   // Oscillate down and up if held past 100%
    [Export] public float MinChargePower { get; set; } = 0.08f;
    [Export] public float MaxChargePower { get; set; } = 1.0f;
    [Export] public bool ResetPowerOnNewTurn { get; set; } = true;
    [Export] public bool ResetAngleOnNewTurn { get; set; } = false;

    [ExportGroup("Aim Sensitivity")]
    [Export] public float AimSensitivity { get; set; } = 0.15f;
    [Export] public float AimWhileChargingSensitivity { get; set; } = 0.0f;
    [Export] public float PowerSensitivity { get; set; } = 0.004f;
    [Export] public float PowerStep { get; set; } = 0.05f;
    [Export] public float TiltStep { get; set; } = 5.0f;
    [Export] public float TiltSensitivity { get; set; } = 0.20f;
    [Export] public bool LockAimWhileCharging { get; set; } = true;

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
    public bool IsHoldingMmb => _isHoldingMmb;

    public Vector3 Direction => GetDirection();

    public event System.Action<ThrowParameters>? ThrowRequested;
    public event System.Action? ResetRequested;
    public event System.Action? FreeLookStarted;
    public event System.Action? FreeLookEnded;
    public event System.Action<Vector2>? FreeLookMotion;
    public event System.Action<ThrowTechnique>? TechniqueChanged;

    private bool _isHoldingLmb;
    private bool _isHoldingRmb;
    private bool _isHoldingMmb;
    private float _mmbDragAccum;
    private float _chargeDirection = 1.0f;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Power = AutoChargePower ? MinChargePower : DefaultPower;

        if (Disc != null)
        {
            Disc.NewTurnStarted += OnNewTurnStarted;
        }
    }

    public override void _ExitTree()
    {
        if (Disc != null)
        {
            Disc.NewTurnStarted -= OnNewTurnStarted;
        }
    }

    public override void _Process(double delta)
    {
        // Smooth hold-to-charge when auto-charge is enabled
        if (AutoChargePower && _isHoldingLmb)
        {
            float chargeSpeed = (MaxChargePower - MinChargePower) / Mathf.Max(0.1f, ChargeDuration);
            Power += _chargeDirection * chargeSpeed * (float)delta;

            if (Power >= MaxChargePower)
            {
                Power = MaxChargePower;
                if (PingPongCharge)
                {
                    _chargeDirection = -1.0f;
                }
            }
            else if (Power <= MinChargePower)
            {
                Power = MinChargePower;
                if (PingPongCharge)
                {
                    _chargeDirection = 1.0f;
                }
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Escape)
            {
                if (_isHoldingLmb)
                {
                    // Cancel current charge without throwing
                    _isHoldingLmb = false;
                    ResetPowerToStanceBaseline();
                    return;
                }

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

            // T or Tab cycles throw technique (Shift+T or Shift+Tab moves backward)
            if (keyEvent.Keycode == Key.T || keyEvent.Keycode == Key.Tab)
            {
                bool backward = keyEvent.ShiftPressed || Input.IsKeyPressed(Key.Shift);
                CycleTechnique(backward);
                GetViewport()?.SetInputAsHandled();
                return;
            }

            // W / S or Up / Down keys allow fine-tuning power independently of mouse aim
            if (keyEvent.Keycode == Key.W || keyEvent.Keycode == Key.Up)
            {
                AdjustPower(PowerStep);
                return;
            }

            if (keyEvent.Keycode == Key.S || keyEvent.Keycode == Key.Down)
            {
                AdjustPower(-PowerStep);
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

            // Shift + Scroll Wheel modifies power cleanly without moving mouse or zooming
            if (mouseButton.Pressed && (mouseButton.ShiftPressed || Input.IsKeyPressed(Key.Shift)))
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    AdjustPower(PowerStep);
                    GetViewport()?.SetInputAsHandled();
                    return;
                }
                if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    AdjustPower(-PowerStep);
                    GetViewport()?.SetInputAsHandled();
                    return;
                }
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

    public void CycleTechnique(bool backward = false)
    {
        if (backward)
        {
            Technique = Technique switch
            {
                ThrowTechnique.RHBH => ThrowTechnique.LHFH,
                ThrowTechnique.LHFH => ThrowTechnique.LHBH,
                ThrowTechnique.LHBH => ThrowTechnique.RHFH,
                ThrowTechnique.RHFH => ThrowTechnique.RHBH,
                _ => ThrowTechnique.RHBH
            };
        }
        else
        {
            Technique = Technique switch
            {
                ThrowTechnique.RHBH => ThrowTechnique.RHFH,
                ThrowTechnique.RHFH => ThrowTechnique.LHBH,
                ThrowTechnique.LHBH => ThrowTechnique.LHFH,
                ThrowTechnique.LHFH => ThrowTechnique.RHBH,
                _ => ThrowTechnique.RHBH
            };
        }

        TechniqueChanged?.Invoke(Technique);
    }

    public void SetTechnique(ThrowTechnique technique)
    {
        Technique = technique;
        TechniqueChanged?.Invoke(Technique);
    }

    public void AdjustPower(float delta)
    {
        Power = Mathf.Clamp(Power + delta, 0.05f, 1.0f);
    }

    public void SetAim(float yaw, float pitch)
    {
        Yaw = yaw;
        Pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        // 1. Right Mouse Button: Free-Look Orbit or Cancel Throw Charge
        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            if (mouseButton.Pressed)
            {
                if (_isHoldingLmb)
                {
                    // RMB click while charging cancels the throw safely
                    _isHoldingLmb = false;
                    ResetPowerToStanceBaseline();
                    return;
                }

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

        // 2. Middle Mouse Button: Hold & Drag for smooth Hyzer/Anhyzer, Single Click for instant Flat (0°) reset
        if (mouseButton.ButtonIndex == MouseButton.Middle)
        {
            if (mouseButton.Pressed)
            {
                if (_isHoldingLmb)
                {
                    _isHoldingLmb = false;
                    ResetPowerToStanceBaseline();
                    return;
                }

                _isHoldingMmb = true;
                _mmbDragAccum = 0.0f;
            }
            else
            {
                if (_isHoldingMmb)
                {
                    // If released with negligible movement, treat as a quick-reset click
                    if (_mmbDragAccum < 4.0f)
                    {
                        ReleaseAngle = 0.0f;
                    }
                    _isHoldingMmb = false;
                }
            }
            return;
        }

        // 3. Left Mouse Button: Power Pull-back / Auto-Charge & Launch
        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (_isHoldingRmb || _isHoldingMmb)
            {
                return;
            }

            if (mouseButton.Pressed)
            {
                if (AutoChargePower)
                {
                    Power = MinChargePower;
                    _chargeDirection = 1.0f;
                }

                _isHoldingLmb = true;
            }
            else if (_isHoldingLmb)
            {
                _isHoldingLmb = false;
                RequestThrow();
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

        // B. If holding MMB: Smooth Hyzer / Anhyzer disc tilt control
        if (_isHoldingMmb)
        {
            _mmbDragAccum += Mathf.Abs(mouseMotion.Relative.X) + Mathf.Abs(mouseMotion.Relative.Y);
            ReleaseAngle = Mathf.Clamp(ReleaseAngle + mouseMotion.Relative.X * TiltSensitivity, -MaxReleaseAngle, MaxReleaseAngle);
            return;
        }

        // C. If holding LMB:
        if (_isHoldingLmb)
        {
            if (!LockAimWhileCharging && AimWhileChargingSensitivity > 0.001f)
            {
                Yaw -= mouseMotion.Relative.X * AimWhileChargingSensitivity;
            }

            // Only manually pull back power when AutoChargePower is disabled
            if (!AutoChargePower)
            {
                Power += mouseMotion.Relative.Y * PowerSensitivity;
                Power = Mathf.Clamp(Power, 0.05f, 1.0f);
            }
            return;
        }

        // D. Default (Idle Stance): Mouse directly rotates Aim direction & Camera (Yaw & Pitch)
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
            ReleaseAngle,
            Technique
        );

        ThrowRequested?.Invoke(parameters);
    }

    private void OnNewTurnStarted()
    {
        if (ResetPowerOnNewTurn)
        {
            ResetPowerToStanceBaseline();
        }

        if (ResetAngleOnNewTurn)
        {
            ReleaseAngle = 0.0f;
        }
    }

    private void ResetPowerToStanceBaseline()
    {
        Power = AutoChargePower ? MinChargePower : DefaultPower;
        _chargeDirection = 1.0f;
    }
}
