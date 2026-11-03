using System;
using Content.Shared._Sunrise.Helpers;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private void RefreshPlayerInfo()
    {
        var session = _player.LocalSession;
        if (session == null)
        {
            SetAccountValue(MainPlayerNameValue, Loc.GetString("donation-terminal-unavailable"));
            SetAccountValue(MainPlayerIdValue, Loc.GetString("donation-terminal-unavailable"));
            return;
        }

        SetAccountValue(MainPlayerNameValue, session.Name);
        SetAccountValue(MainPlayerIdValue, session.UserId.ToString());
    }

    private void OnBindingsChanged(AccountBindingsSnapshot snapshot)
    {
        RefreshBindings(snapshot);
    }

    private void RefreshBindings(AccountBindingsSnapshot snapshot)
    {
        SetBindingValue(MainDiscordValue, snapshot.Discord);
        SetBindingValue(MainTelegramValue, snapshot.Telegram);
        SetBindingValue(MainGithubValue, snapshot.Github);
    }

    private void SetBindingValue(Label label, AccountBindingEntry entry)
    {
        if (entry.State == AccountBindingState.Unavailable)
        {
            SetAccountValue(label, Loc.GetString("donation-terminal-unavailable"));
            return;
        }

        if (entry.State == AccountBindingState.Unlinked)
        {
            SetAccountValue(label, Loc.GetString("donation-terminal-main-managed-on-site"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(entry.DisplayValue))
        {
            SetAccountValue(label, entry.DisplayValue);
            return;
        }

        SetAccountValue(label, Loc.GetString("donation-terminal-main-managed-on-site"));
    }

    private void SetAccountValue(Label label, string text)
    {
        label.Text = text;
        label.ToolTip = text;
    }
}
