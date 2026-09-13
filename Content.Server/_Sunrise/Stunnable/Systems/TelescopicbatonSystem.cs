using Content.Server.Stunnable.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Events;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared._Sunrise.Stunnable;

namespace Content.Server._Sunrise.Stunnable.Systems
{
    public sealed partial class TelescopicbatonSystem : SharedTelescopicbatonSystem
    {
        [Dependency] private SharedItemSystem _item = default!;
        [Dependency] private ItemToggleSystem _itemToggle = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<TelescopicbatonComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<TelescopicbatonComponent, StaminaDamageOnHitAttemptEvent>(OnStaminaHitAttempt);
            SubscribeLocalEvent<TelescopicbatonComponent, ItemToggledEvent>(ToggleDone);
        }

        private void OnStaminaHitAttempt(Entity<TelescopicbatonComponent> entity, ref StaminaDamageOnHitAttemptEvent args)
        {
            if (!_itemToggle.IsActivated(entity.Owner))
            {
                args.Cancelled = true;
            }
        }

        private void OnExamined(Entity<TelescopicbatonComponent> entity, ref ExaminedEvent args)
        {
            var onMsg = _itemToggle.IsActivated(entity.Owner)
            ? Loc.GetString("comp-telescopicbaton-examined-on")
            : Loc.GetString("comp-telescopicbaton-examined-off");
            args.PushMarkup(onMsg);
        }

        private void ToggleDone(Entity<TelescopicbatonComponent> entity, ref ItemToggledEvent args)
        {
            _item.SetHeldPrefix(entity.Owner, args.Activated ? "on" : "off");
        }
    }
}
