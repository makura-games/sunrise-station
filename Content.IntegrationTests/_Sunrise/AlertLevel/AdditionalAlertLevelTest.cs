#nullable enable

using System.Collections.Generic;
using Content.Server.AlertLevel;
using Content.Server.Communications;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._Sunrise.AlertLevel;

[TestFixture]
[TestOf(typeof(AlertLevelSystem))]
public sealed class AdditionalAlertLevelTest
{
    [Test]
    public async Task AdditionalLevelsRespectSharedCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var alertLevelSystem = server.System<AlertLevelSystem>();

        EntityUid station = default;
        AlertLevelComponent alertLevel = null!;
        var blockedByPrimaryCooldown = false;
        var yellowEnabled = false;
        var forcedVioletEnabled = false;
        var yellowDisableBlockedByCooldown = false;
        var yellowDisabled = false;

        await server.WaitPost(() =>
        {
            station = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            alertLevel = entityManager.AddComponent<AlertLevelComponent>(station);
            alertLevel.AlertLevels = prototypeManager.Index<AlertLevelPrototype>(AlertLevelSystem.DefaultAlertLevelSet);
            alertLevel.CurrentLevel = "green";
            alertLevel.CurrentDelay = 30;

            blockedByPrimaryCooldown = !alertLevelSystem.TrySetAdditionalLevel(
                station,
                "yellow",
                true,
                playSound: false,
                announce: false,
                component: alertLevel);

            alertLevel.CurrentDelay = 0;
            alertLevel.ActiveDelay = false;
            yellowEnabled = alertLevelSystem.TrySetAdditionalLevel(
                station,
                "yellow",
                true,
                playSound: false,
                announce: false,
                component: alertLevel);

            forcedVioletEnabled = alertLevelSystem.TrySetAdditionalLevel(
                station,
                "violet",
                true,
                playSound: false,
                announce: false,
                force: true,
                component: alertLevel);

            yellowDisableBlockedByCooldown = !alertLevelSystem.TrySetAdditionalLevel(
                station,
                "yellow",
                false,
                playSound: false,
                announce: false,
                component: alertLevel);

            alertLevel.CurrentDelay = 0;
            alertLevel.ActiveDelay = false;
            yellowDisabled = alertLevelSystem.TrySetAdditionalLevel(
                station,
                "yellow",
                false,
                playSound: false,
                announce: false,
                component: alertLevel);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(blockedByPrimaryCooldown, Is.True);
                Assert.That(yellowEnabled, Is.True);
                Assert.That(forcedVioletEnabled, Is.True);
                Assert.That(yellowDisableBlockedByCooldown, Is.True);
                Assert.That(yellowDisabled, Is.True);
                Assert.That(alertLevel.CurrentLevel, Is.EqualTo("green"));
                Assert.That(alertLevel.ActiveDelay, Is.True);
                Assert.That(alertLevel.CurrentDelay, Is.GreaterThan(0));
                Assert.That(alertLevel.ActiveAdditionalLevels, Is.EquivalentTo(new[] { "violet" }));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VisualPrioritySelectsHighestActiveLevel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var alertLevelSystem = server.System<AlertLevelSystem>();

        EntityUid station = default;
        AlertLevelComponent alertLevel = null!;
        var effectiveLevels = new List<string>();

        await server.WaitPost(() =>
        {
            station = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            alertLevel = entityManager.AddComponent<AlertLevelComponent>(station);
            alertLevel.AlertLevels = prototypeManager.Index<AlertLevelPrototype>(AlertLevelSystem.DefaultAlertLevelSet);
            alertLevel.CurrentLevel = "green";

            alertLevel.ActiveAdditionalLevels.Add("yellow");
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out var level, out _)
                ? level
                : string.Empty);

            alertLevel.CurrentLevel = "red";
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out level, out _)
                ? level
                : string.Empty);

