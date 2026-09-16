using Godot;

public partial class FlightPhaseUI : CanvasLayer
{
    [Export] public DiscFlightController? Disc { get; set; }
    [Export] public ThrowController? ThrowController { get; set; }
    [Export] public CameraController? CameraController { get; set; }

    [ExportGroup("Phase Colors")]
    [Export] public Color LaunchColor { get; set; } = new(0.98f, 0.55f, 0.12f, 0.90f); // Vibrant Orange
    [Export] public Color TurnColor { get; set; } = new(0.18f, 0.78f, 0.72f, 0.90f);   // Neon Aqua
    [Export] public Color GlideColor { get; set; } = new(0.22f, 0.56f, 0.98f, 0.90f);  // Royal Sky Blue
    [Export] public Color FadeColor { get; set; } = new(0.55f, 0.24f, 0.92f, 0.90f);   // Deep Purple

    private Control _rootContainer = null!;
    private PanelContainer _cardPanel = null!;

    // 4 Compact Style Pills
    private Button _cardRHBH = null!;
    private Button _cardRHFH = null!;
    private Button _cardLHBH = null!;
    private Button _cardLHFH = null!;

    // Header Badges
    private Label _ratingSpeedLabel = null!;
    private Label _ratingGlideLabel = null!;
    private Label _ratingTurnLabel = null!;
    private Label _ratingFadeLabel = null!;
    private Label _flightTendencyLabel = null!;

    // Phase Segment Boxes
    private Container _barContainer = null!;
    private PanelContainer _launchSegment = null!;
    private PanelContainer _turnSegment = null!;
    private PanelContainer _glideSegment = null!;
    private PanelContainer _fadeSegment = null!;

    private Label _launchLabel = null!;
    private Label _turnLabel = null!;
    private Label _glideLabel = null!;
    private Label _fadeLabel = null!;

    // Status Footer
    private Label _livePhaseBadge = null!;
    private Label _powerLabel = null!;
    private Label _pitchLabel = null!;
    private Label _angleLabel = null!;
    private Label _camModeBadge = null!;
    private Label _airtimeLabel = null!;

    private const float TotalCardWidth = 360.0f;
    private const float BarHeight = 16.0f;

    public override void _Ready()
    {
        BuildUI();

        if (Disc != null)
        {
            Disc.FlightPhaseChanged += OnFlightPhaseChanged;
        }
    }

    public override void _ExitTree()
    {
        if (Disc != null)
        {
            Disc.FlightPhaseChanged -= OnFlightPhaseChanged;
        }
    }

    public override void _Process(double delta)
    {
        if (Disc == null || ThrowController == null)
        {
            return;
        }

        UpdateVisuals((float)delta);
    }

    private void BuildUI()
    {
        // 1. Position in bottom-right corner
        _rootContainer = new Control
        {
            Name = "HUD_FlightRoot",
            AnchorLeft = 1.0f,
            AnchorRight = 1.0f,
            AnchorTop = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = -TotalCardWidth - 28.0f,
            OffsetRight = -16.0f,
            OffsetTop = -135.0f,
            OffsetBottom = -16.0f,
            GrowHorizontal = Control.GrowDirection.Begin,
            GrowVertical = Control.GrowDirection.Begin
        };
        AddChild(_rootContainer);

        // 2. Translucent Glass Card Background
        _cardPanel = new PanelContainer
        {
            Name = "CardPanel",
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f
        };

        var cardStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.09f, 0.88f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = new Color(0.35f, 0.45f, 0.60f, 0.35f),
            ContentMarginBottom = 6,
            ContentMarginTop = 6,
            ContentMarginLeft = 8,
            ContentMarginRight = 8
        };
        _cardPanel.AddThemeStyleboxOverride("panel", cardStyle);
        _rootContainer.AddChild(_cardPanel);

        var vBox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(TotalCardWidth, 0),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        vBox.AddThemeConstantOverride("separation", 5);
        _cardPanel.AddChild(vBox);

        // 3. Row 1: 4 Clean Compact Style Pills
        var styleRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        styleRow.AddThemeConstantOverride("separation", 4);
        vBox.AddChild(styleRow);

        _cardRHBH = CreateStylePill("RHBH ↻", ThrowTechnique.RHBH);
        _cardRHFH = CreateStylePill("RHFH ↺", ThrowTechnique.RHFH);
        _cardLHBH = CreateStylePill("LHBH ↺", ThrowTechnique.LHBH);
        _cardLHFH = CreateStylePill("LHFH ↻", ThrowTechnique.LHFH);

