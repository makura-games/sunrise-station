using Content.Server.EUI;
using Content.Shared._Sunrise.NewLife;
using Content.Shared.Eui;

namespace Content.Server._Sunrise.NewLife.UI;

public sealed partial class NewLifeEui : BaseEui
{
    [Dependency] private IEntityManager _entityManager = default!;

    public NewLifeEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override NewLifeEuiState GetNewState()
    {
        var newLife = _entityManager.System<NewLifeSystem>();

        if (newLife.TryGetEuiState(Player, out var state))
            return state;

        return new NewLifeEuiState(
            new List<NewLifeCharacterInfo>(),
            new Dictionary<NetEntity, string>(),
            new Dictionary<NetEntity, List<NewLifeRolesInfo>>(),
            TimeSpan.Zero,
            new List<int>());
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case NewLifeRequestSpawnMessage req:
                _entityManager.System<NewLifeSystem>().OnGhostRespawnMenuRequest(Player, req.CharacterId, req.StationId, req.RoleProto);
                break;
        }
    }

    public override void Closed()
    {
        base.Closed();

        _entityManager.System<NewLifeSystem>().CloseEui(Player);
    }
}
