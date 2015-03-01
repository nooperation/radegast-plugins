using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using OpenMetaverse;

namespace Radegast.Plugin.Clicker
{
	[Radegast.Plugin(Name = "Clicker Plugin", Description = "Clicks something.", Version = "1.0")]
	public class GreedyBotPlugin : IRadegastPlugin
	{
		private RadegastInstance instance;

		public void StartPlugin(RadegastInstance inst)
		{
			instance = inst;
			
			instance.TabConsole.DisplayNotificationInChat("asdfClicker loaded", ChatBufferTextStyle.StatusBlue);

			inst.TabConsole.MainChatManger.ChatLineAdded += MainChatManger_ChatLineAdded;

			uint objectLocalId = GetLocalId(new UUID("03b304ce-b108-4804-6659-7809b81f68d0"));
			instance.TabConsole.DisplayNotificationInChat("objectLocalId = " + objectLocalId, ChatBufferTextStyle.StatusBlue);

			if (objectLocalId != 0)
			{
				instance.TabConsole.DisplayNotificationInChat("Clicker: Clicking...", ChatBufferTextStyle.StatusDarkBlue);

				instance.Client.Self.Grab(objectLocalId, Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero,
				                                          Vector3.Zero, Vector3.Zero);
				instance.Client.Self.DeGrab(objectLocalId, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero,
				                                            Vector3.Zero, Vector3.Zero);
			}

		}
	
		public void StopPlugin(RadegastInstance inst)
		{
			inst.TabConsole.MainChatManger.ChatLineAdded -= MainChatManger_ChatLineAdded;
		}

		void MainChatManger_ChatLineAdded(object sender, ChatLineAddedArgs e)
		{
			if (e.Item.RawMessage.SourceID == new UUID("03b304ce-b108-4804-6659-7809b81f68d0"))
			{
				if (e.Item.RawMessage.Message == "Touched.")
				{
					instance.TabConsole.DisplayNotificationInChat("Target message found!", ChatBufferTextStyle.StatusBlue);
				}
			}
		}

		private uint GetLocalId(UUID objectId)
		{
			Primitive targetObject = instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == objectId);
			if (targetObject == null)
			{
				instance.TabConsole.DisplayNotificationInChat("Clicker: Failed to find object by UUID", ChatBufferTextStyle.StatusBlue);
				return 0;
			}

			instance.TabConsole.DisplayNotificationInChat("Clicker: found object!", ChatBufferTextStyle.StatusBlue);
			return targetObject.LocalID;
		}
	}
}