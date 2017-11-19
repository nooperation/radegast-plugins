using System;
using OpenMetaverse;
using System.Windows.Forms;
using Radegast.Automation;

namespace Radegast.Plugin.FixAutositAlertPlugin
{
    [Radegast.Plugin(Name = "FixAutositAlert Plugin", Description = "FixAutositAlert plugin.", Version = "1.0")]
    public class FixAutositAlert : IRadegastPlugin
    {
        private RadegastInstance _instance;
        private Timer _mainTimer;

        private void Output(string message)
        {
            _instance.TabConsole.DisplayNotificationInChat(message, ChatBufferTextStyle.StatusBlue);
        }

        public void StartPlugin(RadegastInstance inst)
        {
            _mainTimer = new Timer();
            _mainTimer.Interval = 1000;
            _mainTimer.Tick += mainTimer_Tick;
            _mainTimer.Start();
            _instance = inst;
        }

        bool IsSitonTargetInCurrentRegion()
        {
            if(_instance == null || 
                _instance.State == null ||
                _instance.State.AutoSit == null ||
                _instance.State.AutoSit.Preferences == null ||
                _instance.State.AutoSit.Preferences.Primitive == UUID.Zero)
            {
                return false;
            }

            if (_instance.Client == null ||
               _instance.Client.Network == null ||
               _instance.Client.Network.CurrentSim == null ||
               _instance.Client.Network.CurrentSim.ObjectsPrimitives == null)
            {
                return false;
            }

            var primToSitOn = _instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == _instance.State.AutoSit.Preferences.Primitive);
            return primToSitOn != null;
        }

        void mainTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!_instance.Client.Network.Connected)
                {
                    return;
                }

                var preferences = (AutoSitPreferences)_instance.ClientSettings["AutoSit"];
                if (preferences.Primitive != UUID.Zero)
                {
                    var wasAutositEnabled = preferences.Enabled;
                    preferences.Enabled = IsSitonTargetInCurrentRegion();
                    _instance.ClientSettings["AutoSit"] = preferences;

                    if (wasAutositEnabled != preferences.Enabled)
                    {
                        if (preferences.Enabled)
                        {
                            Output("Enabled autosit");
                        }
                        else
                        {
                            Output("Disabled autosit");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Output("Exception: " + ex.Message);
            }
        }

        public void StopPlugin(RadegastInstance inst)
        {
            _mainTimer.Stop();
        }
    }
}