using System;
using Content.Client._Sunrise.Light.Visualizers;
using Content.IntegrationTests.Fixtures;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Sunrise.Light.Visualizers;
using Content.Shared.Light;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Sunrise.Light;

public sealed class PoweredLightSparksTest : GameTest
{
    [TestCase("Poweredlight")]
    [TestCase("PoweredSmallLight")]
    [TestCase("PoweredStrobeLightEmpty")]
    public async Task BrokenLightWithoutPowerDoesNotSpark(string prototype)
    {
        var entMan = Server.EntMan;
        EntityUid map = default;
        EntityUid lamp = default;

        await Server.WaitPost(() =>
        {
            map = entMan.System<SharedMapSystem>().CreateMap(out _);
            lamp = entMan.SpawnEntity(prototype, new EntityCoordinates(map, 0, 0));
            entMan.GetComponent<SunrisePoweredLightSparksComponent>(lamp).FlickerChance = 1;
            var lights = entMan.System<PoweredLightSystem>();
            if (lights.GetBulb(lamp) == null)
            {
                var bulb = entMan.SpawnEntity("LightBulb", new EntityCoordinates(map, 0, 0));
                lights.InsertBulb(lamp, bulb);
            }
            lights.SetState(lamp, true);
            lights.TryDestroyBulb(lamp);
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var receiver = entMan.GetComponent<ApcPowerReceiverComponent>(lamp);
            Assert.That(receiver.Powered, Is.False, "A broken bulb must not report power without an APC.");
            var appearance = entMan.System<SharedAppearanceSystem>();
            Assert.That(appearance.TryGetData<bool>(lamp, SunrisePoweredLightVisuals.HasPower, out var hasPower), Is.True);
            Assert.That(hasPower, Is.False);
            Assert.That(appearance.TryGetData<bool>(lamp, SunrisePoweredLightVisuals.ShouldFlicker, out var shouldFlicker), Is.True);
            Assert.That(shouldFlicker, Is.True, "The server should choose one flicker result for this broken lamp.");
        });

        await Server.WaitPost(() => entMan.System<PowerReceiverSystem>().SetNeedsPower(lamp, false));
        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            entMan.System<SharedAppearanceSystem>().TryGetData<bool>(lamp, SunrisePoweredLightVisuals.HasPower, out var hasPower);
            Assert.That(hasPower, Is.True, "Lights with an explicit power override still flicker.");
        });

        await Server.WaitPost(() => entMan.System<PoweredLightSystem>().SetState(lamp, false));
        await Server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ApcPowerReceiverComponent>(lamp).Load, Is.Zero);
            entMan.System<SharedAppearanceSystem>().TryGetData<bool>(lamp, SunrisePoweredLightVisuals.HasPower, out var hasPower);
            Assert.That(hasPower, Is.False, "The fixture switch must disable sparks even with a power override.");
        });

        await Server.WaitPost(() => entMan.DeleteEntity(map));
    }

    [TestCase("PoweredlightEmpty")]
    [TestCase("FloorLightEmpty")]
    [TestCase("AbductorLamp")]
    public async Task ClientFlickerHandlesEmptyStatesAndPowerLoss(string prototype)
    {
        var entMan = Client.EntMan;
        EntityUid map = default;
        EntityUid lamp = default;
        EntityUid invalidLamp = default;
        float originalEnergy = 0;

        await Client.WaitPost(() =>
        {
            map = entMan.System<SharedMapSystem>().CreateMap(out _);
            invalidLamp = entMan.SpawnEntity("PoweredlightEmpty", new EntityCoordinates(map, 0, 0));
            lamp = entMan.SpawnEntity(prototype, new EntityCoordinates(map, 1, 0));
            originalEnergy = entMan.GetComponent<PointLightComponent>(lamp).Energy;
            foreach (var uid in new[] { invalidLamp, lamp })
            {
                var sparks = entMan.GetComponent<SunrisePoweredLightSparksComponent>(uid);
                sparks.FlickerChance = 1;
                sparks.SparksChance = 1;
                sparks.FlickerSound = null;
                sparks.MinFlickerDelay = TimeSpan.Zero;
                sparks.MaxFlickerDelay = TimeSpan.Zero;
                var appearance = entMan.System<AppearanceSystem>();
                appearance.SetData(uid, PoweredLightVisuals.BulbState, PoweredLightState.Broken);
                appearance.SetData(uid, SunrisePoweredLightVisuals.HasPower, true);
                appearance.SetData(uid, SunrisePoweredLightVisuals.ShouldFlicker, true);
            }
            entMan.GetComponent<SunrisePoweredLightSparksComponent>(invalidLamp).States.Clear();
        });

        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<ActiveSunrisePoweredLightSparksComponent>(invalidLamp), Is.False);
            Assert.That(entMan.GetComponent<PointLightComponent>(lamp).Enabled, Is.True);
        });

        // Повторная вспышка до завершения предыдущей не должна скрыть новые слои.
        await Pair.RunTicksSync(1);
        await Client.WaitAssertion(() =>
        {
            var sprite = entMan.GetComponent<SpriteComponent>(lamp);
            var sprites = entMan.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((lamp, sprite), "sunrisePoweredLightFlicker", out var index, false), Is.True);
            Assert.That(sprites.TryGetLayer((lamp, sprite), index, out var layer, false), Is.True);
            Assert.That(layer.Visible, Is.True);
            if (sprite.BaseRSI?.TryGetState("spark-light1", out _) == true)
                Assert.That(layer.ActualRsi, Is.SameAs(sprite.BaseRSI), "Custom fixtures must keep their own spark artwork.");
        });

        await Client.WaitPost(() =>
        {
            entMan.System<AppearanceSystem>().SetData(lamp, SunrisePoweredLightVisuals.HasPower, false);
        });
        await Pair.RunTicksSync(1);
        await Client.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<ActiveSunrisePoweredLightSparksComponent>(lamp), Is.False);
            var light = entMan.GetComponent<PointLightComponent>(lamp);
            Assert.That(light.Enabled, Is.False);
            Assert.That(light.Energy, Is.EqualTo(originalEnergy));
        });

        await Client.WaitPost(() =>
        {
            var appearance = entMan.System<AppearanceSystem>();
            appearance.SetData(lamp, SunrisePoweredLightVisuals.HasPower, true);

            // Новая лампа получает собственную энергию до обработки очереди Appearance.
            entMan.System<SharedPointLightSystem>().SetEnergy(lamp, 0.5f);
            appearance.SetData(lamp, PoweredLightVisuals.BulbState, PoweredLightState.On);
        });
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() =>
        {
            var light = entMan.GetComponent<PointLightComponent>(lamp);
            Assert.That(light.Enabled, Is.True);
            Assert.That(light.Energy, Is.EqualTo(0.5f));
            Assert.That(entMan.HasComponent<ActiveSunrisePoweredLightSparksComponent>(lamp), Is.False);
        });

        await Client.WaitPost(() => entMan.DeleteEntity(map));
    }

}
