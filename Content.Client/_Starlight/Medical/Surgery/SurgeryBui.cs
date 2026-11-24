using System.Collections.Generic;
using Content.Client._Starlight;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Hands.Systems;
using Content.Server.Administration.Systems;
using Content.Shared.Body;
using Content.Shared.Starlight.Medical.Surgery;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Control;

namespace Content.Client._Starlight.Medical.Surgery;
// Based on the RMC14 build.
// https://github.com/RMC-14/RMC-14

[UsedImplicitly]
public sealed partial class SurgeryBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _game = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    private readonly StarlightEntitySystem _entitySystem;
    private readonly SurgerySystem _system;
    private readonly HandsSystem _hands;

    [ViewVariables]
    private SurgeryWindow? _window;

    private EntityUid? _part;
    private (EntityUid Ent, EntProtoId Proto)? _surgery;
    private readonly List<EntProtoId> _previousSurgeries = new();

    public SurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = _entities.System<SurgerySystem>();
        _hands = _entities.System<HandsSystem>();
        _entitySystem = _entities.System<StarlightEntitySystem>();

        _hands.OnPlayerItemAdded += OnPlayerItemAdded;
    }
    private void OnPlayerItemAdded(string k1, EntityUid k2)
    {
        if (!_game.IsFirstTimePredicted) return;
        RefreshUI();
    }
    protected override void Open()
    {
        base.Open();
        UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (state is SurgeryBuiState s)
            Update(s);
    }

    public override void Update()
    {
        RefreshUI();
    }

    private void Update(SurgeryBuiState state)
    {
        TryInitWindow();

        _window!.Surgeries.DisposeAllChildren();
        _window.Steps.DisposeAllChildren();
        _window.Parts.DisposeAllChildren();

        View(ViewType.Parts);

        var oldSurgery = _surgery;
        var oldPart = _part;
        _part = null;
        _surgery = null;

        var parts = new List<Entity<OrganComponent>>(state.Choices.Keys.Count);
        foreach (var choice in state.Choices.Keys)
        {
            if (_entities.TryGetEntity(choice, out var ent) &&
                _entities.TryGetComponent(ent, out OrganComponent? part) &&
                SharedSurgerySystem.IsSurgeryTarget(part))
            {
                parts.Add((ent.Value, part));
            }
        }

        parts.Sort((a, b) =>
        {
            return SharedSurgerySystem.GetSurgeryTargetScore(a.Comp) - SharedSurgerySystem.GetSurgeryTargetScore(b.Comp);
        });

        foreach (var part in parts)
        {
            var netPart = _entities.GetNetEntity(part.Owner);
            var surgeries = state.Choices[netPart];
            var partName = GetPartName(part);
            var partButton = new ChoiceControl();

            partButton.Set(partName, null);
            partButton.Button.OnPressed += _ => OnPartPressed(netPart, surgeries);

            _window.Parts.AddChild(partButton);

            foreach (var (surgeryId, suffix, isCompleted) in surgeries)
            {
                if (!_entitySystem.TryGetSingleton(surgeryId, out var surgery) ||
                    !_entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
                {
                    continue;
                }

                if (oldPart == part && oldSurgery?.Proto == surgeryId)
                    OnSurgeryPressed((surgery, surgeryComp), netPart, surgeryId);
            }

            if (oldPart == part && oldSurgery == null)
                OnPartPressed(netPart, surgeries);
        }

        RefreshUI();

        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private void TryInitWindow()
    {
        if (_window != null) return;
        _window = new SurgeryWindow();
        _window.OnClose += Close;
        _window.Title = _loc.GetString("surgery-window-name");
        _window.PartsButton.Text = _loc.GetString("surgery-window-partsbutton-name");
        _window.SurgeriesButton.Text = _loc.GetString("surgery-window-surgeriesbutton-name");
        _window.StepsButton.Text = _loc.GetString("surgery-window-stepsbutton-name");

        _window.PartsButton.OnPressed += _ =>
        {
            _part = null;
            _surgery = null;
            _previousSurgeries.Clear();
            View(ViewType.Parts);
        };

        _window.SurgeriesButton.OnPressed += _ =>
        {
            _surgery = null;
            _previousSurgeries.Clear();

            if (!_entities.TryGetNetEntity(_part, out var netPart) ||
                State is not SurgeryBuiState s ||
                !s.Choices.TryGetValue(netPart.Value, out var surgeries))
            {
                return;
            }

            OnPartPressed(netPart.Value, surgeries);
        };

        _window.StepsButton.OnPressed += _ =>
        {
            if (!_entities.TryGetNetEntity(_part, out var netPart) ||
                _previousSurgeries.Count == 0)
            {
                return;
            }

            var last = _previousSurgeries[^1];
            _previousSurgeries.RemoveAt(_previousSurgeries.Count - 1);

            if (!_entitySystem.TryGetSingleton(last, out var previousId) ||
                !_entities.TryGetComponent(previousId, out SurgeryComponent? previous))
            {
                return;
            }

            OnSurgeryPressed((previousId, previous), netPart.Value, last);
        };
    }

    private void AddStep(EntProtoId stepId, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null ||
            !_entitySystem.TryGetSingleton(stepId, out var step))
        {
            return;
        }

        var stepName = new FormattedMessage();
        stepName.AddText(_entities.GetComponent<MetaDataComponent>(step).EntityName);

        var stepButton = new SurgeryStepButton { Step = step };
        stepButton.Button.OnPressed += _ => SendMessage(new SurgeryStepChosenBuiMsg()
        {
            Step = stepId,
            Part = netPart,
            Surgery = surgeryId,
        });

        _window.Steps.AddChild(stepButton);
    }

    private void OnSurgeryPressed(Entity<SurgeryComponent> surgery, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        _surgery = (surgery, surgeryId);

        _window.Steps.DisposeAllChildren();

        if (surgery.Comp.Requirement is { } requirementIds)
        {
            foreach (var requirementId in requirementIds)
            {
                if (_entitySystem.TryGetSingleton(requirementId, out var requirement)
                    && _entities.TryGetComponent(_part, out OrganComponent? partComp)
                    && partComp.Body is { } body
                    && _part is { } part
                    && _system.IsSurgeryValid(body, part, requirementId, surgeryId, out _, out _, out _))
                {
                    var label = new ChoiceControl();
                    label.Button.OnPressed += _ =>
                    {
                        _previousSurgeries.Add(surgeryId);

                        if (_entities.TryGetComponent(requirement, out SurgeryComponent? requirementComp))
                            OnSurgeryPressed((requirement, requirementComp), netPart, requirementId);
                    };

                    var msg = new FormattedMessage();
                    var surgeryName = _entities.GetComponent<MetaDataComponent>(requirement).EntityName;
                    msg.AddMarkupOrThrow(_loc.GetString("surgery-window-reguires", ("surgeryname", surgeryName)));
                    label.Set(msg, null);

                    _window.Steps.AddChild(label);
                    _window.Steps.AddChild(new HSeparator(Color.FromHex("#4972A1")) { Margin = new Thickness(0, 0, 0, 1) });
                }
            }
        }

        foreach (var stepId in surgery.Comp.Steps)
        {
            AddStep(stepId, netPart, surgeryId);
        }

        View(ViewType.Steps);
        RefreshUI();
    }

    private void OnPartPressed(NetEntity netPart, List<(EntProtoId, string, bool)> surgeryIds)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);

        _window.Surgeries.DisposeAllChildren();

        var surgeries = new List<(Entity<SurgeryComponent> Ent, EntProtoId Id, string Name, bool IsCompleted, Texture?)>();
        foreach (var (surgeryId, suffix, isCompleted) in surgeryIds)
        {
            if (!_entitySystem.TryGetSingleton(surgeryId, out var surgery)||
                !_entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
            {
                continue;
            }

            var texture = _entities.GetComponentOrNull<SpriteComponent>(surgery)?.Icon?.Default;
            var name = $"{_entities.GetComponent<MetaDataComponent>(surgery).EntityName} {suffix}";
            surgeries.Add(((surgery, surgeryComp), surgeryId, name, isCompleted, texture));
        }

        surgeries.Sort((a, b) =>
        {
            var priority = a.Ent.Comp.Priority.CompareTo(b.Ent.Comp.Priority);
            if (priority != 0)
                return priority;

            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        foreach (var (Ent, Id, Name, IsCompleted, texture) in surgeries)
        {
            var surgeryButton = new ChoiceControl();

            surgeryButton.Set(Name, texture);
            if (IsCompleted)
                surgeryButton.Button.Modulate = Color.Green;
            surgeryButton.Button.OnPressed += _ => OnSurgeryPressed(Ent, netPart, Id);
            _window.Surgeries.AddChild(surgeryButton);
        }

        RefreshUI();
        View(ViewType.Surgeries);
    }

    private void RefreshUI()
    {
        if (_window == null ||
            !_entities.HasComponent<SurgeryComponent>(_surgery?.Ent) ||
            !_entities.TryGetComponent(_part, out OrganComponent? part))
        {
            return;
        }

        var next = _system.GetNextStep(Owner, _part.Value, _surgery.Value.Ent);
        var i = 0;
        foreach (var child in _window.Steps.Children)
        {
            if (child is not SurgeryStepButton stepButton)
                continue;

            var status = StepStatus.Incomplete;
            if (next == null)
            {
                status = StepStatus.Complete;
            }
            else if (next.Value.Surgery.Owner != _surgery.Value.Ent)
            {
                status = StepStatus.Incomplete;
            }
            else if (next.Value.Step == i)
            {
                status = StepStatus.Next;
            }
            else if (i < next.Value.Step)
            {
                status = StepStatus.Complete;
            }

            stepButton.Button.Disabled = status != StepStatus.Next;

            var stepName = new FormattedMessage();
            stepName.AddText(_entities.GetComponent<MetaDataComponent>(stepButton.Step).EntityName);

            if (status == StepStatus.Complete)
            {
                stepButton.Button.Modulate = Color.Green;
            }
            else if (status == StepStatus.Next)
            {
                stepButton.Button.Modulate = Color.White;
                if (_player.LocalEntity is { } player &&
                    !_system.CanPerformStep(player, Owner, _part.Value, stepButton.Step, false, out var popup, out var reason, out _))
                {
                    stepButton.ToolTip = popup;
                    stepButton.Button.Disabled = true;

                    switch (reason)
                    {
                        case StepInvalidReason.NeedsOperatingTable:
                            stepName.AddMarkupOrThrow(" " + _loc.GetString("surgery-window-reguires-table"));
                            break;
                        case StepInvalidReason.Armor:
                            stepName.AddMarkupOrThrow(" " + _loc.GetString("surgery-window-reguires-undress"));
                            break;
                        case StepInvalidReason.MissingTool:
                            stepName.AddMarkupOrThrow(" " + _loc.GetString("surgery-window-reguires-tool"));
                            break;
                        case StepInvalidReason.DisabledTool:
                            stepName.AddMarkupOrThrow(" " + _loc.GetString("surgery-window-reguires-enable"));
                            break;
                        case StepInvalidReason.TooHigh:
                            stepName.AddMarkupOrThrow(" " + _loc.GetString("surgery-window-too-high"));
                            break;
                    }
                }
            }

            var texture = _entities.GetComponentOrNull<SpriteComponent>(stepButton.Step)?.Icon?.Default;
            stepButton.Set(stepName, texture);
            i++;
        }
    }

    private void View(ViewType type)
    {
        if (_window == null)
            return;

        _window.PartsButton.Parent!.Margin = new Thickness(0, 0, 0, 10);

        _window.Parts.Visible = type == ViewType.Parts;
        _window.PartsButton.Disabled = type == ViewType.Parts;

        _window.Surgeries.Visible = type == ViewType.Surgeries;
        _window.SurgeriesButton.Disabled = type != ViewType.Steps;

        _window.Steps.Visible = type == ViewType.Steps;
        _window.StepsButton.Disabled = type != ViewType.Steps || _previousSurgeries.Count == 0;

        var partName = GetSelectedPartName();

        if (partName != null &&
            _entities.TryGetComponent(_surgery?.Ent, out MetaDataComponent? surgeryMeta))
        {
            _window.Title = _loc.GetString("surgery-window-title-part-surgery",
                ("part", partName),
                ("surgery", surgeryMeta.EntityName));
        }
        else if (partName != null)
        {
            _window.Title = _loc.GetString("surgery-window-title-part", ("part", partName));
        }
        else
        {
            _window.Title = _loc.GetString("surgery-window-name");
        }
    }

    private string? GetSelectedPartName()
    {
        if (_part is not { } part ||
            !_entities.TryGetComponent(part, out OrganComponent? organ))
        {
            return null;
        }

        return GetPartName((part, organ));
    }

    private string GetPartName(Entity<OrganComponent> part)
    {
        if (part.Comp.Category?.Id is { } category &&
            GetPartLocId(category) is { } locId)
        {
            return _loc.GetString(locId);
        }

        return _entities.GetComponent<MetaDataComponent>(part).EntityName;
    }

    private static LocId? GetPartLocId(string category)
    {
        return category switch
        {
            "Torso" => "surgery-window-part-torso",
            "Head" => "surgery-window-part-head",
            "ArmLeft" => "surgery-window-part-left-arm",
            "ArmRight" => "surgery-window-part-right-arm",
            "HandLeft" => "surgery-window-part-left-hand",
            "HandRight" => "surgery-window-part-right-hand",
            "LegLeft" => "surgery-window-part-left-leg",
            "LegRight" => "surgery-window-part-right-leg",
            "FootLeft" => "surgery-window-part-left-foot",
            "FootRight" => "surgery-window-part-right-foot",
            _ => null,
        };
    }

    private enum ViewType
    {
        Parts,
        Surgeries,
        Steps
    }

    private enum StepStatus
    {
        Next,
        Complete,
        Incomplete
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
        _hands.OnPlayerItemAdded -= OnPlayerItemAdded;
    }
}
