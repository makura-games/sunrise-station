using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Vaccinator;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Disease;

/// <summary>
/// Интерфейс вакцинатора. Всё, что показывает окно, лежит в состоянии компонента машины,
/// поэтому окно собирается из уже имеющихся у клиента данных.
/// </summary>
public sealed class VaccinatorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private VaccinatorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<VaccinatorWindow>();
        _window.OnSynthesizePressed += index => SendPredictedMessage(new VaccinatorSynthesizeMessage(index));
        _window.OnProducePressed += () => SendPredictedMessage(new VaccinatorProduceMessage());
        _window.OnExtractSerumPressed += index => SendPredictedMessage(new VaccinatorExtractSerumMessage(index));

        Update();
    }

    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out VaccinatorComponent? vaccinator))
            return;

        _window.UpdateState((Owner, vaccinator));
    }
}
