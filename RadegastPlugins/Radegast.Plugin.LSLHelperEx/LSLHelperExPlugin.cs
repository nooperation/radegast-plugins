using System;
using System.IO;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using MySql.Data.MySqlClient;

namespace Radegast.Plugin.LSLHelperEx
{
	[Radegast.Plugin(Name = "LSLHelperEx Plugin", Description = "Extends the LSLHelper functionality.", Version = "1.0")]
	public class LSLHelperExPlugin : IRadegastPlugin
	{
		private RadegastInstance instance;
		public bool Enabled;
		public UUID AllowedOwner;
		private MySqlConnection db;

		const string cmdAddUser = "insert ignore into greeted (`serverId`, `key`) values (@serverId, @key)";

		public void StartPlugin(RadegastInstance inst)
		{
			instance = inst;
			instance.Client.Self.IM += Self_IM;

			try
			{
				db = new MySqlConnection(File.ReadAllText(@"db_Greeter.ini"));
                db.Open();
			}
			catch (Exception ex)
			{
                db = null;
				Output("Failed to connect to greeter database: " + ex.Message);
				return;
			}

			Output("LSLHelperEx loaded!");
		}

		void Self_IM(object sender, InstantMessageEventArgs e)
		{
			ProcessIM(e);
		}
	
		public void StopPlugin(RadegastInstance inst)
		{
			inst.Client.Self.IM -= Self_IM;

            if(db != null)
            {
                db.Close();
            }
		}

		private void Output(string message)
		{
			instance.TabConsole.DisplayNotificationInChat(message, ChatBufferTextStyle.StatusBlue);
		}

		private bool IsGreetingRequired(int serverId, string agentKey)
		{
            try
            {
                MySqlCommand cmd = new MySqlCommand(cmdAddUser, db);
                cmd.Parameters.AddWithValue("serverId", serverId);
                cmd.Parameters.AddWithValue("key", agentKey);

                return cmd.ExecuteNonQuery() != 0;
            }
            catch (Exception ex)
            {
                Output("Failed to check of greeting is required: " + ex.Message);
                return false;
            }
		}

		public void LoadSettings()
		{
			if (!instance.Client.Network.Connected)
			{
				return;
			}

			try
			{
				if (!(instance.ClientSettings["LSLHelper"] is OSDMap))
				{
					return;
				}

				OSDMap map = (OSDMap)instance.ClientSettings["LSLHelper"];
				Enabled = map["enabled"];
				AllowedOwner = map["allowed_owner"];
			}
			catch(Exception ex)
			{
				Output("Exception when loading settings: " + ex.Message);
			}
		}

		/// <summary>
		/// Dispatcher for incoming IM automation
		/// </summary>
		/// <param name="e">Incoming message</param>
		/// <returns>If message processed correctly, should GUI processing be halted</returns>
		public bool ProcessIM(InstantMessageEventArgs e)
		{
			LoadSettings();

			if (!Enabled)
			{
				return false;
			}

			switch (e.IM.Dialog)
			{
				case InstantMessageDialog.MessageFromObject:
				{
					if (e.IM.FromAgentID != AllowedOwner)
					{
						return true;
					}

					string[] args = e.IM.Message.Trim().Split('^');
					if (args.Length < 1)
					{ 
						return false;
					}

					switch (args[0].Trim())
					{
						case "send_chat":
						{
							if (args.Length < 2)
							{
								return false;
							}

							string msg = args[1].Trim();
							instance.Client.Self.Chat(msg, 0, ChatType.Normal);
							return true;
						}
						case "send_greeting":
						{
							if (args.Length < 3)
							{
								return false;
							}

							UUID sendTo = UUID.Zero;
							if (!UUID.TryParse(args[1].Trim(), out sendTo))
							{
								return false;
							}

							if (!IsGreetingRequired(1, sendTo.ToString()))
							{
								return false;
							}

							string msg = args[2].Trim();
							instance.Client.Self.Chat(msg, 0, ChatType.Normal);
							return true;
						}
					}
				}
				break;
			}

			return false;
		}
	}
}