        styleRow.AddChild(_cardRHBH);
        styleRow.AddChild(_cardRHFH);
        styleRow.AddChild(_cardLHBH);
        styleRow.AddChild(_cardLHFH);

        // 4. Row 2: Disc Flight Ratings & Flight Tendency
        var statsRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        statsRow.AddThemeConstantOverride("separation", 4);
        vBox.AddChild(statsRow);

        _ratingSpeedLabel = CreateBadge("9", new Color(0.95f, 0.95f, 0.95f));
        _ratingGlideLabel = CreateBadge("5", new Color(0.95f, 0.95f, 0.95f));
        _ratingTurnLabel = CreateBadge("-1.5", new Color(0.3f, 0.9f, 0.85f));
        _ratingFadeLabel = CreateBadge("2.5", new Color(0.85f, 0.45f, 0.95f));

        statsRow.AddChild(_ratingSpeedLabel);
        statsRow.AddChild(_ratingGlideLabel);
        statsRow.AddChild(_ratingTurnLabel);
        statsRow.AddChild(_ratingFadeLabel);

        _flightTendencyLabel = new Label
        {
            Text = "Fade Left ⬅",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _flightTendencyLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.86f, 0.35f));
        _flightTendencyLabel.AddThemeFontSizeOverride("font_size", 10);
        statsRow.AddChild(_flightTendencyLabel);

        // 5. Row 3: Flight Phase Bar
        _barContainer = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(TotalCardWidth, BarHeight),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _barContainer.AddThemeConstantOverride("separation", 2);
        vBox.AddChild(_barContainer);

        _launchSegment = CreatePhaseSegment("LAUNCH", LaunchColor, out _launchLabel);
        _turnSegment = CreatePhaseSegment("TURN", TurnColor, out _turnLabel);
        _glideSegment = CreatePhaseSegment("GLIDE", GlideColor, out _glideLabel);
        _fadeSegment = CreatePhaseSegment("FADE", FadeColor, out _fadeLabel);

        _barContainer.AddChild(_launchSegment);
        _barContainer.AddChild(_turnSegment);
        _barContainer.AddChild(_glideSegment);
        _barContainer.AddChild(_fadeSegment);

        // 6. Row 4: Status Footer
        var bottomRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        bottomRow.AddThemeConstantOverride("separation", 5);
        vBox.AddChild(bottomRow);

        _livePhaseBadge = CreateBadge("READY", new Color(0.2f, 0.9f, 0.4f));
        bottomRow.AddChild(_livePhaseBadge);

        _powerLabel = new Label { Text = "50%" };
        _powerLabel.AddThemeFontSizeOverride("font_size", 9);
        _powerLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.92f));
        bottomRow.AddChild(_powerLabel);

        _pitchLabel = new Label { Text = "0° Level" };
        _pitchLabel.AddThemeFontSizeOverride("font_size", 9);
        _pitchLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.45f));
        bottomRow.AddChild(_pitchLabel);

        _angleLabel = new Label { Text = "Flat" };
        _angleLabel.AddThemeFontSizeOverride("font_size", 9);
        _angleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.92f));
        bottomRow.AddChild(_angleLabel);

        _camModeBadge = CreateBadge("[F] Snap", new Color(0.45f, 0.6f, 0.75f));
        bottomRow.AddChild(_camModeBadge);

        _airtimeLabel = new Label
        {
            Text = "3.2s",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _airtimeLabel.AddThemeFontSizeOverride("font_size", 9);
        _airtimeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.82f));
        bottomRow.AddChild(_airtimeLabel);
    }

    private Button CreateStylePill(string labelText, ThrowTechnique technique)
    {
        var button = new Button
        {
            Text = labelText,
            CustomMinimumSize = new Vector2((TotalCardWidth - 12.0f) * 0.25f, 22.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };
        button.AddThemeFontSizeOverride("font_size", 9);

        button.Pressed += () =>
        {
            ThrowController?.SetTechnique(technique);
        };

        return button;
    }

    private PanelContainer CreatePhaseSegment(string name, Color color, out Label label)
    {
        var segment = new PanelContainer
        {
            Name = $"Segment_{name}",
            CustomMinimumSize = new Vector2(40.0f, BarHeight),
            SizeFlagsVertical = Control.SizeFlags.Fill
        };

        var style = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.30f)
        };
        segment.AddThemeStyleboxOverride("panel", style);

        label = new Label
        {
            Text = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 8);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 0.95f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.8f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);

        segment.AddChild(label);
        return segment;
    }

    private Label CreateBadge(string text, Color color)
    {
        var label = new Label
        {
            Text = $" {text} "
        };
        label.AddThemeFontSizeOverride("font_size", 8);
        label.AddThemeColorOverride("font_color", color);

        var badgeStyle = new StyleBoxFlat
        {
            BgColor = new Color(color.R * 0.25f, color.G * 0.25f, color.B * 0.25f, 0.75f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = color * 0.6f
        };
        label.AddThemeStyleboxOverride("normal", badgeStyle);
        return label;
    }

    private void UpdateVisuals(float dt)
    {
        // 1. Update 4 Style Pills
        ThrowTechnique technique = (Disc!.IsFlying && Disc.CurrentThrow.Direction != Vector3.Zero)
            ? Disc.CurrentTechnique
            : ThrowController!.Technique;
        float spinSign = DiscFlightController.GetSpinSign(technique);

        UpdatePillStyle(_cardRHBH, technique == ThrowTechnique.RHBH, new Color(0.20f, 0.85f, 1.0f));
        UpdatePillStyle(_cardRHFH, technique == ThrowTechnique.RHFH, new Color(1.0f, 0.40f, 0.85f));
        UpdatePillStyle(_cardLHBH, technique == ThrowTechnique.LHBH, new Color(1.0f, 0.40f, 0.85f));
        UpdatePillStyle(_cardLHFH, technique == ThrowTechnique.LHFH, new Color(0.20f, 0.85f, 1.0f));

        // 2. Update Flight Numbers Header
        _ratingSpeedLabel.Text = $" {Disc.DiscSpeed:F0} ";
        _ratingGlideLabel.Text = $" {Disc.DiscGlide:F0} ";
        _ratingTurnLabel.Text = $" {Disc.DiscTurn:+0.0;-0.0;0.0} ";
        _ratingFadeLabel.Text = $" {Disc.DiscFade:F1} ";

        // 3. Query Flight Tendency
        var throwParams = (Disc.IsFlying && Disc.CurrentThrow.Direction != Vector3.Zero)
            ? Disc.CurrentThrow
            : new ThrowParameters(
                ThrowController!.Direction,
                ThrowController.Power,
                ThrowController.ReleaseAngle,
                technique
            );
        var tendency = Disc.EstimateFlightTendency(throwParams);

        // 4. Update Segment Widths
        float launchW = Mathf.Max(18.0f, tendency.LaunchWeight * (TotalCardWidth - 8.0f));
        float turnW = Mathf.Max(tendency.TurnWeight > 0.02f ? 18.0f : 0.0f, tendency.TurnWeight * (TotalCardWidth - 8.0f));
        float glideW = Mathf.Max(18.0f, tendency.GlideWeight * (TotalCardWidth - 8.0f));
        float fadeW = Mathf.Max(18.0f, tendency.FadeWeight * (TotalCardWidth - 8.0f));

        SetSegmentWidth(_launchSegment, launchW);
        SetSegmentWidth(_turnSegment, turnW);
        SetSegmentWidth(_glideSegment, glideW);
        SetSegmentWidth(_fadeSegment, fadeW);

        _turnSegment.Visible = tendency.TurnWeight > 0.02f;

        // 5. Update Highlights for Active Flight Phase
        HighlightSegment(_launchSegment, LaunchColor, Disc.CurrentFlightPhase == DiscFlightController.FlightPhase.Launch);
        HighlightSegment(_turnSegment, TurnColor, Disc.CurrentFlightPhase == DiscFlightController.FlightPhase.Turn);
        HighlightSegment(_glideSegment, GlideColor, Disc.CurrentFlightPhase == DiscFlightController.FlightPhase.Glide);
        HighlightSegment(_fadeSegment, FadeColor, Disc.CurrentFlightPhase == DiscFlightController.FlightPhase.Fade);

        // 6. Update Status Text & Badge
        _livePhaseBadge.Text = $" {Disc.CurrentFlightPhase.ToString().ToUpper()} ";
        _powerLabel.Text = $"Pwr: {throwParams.Power * 100.0f:F0}%";

        // Pitch Readout
        float pitch = (Disc.IsFlying && Disc.CurrentThrow.Direction != Vector3.Zero)
            ? Mathf.RadToDeg(Mathf.Atan2(throwParams.Direction.Y, new Vector2(throwParams.Direction.X, throwParams.Direction.Z).Length()))
            : ThrowController!.Pitch;

        if (Mathf.Abs(pitch) < 0.5f)
        {
            _pitchLabel.Text = "0° Level";
            _pitchLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.95f, 0.6f));
        }
        else if (pitch > 0.0f)
        {
            _pitchLabel.Text = $"+{pitch:F0}° Up";
            _pitchLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.35f));
        }
        else
        {
            _pitchLabel.Text = $"{pitch:F0}° Down";
            _pitchLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1.0f));
        }

        // Release Roll Angle Readout
        float releaseAngle = throwParams.ReleaseAngle;
        if (Mathf.Abs(releaseAngle) < 1.0f)
        {
            _angleLabel.Text = "Flat";
        }
        else if (releaseAngle > 0.0f)
        {
            _angleLabel.Text = spinSign > 0 ? $"+{releaseAngle:F0}° Anh" : $"+{releaseAngle:F0}° Hyz";
        }
        else
        {
            _angleLabel.Text = spinSign > 0 ? $"{releaseAngle:F0}° Hyz" : $"{releaseAngle:F0}° Anh";
        }

        // Camera Free-Look Reset Mode Badge
        if (CameraController != null)
        {
            if (CameraController.ResetCameraOnFreeLookEnd)
            {
                _camModeBadge.Text = " [F] Snap ";
                _camModeBadge.AddThemeColorOverride("font_color", new Color(0.65f, 0.72f, 0.82f));
            }
            else
            {
                _camModeBadge.Text = " [F] Keep ";
                _camModeBadge.AddThemeColorOverride("font_color", new Color(0.2f, 0.95f, 0.6f));
            }
        }

        _airtimeLabel.Text = $"~{tendency.EstimatedAirTime:F1}s";

        // 7. Flight Line Tag
        if (tendency.TurnWeight > 0.15f && tendency.FadeWeight > 0.20f)
        {
            _flightTendencyLabel.Text = "S-Curve (Flex)";
        }
        else if (tendency.TurnWeight > 0.25f)
        {
            _flightTendencyLabel.Text = spinSign > 0 ? "Turn Right ➔" : "Turn Left ⬅";
        }
        else if (tendency.FadeWeight > 0.35f)
        {
            _flightTendencyLabel.Text = spinSign > 0 ? "Fade Left ⬅" : "Fade Right ➔";
        }
        else
        {
            _flightTendencyLabel.Text = "Straight Laser";
        }
    }

    private void UpdatePillStyle(Button button, bool isSelected, Color activeColor)
    {
        var style = new StyleBoxFlat
        {
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginBottom = 2,
            ContentMarginTop = 2,
            ContentMarginLeft = 2,
            ContentMarginRight = 2
        };

        if (isSelected)
        {
            style.BgColor = new Color(activeColor.R * 0.35f, activeColor.G * 0.35f, activeColor.B * 0.35f, 0.90f);
            style.BorderColor = activeColor;
            style.BorderWidthBottom = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            button.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f));
        }
        else
        {
            style.BgColor = new Color(0.10f, 0.12f, 0.16f, 0.40f);
            style.BorderColor = new Color(0.35f, 0.40f, 0.50f, 0.25f);
            style.BorderWidthBottom = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            button.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.68f));
        }

        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
    }

    private void SetSegmentWidth(PanelContainer segment, float width)
    {
        segment.CustomMinimumSize = new Vector2(width, BarHeight);
    }

    private void HighlightSegment(PanelContainer segment, Color baseColor, bool isActive)
    {
        if (segment.GetThemeStylebox("panel") is StyleBoxFlat style)
        {
            if (isActive)
            {
                style.BgColor = baseColor.Lightened(0.28f);
                style.BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.95f);
                style.BorderWidthBottom = 2;
                style.BorderWidthTop = 2;
                style.BorderWidthLeft = 2;
                style.BorderWidthRight = 2;
            }
            else
            {
                style.BgColor = baseColor;
                style.BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.30f);
                style.BorderWidthBottom = 1;
                style.BorderWidthTop = 1;
                style.BorderWidthLeft = 1;
                style.BorderWidthRight = 1;
            }
        }
    }

    private void OnFlightPhaseChanged(DiscFlightController.FlightPhase phase)
    {
        if (_livePhaseBadge != null)
        {
            _livePhaseBadge.Text = $" {phase.ToString().ToUpper()} ";
        }
    }
}
