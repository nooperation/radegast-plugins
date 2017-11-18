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
        private bool _isAutositEnabled = false;

        public void StartPlugin(RadegastInstance inst)
        {
            _mainTimer = new Timer();
            _mainTimer.Interval = 5000;
            _mainTimer.Tick += mainTimer_Tick;
            _mainTimer.Start();
            _instance = inst;
        }

        bool IsSitonTargetInCurrentRegion()
        {
            if (_instance.State.AutoSit.Preferences.Primitive == UUID.Zero)
            {
                return false;
            }
            var primToSitOn = _instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == _instance.State.AutoSit.Preferences.Primitive);
            return primToSitOn != null;
        }

        void mainTimer_Tick(object sender, EventArgs e)
        {
            if (!_instance.Client.Network.Connected)
            {
                return;
            }

            var preferences = (AutoSitPreferences)_instance.ClientSettings["AutoSit"];
            if (preferences.Enabled)
            {
                _isAutositEnabled = true;
                if (IsSitonTargetInCurrentRegion() == false)
                {
                    preferences.Enabled = false;
                    _instance.ClientSettings["AutoSit"] = preferences;
                }
            }
            else if (_isAutositEnabled && IsSitonTargetInCurrentRegion())
            {
                preferences.Enabled = true;
                _instance.ClientSettings["AutoSit"] = preferences;
            }
        }

        public void StopPlugin(RadegastInstance inst)
        {
            _mainTimer.Stop();
        }
    }
}