namespace Content.Client.Options.UI;

public sealed partial class OptionsMenu
{
    [Dependency] private ILocalizationManager _loc = default!;

    private void SetTabsName()
    {
        Tabs.SetTabTitle(0, _loc.GetString("ui-options-tab-extra"));
        Tabs.SetTabTitle(1, _loc.GetString("ui-options-tab-misc"));
        Tabs.SetTabTitle(2, _loc.GetString("ui-options-tab-graphics"));
        Tabs.SetTabTitle(3, _loc.GetString("ui-options-tab-controls"));
        Tabs.SetTabTitle(4, _loc.GetString("ui-options-tab-audio"));
        Tabs.SetTabTitle(5, _loc.GetString("ui-options-tab-accessibility"));
        Tabs.SetTabTitle(6, _loc.GetString("ui-options-tab-admin"));
    }
}
