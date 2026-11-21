using Robust.Client.UserInterface.Controls;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>
/// Лента генома: двойная спираль, на которой каждая стадия развития отмечена узлом.
/// Закрытые стадии подсвечены цветом вашей колонии, текущая пульсирует, будущие остаются тусклыми.
/// </summary>
/// <remarks>
/// Лента собрана из отдельных узлов, а не нарисована одним элементом: подсказка в движке
/// привязана к элементу под курсором, поэтому на цельной ленте она не сменялась при переходе
/// между стадиями и появлялась только после того, как курсор замрёт.
/// </remarks>
public sealed class DiseaseGenomeStrip : BoxContainer
{
    /// <summary>Узел ленты: закрытая стадия, текущая или ещё недоступная.</summary>
    public readonly record struct StageNode(bool Done, string Tooltip);

    public DiseaseGenomeStrip()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 0;
        HorizontalExpand = true;
        MinHeight = 54;
    }

    /// <summary>Обновляет ленту по текущему состоянию заготовки.</summary>
    /// <param name="nodes">Стадии по порядку вместе с их подсказками.</param>
    /// <param name="stage">Номер текущей стадии, считая с нуля.</param>
    public void SetGenome(IReadOnlyList<StageNode> nodes, int stage)
    {
        // Узлы переиспользуются, пока их число не менялось: пересборка сбрасывала бы наведение.
        if (ChildCount != nodes.Count)
        {
            RemoveAllChildren();
            for (var i = 0; i < nodes.Count; i++)
                AddChild(new DiseaseGenomeNode());
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (GetChild(i) is not DiseaseGenomeNode node)
                continue;

            node.SetStage(i, nodes[i].Done, i == stage, nodes[i].Tooltip);
        }
    }
}
