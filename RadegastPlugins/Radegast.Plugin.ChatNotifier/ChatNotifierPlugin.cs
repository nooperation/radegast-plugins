using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Text;
using OpenMetaverse;

namespace Radegast.Plugin.ChatNotifier
{
	[Radegast.Plugin(Name = "ChatNotifier Plugin", Description = "Alerts user whenever name is mentioned in chat.", Version = "1.0")]
	public class GreedyBotPlugin : IRadegastPlugin
	{
		/// <summary>
		/// Path to .wav file to play when our name is mentioned in chat. Leave blank to use the default sound.
		/// </summary>
		private const string SoundToPlay = "";
		/// <summary>
		/// Determines if we will automatically add the current users first/last/display name to the list of triggers.
		/// </summary>
		private const bool IsAddingNameToTriggers = true;
		/// <summary>
		/// List of additional triggers. All triggers must be lowercase.
		/// </summary>
		private List<string> additionalTriggers = new List<string>()
		{
			"exampletrigger",
		};

		private RadegastInstance instance;
		private SoundPlayer soundPlayer = null;
		private List<string> triggers = new List<string>();

		public void StartPlugin(RadegastInstance inst)
		{
			instance = inst;

			if (SoundToPlay != string.Empty)
			{
				try
				{
					soundPlayer = new SoundPlayer(SoundToPlay);
					soundPlayer.Load();
				}
				catch (Exception ex)
				{
					instance.TabConsole.DisplayNotificationInChat("ChatNotifierPlugin Failed to load sound: " + ex.Message, ChatBufferTextStyle.Error);
					soundPlayer = null;
				}
			}
			else if (DefaultSoundBase64 != string.Empty)
			{
				using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(DefaultSoundBase64)))
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

			triggers = new List<string>(additionalTriggers);

			if (IsAddingNameToTriggers)
			{
				AddTrigger(instance.Client.Self.FirstName);
				if (instance.Client.Self.LastName != "Resident")
				{
					AddTrigger(instance.Client.Self.LastName);
				}

				inst.Names.NameUpdated += Names_NameUpdated;
				string displayName = inst.Names.GetDisplayName(inst.Client.Self.AgentID);
				if (displayName != RadegastInstance.INCOMPLETE_NAME)
				{
					AddTrigger(displayName);
					inst.Names.NameUpdated -= Names_NameUpdated;
				}
			}

			instance.TabConsole.DisplayNotificationInChat("ChatNotifier Triggers: " + string.Join(", ", triggers.ToArray()), ChatBufferTextStyle.StatusBlue);
			inst.Client.Avatars.DisplayNameUpdate += Avatars_DisplayNameUpdate;
			inst.TabConsole.MainChatManger.ChatLineAdded += MainChatManger_ChatLineAdded;
		}

		private void AddTrigger(string trigger)
		{
			string triggerLower = trigger.ToLower();
			if (!triggers.Contains(triggerLower))
			{
				triggers.Add(triggerLower);
			}
		}

		void Names_NameUpdated(object sender, UUIDNameReplyEventArgs e)
		{
			foreach (var name in e.Names)
			{
				if (name.Key == instance.Client.Self.AgentID)
				{
					AddTrigger(name.Value);
					return;
				}
			}
		}

		void Avatars_DisplayNameUpdate(object sender, OpenMetaverse.DisplayNameUpdateEventArgs e)
		{
			if (e.DisplayName.ID == instance.Client.Self.AgentID)
			{
				AddTrigger(e.DisplayName.DisplayName);
			}
		}

		public void StopPlugin(RadegastInstance inst)
		{
			inst.Client.Avatars.DisplayNameUpdate -= Avatars_DisplayNameUpdate;
			inst.TabConsole.MainChatManger.ChatLineAdded -= MainChatManger_ChatLineAdded;
			inst.Names.NameUpdated -= Names_NameUpdated;
		}

		void MainChatManger_ChatLineAdded(object sender, ChatLineAddedArgs e)
		{
			string rawMessage = e.Item.RawMessage.Message.ToLower();
			bool isPlayingAlert = false;
			
			foreach (var trigger in triggers)
			{
				if (rawMessage.Contains(trigger))
				{
					isPlayingAlert = true;
					break;
				}
			}
			
			if (isPlayingAlert)
			{
				e.Item.Style = ChatBufferTextStyle.StartupTitle;
				if (soundPlayer != null)
				{
					soundPlayer.Play();
				}
			}
		}

		private const string DefaultSoundBase64 =
		"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";
	}
}