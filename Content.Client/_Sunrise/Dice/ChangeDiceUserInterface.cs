using Content.Shared._Sunrise.Dice;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Dice.UI
{
    [UsedImplicitly]
    public sealed class ChangeDiceUserInterface : BoundUserInterface
    {
        private IEntityManager _entManager;
        private EntityUid _owner;
        [ViewVariables]
        private ChangeDiceWindow? _window;

        public ChangeDiceUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _owner = owner;
            _entManager = IoCManager.Resolve<IEntityManager>();
        }

        protected override void Open()
        {
            base.Open();
            _window = this.CreateWindow<ChangeDiceWindow>();
            _window.OpenCentered();

            _window.ApplyButton.OnPressed += _ =>
            {
                if (int.TryParse(_window.AmountStartLineEdit.Text, out var x) && int.TryParse(_window.AmountEndLineEdit.Text, out var y))
                {
                    SendMessage(new ChangeDiceSetValueMessage(FixedPoint2.New(x), FixedPoint2.New(y)));
                    _window.Close();
                }
            };
        }
    }
}



