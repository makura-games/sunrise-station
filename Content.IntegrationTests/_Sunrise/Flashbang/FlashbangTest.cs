using Content.Shared._Sunrise.Flashbang;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._Sunrise.Flashbang;

[TestFixture]
[TestOf(typeof(SharedFlashbangSystem))]
public sealed class FlashbangTest
{
    private static EntityUid CreateSource(IEntityManager entMan, MapCoordinates coords)
    {
        var source = entMan.SpawnEntity(null, coords);
        var comp = entMan.AddComponent<FlashbangRadiusOnTriggerComponent>(source);
        comp.Range = 10f;
        comp.StunDuration = TimeSpan.FromSeconds(4);
        comp.KnockdownDuration = TimeSpan.FromSeconds(4);
        comp.MinEffectStrength = 0.01f;
        comp.MinAmbientPressure = 0f; // в тестах атмосферы нет
        entMan.Dirty(source, comp);
        return source;
    }

    /// <summary>
    /// Цель в эпицентре должна получить стан и нокдаун.
    /// </summary>
    [Test]
    public async Task TargetAtCenter_ShouldReceiveStunAndKnockdown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var flashbangSys = entMan.System<SharedFlashbangSystem>();

        EntityUid source = default;
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            source = CreateSource(entMan, new MapCoordinates(0f, 0f, mapId));
            target = entMan.SpawnEntity("MobHuman", new MapCoordinates(0f, 0f, mapId));
        });

        await server.WaitPost(() =>
        {
            flashbangSys.TryFlashbangArea(source, null);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.True, "Цель в эпицентре должна быть оглушена.");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.True, "Цель в эпицентре должна упасть.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Цель за пределами радиуса не должна получить эффект.
    /// </summary>
    [TestCase(15f, 0f, TestName = "TargetOutsideRange_Right")]
    [TestCase(-15f, 0f, TestName = "TargetOutsideRange_Left")]
    [TestCase(0f, 15f, TestName = "TargetOutsideRange_Top")]
    [TestCase(0f, -15f, TestName = "TargetOutsideRange_Bottom")]
    [TestCase(11f, 11f, TestName = "TargetOutsideRange_Diagonal")]
    public async Task TargetOutsideRange_ShouldNotBeAffected(float x, float y)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var flashbangSys = entMan.System<SharedFlashbangSystem>();

        EntityUid source = default;
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            source = CreateSource(entMan, new MapCoordinates(0f, 0f, mapId));
            // Range = 10, ставим за пределами
            target = entMan.SpawnEntity("MobHuman", new MapCoordinates(x, y, mapId));
        });

        await server.WaitPost(() =>
        {
            flashbangSys.TryFlashbangArea(source, null);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.False, "Цель вне радиуса не должна быть оглушена.");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.False, "Цель вне радиуса не должна упасть.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Защита через FlashbangProtectionComponent должна уменьшать эффективную дистанцию,
    /// полностью нейтрализуя слабый эффект у края зоны.
    /// </summary>
    [Test]
    public async Task ProtectionComponent_ShouldNeutralizeEdgeEffect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var flashbangSys = entMan.System<SharedFlashbangSystem>();

        EntityUid source = default;
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            source = CreateSource(entMan, new MapCoordinates(0f, 0f, mapId));
            // Реальная дистанция = 7, coeff = 0.5, effective = 12 >= range 10 → эффект не применяется
            target = entMan.SpawnEntity("MobHuman", new MapCoordinates(7f, 0f, mapId));
            // Добавляем защиту напрямую на цель (имитирует шлем/ухо)
            var prot = entMan.AddComponent<FlashbangProtectionComponent>(target);
            prot.ProtectionRangeCoefficient = 0.5f;
            entMan.Dirty(target, prot);
        });

        await server.WaitPost(() =>
        {
            flashbangSys.TryFlashbangArea(source, null);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.False, "Защита должна нейтрализовать слабый эффект у края зоны.");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.False, "Защита должна предотвратить нокдаун у края зоны.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// IgnoreResistances = true должен игнорировать защиту.
    /// </summary>
    [Test]
    public async Task IgnoreResistances_ShouldBypassProtection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var flashbangSys = entMan.System<SharedFlashbangSystem>();

        EntityUid source = default;
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            source = CreateSource(entMan, new MapCoordinates(0f, 0f, mapId));
            // Реальная дистанция = 7, большая защита
            target = entMan.SpawnEntity("MobHuman", new MapCoordinates(7f, 0f, mapId));
            var prot = entMan.AddComponent<FlashbangProtectionComponent>(target);
            prot.ProtectionRangeCoefficient = 1f;
            entMan.Dirty(target, prot);

            // Получаем компонент и включаем IgnoreResistances
            var comp = entMan.GetComponent<FlashbangRadiusOnTriggerComponent>(source);
            comp.IgnoreResistances = true;
            entMan.Dirty(source, comp);
        });

        await server.WaitPost(() =>
        {
            flashbangSys.TryFlashbangArea(source, null);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.True, "IgnoreResistances должен игнорировать защиту и оглушить цель.");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.True, "IgnoreResistances должен игнорировать защиту и уронить цель.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// FlashbangVulnerableComponent должен позволять применить эффект без сбора защиты.
    /// </summary>
    [Test]
    public async Task VulnerableTarget_ShouldBeAffectedDespiteProtection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var flashbangSys = entMan.System<SharedFlashbangSystem>();

        EntityUid source = default;
        EntityUid target = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            source = CreateSource(entMan, new MapCoordinates(0f, 0f, mapId));
            target = entMan.SpawnEntity("MobHuman", new MapCoordinates(3f, 0f, mapId));
            var vuln = entMan.AddComponent<FlashbangVulnerableComponent>(target);
            vuln.BypassProtection = true;
            entMan.Dirty(target, vuln);
            // Добавляем большую защиту — для уязвимой цели она должна игнорироваться
            var prot = entMan.AddComponent<FlashbangProtectionComponent>(target);
            prot.ProtectionRangeCoefficient = 1f;
            entMan.Dirty(target, prot);
        });

        await server.WaitPost(() =>
        {
            flashbangSys.TryFlashbangArea(source, null);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.True, "Уязвимая цель должна получить стан несмотря на защиту.");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.True, "Уязвимая цель должна упасть несмотря на защиту.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// FlashbangAttemptEvent с Cancelled = true должен отменять применение эффекта.
    /// </summary>
    [Test]
    public async Task CancelledAttemptEvent_ShouldPreventEffect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var flashbangSys = entMan.System<SharedFlashbangSystem>();

        EntityUid source = default;
        EntityUid target = default;
        bool eventRaised = false;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            source = CreateSource(entMan, new MapCoordinates(0f, 0f, mapId));
            target = entMan.SpawnEntity("MobHuman", new MapCoordinates(0f, 0f, mapId));

            // Подписываемся на событие попытки и отменяем
            entMan.EventBus.SubscribeLocalEvent<MobStateComponent, FlashbangAttemptEvent>(
                (EntityUid uid, MobStateComponent _, ref FlashbangAttemptEvent ev) =>
                {
                    if (uid != target)
                        return;
                    ev.Cancelled = true;
                    eventRaised = true;
                },
                typeof(FlashbangTest));
        });

        await server.WaitPost(() =>
        {
            flashbangSys.TryFlashbangArea(source, null);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(eventRaised, Is.True, "FlashbangAttemptEvent должен быть вызван.");
                Assert.That(entMan.HasComponent<StunnedComponent>(target), Is.False, "Отменённая попытка не должна оглушать цель.");
                Assert.That(entMan.HasComponent<KnockedDownComponent>(target), Is.False, "Отменённая попытка не должна ронять цель.");
            });
        });

        await pair.CleanReturnAsync();
    }
}
