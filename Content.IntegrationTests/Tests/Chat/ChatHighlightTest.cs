#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Client.CharacterInfo;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chat;

public sealed class ChatHighlightTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly IConfigurationManager _configManager = null!;
    // Sunrise-Edit: Тест учитывает активную локаль клиента.
    [SidedDependency(Side.Client)] private readonly ILocalizationManager _localizationManager = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _uiManager = null!;
    private static readonly ProtoId<JobPrototype> Captain = "Captain";

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestCustomHighlightsPreserved()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();
        var captainHighlights = GetCaptainHighlights();

        // 1. Enable auto-fill highlights
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        // 2. Set custom highlights
        var customHighlights = "ling\nrev";
        chatController.UpdateHighlights(customHighlights);

        // Verify they are saved
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // 3. Simulate character update
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            Captain,
            "John Doe"
        );

        var method = chatController.GetType().GetMethod(
            "OnCharacterUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.That(method, Is.Not.Null);

        // Set internal state to allow character update processing
        var attachField = chatController.GetType().GetField(
            "_charInfoIsAttach",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(attachField, Is.Not.Null);
        attachField.SetValue(chatController, true);

        // Invoke update
        method.Invoke(chatController, new object[] { characterData });

        // 4. Assertions:
        // - Custom highlights in config must remain unchanged
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // - Internal active regex highlights must contain both custom & auto-filled highlights
        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        // Check that custom and auto highlights are loaded
        // Custom:
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        // Auto:
        Assert.That(activeHighlights, Contains.Item(captainHighlights.Name));
        Assert.That(activeHighlights, Contains.Item(captainHighlights.ShortName));

        // 5. Disable auto-fill highlights and verify auto-filled highlights are removed
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, false);

        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights, Is.Not.Contains(captainHighlights.Name));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestEnablingAutoFillPreservesCustomHighlights()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();
        var captainHighlights = GetCaptainHighlights();

        // 1. Start with auto-fill disabled
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, false);

        // 2. Set custom highlights
        var customHighlights = "ling\nrev";
        chatController.UpdateHighlights(customHighlights);

        // Verify active matches are ONLY custom highlights
        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights.Count, Is.EqualTo(2));

        // 3. Enable auto-fill highlights
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        // 4. Simulate character update (spawning into round)
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            Captain,
            "John Doe"
        );

        var method = chatController.GetType().GetMethod(
            "OnCharacterUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.That(method, Is.Not.Null);

        var attachField = chatController.GetType().GetField(
            "_charInfoIsAttach",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(attachField, Is.Not.Null);
        attachField.SetValue(chatController, true);

        // Invoke character update
        method.Invoke(chatController, new object[] { characterData });

        // 5. Assertions:
        // - Config highlights MUST NOT be wiped and remain as custom highlights
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // - Active highlights list must now merge both custom and auto-filled ones
        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights, Contains.Item(captainHighlights.Name));
        Assert.That(activeHighlights, Contains.Item(captainHighlights.ShortName));
    }

    private (string Name, string ShortName) GetCaptainHighlights()
    {
        var locKey = $"highlights-{Captain.Id.Replace(' ', '-').ToLowerInvariant()}";
        var highlights = _localizationManager.GetString(locKey)
            .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.That(highlights, Has.Length.GreaterThanOrEqualTo(2));

        var shortName = highlights[1].Trim('"');
        return (highlights[0], $"(?<!\\w){Regex.Escape(shortName)}(?!\\w)");
    }
}
