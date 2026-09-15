using Godot;

public partial class FlightPhaseUI : CanvasLayer
{
    [Export] public DiscFlightController? Disc { get; set; }
    [Export] public ThrowController? ThrowController { get; set; }

    // Colors for each flight phase
    [Export] public Color LaunchColor { get; set; } = new(0.98f, 0.55f, 0.12f, 0.92f); // Vibrant Orange
    [Export] public Color TurnColor { get; set; } = new(0.18f, 0.78f, 0.72f, 0.92f);   // Neon Aqua
    [Export] public Color GlideColor { get; set; } = new(0.22f, 0.56f, 0.98f, 0.92f);  // Royal Sky Blue
    [Export] public Color FadeColor { get; set; } = new(0.55f, 0.24f, 0.92f, 0.92f);   // Deep Purple
    [Export] public Color GroundColor { get; set; } = new(0.85f, 0.35f, 0.25f, 0.92f); // Earth Terracotta

    private Control _rootContainer = null!;
    private PanelContainer _cardPanel = null!;

    // Flight Ratings Header
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

    // Live Status Footer
    private Label _livePhaseBadge = null!;
    private Label _powerLabel = null!;
    private Label _angleLabel = null!;
    private Label _airtimeLabel = null!;

    private const float TotalBarWidth = 560.0f;
    private const float BarHeight = 34.0f;

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
        // 1. Root container positioned at bottom center
        _rootContainer = new Control
        {
            Name = "HUD_FlightRoot",
            AnchorTop = 1.0f,
            AnchorBottom = 1.0f,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -TotalBarWidth * 0.5f - 20.0f,
            OffsetRight = TotalBarWidth * 0.5f + 20.0f,
            OffsetTop = -145.0f,
            OffsetBottom = -15.0f,
            GrowHorizontal = Control.GrowDirection.Both,
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
            BgColor = new Color(0.06f, 0.08f, 0.12f, 0.82f),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = new Color(0.3f, 0.4f, 0.55f, 0.45f),
            ContentMarginBottom = 10,
            ContentMarginTop = 10,
            ContentMarginLeft = 16,
            ContentMarginRight = 16
        };
        _cardPanel.AddThemeStyleboxOverride("panel", cardStyle);
        _rootContainer.AddChild(_cardPanel);

        var vBox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(TotalBarWidth, 0),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        vBox.AddThemeConstantOverride("separation", 6);
        _cardPanel.AddChild(vBox);

        // 3. Top Row: Disc Ratings & Tendency Summary
        var topRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        topRow.AddThemeConstantOverride("separation", 10);
        vBox.AddChild(topRow);

        _ratingSpeedLabel = CreateBadge("SPD: 9", new Color(0.9f, 0.9f, 0.95f));
        _ratingGlideLabel = CreateBadge("GLD: 5", new Color(0.9f, 0.9f, 0.95f));
        _ratingTurnLabel = CreateBadge("TRN: -1.5", new Color(0.3f, 0.9f, 0.85f));
        _ratingFadeLabel = CreateBadge("FAD: 2.5", new Color(0.85f, 0.45f, 0.95f));

        topRow.AddChild(_ratingSpeedLabel);
        topRow.AddChild(_ratingGlideLabel);
        topRow.AddChild(_ratingTurnLabel);
        topRow.AddChild(_ratingFadeLabel);

