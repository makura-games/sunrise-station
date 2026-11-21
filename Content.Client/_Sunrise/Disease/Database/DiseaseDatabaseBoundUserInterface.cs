using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Database;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Disease.Database;

/// <summary>
/// Интерфейс базы данных вирусов. Записи лежат в состоянии компонента консоли,
/// поэтому окно собирается из уже имеющихся у клиента данных.
/// </summary>
public sealed class DiseaseDatabaseBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DiseaseDatabaseWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DiseaseDatabaseWindow>();
        _window.OnDispensePressed += index => SendPredictedMessage(new DiseaseDatabaseDispenseMessage(index));

        Update();
    }

    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out DiseaseDatabaseConsoleComponent? console))
            return;

        _window.UpdateState((Owner, console));
    }
}
