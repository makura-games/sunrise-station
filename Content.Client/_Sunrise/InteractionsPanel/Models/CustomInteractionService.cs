using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.InteractionsPanel.Models;

public sealed partial class CustomInteractionService
{
    [Dependency] private IResourceManager _resourceManager = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly Dictionary<string, CustomInteraction> _customInteractions = new();
    private readonly ISawmill _sawmill;
    private readonly ResPath _customInteractionsDirectory = new("/CustomInteractions");
    private readonly ResPath _customInteractionsFile;

    public IReadOnlyDictionary<string, CustomInteraction> CustomInteractions => _customInteractions;

    public CustomInteractionService()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _logManager.GetSawmill("interactions.custom");
        _customInteractionsFile = _customInteractionsDirectory / "interactions.txt";

        InitializeDirectory();
        LoadInteractions();
    }

    private void InitializeDirectory()
    {
        if (!_resourceManager.UserData.IsDir(_customInteractionsDirectory))
        {
            _resourceManager.UserData.CreateDir(_customInteractionsDirectory);
        }
    }

    public void SaveInteraction(CustomInteraction interaction)
    {
        if (string.IsNullOrEmpty(interaction.Id))
        {
            interaction.Id = $"custom_{Guid.NewGuid():N}";
        }

        _customInteractions[interaction.Id] = interaction;
        SaveInteractions();
    }

    public void RemoveInteraction(string id)
    {
        if (_customInteractions.Remove(id))
        {
            SaveInteractions();
        }
    }

    public List<CustomInteraction> GetInteractions()
    {
        if (_customInteractions.Count == 0)
        {
            LoadInteractions();
        }
        return _customInteractions.Values.ToList();
    }

    public CustomInteraction? GetInteraction(string id)
    {
        if (_customInteractions.Count == 0)
        {
            LoadInteractions();
        }

        if (_customInteractions.TryGetValue(id, out var interaction))
        {
            return interaction;
        }

        return null;
    }

    public void LoadInteractions()
    {
        _customInteractions.Clear();

        try
        {
            if (!_resourceManager.UserData.Exists(_customInteractionsFile))
            {
                _sawmill.Debug("Custom interactions file not found, creating empty one");
                SaveInteractions();
                return;
            }

            using var fileStream = _resourceManager.UserData.Open(_customInteractionsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);

            CustomInteraction? currentInteraction = null;
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (currentInteraction != null && !string.IsNullOrEmpty(currentInteraction.Id))
                    {
                        _customInteractions[currentInteraction.Id] = currentInteraction;
                        currentInteraction = null;
                    }
                    continue;
                }

                var parts = line.Split(':', 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (key == "ID")
                {
                    if (currentInteraction != null && !string.IsNullOrEmpty(currentInteraction.Id))
                    {
                        _customInteractions[currentInteraction.Id] = currentInteraction;
                    }

                    currentInteraction = new CustomInteraction
                    {
                        Id = value,
                        InteractionMessages = new List<string>(),
                        SoundIds = new List<string>()
                    };
                    continue;
                }

                if (currentInteraction == null)
                    continue;

                switch (key)
                {
                    case "NAME":
                        currentInteraction.Name = value;
                        break;
                    case "DESCRIPTION":
                        currentInteraction.Description = value;
                        break;
                    case "ICON":
                        currentInteraction.IconId = value;
                        break;
                    case "CATEGORY":
                        currentInteraction.CategoryId = value;
                        break;
                    case "MESSAGE":
                        currentInteraction.InteractionMessages.Add(value);
                        break;
                    case "SOUND":
                        if (!string.IsNullOrEmpty(value))
                            currentInteraction.SoundIds.Add(value);
                        break;
                    case "SPAWNS_EFFECT":
                        currentInteraction.SpawnsEffect = bool.TryParse(value, out var spawnsEffect) && spawnsEffect;
                        break;
                    case "EFFECT_CHANCE":
                        currentInteraction.EffectChance = float.TryParse(value, out var effectChance) ? effectChance : 0;
                        break;
                    case "EFFECT_ID":
                        currentInteraction.EntityEffectId = value;
                        break;
                    case "COOLDOWN":
                        currentInteraction.Cooldown = float.TryParse(value, out var cooldown) ? cooldown : 5;
                        break;
                }
            }

            if (currentInteraction != null && !string.IsNullOrEmpty(currentInteraction.Id))
            {
                _customInteractions[currentInteraction.Id] = currentInteraction;
            }

            _sawmill.Debug($"Loaded {_customInteractions.Count} custom interactions");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error loading custom interactions: {e}");
        }
    }

    private void SaveInteractions()
    {
        try
        {
            using var fileStream = _resourceManager.UserData.Open(_customInteractionsFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(fileStream);

            foreach (var interaction in _customInteractions.Values)
            {
                writer.WriteLine($"ID:{interaction.Id}");
                writer.WriteLine($"NAME:{interaction.Name}");
                writer.WriteLine($"DESCRIPTION:{interaction.Description}");
                writer.WriteLine($"ICON:{interaction.IconId}");
                writer.WriteLine($"CATEGORY:{interaction.CategoryId}");

                foreach (var message in interaction.InteractionMessages)
                {
                    writer.WriteLine($"MESSAGE:{message}");
                }

                foreach (var soundId in interaction.SoundIds)
                {
                    writer.WriteLine($"SOUND:{soundId}");
                }

                writer.WriteLine($"SPAWNS_EFFECT:{interaction.SpawnsEffect}");
                writer.WriteLine($"EFFECT_CHANCE:{interaction.EffectChance}");
                writer.WriteLine($"EFFECT_ID:{interaction.EntityEffectId}");
                writer.WriteLine($"COOLDOWN:{interaction.Cooldown}");

                writer.WriteLine();
            }

            _sawmill.Debug($"Saved {_customInteractions.Count} custom interactions");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error saving custom interactions: {e}");
        }
    }
}
