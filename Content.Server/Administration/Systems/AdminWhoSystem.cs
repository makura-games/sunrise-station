using Content.Server.Administration.Managers;
using Content.Server.Afk;
using Content.Shared.Administration;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

/// <summary>
/// Server system for handling admin who requests
/// </summary>
public sealed partial class AdminWhoSystem : EntitySystem
{
    [Dependency] private IAfkManager _afkManager = default!;
    [Dependency] private IAdminManager _adminManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeNetworkEvent<RequestAdminWhoEvent>(OnRequestAdminWho);
    }

    private void OnRequestAdminWho(RequestAdminWhoEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession == null)
            return;

        var seeStealth = false;
        var seeAfk = false;

        // Check if the requesting player can see stealth admins and AFK status
        if (session.SenderSession.AttachedEntity != null)
        {
            var playerData = _adminManager.GetAdminData(session.SenderSession);
            if (playerData != null)
            {
                seeStealth = playerData.CanStealth();
                seeAfk = _adminManager.HasAdminFlag(session.SenderSession, AdminFlags.Admin);
            }
        }

        var adminList = new List<AdminWhoEntry>();
        
        foreach (var admin in _adminManager.ActiveAdmins)
        {
            var adminData = _adminManager.GetAdminData(admin);
            DebugTools.AssertNotNull(adminData);

            if (adminData!.Stealth && !seeStealth)
                continue;

            var isAfk = seeAfk && _afkManager.IsAfk(admin);
            
            adminList.Add(new AdminWhoEntry(
                admin.Name,
                adminData.Title,
                adminData.Stealth,
                isAfk
            ));
        }

        RaiseNetworkEvent(new AdminWhoResponseEvent(adminList), session.SenderSession);
    }
}