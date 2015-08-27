using System;
using OpenMetaverse;
using System.Windows.Forms;

namespace Radegast.Plugin.TimerExamplePlugin
{
	[Radegast.Plugin(Name = "TimerExample Plugin", Description = "TimerExample plugin.", Version = "1.0")]
	public class TimerExample : IRadegastPlugin
	{
		private RadegastInstance Instance;
		private Timer mainTimer;

		public void StartPlugin(RadegastInstance inst)
		{
			mainTimer = new Timer();
			mainTimer.Interval = 5000;
			mainTimer.Tick += mainTimer_Tick;
			mainTimer.Start();
			Instance = inst;
		}

		void mainTimer_Tick(object sender, EventArgs e)
		{
			if (!Instance.Client.Network.Connected)
			{
				return;
			}

			Instance.Client.Self.PlayGesture(new UUID("60fa264c-497f-b548-b25a-b5b8b339a7d4"));
		}
	
		public void StopPlugin(RadegastInstance inst)
		{
			mainTimer.Stop();
		}
	}
}