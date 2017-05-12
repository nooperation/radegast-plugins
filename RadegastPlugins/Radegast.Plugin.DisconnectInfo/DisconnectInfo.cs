using OpenMetaverse;

namespace Radegast.Plugin.DisconnectInfoPlugin
{
  [Radegast.Plugin(Name = "DisconnectInfo Plugin", Description = "DisconnectInfo plugin.", Version = "1.0")]
  public class DisconnectInfo : IRadegastPlugin
  {
    private RadegastInstance Instance;

    public void StartPlugin(RadegastInstance inst)
    {
      Instance = inst;

      Instance.Client.Network.Disconnected += Network_Disconnected;
      Instance.Netcom.ClientDisconnected += Netcom_ClientDisconnected;
      Instance.Netcom.ClientLoginStatus += Netcom_ClientLoginStatus;
    }

    private void Netcom_ClientLoginStatus(object sender, LoginProgressEventArgs e)
    {
      Instance.TabConsole.DisplayNotificationInChat("Debug: Netcom_ClientLoginStatus.\r\n  Reason: " + e.FailReason + "\r\n  Message: " + e.Message + "\r\n  Status: " + e.Status, ChatBufferTextStyle.Alert);
    }

    private void Netcom_ClientDisconnected(object sender, DisconnectedEventArgs e)
    {
      Instance.TabConsole.DisplayNotificationInChat("Debug: Netcom_ClientDisconnected.\r\n  Reason: " + e.Reason + "\r\n  Message: " + e.Message, ChatBufferTextStyle.Alert);
    }

    private void Network_Disconnected(object sender, DisconnectedEventArgs e)
    {
      Instance.TabConsole.DisplayNotificationInChat("Debug: Network_Disconnected.\r\n  Reason: " + e.Reason + "\r\n  Message: " + e.Message, ChatBufferTextStyle.Alert);
    }

    public void StopPlugin(RadegastInstance inst)
    {
      Instance.Client.Network.Disconnected -= Network_Disconnected;
      Instance.Netcom.ClientDisconnected -= Netcom_ClientDisconnected;
      Instance.Netcom.ClientLoginStatus -= Netcom_ClientLoginStatus;
    }
  }
}