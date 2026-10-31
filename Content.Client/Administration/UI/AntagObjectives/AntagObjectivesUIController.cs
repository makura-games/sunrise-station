using System.Linq;
using Content.Client._Sunrise.Lobby;
using Content.Client._Sunrise.AntagObjectives;
using Content.Client.Administration.Systems;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Character;
using Content.Client.UserInterface.Systems.Character.Controls;
using Content.Client.UserInterface.Systems.Objectives.Controls;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.AntagObjectives;

[UsedImplicitly]
public sealed class AntagObjectivesUIController : UIController,
    IOnStateEntered<GameplayState>,
    IOnStateEntered<SunriseLobbyState>, // Sunrise-Edit
    IOnSystemChanged<AdminSystem>,
    IOnSystemChanged<AntagObjectivesSystem>
{
    [UISystemDependency] private readonly SpriteSystem _sprite = default!;

    private AntagObjectivesWindow? _window;

    public void OnStateEntered(GameplayState state)
    {
        EnsureWindow();
    }

    public void OnStateEntered(SunriseLobbyState state) // Sunrise-Edit
    {
        EnsureWindow();
    }

    public void OnSystemLoaded(AdminSystem system)
    {
        EnsureWindow();
    }

    public void OnSystemLoaded(AntagObjectivesSystem system)
    {
        EnsureWindow();
        system.OnAntagObjectivesUpdate += AntagObjectivesUpdated;
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window.Dispose();
            _window = null;
        }

        CommandBinds.Unregister<CharacterUIController>();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        if (_window?.Disposed ?? false)
            OnWindowDisposed();

        _window = UIManager.CreateWindow<AntagObjectivesWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Center);
    }

    private void OnWindowDisposed()
    {
        if (_window == null)
            return;

        _window = null;
    }

    public void OnSystemUnloaded(AdminSystem system)
    {
        if (_window != null)
            _window.Dispose();
    }

    public void OnSystemUnloaded(AntagObjectivesSystem system)
    {
        system.OnAntagObjectivesUpdate -= AntagObjectivesUpdated;
    }

    private void AntagObjectivesUpdated(AntagObjectivesSystem.AntagObjectivesData data)
    {
        if (_window == null)
        {
            return;
        }

        var (objectives, briefing) = data;

        _window.Objectives.RemoveAllChildren();
        _window.ObjectivesLabel.Visible = objectives.Any();

        foreach (var (groupId, conditions) in objectives)
        {
            var objectiveControl = new CharacterObjectiveControl
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Modulate = Color.Gray
            };

            var objectiveText = new FormattedMessage();
            objectiveText.TryAddMarkup(groupId, out _);

            var objectiveLabel = new RichTextLabel
            {
                StyleClasses = {StyleNano.StyleClassTooltipActionTitle}
            };
            objectiveLabel.SetMessage(objectiveText);

            objectiveControl.AddChild(objectiveLabel);

            foreach (var condition in conditions)
            {
                var conditionControl = new ObjectiveConditionsControl();
                conditionControl.ProgressTexture.Texture = _sprite.Frame0(condition.Icon);
                conditionControl.ProgressTexture.Progress = condition.Progress;
                var titleMessage = new FormattedMessage();
                var descriptionMessage = new FormattedMessage();
                titleMessage.AddText(condition.Title);
                descriptionMessage.AddText(condition.Description);

                conditionControl.Title.SetMessage(titleMessage);
                conditionControl.Description.SetMessage(descriptionMessage);

                objectiveControl.AddChild(conditionControl);
            }

            _window.Objectives.AddChild(objectiveControl);
        }

        if (briefing != null)
        {
            var briefingControl = new ObjectiveBriefingControl();
            var text = new FormattedMessage();
            text.PushColor(Color.Yellow);
            text.AddText(briefing);
            briefingControl.Label.SetMessage(text);
            _window.Objectives.AddChild(briefingControl);
        }
    }

    private void CloseWindow()
    {
        _window?.Close();
    }

    public void OpenWindow()
    {
        if (_window == null)
            return;

        if (!_window.IsOpen)
        {
            _window.Open();
        }
    }
}
