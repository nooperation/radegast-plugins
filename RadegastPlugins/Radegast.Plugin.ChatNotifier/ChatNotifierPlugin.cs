using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using OpenMetaverse;

namespace Radegast.Plugin.ChatNotifier
{
	[Radegast.Plugin(Name = "ChatNotifier Plugin", Description = "Notifies you when certain words are said in public chat.", Version = "1.0")]
	public class GreedyBotPlugin : IRadegastPlugin
	{
		/// <summary>
		/// Path to sound file (only supports .wav files) to play when a trigger word is used in main chat.
		/// </summary>
		private const string SoundToPlay = "CommBeep.wav";

		/// <summary>
		/// List of words to to trigger on. All triggers must be lowercase.
		/// </summary>
		private readonly List<string> triggers = new List<string>()
		{
			"example",
			"foobar",
			"badcoder",
			"bad coder",
			"coder",
			"nop",
			"n0p",
			"kyomuno",
		};

		private SoundPlayer soundPlayer = null;
		private RadegastInstance instance;

		public void StartPlugin(RadegastInstance inst)
		{
			instance = inst;
			byte[] soundData = null;

			if (!String.IsNullOrEmpty(SoundToPlay))
			{
				try
				{
					soundData = File.ReadAllBytes(SoundToPlay);
				}
				catch (Exception ex)
				{
					instance.TabConsole.DisplayNotificationInChat("ChatNotifierPlugin Failed to read sound file: " + ex.Message, ChatBufferTextStyle.Error);
					soundData = null;
				}
			}

			if (soundData != null)
			{
				using (MemoryStream ms = new MemoryStream(soundData))
				{
					try
					{
						soundPlayer = new SoundPlayer(ms);
						soundPlayer.Load();
					}
					catch (Exception ex)
					{
						instance.TabConsole.DisplayNotificationInChat("ChatNotifierPlugin Failed to load sound: " + ex.Message, ChatBufferTextStyle.Error);
						soundPlayer = null;
					}
				}
			}

			instance.TabConsole.DisplayNotificationInChat("ChatNotifier Triggers: " + string.Join(", ", triggers.ToArray()), ChatBufferTextStyle.StatusBlue);
			inst.TabConsole.MainChatManger.ChatLineAdded += MainChatManger_ChatLineAdded;
		}
	
		public void StopPlugin(RadegastInstance inst)
		{
			inst.TabConsole.MainChatManger.ChatLineAdded -= MainChatManger_ChatLineAdded;
		}

		void MainChatManger_ChatLineAdded(object sender, ChatLineAddedArgs e)
		{
			string rawMessage = e.Item.RawMessage.Message.ToLower();
			bool isPlayingAlert = triggers.Any(rawMessage.Contains);

			if (isPlayingAlert)
			{
				e.Item.Style = ChatBufferTextStyle.StartupTitle;
				if (soundPlayer != null)
				{
					soundPlayer.Play();
				}
			}
		}
	}
}