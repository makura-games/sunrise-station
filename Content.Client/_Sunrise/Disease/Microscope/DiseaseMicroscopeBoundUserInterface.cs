using Content.Client.UserInterface;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Microscope;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.Timing;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>Отправляет операции с проверкой ревизии и рисует состояние культуры с сервера.</summary>
public sealed partial class DiseaseMicroscopeBoundUserInterface : BoundUserInterface, IBuiPreTickUpdate
{
    [Dependency] private IClientGameTiming _timing = default!;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

    private DiseaseMicroscopeWindow? _window;
    private int? _pendingRevision;
    private TimeSpan _pendingUntil;

    public DiseaseMicroscopeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _pendingRevision = null;
        _window = this.CreateWindow<DiseaseMicroscopeWindow>();
        _window.OnSamplePressed += () =>
        {
            if (EntMan.TryGetComponent(Owner, out DiseaseMicroscopeComponent? microscope))
                SendPredictedMessage(new ItemSlotButtonPressedEvent(microscope.SampleSlotId));
        };
        // Засев, партия за путь, ход и прогноз предсказываются: окно не ждёт ответа сервера.
        _window.OnPrepare += symptom =>
        {
            if (TryGetIdle(out var microscope))
                SendPredictedMessage(new DiseaseMicroscopePrepareMessage(symptom, microscope.MatchCounter));
        };
        _window.OnTransmission += route =>
        {
            if (TryGetIdle(out var microscope))
                SendPredictedMessage(new DiseaseMicroscopeTransmissionMessage(route, microscope.MatchCounter));
        };
        _window.OnApply += (tool, x, y) =>
        {
            if (TryGetIdle(out var microscope))
                SendPredictedMessage(new DiseaseMicroscopeApplyMessage(tool, x, y, microscope.Turn));
        };
        _window.OnForecast += () =>
        {
            if (TryGetIdle(out var microscope))
                SendPredictedMessage(new DiseaseMicroscopeForecastMessage(microscope.Turn));
        };
        _window.OnFinish += () =>
        {
            if (TryBeginRequest(out var revision))
                SendMessage(new DiseaseMicroscopeFinishMessage(revision));
        };
        _window.OnAdvance += () =>
        {
            if (TryBeginRequest(out var revision))
                SendMessage(new DiseaseMicroscopeAdvanceMessage(revision));
        };
        _window.OnProduce += () =>
        {
            if (TryBeginRequest(out var revision))
                SendMessage(new DiseaseMicroscopeProduceMessage(revision));
        };
        Update();
    }

    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out DiseaseMicroscopeComponent? microscope))
            return;

        if (_pendingRevision != microscope.Revision)
            _pendingRevision = null;

        _window.UpdateState(microscope, _pendingRevision != null);
    }

    void IBuiPreTickUpdate.PreTickUpdate()
    {
        if (_pendingRevision == null || _timing.RealTime < _pendingUntil)
            return;

        // Оглушение может отклонить сообщение до системы машины; повтор автоматически не отправляем.
        _pendingRevision = null;
        Update();
        _window?.ShowRequestTimeout();
    }

    /// <summary>
    /// Машина, если окно не ждёт ответа на серверную команду. Предсказанные операции
    /// во время такой команды не шлются: завершение партии меняет поле под ними.
    /// </summary>
    private bool TryGetIdle([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DiseaseMicroscopeComponent? microscope)
    {
        microscope = null;
        return _pendingRevision == null && EntMan.TryGetComponent(Owner, out microscope);
    }

    private bool TryBeginRequest(out int revision)
    {
        revision = 0;
        if (_pendingRevision != null || !EntMan.TryGetComponent(Owner, out DiseaseMicroscopeComponent? microscope))
            return false;

        revision = microscope.Revision;
        _pendingRevision = revision;
        _pendingUntil = _timing.RealTime + RequestTimeout;
        _window?.SetPending();
        return true;
    }
}
