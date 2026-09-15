using Godot;

public readonly record struct ThrowParameters(
    Vector3 Direction,
    float Power,
    float ReleaseAngle
);