using System.Linq;
using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private static readonly ProtoId<TagPrototype> ShotgunShellTag = "ShellShotgun";

    private void InitializeSpentAmmo()
    {
        SubscribeLocalEvent<SpentAmmoVisualsComponent, AppearanceChangeEvent>(OnSpentAmmoAppearance);
    }

    private void OnSpentAmmoAppearance(Entity<SpentAmmoVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        if (sprite == null) return;

        if (!args.AppearanceData.TryGetValue(AmmoVisuals.Spent, out var varSpent))
        {
            return;
        }

        var spent = (bool)varSpent;

        // Sunrise edit start - restore Starlight modular Spent layer logic dynamically
        var revealSpent = ent.Comp.RevealSpent || TagSystem.HasTag(ent, ShotgunShellTag);

        if (revealSpent)
        {
            if (spent)
            {
                // We are spent!
                bool hasCustomSpentLayer = false;
                string spentOverlayState = "spent";

                // First, check all custom layers (index > 1) to see if they have a spent state
                for (int i = 2; i < sprite.AllLayers.Count(); i++)
                {
                    if (sprite[i] is SpriteComponent.Layer layer)
                    {
                        var stateStr = layer.State.ToString();
                        if (!string.IsNullOrEmpty(stateStr) && !stateStr.EndsWith("-spent"))
                        {
                            var spentStateName = $"{stateStr}-spent";
                            if (sprite.BaseRSI?.TryGetState(spentStateName, out _) ?? false)
                            {
                                _sprite.LayerSetRsiState((ent, sprite), i, spentStateName);
                                hasCustomSpentLayer = true;
                            }
                        }
                    }
                }

                // If Tip layer exists, determine if it is long or short
                if (_sprite.LayerExists((ent, sprite), AmmoVisualLayers.Tip))
                {
                    var tipIndex = _sprite.LayerMapGet((ent, sprite), AmmoVisualLayers.Tip);
                    if (sprite[tipIndex] is SpriteComponent.Layer tipLayer)
                    {
                        if (tipLayer.State.ToString() == "tip-long")
                        {
                            spentOverlayState = "spent-long";
                        }
                    }

                    // If we have a custom spent layer, we hide the unspent Tip layer (bullet tip)
                    if (hasCustomSpentLayer)
                    {
                        _sprite.LayerSetVisible((ent, sprite), AmmoVisualLayers.Tip, false);
                    }
                    else
                    {
                        _sprite.LayerSetVisible((ent, sprite), AmmoVisualLayers.Tip, true);
                    }
                }

                // Check and set Base layer to spent if a spent version exists
                if (sprite[0] is SpriteComponent.Layer baseLayer)
                {
                    var baseStateStr = baseLayer.State.ToString();
                    if (!string.IsNullOrEmpty(baseStateStr) && !baseStateStr.EndsWith("-spent"))
                    {
                        var spentBaseState = $"{baseStateStr}-spent";
                        if (sprite.BaseRSI?.TryGetState(spentBaseState, out _) ?? false)
                        {
                            _sprite.LayerSetRsiState((ent, sprite), 0, spentBaseState);
                        }
                    }
                }

                // If we do not have a custom spent layer, we must add/reveal the spent overlay layer
                if (!hasCustomSpentLayer)
                {
                    var spentState = ent.Comp.SpentState ?? spentOverlayState;
                    if (!_sprite.LayerExists((ent, sprite), AmmoVisualLayers.Spent))
                    {
                        var rsiPath = sprite.BaseRSI?.Path ?? sprite.LayerGetActualRSI((int)AmmoVisualLayers.Base)?.Path;
                        if (rsiPath != null)
                        {
                            var spentSpecifier = new SpriteSpecifier.Rsi(rsiPath.Value, spentState);
                            var index = _sprite.AddLayer((ent, sprite), spentSpecifier);
                            _sprite.LayerMapSet((ent, sprite), AmmoVisualLayers.Spent, index);
                        }
                    }
                    else
                    {
                        _sprite.LayerSetRsiState((ent, sprite), AmmoVisualLayers.Spent, spentState);
                        _sprite.LayerSetVisible((ent, sprite), AmmoVisualLayers.Spent, true);
                    }
                }
                else
                {
                    // If we had a dynamically added Spent layer before, hide it
                    if (_sprite.LayerExists((ent, sprite), AmmoVisualLayers.Spent))
                    {
                        _sprite.LayerSetVisible((ent, sprite), AmmoVisualLayers.Spent, false);
                    }
                }
            }
            else
            {
                // We are unspent (restoring original state)
                // First, restore custom layers (index > 1) to their unspent states
                for (int i = 2; i < sprite.AllLayers.Count(); i++)
                {
                    if (sprite[i] is SpriteComponent.Layer layer)
                    {
                        var stateStr = layer.State.ToString();
                        if (!string.IsNullOrEmpty(stateStr) && stateStr.EndsWith("-spent"))
                        {
                            var unspentStateName = stateStr.Substring(0, stateStr.Length - "-spent".Length);
                            if (sprite.BaseRSI?.TryGetState(unspentStateName, out _) ?? false)
                            {
                                _sprite.LayerSetRsiState((ent, sprite), i, unspentStateName);
                            }
                        }
                    }
                }

                // Restore Base layer
                if (sprite[0] is SpriteComponent.Layer baseLayer)
                {
                    var baseStateStr = baseLayer.State.ToString();
                    if (!string.IsNullOrEmpty(baseStateStr) && baseStateStr.EndsWith("-spent"))
                    {
                        var unspentBaseState = baseStateStr.Substring(0, baseStateStr.Length - "-spent".Length);
                        if (sprite.BaseRSI?.TryGetState(unspentBaseState, out _) ?? false)
                        {
                            _sprite.LayerSetRsiState((ent, sprite), 0, unspentBaseState);
                        }
                    }
                }

                // Hide Spent overlay layer
                if (_sprite.LayerExists((ent, sprite), AmmoVisualLayers.Spent))
                {
                    _sprite.LayerSetVisible((ent, sprite), AmmoVisualLayers.Spent, false);
                }

                // Show Tip layer
                if (_sprite.LayerExists((ent, sprite), AmmoVisualLayers.Tip))
                {
                    _sprite.LayerSetVisible((ent, sprite), AmmoVisualLayers.Tip, true);
                }
            }
            return;
        }
        // Sunrise edit end

        string state;

        if (spent)
        {
            state = ent.Comp.SpentState ?? (ent.Comp.Suffix ? $"{ent.Comp.State}-spent" : "spent");
        }
        else
            state = ent.Comp.State;

        _sprite.LayerSetRsiState((ent, sprite), AmmoVisualLayers.Base, state);
        _sprite.RemoveLayer((ent, sprite), AmmoVisualLayers.Tip, false);
    }
}
