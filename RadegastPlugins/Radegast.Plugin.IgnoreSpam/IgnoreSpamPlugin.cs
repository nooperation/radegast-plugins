using OpenMetaverse;

namespace Radegast.Plugin.IgnoreSpamPlugin
{
    [Radegast.Plugin(Name = "IgnoreSpam Plugin", Description = "IgnoreSpam plugin.", Version = "1.0")]
    public class ReconnectPlugin : IRadegastPlugin
    {
        public RadegastInstance Instance { get; set; }

        private void Output(string message)
        {
            Instance.TabConsole.DisplayNotificationInChat(message, ChatBufferTextStyle.StatusBlue);
        }

        public void StartPlugin(RadegastInstance inst)
        {
            Instance = inst;
            Instance.Netcom.InstantMessageReceived += Netcom_InstantMessageReceived;
        }

        private void Netcom_InstantMessageReceived(object sender, InstantMessageEventArgs e)
        {
            if (e.IM.Dialog == InstantMessageDialog.SessionSend)
            {
                if (Instance.Groups.ContainsKey(e.IM.IMSessionID) == false)
                {
                    Output("Conference from secondlife:///app/agent/" + e.IM.FromAgentID + "/about : " + e.IM.Message);
                    var tab = Instance.TabConsole.GetTab(e.IM.IMSessionID.ToString());
                    if (tab != null)
                    {
                        tab.Close();
                    }
                }
            }
        }

        public void StopPlugin(RadegastInstance inst)
        {
            Instance.Netcom.InstantMessageReceived -= Netcom_InstantMessageReceived;
        }
    }
}
