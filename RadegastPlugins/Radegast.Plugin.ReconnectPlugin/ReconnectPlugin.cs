using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System.Timers;

namespace Radegast.Plugin.ReconnectPlugin
{
  [Radegast.Plugin(Name = "Reconnect Plugin", Description = "Reconnect plugin.", Version = "1.0")]
  public class ReconnectPlugin : IRadegastPlugin
  {
    private const double kReconnectTimerDelayInMs = 10000.0;

    public RadegastInstance Instance { get; set; }
    public Timer ReconnectTimer { get; set; }

    public void StartPlugin(RadegastInstance radegast_instance)
    {
      Instance = radegast_instance;
      ReconnectTimer = new Timer();
      ReconnectTimer.AutoReset = false;
      ReconnectTimer.Interval = kReconnectTimerDelayInMs;
      ReconnectTimer.Elapsed += ReconnectTimer_Elapsed;

      Instance.Netcom.ClientLoginStatus += Netcom_ClientLoginStatus;
    }

    private void Netcom_ClientLoginStatus(object sender, LoginProgressEventArgs e)
    {
      Instance.TabConsole.DisplayNotificationInChat("Debug: Netcom_ClientLoginStatus.\r\n  Reason: " + e.FailReason + "\r\n  Message: " + e.Message + "\r\n  Status: " + e.Status, ChatBufferTextStyle.Alert);

      if(e.Status == LoginStatus.Failed)
      {
        Instance.TabConsole.DisplayNotificationInChat("Debug: Scheduling attempt to reconnect...");
        ReconnectTimer.Start();
      }
    }

    private void ReconnectTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
      if(Instance.MainForm.InAutoReconnect == false)
      {
        Instance.TabConsole.DisplayNotificationInChat("Debug: Attempting reconnect...");
        Instance.MainForm.BeginAutoReconnect();
      }
      else
      {
        Instance.TabConsole.DisplayNotificationInChat("Debug: Reconnect already in progress. Skipping reconnect attempt...");
      }
    }

    public void StopPlugin(RadegastInstance radegast_instance)
    {
      ReconnectTimer.Stop();
      ReconnectTimer.Close();

      Instance.Netcom.ClientLoginStatus -= Netcom_ClientLoginStatus;
    }
  }
}
