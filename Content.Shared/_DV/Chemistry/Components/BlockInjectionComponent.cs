using Robust.Shared.GameStates;

namespace Content.Shared._DV.Chemistry.Components;

/// <summary>
/// Prevents injections being used on this entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlockInjectionComponent : Component
{
    // Aurora's Song Start: These fields are no longer used after being broken by the unification of the syringe and hypospray systems.
    /// <summary>
    /// If true, this component will block injections from syringes.
    /// </summary>
    //[DataField]
    //public bool BlockSyringe = true;

    /// <summary>
    /// If true, this component will block injections from hypospray.
    /// </summary>
    //[DataField]
    //public bool BlockHypospray;
    // Aurora's Song End

    /// <summary>
    /// If true, this component will block injections from projectile.
    /// </summary>
    [DataField]
    public bool BlockInjectOnProjectile;
}
