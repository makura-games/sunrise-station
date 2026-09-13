using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidCharacterAppearance
{
    /// <summary>
    /// Создаёт маркировки волос для случайно сгенерированных гуманоидов.
    /// </summary>
    private static Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> CreateRandomHairMarkings(
        ProtoId<SpeciesPrototype> species,
        Sex sex,
        IRobustRandom random,
        MarkingManager markingManager)
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var markingData = markingManager.GetMarkingData(species);
        var markings = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();

        var hairColor = random.Pick(HairStyles.RealisticHairColors);
        hairColor = hairColor
            .WithRed(RandomizeColor(hairColor.R))
            .WithGreen(RandomizeColor(hairColor.G))
            .WithBlue(RandomizeColor(hairColor.B));

        AddRandomHairMarking(HumanoidVisualLayers.Hair);

        // Сохраняем поведение старого рандомайзера: женским профилям борода не выдаётся.
        if (sex != Sex.Female)
            AddRandomHairMarking(HumanoidVisualLayers.FacialHair);

        return markings;

        void AddRandomHairMarking(HumanoidVisualLayers layer)
        {
            foreach (var (organ, organData) in markingData)
            {
                if (!organData.Layers.Contains(layer) ||
                    !prototypeManager.TryIndex(organData.Group, out var group) ||
                    !group.Limits.TryGetValue(layer, out var limit) ||
                    limit.Limit <= 0)
                {
                    continue;
                }

                var available = markingManager.MarkingsByLayerAndGroupAndSex(layer, organData.Group, sex);
                if (available.Count == 0)
                    continue;

                var marking = random.Pick(available.Values.ToArray()).AsMarking();
                marking.SetColor(hairColor);

                if (!markings.TryGetValue(organ, out var organMarkings))
                {
                    organMarkings = [];
                    markings[organ] = organMarkings;
                }

                organMarkings[layer] = new List<Marking> { marking };
                return;
            }
        }

        float RandomizeColor(float channel)
        {
            return MathHelper.Clamp01(channel + random.Next(-25, 25) / 100f);
        }
    }
}
