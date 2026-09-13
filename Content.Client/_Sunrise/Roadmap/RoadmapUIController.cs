using System.Text;
using Content.Client.Lobby;
using Content.Shared._Sunrise.Roadmap;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Roadmap;

public sealed partial class RoadmapUIController : UIController, IOnStateEntered<LobbyState>
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [UISystemDependency] private readonly RoadmapSystem _roadmap = default!;

    private Roadmap? _window;

    public void OnStateEntered(LobbyState state)
    {
        _roadmap.RequestLikes();

        if (_window != null)
            return;

        var roadmapId = _cfg.GetCVar(SunriseCCVars.RoadmapId);
        if (!_prototype.Resolve<RoadmapVersionsPrototype>(roadmapId, out var roadmapVersions))
            return;

        var currentHash = ComputeHash(roadmapVersions);
        var lastSeenHash = _cfg.GetCVar(SunriseCCVars.RoadmapLastSeenHash);
        if (lastSeenHash == currentHash)
            return;

        OpenRoadmap();
        _cfg.SetCVar(SunriseCCVars.RoadmapLastSeenHash, currentHash);
        _cfg.SaveToFile();
    }

    public void ToggleRoadmap()
    {
        if (_window != null)
        {
            _window.Close();
            return;
        }

        OpenRoadmap();
    }

    private void OpenRoadmap()
    {
        _window = new Roadmap();
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }
    private static void AppendHashField(StringBuilder sb, string? value)
    {
        var text = value ?? string.Empty;
        sb.Append(text.Length);
        sb.Append(':');
        sb.Append(text);
        sb.Append('|');
    }

    private static string ComputeHash(RoadmapVersionsPrototype proto)
    {
        var sb = new StringBuilder();
        AppendHashField(sb, proto.ID);
        AppendHashField(sb, proto.Fork);
        foreach (var group in proto.Versions)
        {
            AppendHashField(sb, group.Name);
            foreach (var goal in group.Goals)
            {
                AppendHashField(sb, goal.Id);
                AppendHashField(sb, goal.Name);
                AppendHashField(sb, goal.Desc);
                AppendHashField(sb, ((int)goal.State).ToString());
            }
        }

        // FNV-1a 64-bit
        unchecked
        {
            var hash = 14695981039346656037UL;
            foreach (var c in sb.ToString())
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16");
        }
    }
}
