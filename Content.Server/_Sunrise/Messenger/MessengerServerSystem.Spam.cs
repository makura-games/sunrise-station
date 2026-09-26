using System.Linq;
using Content.Shared._Sunrise.Messenger;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Dataset;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Messenger;

public sealed partial class MessengerServerSystem
{
    private bool _spamEnabled;
    private float _spamMinTime;
    private float _spamMaxTime;
    private float _spamPlayerPercentage;

    private TimeSpan _nextTick = TimeSpan.Zero;
    private readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(1);

    private void InitializeSpam()
    {
        Subs.CVar(_cfg, SunriseCCVars.MessengerSpamEnabled, OnSpamEnabledChanged, true);
        Subs.CVar(_cfg, SunriseCCVars.MessengerSpamMinInterval, OnSpamMinTimeChanged, true);
        Subs.CVar(_cfg, SunriseCCVars.MessengerSpamMaxInterval, OnSpamMaxTimeChanged, true);
        Subs.CVar(_cfg, SunriseCCVars.MessengerSpamPlayerPercentage, OnSpamPlayerPercentageChanged, true);
    }

    private void OnSpamEnabledChanged(bool value)
    {
        _spamEnabled = value;
    }

    private void OnSpamMinTimeChanged(float value)
    {
        UpdateSpamTiming(value, _spamMaxTime);
    }

    private void OnSpamMaxTimeChanged(float value)
    {
        UpdateSpamTiming(_spamMinTime, value);
    }

    private void OnSpamPlayerPercentageChanged(float value)
    {
        _spamPlayerPercentage = value;
    }

    private void UpdateSpamTiming(float min, float max)
    {
        _spamMinTime = min;
        _spamMaxTime = max;

        if (_spamMinTime > _spamMaxTime)
        {
            (_spamMinTime, _spamMaxTime) = (_spamMaxTime, _spamMinTime);
        }
    }

    private void ResetSpamTimer(StationMessengerSpamComponent component)
    {
        component.Timer = 0;
        component.NextSpamTime = _random.NextFloat(_spamMinTime, _spamMaxTime);
    }

    private void UpdateSpam(float frameTime)
    {
        if (_nextTick > _timing.CurTime)
            return;

        _nextTick = _timing.CurTime + _refreshCooldown;

        if (!_spamEnabled)
            return;

        var query = EntityQueryEnumerator<StationMessengerSpamComponent>();
        while (query.MoveNext(out var uid, out var spam))
        {
            if (spam.NextSpamTime <= 0)
            {
                ResetSpamTimer(spam);
                continue;
            }

            spam.Timer += (float)_refreshCooldown.TotalSeconds;

            if (spam.Timer < spam.NextSpamTime)
                continue;

            ResetSpamTimer(spam);

            var serverResult = GetServerEntity(uid);
            if (serverResult != null)
            {
                Sawmill.Info($"Triggering spam wave for station {uid} (Server: {serverResult.Value.Item1})");
                SendSpamWave(serverResult.Value.Item1, serverResult.Value.Item2);
            }
            else
            {
                Sawmill.Warning($"Could not find messenger server for station {uid}, skipping spam wave.");
            }
        }
    }

    private void SendSpamWave(EntityUid uid, MessengerServerComponent component)
    {
        var prototypes = ProtoMan.EnumeratePrototypes<MessengerSpamPrototype>().ToList();
        if (prototypes.Count == 0)
            return;

        var players = new List<(EntityUid Uid, MessengerServerComponent Component, MessengerUser User)>();

        foreach (var user in component.Users.Values)
        {
            // Skip spam bots
            if (user.UserId.StartsWith("spam_"))
                continue;

            players.Add((uid, component, user));
        }

        if (players.Count == 0)
        {
            return;
        }

        var targetCount = (int)(players.Count * _spamPlayerPercentage);
        if (targetCount < 1)
            targetCount = 1;

        _random.Shuffle(players);

        var count = Math.Min(targetCount, players.Count);

        Sawmill.Info($"Sending spam to {count} users (Total: {players.Count}, Target %: {_spamPlayerPercentage})");

        for (int i = 0; i < count; i++)
        {
            var p = players[i];
            SendSpamToUser(p.Uid, p.Component, p.User, prototypes);
        }
    }

    private void SendSpamToUser(EntityUid uid, MessengerServerComponent component, MessengerUser user, List<MessengerSpamPrototype> prototypes)
    {
        var proto = _random.Pick(prototypes);

        var senderName = GetRandomString(proto.SenderDataset);
        if (proto.NameDataset != null && proto.SurnameDataset != null)
        {
            senderName += " " + GetRandomString(proto.NameDataset);
            senderName += " " + GetRandomString(proto.SurnameDataset);
        }

        var messageContent = GetRandomString(proto.MessageDataset);
        if (string.IsNullOrWhiteSpace(messageContent))
            return;

        var senderId = $"spam_{Math.Abs(senderName.GetHashCode())}";

        if (!component.Users.ContainsKey(senderId))
        {
            var spamUser = new MessengerUser(senderId, senderName);
            component.Users.Add(senderId, spamUser);
        }

        SendFakePersonalMessage(uid, user.UserId, senderId, senderName, messageContent);
    }

    private string GetRandomString(string datasetId)
    {
        if (!ProtoMan.TryIndex(datasetId, out LocalizedDatasetPrototype? dataset))
            return "Error";

        var key = _random.Pick(dataset.Values);
        return _loc.GetString(key);
    }
}
