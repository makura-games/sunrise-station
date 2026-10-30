using System.Collections.Generic;
using System.Numerics;
using Content.Client._Sunrise.Sheetlets;
using Content.Client._Sunrise.Sheetlets.SciFiStyle;
using Content.Client._Sunrise.TTS;
using Content.Client.Humanoid;
using Content.Client.Lobby;
using Content.Shared._Sunrise.GhostTheme;
using Content.Shared._Sunrise.Helpers;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Body;
using Content.Shared.Ghost.Roles;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    // Внутренний экран подробностей спонсорских подписок и построение списков преимуществ.
    [Dependency] private IClientPreferencesManager _preferences = default!;
    [Dependency] private MarkingManager _markingManager = default!;

    private const int SponsorTierDetailsBuildBudget = 10;
    private const int SponsorTierVoiceNameMaxLength = 22;
    private const float SponsorTierVoiceButtonHeight = 40f;
    private const float SponsorTierVoiceButtonWidth = 180f;
    private static readonly StyleBoxFlat SponsorTierVisualPanelStyle = new()
    {
        BackgroundColor = SciFiPalette.PanelBackgroundDark,
        BorderColor = SciFiPalette.BorderDim,
        BorderThickness = new Thickness(1),
    };
    private readonly List<EntityUid> _sponsorTierPreviewEntities = new();
    private readonly List<SpriteView> _sponsorTierSpriteViews = new();
    private readonly Queue<SponsorTierDetailsBuildRequest> _sponsorTierDetailsBuildQueue = new();
    private DonationTerminalTab _subscriptionDetailsReturnTab = DonationTerminalTab.Main;
    private int? _selectedSubscriptionDetailsTier;
    private HumanoidCharacterProfile? _sponsorTierPreviewProfile;
    private LobbyUIController? _sponsorTierLobbyController;
    private Direction _sponsorTierPreviewDirection = Direction.South;
    private float _sponsorTierPreviewTime;

    private void OpenSponsorTierDetails()
    {
        var sponsorTier = GetVisibleSponsorTier(_currentSponsorTier) ?? GetHighestSponsorTier();
        if (sponsorTier == null)
            return;

        ShowSponsorTierDetails(sponsorTier);
    }

    private void OpenSponsorTierDetails(int sponsorTier)
    {
        var tier = GetVisibleSponsorTier(sponsorTier);
        if (tier == null)
            return;

        ShowSponsorTierDetails(tier);
    }

    private void ShowSponsorTierDetails(SponsorInfo sponsorTier)
    {
        _subscriptionDetailsReturnTab = _selectedTab;
        _selectedSubscriptionDetailsTier = sponsorTier.Tier;

        MainContent.Visible = false;
        ShopContent.Visible = false;
        SubscriptionsContent.Visible = false;
        PurchaseConfirmationContent.Visible = false;
        SubscriptionDetailsContent.Visible = true;
        Footer.Text = Loc.GetString("donation-terminal-footer-subscription-details");

        RefreshSponsorTierDetails();
    }

    private void HideSponsorTierDetails()
    {
        _selectedSubscriptionDetailsTier = null;
        SelectTab(_subscriptionDetailsReturnTab);
    }

    private void SelectSponsorTierDetails(int sponsorTier)
    {
        if (GetSponsorTier(sponsorTier) == null)
            return;

        _selectedSubscriptionDetailsTier = sponsorTier;
        RefreshSponsorTierDetails();
    }

    private void OnSponsorTierPreferencesLoaded()
    {
        RefreshSponsorTierDetails();
    }

    private void RefreshSponsorTierDetails()
    {
        if (!SubscriptionDetailsContent.Visible)
            return;

        var sponsorTier = _selectedSubscriptionDetailsTier == null
            ? null
            : GetVisibleSponsorTier(_selectedSubscriptionDetailsTier.Value);

        sponsorTier ??= GetHighestSponsorTier();
        if (sponsorTier == null)
        {
            HideSponsorTierDetails();
            return;
        }

        _selectedSubscriptionDetailsTier = sponsorTier.Tier;
        PopulateSponsorTierButtons(sponsorTier.Tier);
        PopulateSponsorTierBenefits(sponsorTier);
    }

    private SponsorInfo? GetHighestSponsorTier()
    {
        SponsorInfo? highestTier = null;
        foreach (var sponsorTier in GetVisibleSponsorTiers())
        {
            if (highestTier == null || sponsorTier.Tier > highestTier.Tier)
                highestTier = sponsorTier;
        }

        return highestTier;
    }

    private void PopulateSponsorTierButtons(int selectedTier)
    {
        SubscriptionDetailsTierButtons.RemoveAllChildren();

        var tiers = GetVisibleSponsorTiers();
        tiers.Sort((left, right) => right.Tier.CompareTo(left.Tier));

        foreach (var sponsorTier in tiers)
        {
            var tier = sponsorTier.Tier;
            var button = new Button
            {
                MinWidth = 150,
                Text = GetSponsorTierName(sponsorTier),
                TextAlign = Label.AlignMode.Center,
                StyleClasses =
                {
                    SunriseStyleClass.StyleClassSciFiDonateButton,
                },
            };

            SetTabActive(button, tier == selectedTier);
            button.OnPressed += _ => SelectSponsorTierDetails(tier);
            SubscriptionDetailsTierButtons.AddChild(button);
        }
    }

    private void PopulateSponsorTierBenefits(SponsorInfo sponsorTier)
    {
        SubscriptionDetailsBenefitsList.RemoveAllChildren();
        ClearSponsorTierDetailsPreviews();
        _sponsorTierPreviewProfile = (HumanoidCharacterProfile?)_preferences.Preferences?.SelectedCharacter;
        _sponsorTierLobbyController = UserInterfaceManager.GetUIController<LobbyUIController>();

        AddSponsorTierInfoSection(sponsorTier);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-open-antags",
            "sponsor-tiers-gui-open-antags-description",
            sponsorTier.OpenAntags,
            SponsorTierDetailsBuildKind.Antag);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-priority-antags",
            "sponsor-tiers-gui-priority-antags-description",
            sponsorTier.PriorityAntags,
            SponsorTierDetailsBuildKind.Antag);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-open-ghost-roles",
            "sponsor-tiers-gui-open-ghost-roles-description",
            sponsorTier.OpenGhostRoles,
            SponsorTierDetailsBuildKind.GhostRole);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-priority-ghost-roles",
            "sponsor-tiers-gui-priority-ghost-roles-description",
            sponsorTier.PriorityGhostRoles,
            SponsorTierDetailsBuildKind.GhostRole);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-ghost-themes",
            "sponsor-tiers-gui-ghost-themes-description",
            sponsorTier.GhostThemes,
            SponsorTierDetailsBuildKind.GhostTheme);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-open-roles",
            "sponsor-tiers-gui-open-roles-description",
            sponsorTier.OpenRoles,
            SponsorTierDetailsBuildKind.Job);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-priority-roles",
            "sponsor-tiers-gui-priority-roles-description",
            sponsorTier.PriorityRoles,
            SponsorTierDetailsBuildKind.Job);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-bypass-roles",
            "sponsor-tiers-gui-bypass-roles-description",
            sponsorTier.BypassRoles,
            SponsorTierDetailsBuildKind.Job);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-allowed-markings",
            "sponsor-tiers-gui-allowed-markings-description",
            sponsorTier.AllowedMarkings,
            SponsorTierDetailsBuildKind.Marking);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-allowed-species",
            "sponsor-tiers-gui-allowed-species-description",
            sponsorTier.AllowedSpecies,
            SponsorTierDetailsBuildKind.Species);
        QueueSponsorTierVisualSection(
            "sponsor-tiers-gui-tts-voices",
            "sponsor-tiers-gui-tts-voices-description",
            sponsorTier.AllowedVoices,
            SponsorTierDetailsBuildKind.Voice);
        QueueSponsorTierVisualSection(
            "donation-terminal-subscription-details-pets",
            "donation-terminal-subscription-details-pets-description",
            sponsorTier.Pets,
            SponsorTierDetailsBuildKind.Entity);
    }

    private void AddSponsorTierInfoSection(SponsorInfo sponsorTier)
    {
        var section = CreateSponsorTierSection("sponsor-tiers-gui-entry-info", null);
        AddSponsorTierInfoRow(section, "sponsor-tiers-gui-tier", sponsorTier.Tier.ToString());
        AddSponsorTierInfoRow(
            section,
            "sponsor-tiers-gui-ooc-color",
            string.IsNullOrWhiteSpace(sponsorTier.OOCColor)
                ? Loc.GetString("donation-terminal-unavailable")
                : sponsorTier.OOCColor,
            GetSponsorTierOocColor(sponsorTier.OOCColor));
        AddSponsorTierInfoRow(section, "sponsor-tiers-gui-extra-slots", sponsorTier.ExtraSlots.ToString());
        AddSponsorTierInfoRow(
            section,
            "sponsor-tiers-gui-priority-join",
            GetSponsorTierBooleanText(sponsorTier.HavePriorityJoin));
        AddSponsorTierInfoRow(
            section,
            "sponsor-tiers-gui-allowed-respawn",
            GetSponsorTierBooleanText(sponsorTier.AllowedRespawn));
        AddSponsorTierInfoRow(
            section,
            "sponsor-tiers-gui-allowed-flavor",
            GetSponsorTierBooleanText(sponsorTier.AllowedFlavor));
        AddSponsorTierInfoRow(section, "sponsor-tiers-gui-size-flavor", sponsorTier.SizeFlavor.ToString());
        SubscriptionDetailsBenefitsList.AddChild(section);
    }

    private static Color? GetSponsorTierOocColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;

        return Color.TryFromHex(color, out var oocColor)
            ? oocColor
            : null;
    }

    private string GetSponsorTierBooleanText(bool value)
    {
        return Loc.GetString(value ? "sponsor-tiers-gui-yes" : "sponsor-tiers-gui-no");
    }

    private void AddSponsorTierInfoRow(BoxContainer section, string titleKey, string value, Color? valueColor = null)
    {
        var row = new BoxContainer
        {
            HorizontalExpand = true,
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        row.AddChild(new Label
        {
            FontColorOverride = SciFiPalette.TextMuted,
            Text = Loc.GetString(titleKey),
        });
        row.AddChild(new Label
        {
            FontColorOverride = valueColor ?? SciFiPalette.Text,
            Text = value,
        });
        section.AddChild(row);
    }

    private void QueueSponsorTierVisualSection(
        string titleKey,
        string descriptionKey,
        IReadOnlyCollection<string> prototypeIds,
        SponsorTierDetailsBuildKind kind)
    {
        if (prototypeIds.Count == 0)
            return;

        Container entriesContainer;
        if (kind == SponsorTierDetailsBuildKind.Voice)
        {
            entriesContainer = new WrapContainer
            {
                CrossSeparationOverride = 8,
                EqualSize = true,
                HorizontalExpand = true,
                Justification = WrapContainer.ItemJustification.Center,
                LayoutAxis = Axis.Horizontal,
                SeparationOverride = 8,
            };
        }
        else
        {
            entriesContainer = new GridContainer
            {
                Columns = 4,
                HSeparationOverride = 8,
                HorizontalAlignment = HAlignment.Center,
                VSeparationOverride = 8,
            };
        }

        var validEntries = 0;
        foreach (var prototypeId in prototypeIds)
        {
            if (!CanBuildSponsorTierDetailsEntry(kind, prototypeId))
                continue;

            var placeholder = CreateSponsorTierDetailsPlaceholder(kind);
            entriesContainer.AddChild(placeholder);
            _sponsorTierDetailsBuildQueue.Enqueue(new SponsorTierDetailsBuildRequest(
                kind,
                prototypeId,
                placeholder));
            validEntries++;
        }

        if (validEntries == 0)
            return;

        var section = CreateSponsorTierSection(titleKey, descriptionKey);
        section.AddChild(entriesContainer);
        SubscriptionDetailsBenefitsList.AddChild(section);
    }

    private void ProcessSponsorTierDetailsBuild()
    {
        if (!SubscriptionDetailsContent.Visible || _sponsorTierDetailsBuildQueue.Count == 0)
            return;

        var remainingBudget = SponsorTierDetailsBuildBudget;
        while (_sponsorTierDetailsBuildQueue.TryPeek(out var request))
        {
            var cost = GetSponsorTierDetailsBuildCost(request.Kind);
            if (cost > remainingBudget && remainingBudget < SponsorTierDetailsBuildBudget)
                break;

            _sponsorTierDetailsBuildQueue.Dequeue();
            remainingBudget -= cost;

            var entry = CreateSponsorTierDetailsEntry(request.Kind, request.PrototypeId);
            if (entry != null)
                request.Placeholder.AddChild(entry);
        }
    }

    private static int GetSponsorTierDetailsBuildCost(SponsorTierDetailsBuildKind kind)
    {
        return kind is SponsorTierDetailsBuildKind.Entity
            or SponsorTierDetailsBuildKind.GhostRole
            or SponsorTierDetailsBuildKind.Job
            or SponsorTierDetailsBuildKind.Marking
            or SponsorTierDetailsBuildKind.Species
            ? SponsorTierDetailsBuildBudget
            : 1;
    }

    private bool CanBuildSponsorTierDetailsEntry(SponsorTierDetailsBuildKind kind, string prototypeId)
    {
        return kind switch
        {
            SponsorTierDetailsBuildKind.Antag => _prototype.HasIndex<AntagPrototype>(prototypeId),
            SponsorTierDetailsBuildKind.Entity => _prototype.HasIndex<EntityPrototype>(prototypeId),
            SponsorTierDetailsBuildKind.GhostRole => true,
            SponsorTierDetailsBuildKind.GhostTheme => _prototype.HasIndex<GhostThemePrototype>(prototypeId),
            SponsorTierDetailsBuildKind.Job => _sponsorTierLobbyController != null &&
                                                _prototype.HasIndex<JobPrototype>(prototypeId),
            SponsorTierDetailsBuildKind.Marking => CanCreateSponsorTierMarkingPreview(prototypeId),
            SponsorTierDetailsBuildKind.Species => _prototype.HasIndex<SpeciesPrototype>(prototypeId),
            SponsorTierDetailsBuildKind.Voice => _prototype.HasIndex<TTSVoicePrototype>(prototypeId),
            _ => false,
        };
    }

    private bool CanCreateSponsorTierMarkingPreview(string prototypeId)
    {
        return _prototype.HasIndex<MarkingPrototype>(prototypeId) &&
               _prototype.HasIndex<SpeciesPrototype>(HumanoidCharacterProfile.DefaultSpecies);
    }

    private static PanelContainer CreateSponsorTierDetailsPlaceholder(SponsorTierDetailsBuildKind kind)
    {
        if (kind == SponsorTierDetailsBuildKind.Voice)
        {
            return new PanelContainer
            {
                SetSize = new Vector2(SponsorTierVoiceButtonWidth, SponsorTierVoiceButtonHeight),
            };
        }

        return CreateSponsorTierVisualPanel();
    }

    private Control? CreateSponsorTierDetailsEntry(SponsorTierDetailsBuildKind kind, string prototypeId)
    {
        return kind switch
        {
            SponsorTierDetailsBuildKind.Antag => CreateSponsorTierAntagEntry(prototypeId),
            SponsorTierDetailsBuildKind.Entity => CreateSponsorTierEntityEntry(prototypeId),
            SponsorTierDetailsBuildKind.GhostRole => CreateSponsorTierGhostRoleEntry(prototypeId),
            SponsorTierDetailsBuildKind.GhostTheme => CreateSponsorTierGhostThemeEntry(prototypeId),
            SponsorTierDetailsBuildKind.Job => CreateSponsorTierJobEntry(prototypeId),
            SponsorTierDetailsBuildKind.Marking => CreateSponsorTierMarkingEntry(prototypeId),
            SponsorTierDetailsBuildKind.Species => CreateSponsorTierSpeciesEntry(prototypeId),
            SponsorTierDetailsBuildKind.Voice => CreateSponsorTierVoiceEntry(prototypeId),
            _ => null,
        };
    }

    private Control? CreateSponsorTierAntagEntry(string prototypeId)
    {
        if (!_prototype.TryIndex<AntagPrototype>(prototypeId, out var prototype))
            return null;

        return CreateSponsorTierVisualContent(
            Loc.GetString(prototype.Name),
            CreateSponsorTierSpritePreview(prototype.PreviewIcon));
    }

    private Control? CreateSponsorTierEntityEntry(string prototypeId)
    {
        if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var prototype))
            return null;

        return CreateSponsorTierVisualContent(
            Loc.GetString($"ent-{prototype.ID}"),
            CreateSponsorTierEntityPreview(prototype.ID));
    }

    private Control CreateSponsorTierGhostRoleEntry(string prototypeId)
    {
        var (name, preview) = CreateSponsorTierGhostRolePreview(prototypeId);
        return CreateSponsorTierVisualContent(name, preview);
    }

    private (string Name, Control Preview) CreateSponsorTierGhostRolePreview(string prototypeId)
    {
        if (_prototype.TryIndex<GhostRolePrototype>(prototypeId, out var ghostRole))
        {
            var previewPrototype = ghostRole.IconPrototype ?? ghostRole.EntityPrototype;
            if (TryCreateSponsorTierEntityPreview(previewPrototype, out var preview))
                return (Loc.GetString(ghostRole.Name), preview);
        }

        if (_prototype.TryIndex<EntityPrototype>(prototypeId, out var entityPrototype) &&
            TryCreateSponsorTierEntityPreview(entityPrototype.ID, out var entityPreview))
        {
            return (Loc.GetString($"ent-{entityPrototype.ID}"), entityPreview);
        }

        if (_prototype.TryIndex<AntagPrototype>(prototypeId, out var antagPrototype))
        {
            return (
                Loc.GetString(antagPrototype.Name),
                CreateSponsorTierSpritePreview(antagPrototype.PreviewIcon));
        }

        if (_prototype.TryIndex<GhostThemePrototype>(prototypeId, out var ghostThemePrototype))
        {
            return (
                Loc.GetString(ghostThemePrototype.Name),
                CreateSponsorTierSpritePreview(ghostThemePrototype.Sprite));
        }

        return (prototypeId, CreateSponsorTierEntityPreview("MobObserver"));
    }

    private Control? CreateSponsorTierGhostThemeEntry(string prototypeId)
    {
        if (!_prototype.TryIndex<GhostThemePrototype>(prototypeId, out var prototype))
            return null;

        return CreateSponsorTierVisualContent(
            Loc.GetString(prototype.Name),
            CreateSponsorTierSpritePreview(prototype.Sprite));
    }

    private Control? CreateSponsorTierJobEntry(string prototypeId)
    {
        if (_sponsorTierLobbyController == null ||
            !_prototype.TryIndex<JobPrototype>(prototypeId, out var prototype))
            return null;

        var dummy = _sponsorTierLobbyController.LoadProfileEntity(_sponsorTierPreviewProfile, prototype, true);
        _sponsorTierPreviewEntities.Add(dummy);
        return CreateSponsorTierVisualContent(
            Loc.GetString(prototype.Name),
            CreateSponsorTierSpriteView(dummy));
    }

    private Control? CreateSponsorTierMarkingEntry(string prototypeId)
    {
        if (!_prototype.TryIndex<MarkingPrototype>(prototypeId, out var prototype))
            return null;

        var preview = CreateSponsorTierMarkingPreview(prototype);
        return preview == null
            ? null
            : CreateSponsorTierVisualContent(Loc.GetString($"marking-{prototype.ID}"), preview);
    }

    private Control? CreateSponsorTierSpeciesEntry(string prototypeId)
    {
        if (!_prototype.TryIndex<SpeciesPrototype>(prototypeId, out var prototype))
            return null;

        return CreateSponsorTierVisualContent(
            Loc.GetString(prototype.Name),
            CreateSponsorTierEntityPreview(prototype.DollPrototype));
    }

    private Control? CreateSponsorTierVoiceEntry(string prototypeId)
    {
        if (!_prototype.TryIndex<TTSVoicePrototype>(prototypeId, out var prototype))
            return null;

        var voiceId = prototypeId;
        var voiceName = Loc.GetString(prototype.Name);
        var button = new Button
        {
            ClipText = true,
            MaxHeight = SponsorTierVoiceButtonHeight,
            MaxWidth = SponsorTierVoiceButtonWidth,
            MinHeight = SponsorTierVoiceButtonHeight,
            MinWidth = SponsorTierVoiceButtonWidth,
            SetHeight = SponsorTierVoiceButtonHeight,
            SetWidth = SponsorTierVoiceButtonWidth,
            Text = voiceName.TruncateWithEllipsis(SponsorTierVoiceNameMaxLength),
            TextAlign = Label.AlignMode.Center,
            ToolTip = voiceName,
            StyleClasses =
            {
                SunriseStyleClass.StyleClassSciFiDonateButton,
            },
        };
        button.OnPressed += _ => _entity.System<TTSSystem>().RequestPreviewTts(voiceId);
        return button;
    }

    private BoxContainer CreateSponsorTierSection(string titleKey, string? descriptionKey)
    {
        var section = new BoxContainer
        {
            HorizontalExpand = true,
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
        };

        var header = new BoxContainer
        {
            HorizontalExpand = true,
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
        header.AddChild(CreateSponsorTierSectionDivider());
        header.AddChild(new Label
        {
            FontColorOverride = SciFiPalette.Accent,
            StyleClasses =
            {
                "LabelHeading",
            },
            Text = Loc.GetString(titleKey),
            VAlign = Label.VAlignMode.Center,
        });
        header.AddChild(CreateSponsorTierSectionDivider());
        section.AddChild(header);

        if (descriptionKey != null)
        {
            var description = new RichTextLabel
            {
                HorizontalExpand = true,
            };
            description.SetMessage(
                FormattedMessage.FromUnformatted(Loc.GetString(descriptionKey)),
                SciFiPalette.TextMuted);
            section.AddChild(description);
        }

        return section;
    }

    private static PanelContainer CreateSponsorTierSectionDivider()
    {
        return new PanelContainer
        {
            HorizontalExpand = true,
            MaxHeight = 1,
            MinHeight = 1,
            StyleClasses =
            {
                SunriseStyleClass.SciFiDivider,
            },
            VerticalAlignment = VAlignment.Center,
        };
    }

    private static PanelContainer CreateSponsorTierVisualPanel()
    {
        return new PanelContainer
        {
            Margin = new Thickness(5),
            SetSize = new Vector2(200, 200),
            PanelOverride = SponsorTierVisualPanelStyle,
        };
    }

    private BoxContainer CreateSponsorTierVisualContent(string name, Control preview)
    {
        var content = new BoxContainer
        {
            Align = BoxContainer.AlignMode.Center,
            Margin = new Thickness(5),
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };
        content.AddChild(preview);

        var title = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            MaxWidth = 190,
            StyleClasses =
            {
                "LabelKeyText",
            },
        };
        title.SetMessage(FormattedMessage.FromUnformatted(name), SciFiPalette.Text);
        content.AddChild(title);
        return content;
    }

    private TextureRect CreateSponsorTierSpritePreview(SpriteSpecifier sprite)
    {
        return new TextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            SetSize = new Vector2(128, 128),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            Texture = _entity.System<SpriteSystem>().Frame0(sprite),
            VerticalAlignment = VAlignment.Center,
        };
    }

    private SpriteView CreateSponsorTierEntityPreview(string prototypeId)
    {
        var dummy = _entity.SpawnEntity(prototypeId, MapCoordinates.Nullspace);
        _sponsorTierPreviewEntities.Add(dummy);
        return CreateSponsorTierSpriteView(dummy);
    }

    private bool TryCreateSponsorTierEntityPreview(string prototypeId, out SpriteView preview)
    {
        preview = default!;
        if (!_prototype.HasIndex<EntityPrototype>(prototypeId))
            return false;

        var dummy = _entity.SpawnEntity(prototypeId, MapCoordinates.Nullspace);
        if (!_entity.HasComponent<SpriteComponent>(dummy))
        {
            _entity.DeleteEntity(dummy);
            return false;
        }

        _sponsorTierPreviewEntities.Add(dummy);
        preview = CreateSponsorTierSpriteView(dummy);
        return true;
    }

    private SpriteView? CreateSponsorTierMarkingPreview(MarkingPrototype marking)
    {
        // Маркинги привязаны к группам на органах, поэтому ищем первый вид, чья кукла может принять маркинг.
        foreach (var species in GetSponsorTierMarkingPreviewSpecies())
        {
            var dummy = _entity.SpawnEntity(species.DollPrototype, MapCoordinates.Nullspace);
            if (!TryApplySponsorTierPreviewMarking(dummy, marking))
            {
                _entity.DeleteEntity(dummy);
                continue;
            }

            _sponsorTierPreviewEntities.Add(dummy);
            return CreateSponsorTierSpriteView(dummy);
        }

        return null;
    }

    private IEnumerable<SpeciesPrototype> GetSponsorTierMarkingPreviewSpecies()
    {
        if (_prototype.TryIndex(HumanoidCharacterProfile.DefaultSpecies, out var defaultSpecies))
            yield return defaultSpecies;

        foreach (var species in _prototype.EnumeratePrototypes<SpeciesPrototype>())
        {
            if (species.RoundStart && species.ID != HumanoidCharacterProfile.DefaultSpecies)
                yield return species;
        }
    }

    private bool TryApplySponsorTierPreviewMarking(EntityUid dummy, MarkingPrototype marking)
    {
        var visualBody = _entity.System<SharedVisualBodySystem>();
        if (!visualBody.TryGatherMarkingsData(dummy, null, out _, out var organMarkings, out _))
            return false;

        var sex = marking.SexRestriction ?? Sex.Male;
        foreach (var (organ, markingData) in organMarkings)
        {
            if (!markingData.Layers.Contains(marking.BodyPart) ||
                !_markingManager.CanBeApplied(markingData.Group, sex, marking))
            {
                continue;
            }

            visualBody.ApplyMarkings(dummy,
                new()
                {
                    // Collection expression для List компилируется в CollectionsMarshal.SetCount, а он запрещён песочницей.
                    [organ] = new() { [marking.BodyPart] = new List<Marking> { marking.AsMarking() } },
                });
            return true;
        }

        return false;
    }

    private SpriteView CreateSponsorTierSpriteView(EntityUid dummy)
    {
        var view = new SpriteView
        {
            HorizontalAlignment = HAlignment.Center,
            OverrideDirection = _sponsorTierPreviewDirection,
            Scale = new Vector2(4, 4),
            SetSize = new Vector2(128, 128),
            VerticalAlignment = VAlignment.Center,
        };
        view.SetEntity(dummy);
        _sponsorTierSpriteViews.Add(view);
        return view;
    }

    private void UpdateSponsorTierPreviewDirections(float frameTime)
    {
        if (_sponsorTierSpriteViews.Count == 0)
            return;

        _sponsorTierPreviewTime += frameTime;
        var direction = (Direction)((int)_sponsorTierPreviewTime % 4 * 2);
        if (direction == _sponsorTierPreviewDirection)
            return;

        _sponsorTierPreviewDirection = direction;
        foreach (var view in _sponsorTierSpriteViews)
        {
            view.OverrideDirection = direction;
        }
    }

    private void ClearSponsorTierDetailsPreviews()
    {
        _sponsorTierDetailsBuildQueue.Clear();

        foreach (var entity in _sponsorTierPreviewEntities)
        {
            if (_entity.EntityExists(entity))
                _entity.DeleteEntity(entity);
        }

        _sponsorTierPreviewEntities.Clear();
        _sponsorTierSpriteViews.Clear();
        _sponsorTierPreviewProfile = null;
        _sponsorTierLobbyController = null;
        _sponsorTierPreviewDirection = Direction.South;
        _sponsorTierPreviewTime = 0f;
    }

    private enum SponsorTierDetailsBuildKind : byte
    {
        Antag,
        Entity,
        GhostRole,
        GhostTheme,
        Job,
        Marking,
        Species,
        Voice,
    }

    private readonly record struct SponsorTierDetailsBuildRequest(
        SponsorTierDetailsBuildKind Kind,
        string PrototypeId,
        PanelContainer Placeholder);
}
