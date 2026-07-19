using Content.Shared._AS.DensityScaling.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._AS.DensityScaling
{
    public sealed partial class DensityScalingSystem : EntitySystem
    {
        [Dependency] private SharedPhysicsSystem _physics = default!;
        [Dependency] private IEntityManager _entityManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DensityScalingComponent, ComponentInit>(OnComponentInit);
        }

        private void OnComponentInit(Entity<DensityScalingComponent> ent, ref ComponentInit args)
        {
            if (!_entityManager.TryGetComponent(ent, out FixturesComponent? manager))
                return;

            foreach (var (id, fixture) in manager.Fixtures)
            {
                if (!fixture.Hard || fixture.Density <= 1f) // Aurora's Song: Pulled from NF SizeAttribute system
                    continue; // This will skip the flammable fixture and any other fixture that is not supposed to contribute to mass

                _physics.SetDensity(ent, id, fixture, fixture.Density * ent.Comp.DensityScale);
            }
        }
    }
}
