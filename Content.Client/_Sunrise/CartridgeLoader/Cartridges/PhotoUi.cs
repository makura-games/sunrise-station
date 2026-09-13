using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared._Sunrise.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.CartridgeLoader.Cartridges;

public sealed partial class PhotoUi : UIFragment
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    private PhotoUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        IoCManager.InjectDependencies(this);
        _fragment = new PhotoUiFragment();
        if (fragmentOwner.HasValue)
            _fragment.SetOwner(fragmentOwner.Value);

        _fragment.OnCapturePhoto += () =>
        {
            if (fragmentOwner.HasValue)
                _entitySystemManager.GetEntitySystem<PhotoCartridgeClientSystem>().CaptureAndSendPhoto(fragmentOwner.Value);

            SendPhotoMessage(PhotoUiAction.CapturePhoto, userInterface);
        };
        _fragment.OnRequestGallery += () =>
            SendPhotoMessage(PhotoUiAction.RequestGallery, userInterface);

        _fragment.OnDeletePhoto += photoId =>
            SendPhotoMessage(PhotoUiAction.DeletePhoto, userInterface, photoId: photoId);

        _fragment.OnToggleFlash += flashEnabled =>
            SendPhotoMessage(PhotoUiAction.ToggleFlash, userInterface, flashEnabled: flashEnabled);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not PhotoUiState photoState)
            return;

        _fragment?.UpdateState(photoState);
    }

    private void SendPhotoMessage(
        PhotoUiAction action,
        BoundUserInterface userInterface,
        string? photoId = null,
        string? recipientId = null,
        string? groupId = null,
        bool? flashEnabled = null)
    {
        var photoMessage = new PhotoUiMessageEvent(action, photoId, recipientId, groupId, flashEnabled);
        var message = new CartridgeUiMessage(photoMessage);
        userInterface.SendMessage(message);
    }
}
