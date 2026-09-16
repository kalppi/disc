using Godot;

public enum ThrowTechnique
{
    RHBH, // Right-Hand Backhand  (Clockwise spin, Turn Right, Fade Left)
    RHFH, // Right-Hand Forehand  (Counter-Clockwise spin, Turn Left, Fade Right)
    LHBH, // Left-Hand Backhand   (Counter-Clockwise spin, Turn Left, Fade Right)
    LHFH  // Left-Hand Forehand   (Clockwise spin, Turn Right, Fade Left)
}

public readonly record struct ThrowParameters(
    Vector3 Direction,
    float Power,
    float ReleaseAngle,
    ThrowTechnique Technique = ThrowTechnique.RHBH
);