            alertLevel.ActiveAdditionalLevels.Add("delta");
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out level, out _)
                ? level
                : string.Empty);

            alertLevel.ActiveAdditionalLevels.Add("epsilon");
            effectiveLevels.Add(alertLevelSystem.TryGetVisualAlertLevel((station, alertLevel), out level, out _)
                ? level
                : string.Empty);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(effectiveLevels, Is.EqualTo(new[] { "yellow", "red", "delta", "epsilon" }));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdditionalLevelKeepsPrimaryEmergencyAccesses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var accessReaderSystem = server.System<AccessReaderSystem>();

        AccessReaderComponent accessReader = null!;
        var securityAllowed = false;
        var atmosphericsAllowed = false;
        var engineeringAllowed = false;

        await server.WaitPost(() =>
        {
            var reader = entityManager.SpawnEntity("DoorElectronicsLawyer", MapCoordinates.Nullspace);
            accessReader = entityManager.GetComponent<AccessReaderComponent>(reader);

            accessReaderSystem.UpdateAccess(
                (reader, accessReader),
                "red",
                new[] { "red", "yellow" },
                new HashSet<ProtoId<AccessGroupPrototype>>
                {
                    new("YellowAlertAccesses"),
                });

            securityAllowed = accessReaderSystem.IsAccessAllowedByExtendedAccess(
                new HashSet<ProtoId<AccessLevelPrototype>> { new("Security") },
                accessReader);
            atmosphericsAllowed = accessReaderSystem.IsAccessAllowedByExtendedAccess(
                new HashSet<ProtoId<AccessLevelPrototype>> { new("Atmospherics") },
                accessReader);
            engineeringAllowed = accessReaderSystem.IsAccessAllowedByExtendedAccess(
                new HashSet<ProtoId<AccessLevelPrototype>> { new("Engineering") },
                accessReader);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(accessReader.Group, Is.EqualTo(new ProtoId<AccessGroupPrototype>("RedAlertAccesses")));
                Assert.That(accessReader.AdditionalGroups,
                    Does.Contain(new ProtoId<AccessGroupPrototype>("YellowAlertAccesses")));
                Assert.That(securityAllowed, Is.True);
                Assert.That(atmosphericsAllowed, Is.True);
                Assert.That(engineeringAllowed, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DenyTagsOverrideAlertLevelAccessGroups()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var accessReaderSystem = server.System<AccessReaderSystem>();
        AccessReaderComponent accessReader = null!;
        var allowed = true;

        await server.WaitPost(() =>
        {
            var reader = entityManager.SpawnEntity("DoorElectronicsLawyer", MapCoordinates.Nullspace);
            accessReader = entityManager.GetComponent<AccessReaderComponent>(reader);
            accessReaderSystem.UpdateAccess((reader, accessReader), "red");
            accessReaderSystem.SetDenyTags(
                (reader, accessReader),
                [new ProtoId<AccessLevelPrototype>("Security")]);

            allowed = accessReaderSystem.IsAccessAllowedByExtendedAccess(
                new HashSet<ProtoId<AccessLevelPrototype>> { new("Security") },
                accessReader);
        });

        await server.WaitAssertion(() => Assert.That(allowed, Is.False));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdditionalAccessGroupDoesNotReplacePrimaryGroup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var accessReaderSystem = server.System<AccessReaderSystem>();
        AccessReaderComponent accessReader = null!;

        await server.WaitPost(() =>
        {
            var reader = entityManager.SpawnEntity("DoorElectronicsLawyer", MapCoordinates.Nullspace);
            accessReader = entityManager.GetComponent<AccessReaderComponent>(reader);
            accessReaderSystem.UpdateAccess(
                (reader, accessReader),
                "red",
                new[] { "red", "yellow" },
                new HashSet<ProtoId<AccessGroupPrototype>>
                {
                    new("YellowAlertAccesses"),
                });
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(accessReader.Group, Is.EqualTo(new ProtoId<AccessGroupPrototype>("RedAlertAccesses")));
                Assert.That(accessReader.AdditionalGroups,
                    Does.Contain(new ProtoId<AccessGroupPrototype>("YellowAlertAccesses")));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConsoleAlertLevelAllowlistIsEnforced()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var levels = prototypeManager.Index<AlertLevelPrototype>(AlertLevelSystem.DefaultAlertLevelSet).Levels;
            var engineeringConsole = new CommunicationsConsoleComponent
            {
                AllowedAlertLevels = ["yellow"],
            };
            var disabledConsole = new CommunicationsConsoleComponent
            {
                AllowedAlertLevels = [],
            };
            var centCommConsole = new CommunicationsConsoleComponent
            {
                AllowedAlertLevels = ["green", "blue", "violet", "yellow", "red", "gamma", "delta"],
                ForceAlertLevelChanges = true,
            };

            Assert.Multiple(() =>
            {
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    engineeringConsole,
                    "yellow",
                    levels["yellow"]), Is.True);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    engineeringConsole,
                    "violet",
                    levels["violet"]), Is.False);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    engineeringConsole,
                    "green",
                    levels["green"]), Is.False);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    disabledConsole,
                    "red",
                    levels["red"]), Is.False);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    centCommConsole,
                    "gamma",
                    levels["gamma"]), Is.True);
                Assert.That(levels["gamma"].IsAdditional, Is.False);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    centCommConsole,
                    "delta",
                    levels["delta"]), Is.True);
                Assert.That(levels["delta"].IsAdditional, Is.True);
                Assert.That(CommunicationsConsoleSystem.IsAlertLevelAllowed(
                    centCommConsole,
                    "epsilon",
                    levels["epsilon"]), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CentCommConsoleCanSelectRemoteAlertStation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var communicationsSystem = server.System<CommunicationsConsoleSystem>();

        EntityUid console = default;
        CommunicationsConsoleComponent consoleComponent = null!;
        EntityUid user = default;
        EntityUid validStation = default;
        EntityUid invalidStation = default;
        var validStationSelected = false;
        var remoteAlertLevelSet = false;
        var invalidStationRejected = false;
        var disabledSelectionRejected = false;

        await server.WaitPost(() =>
        {
            console = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            consoleComponent = entityManager.AddComponent<CommunicationsConsoleComponent>(console);
            consoleComponent.CanSelectAlertStation = true;
            consoleComponent.ForceAlertLevelChanges = true;
            consoleComponent.AllowedAlertLevels = ["gamma"];
            user = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);

            validStation = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            entityManager.AddComponent<StationDataComponent>(validStation);
            var validAlert = entityManager.AddComponent<AlertLevelComponent>(validStation);
            validAlert.AlertLevels = prototypeManager.Index<AlertLevelPrototype>(AlertLevelSystem.DefaultAlertLevelSet);
            validAlert.CurrentLevel = "green";

            invalidStation = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            entityManager.AddComponent<StationDataComponent>(invalidStation);

            validStationSelected = communicationsSystem.TrySelectAlertStation(
                (console, consoleComponent),
                validStation,
                user);
            remoteAlertLevelSet = communicationsSystem.TrySetPrimaryAlertLevel(
                (console, consoleComponent),
                "gamma",
                user);
            invalidStationRejected = !communicationsSystem.TrySelectAlertStation(
                (console, consoleComponent),
                invalidStation,
                user);

            consoleComponent.CanSelectAlertStation = false;
            disabledSelectionRejected = !communicationsSystem.TrySelectAlertStation(
                (console, consoleComponent),
                validStation,
                user);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(validStationSelected, Is.True);
                Assert.That(remoteAlertLevelSet, Is.True);
                Assert.That(entityManager.GetComponent<AlertLevelComponent>(validStation).CurrentLevel,
                    Is.EqualTo("gamma"));
                Assert.That(invalidStationRejected, Is.True);
                Assert.That(disabledSelectionRejected, Is.True);
                Assert.That(consoleComponent.SelectedAlertStation, Is.EqualTo(validStation));
            });
        });

        await pair.CleanReturnAsync();
    }
}
