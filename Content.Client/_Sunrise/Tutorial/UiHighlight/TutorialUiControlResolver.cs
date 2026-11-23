using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Client.UserInterface;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Content.Shared.Alert;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Tutorial.UiHighlight;

public sealed class TutorialUiControlResolver(IEntityManager entityManager)
{
    private readonly IEntityManager _entityManager = entityManager;

    public bool TryFind(
        Control root,
        IReadOnlyList<TutorialUiHighlightSelector> selectors,
        [NotNullWhen(true)] out Control? control)
    {
        if (selectors.Count == 0)
        {
            control = null;
            return false;
        }

        control = root;

        for (var i = 0; i < selectors.Count; i++)
        {
            if (!TryResolveSelector(control, selectors[i], out control))
                return false;
        }

        return true;
    }

    private bool TryResolveSelector(
        Control root,
        TutorialUiHighlightSelector selector,
        [NotNullWhen(true)] out Control? control)
    {
        switch (selector)
        {
            case UiByName byName:
                if (string.IsNullOrWhiteSpace(byName.Name))
                    break;

                return TryFindControlByName(root, byName.Name, out control);

            case UiByChildIndex byChildIndex:
                if (byChildIndex.Index < 0 || byChildIndex.Index >= root.ChildCount)
                    break;

                control = root.GetChild(byChildIndex.Index);
                return true;

            case UiByType byType:
                return TryFindControlByType(root, byType.ControlType, byType.Index, out control);

            case UiByEntityPrototype byEntityPrototype:
                return TryFindControlByEntityPrototype(root, byEntityPrototype.Prototype, byEntityPrototype.Index, out control);

            case UiByAlertPrototype byAlertPrototype:
                return TryFindControlByAlertPrototype(root, byAlertPrototype.Prototype, byAlertPrototype.Index, out control);
        }

        control = null;
        return false;
    }

    private static bool TryFindControlByName(Control root, string name, [NotNullWhen(true)] out Control? control)
    {
        if (root.Name == name)
        {
            control = root;
            return true;
        }

        foreach (var child in root.Children)
        {
            if (TryFindControlByName(child, name, out control))
                return true;
        }

        control = null;
        return false;
    }

    private static bool TryFindControlByType(
        Control root,
        string controlType,
        int targetIndex,
        [NotNullWhen(true)] out Control? control)
    {
        if (string.IsNullOrWhiteSpace(controlType) || targetIndex < 0)
        {
            control = null;
            return false;
        }

        var currentIndex = 0;
        return TryFindDescendant(root, candidate =>
        {
            if (!TypeMatches(candidate, controlType))
                return false;

            return currentIndex++ == targetIndex;
        }, out control);
    }

    private bool TryFindControlByEntityPrototype(
        Control root,
        EntProtoId prototype,
        int targetIndex,
        [NotNullWhen(true)] out Control? control)
    {
        if (targetIndex < 0)
        {
            control = null;
            return false;
        }

        var currentIndex = 0;
        return TryFindDescendant(root, candidate =>
        {
            if (candidate is not IEntityControl entityControl || entityControl.UiEntity is not { } entity)
                return false;

            if (!_entityManager.TryGetComponent(entity, out MetaDataComponent? meta) ||
                meta.EntityPrototype == null)
            {
                return false;
            }

            if (!string.Equals(meta.EntityPrototype.ID, prototype.Id, StringComparison.OrdinalIgnoreCase))
                return false;

            return currentIndex++ == targetIndex;
        }, out control);
    }

    private static bool TryFindControlByAlertPrototype(
        Control root,
        ProtoId<AlertPrototype> prototype,
        int targetIndex,
        [NotNullWhen(true)] out Control? control)
    {
        if (targetIndex < 0)
        {
            control = null;
            return false;
        }

        var currentIndex = 0;
        return TryFindDescendant(root, candidate =>
        {
            if (candidate is not AlertControl alertControl)
                return false;

            if (!string.Equals(alertControl.Alert.ID, prototype.Id, StringComparison.OrdinalIgnoreCase))
                return false;

            return currentIndex++ == targetIndex;
        }, out control);
    }

    private static bool TryFindDescendant(
        Control root,
        Func<Control, bool> predicate,
        [NotNullWhen(true)] out Control? control)
    {
        if (predicate(root))
        {
            control = root;
            return true;
        }

        foreach (var child in root.Children)
        {
            if (TryFindDescendant(child, predicate, out control))
                return true;
        }

        control = null;
        return false;
    }

    private static bool TypeMatches(Control control, string typeName)
    {
        for (var type = control.GetType(); type != null; type = type.BaseType)
        {
            if (string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type.FullName, typeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
