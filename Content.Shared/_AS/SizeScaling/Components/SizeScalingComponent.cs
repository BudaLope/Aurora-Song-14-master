using Content.Shared._NF.Cloning;

namespace Content.Shared._AS.SizeScaling.Components;

[RegisterComponent]
public sealed partial class SizeScalingComponent : Component, ITransferredByCloning
{
    [DataField]
    public float DensityScale = 1.0f;
}
