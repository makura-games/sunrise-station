using System.Linq;
using Content.Shared.Ghost.Systems;
using Content.Shared.Roles;
using GhostWarpPlayer = Content.Shared.Ghost.Systems.SharedGhostSystem.GhostWarpPlayer;
using GhostWarpGlobalAntagonist = Content.Shared.Ghost.Systems.SharedGhostSystem.GhostWarpGlobalAntagonist;
using Robust.Shared.Utility;


namespace Content.Client._Sunrise.UserInterface.Systems.Ghost.Controls;

public sealed partial class SunriseGhostTargetWindow
{
    /// <summary>
    /// Сортирует антагонистов по их приоритету. Чем ниже цифра приоритета, тем выше в списке
    /// </summary>
    /// <returns>Отсортированный список со списками антагонистов. Каждый вложенный список это все сущности данного приоритета</returns>
    private static List<List<GhostWarpGlobalAntagonist>> SortAntagsByPriority(List<GhostWarpGlobalAntagonist> antagonists)
    {
        return antagonists
            .GroupBy(a => a.Priority)
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
    }

    /// <summary>
    /// Сортирует по имени в алфавитном порядке
    /// </summary>
    private static List<T> GetSortedByName<T>(List<T> items) where T : SharedGhostSystem.INamedGhostWarp
    {
        return items
            .OrderBy(i => i.Name)
            .ToList();
    }

    /// <summary>
    /// Создает название для кнопки с игроком. Вставляет перед именем иконку профессии через теги
    /// <remarks>
    /// Кнопка должна поддерживать <see cref="RichTextLabel"/>, чтобы иконка отображалась
    /// </remarks>
    /// </summary>
    /// <returns>Сгенерированное название с иконкой</returns>
    private string GeneratePlayerLabel(GhostWarpPlayer warp)
    {
        var playerName = FormattedMessage.EscapeText(UiTextHelper.TruncateWithEllipsis(warp.Name, MaxLenght));
        var jobIcon = _chatIcons.GetJobIcon(warp.JobId, 3);

        return $"{jobIcon} {playerName}";
    }

    /// <summary>
    /// Создает подсказку для кнопки-игрока, содержащую его имя и название работы
    /// </summary>
    /// <returns>Сгенерированную строку для подсказки</returns>
    private string GeneratePlayerTooltip(GhostWarpPlayer warp)
    {
        var jobName = _prototype.TryIndex(warp.JobId, out var jobPrototype)
            ? jobPrototype.LocalizedName
            : Loc.GetString("ghost-panel-unknown-job");

        // К сожалению тултипы это очко, я не хочу туда лезть с ричтекстом
        // var jobIcon = _chatIcons.GetJobIcon(warp.JobId, 3);

        return GenerateGenericTooltip(warp.Name, jobName.ToUpperInvariant());
    }

    /// <summary>
    /// Создает базовую подсказку, содержащую название и описание цели для телепорта в отформатированном формате
    /// </summary>
    /// <returns>Сгенерированную строку для подсказки</returns>
    private static string GenerateGenericTooltip(string fullName, string additionalInfo)
    {
        return $"{fullName}\n{additionalInfo}";
    }

    /// <summary>
    /// Сортирует игроков по департаментам
    /// </summary>
    /// <remarks>
    /// НЕ сортирует по значимости департамента. Департаменты в словаре хранятся в случайном порядке.
    /// </remarks>
    /// <param name="players">Список всех варпов-игроков</param>
    /// <returns>Словарь, где ключом является прототип департамента, а значением список всех игроков в этом департаменте</returns>
    private Dictionary<DepartmentPrototype, List<GhostWarpPlayer>> GroupPlayersByDepartment(List<GhostWarpPlayer> players)
    {
        var result = new Dictionary<DepartmentPrototype, List<GhostWarpPlayer>>();

        foreach (var player in players)
        {
            if (!_prototype.TryIndex(player.DepartmentId, out var department))
                continue;

            if (!result.TryGetValue(department, out var list))
            {
                list = [];
                result[department] = list;
            }

            list.Add(player);
        }

        return result;
    }
}
