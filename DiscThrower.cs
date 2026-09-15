using Godot;

public partial class DiscThrower : Node
{
    [Export] public ThrowController ThrowController { get; set; } = null!;
    [Export] public DiscFlightController Disc { get; set; } = null!;

    public override void _Ready()
    {
        ThrowController.ThrowRequested += OnThrowRequested;
        ThrowController.ResetRequested += OnResetRequested;
    }

    public override void _ExitTree()
    {
        ThrowController.ThrowRequested -= OnThrowRequested;
        ThrowController.ResetRequested -= OnResetRequested;
    }

    private void OnThrowRequested(ThrowParameters parameters)
    {
        Disc.Throw(parameters);
    }

    private void OnResetRequested()
    {
        Disc.ResetPosition();
    }
}
