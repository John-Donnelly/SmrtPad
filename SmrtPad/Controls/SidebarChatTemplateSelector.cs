using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SmrtPad.Controls;

/// <summary>
/// Selects the correct <see cref="DataTemplate"/> for user and assistant chat bubbles.
/// </summary>
public sealed class SidebarChatTemplateSelector : DataTemplateSelector
{
    /// <summary>Template applied to <see cref="SidebarChatRole.User"/> entries.</summary>
    public DataTemplate? UserTemplate { get; set; }

    /// <summary>Template applied to <see cref="SidebarChatRole.Assistant"/> entries.</summary>
    public DataTemplate? AssistantTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is SidebarChatEntry { Role: SidebarChatRole.User })
            return UserTemplate;

        return AssistantTemplate;
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
