using Content.Shared._NF.Cloning;

namespace Content.Shared._AS.DensityScaling.Components;

[RegisterComponent]
public sealed partial class DensityScalingComponent : Component, ITransferredByCloning
{
    [DataField]
    public float DensityScale = 1.0f;
}
