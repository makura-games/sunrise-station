using Content.Server.Chemistry.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Chemistry.EntitySystems
{
    public sealed partial class ChemMasterSystem
    {
        [ValidatePrototypeId<EntityPrototype>]
        private const string PatchPrototypeId = "Patch";

        /// <summary>
        /// Максимальное количество предметов(таблетки, патчи, которые можно заспавнить за 1 нажатие
        /// </summary>
        private const int MaxPerRequest = 50;

        private void OnCreatePatchesMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterCreatePatchesMessage message)
        {
            var user = message.Actor;
            var maybeContainer = _itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.OutputSlotName);
            if (maybeContainer is not { Valid: true } container || !TryComp(container, out StorageComponent? storage))
                return; // output can't fit patches

            // Ensure the number is valid.
            if (message.Number == 0 || !_storageSystem.HasSpace((container, storage)))
                return;

            // Серверная валидация для спасения от модифицированных клиентов.
            // Проблема в том, что валидация выше срабатывает при любом значении, которое не 0 и таблетница не пустая.
            // Даже если в ней остался 1 слот, а мы хотим впихнуть 9999999 таблеток
            if (message.Number > MaxPerRequest)
            {
                _popupSystem.PopupCursor(Loc.GetString("chem-master-window-too-many-pills-text"), user);
                return;
            }

            // Ensure the amount is valid.
            if (message.Dosage == 0 || message.Dosage > chemMaster.Comp.PatchDosageLimit)
                return;

            // Ensure label length is within the character limit.
            if (message.Label.Length > SharedChemMaster.LabelMaxLength)
                return;

            var needed = message.Dosage * message.Number;
            if (!WithdrawFromSource(chemMaster, needed, user, out var withdrawal))
                return;

            _labelSystem.Label(container, message.Label);

            for (var i = 0; i < message.Number; i++)
            {
                var item = Spawn(PatchPrototypeId, Transform(container).Coordinates);
                _storageSystem.Insert(container, item, out _, user: user, storage);
                _labelSystem.Label(item, message.Label);

                _solutionContainerSystem.EnsureSolution(item, SharedChemMaster.PatchSolutionName, out var itemSolution);
                _solutionContainerSystem.SetCapacity(itemSolution, message.Dosage);

                _solutionContainerSystem.TryAddSolution(itemSolution, withdrawal.SplitSolution(message.Dosage));

                // Log patch creation by a user
                _adminLogger.Add(LogType.Action, LogImpact.Low,
                     $"{ToPrettyString(user):user} printed {ToPrettyString(item):patch} {SharedSolutionContainerSystem.ToPrettyString(itemSolution.Comp.Solution)}");
            }

            UpdateUiState(chemMaster);
            ClickSound(chemMaster);
        }
        // Sunrise-Edit end
    }
}