        _flightTendencyLabel = new Label
        {
            Text = "S-Curve Line",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _flightTendencyLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.35f));
        _flightTendencyLabel.AddThemeFontSizeOverride("font_size", 13);
        topRow.AddChild(_flightTendencyLabel);

        // 4. Middle Row: The Segmented Flight Phase Tendency Bar
        _barContainer = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(TotalBarWidth, BarHeight),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _barContainer.AddThemeConstantOverride("separation", 3);
        vBox.AddChild(_barContainer);

        _launchSegment = CreatePhaseSegment("LAUNCH", LaunchColor, out _launchLabel);
        _turnSegment = CreatePhaseSegment("TURN", TurnColor, out _turnLabel);
        _glideSegment = CreatePhaseSegment("GLIDE", GlideColor, out _glideLabel);
        _fadeSegment = CreatePhaseSegment("FADE", FadeColor, out _fadeLabel);

        _barContainer.AddChild(_launchSegment);
        _barContainer.AddChild(_turnSegment);
        _barContainer.AddChild(_glideSegment);
        _barContainer.AddChild(_fadeSegment);

        // 5. Bottom Row: Live Phase Badge, Power, Angle, Airtime
        var bottomRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        bottomRow.AddThemeConstantOverride("separation", 14);
        vBox.AddChild(bottomRow);

        _livePhaseBadge = CreateBadge("READY", new Color(0.2f, 0.9f, 0.4f));
        bottomRow.AddChild(_livePhaseBadge);

        _powerLabel = new Label { Text = "Power: 50%" };
        _powerLabel.AddThemeFontSizeOverride("font_size", 12);
        _powerLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.92f));
        bottomRow.AddChild(_powerLabel);

        _angleLabel = new Label { Text = "Tilt: Flat" };
        _angleLabel.AddThemeFontSizeOverride("font_size", 12);
        _angleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.92f));
        bottomRow.AddChild(_angleLabel);

        _airtimeLabel = new Label
        {
            Text = "Est. Air: 3.2s",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _airtimeLabel.AddThemeFontSizeOverride("font_size", 12);
        _airtimeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.82f));
        bottomRow.AddChild(_airtimeLabel);
    }

    private PanelContainer CreatePhaseSegment(string name, Color color, out Label label)
    {
        var segment = new PanelContainer
        {
            Name = $"Segment_{name}",
            CustomMinimumSize = new Vector2(60.0f, BarHeight),
            SizeFlagsVertical = Control.SizeFlags.Fill
        };

        var style = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.35f)
        };
        segment.AddThemeStyleboxOverride("panel", style);

        label = new Label
        {
            Text = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 11);
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
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", color);

        var badgeStyle = new StyleBoxFlat
        {
            BgColor = new Color(color.R * 0.25f, color.G * 0.25f, color.B * 0.25f, 0.75f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = color * 0.7f
        };
        label.AddThemeStyleboxOverride("normal", badgeStyle);
        return label;
    }

    private void UpdateVisuals(float dt)
    {
        // 1. Update Disc Flight Ratings Header
        _ratingSpeedLabel.Text = $" SPD: {Disc!.DiscSpeed:F0} ";
        _ratingGlideLabel.Text = $" GLD: {Disc.DiscGlide:F0} ";
        _ratingTurnLabel.Text = $" TRN: {Disc.DiscTurn:+0.0;-0.0;0.0} ";
        _ratingFadeLabel.Text = $" FAD: {Disc.DiscFade:F1} ";

        // 2. Query Flight Tendency
        var throwParams = new ThrowParameters(
            ThrowController!.Direction,
            ThrowController.Power,
            ThrowController.ReleaseAngle
        );
        var tendency = Disc.EstimateFlightTendency(throwParams);

        // 3. Update Segment Widths
        float launchW = Mathf.Max(24.0f, tendency.LaunchWeight * (TotalBarWidth - 12.0f));
        float turnW = Mathf.Max(tendency.TurnWeight > 0.02f ? 24.0f : 0.0f, tendency.TurnWeight * (TotalBarWidth - 12.0f));
        float glideW = Mathf.Max(24.0f, tendency.GlideWeight * (TotalBarWidth - 12.0f));
        float fadeW = Mathf.Max(24.0f, tendency.FadeWeight * (TotalBarWidth - 12.0f));

        SetSegmentWidth(_launchSegment, launchW);
        SetSegmentWidth(_turnSegment, turnW);
        SetSegmentWidth(_glideSegment, glideW);
        SetSegmentWidth(_fadeSegment, fadeW);

        _turnSegment.Visible = tendency.TurnWeight > 0.02f;

        // 4. Update Highlights for Active Flight Phase
        HighlightSegment(_launchSegment, LaunchColor, Disc.CurrentFlightPhase == DiscFlightController.FlightPhase.Launch);
        HighlightSegment(_turnSegment, TurnColor, Disc.CurrentFlightPhase == DiscFlightController.FlightPhase.Turn);
        HighlightSegment(_glideSegment, GlideColor, Disc.CurrentFlightPhase == DiscFlightController.FlightPhase.Glide);
        HighlightSegment(_fadeSegment, FadeColor, Disc.CurrentFlightPhase == DiscFlightController.FlightPhase.Fade);

        // 5. Update Status Text & Badge
        _livePhaseBadge.Text = $" {Disc.CurrentFlightPhase.ToString().ToUpper()} ";
        _powerLabel.Text = $"Power: {ThrowController.Power * 100.0f:F0}%";

        float releaseAngle = ThrowController.ReleaseAngle;
        if (Mathf.Abs(releaseAngle) < 1.0f)
        {
            _angleLabel.Text = "Tilt: Flat";
        }
        else if (releaseAngle > 0.0f)
        {
            _angleLabel.Text = $"Tilt: {releaseAngle:F0}° Anhyzer";
        }
        else
        {
            _angleLabel.Text = $"Tilt: {-releaseAngle:F0}° Hyzer";
        }

        _airtimeLabel.Text = $"Est. Air: {tendency.EstimatedAirTime:F1}s";

        // 6. Flight Line Description
        if (tendency.TurnWeight > 0.15f && tendency.FadeWeight > 0.20f)
        {
            _flightTendencyLabel.Text = "S-Curve Line (Flex)";
        }
        else if (tendency.TurnWeight > 0.25f)
        {
            _flightTendencyLabel.Text = "Understable Turnover (Right)";
        }
        else if (tendency.FadeWeight > 0.35f)
        {
            _flightTendencyLabel.Text = "Overstable Fade (Hard Left)";
        }
        else
        {
            _flightTendencyLabel.Text = "Straight Laser Line";
        }
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
                style.BgColor = baseColor.Lightened(0.25f);
                style.BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.95f);
                style.BorderWidthBottom = 3;
                style.BorderWidthTop = 3;
                style.BorderWidthLeft = 3;
                style.BorderWidthRight = 3;
            }
            else
            {
                style.BgColor = baseColor;
                style.BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.35f);
                style.BorderWidthBottom = 1;
                style.BorderWidthTop = 1;
                style.BorderWidthLeft = 1;
                style.BorderWidthRight = 1;
            }
        }
    }

    private void OnFlightPhaseChanged(DiscFlightController.FlightPhase phase)
    {
        // Immediate badge update on transition
        if (_livePhaseBadge != null)
        {
            _livePhaseBadge.Text = $" {phase.ToString().ToUpper()} ";
        }
    }
}
