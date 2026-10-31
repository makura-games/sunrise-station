using System.Linq;
using Content.Shared._Sunrise.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;

namespace Content.Server._Sunrise.CartridgeLoader.Cartridges;

/// <summary>
/// Часть системы картриджа мессенджера, отвечающая за обновление UI состояния
/// </summary>
public sealed partial class MessengerCartridgeSystem
{
    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, MessengerCartridgeComponent? component, Dictionary<string, PhotoMetadata>? photoGallery = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Messenger работает в фоне, но UI-state нужен только активному открытому окну программы.
        if (!CanUpdateUiState(uid, loaderUid, component))
            return;

        var unreadCounts = new Dictionary<string, int>();
        if (component.ServerAddress != null && component.UserId != null)
        {
            foreach (var (chatId, messages) in component.MessageHistory)
            {
                if (messages.Count == 0)
                    continue;

                if (chatId.StartsWith("personal_"))
                {
                    var unreadCount = messages.Count(m => !m.IsRead && m.RecipientId == component.UserId && !string.IsNullOrEmpty(m.RecipientId));

                    if (component.ServerUnreadCounts.TryGetValue(chatId, out var serverCount) && serverCount > unreadCount)
                    {
                        unreadCount = serverCount;
                    }

                    if (unreadCount > 0)
                    {
                        unreadCounts[chatId] = unreadCount;
                    }
                }
            }

            foreach (var (chatId, count) in component.ServerUnreadCounts)
            {
                if (!chatId.StartsWith("personal_") && count > 0)
                {
                    unreadCounts[chatId] = count;
                }
            }
        }

        var state = new MessengerUiState(
            component.IsRegistered,
            component.ServerAddress != null,
            component.UserId,
            component.Users,
            component.Groups,
            component.MessageHistory,
            component.MutedPersonalChats,
            component.MutedGroupChats,
            unreadCounts,
            component.ActiveInvites,
            component.PinnedChats,
            photoGallery,
            _photoUploadEnabled
        );

        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private bool CanUpdateUiState(EntityUid uid, EntityUid loaderUid, MessengerCartridgeComponent component)
    {
        return component.UiReady &&
               TryComp<CartridgeLoaderComponent>(loaderUid, out var loader) &&
               loader.ActiveProgram == uid &&
               _ui.IsUiOpen(loaderUid, loader.UiKey);
    }

    private void ToggleMute(EntityUid uid, MessengerCartridgeComponent component, string chatId, bool isMuted)
    {
        var isGroup = component.Groups.Any(g => g.GroupId == chatId);

        if (isGroup)
        {
            if (isMuted)
                component.MutedGroupChats.Add(chatId);
            else
                component.MutedGroupChats.Remove(chatId);
        }
        else
        {
            if (isMuted)
                component.MutedPersonalChats.Add(chatId);
            else
                component.MutedPersonalChats.Remove(chatId);
        }

        if (component.LoaderUid.HasValue)
            UpdateUiState(uid, component.LoaderUid.Value, component);
    }

    private void TogglePin(EntityUid uid, MessengerCartridgeComponent component, string chatId)
    {
        if (component.PinnedChats.Contains(chatId))
            component.PinnedChats.Remove(chatId);
        else
            component.PinnedChats.Add(chatId);

        if (component.LoaderUid.HasValue)
            UpdateUiState(uid, component.LoaderUid.Value, component);
    }
}
