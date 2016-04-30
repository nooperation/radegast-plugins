using System.Collections.Generic;
using System.Linq;

namespace Radegast.Plugin.GroupChatNotifier
{
  [Radegast.Plugin(Name = "GroupChatNotifier Plugin", Description = "Plays a sound whenever a group message is recieved.", Version = "1.0")]
  public class GroupChatNotifier : IRadegastPlugin
  {
    private readonly bool notify_only_on_specific_groups = false;

    /// <summary>
    /// List of words to to trigger on if notify_only_on_specific_groups is true. All group names must be lowercase.
    /// </summary>
    private readonly HashSet<string> groups = new HashSet<string>()
    {
      "example group name"
    };

    private RadegastInstance instance;

    public void StartPlugin(RadegastInstance inst)
    {
      instance = inst;

      UpdateGroupTabNotifications(true);

      inst.TabConsole.OnTabAdded += TabConsole_OnTabAdded;
      inst.TabConsole.OnTabRemoved += TabConsole_OnTabRemoved;
    }

    private void UpdateGroupTabNotifications(bool ding_on_all_incoming)
    {
      var group_tabs = from tab in instance.TabConsole.Tabs
                       where tab.Value.Control is GroupIMTabWindow && (notify_only_on_specific_groups == false || groups.Contains(tab.Value.Label.ToLower())) 
                       select tab.Value;

      foreach (var tab in group_tabs)
      {
        GroupIMTabWindow group_tab_window = tab.Control as GroupIMTabWindow;
        group_tab_window.TextManager.DingOnAllIncoming = ding_on_all_incoming;
      }
    }

    private void TabConsole_OnTabRemoved(object sender, TabEventArgs e)
    {
      UpdateGroupTabNotifications(true);
    }

    private void TabConsole_OnTabAdded(object sender, TabEventArgs e)
    {
      UpdateGroupTabNotifications(true);
    }

    public void StopPlugin(RadegastInstance inst)
    {
      UpdateGroupTabNotifications(false);

      inst.TabConsole.OnTabAdded -= TabConsole_OnTabAdded;
      inst.TabConsole.OnTabRemoved -= TabConsole_OnTabRemoved;
    }
  }
}
