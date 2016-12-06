using System;
using System.IO;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Radegast.Plugin.LSLHelperEx
{
    [Radegast.Plugin(Name = "LSLHelperEx Plugin", Description = "Extends the LSLHelper functionality.", Version = "1.0")]
    public class LSLHelperExPlugin : IRadegastPlugin
    {
        private RadegastInstance instance;
        public bool Enabled;
        public UUID AllowedOwner;

        public void StartPlugin(RadegastInstance inst)
        {
            instance = inst;
            instance.Client.Self.IM += Self_IM;

            Output("LSLHelperEx loaded!");
        }

        void Self_IM(object sender, InstantMessageEventArgs e)
        {
            ProcessIM(e);
        }

        public void StopPlugin(RadegastInstance inst)
        {
            inst.Client.Self.IM -= Self_IM;
        }

        private void Output(string message)
        {
            instance.TabConsole.DisplayNotificationInChat(message, ChatBufferTextStyle.StatusBlue);
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
            catch (Exception ex)
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
                        case "say":
                        {
                            if (args.Length < 2)
                            {
                                return false;
                            }

                            string msg = args[1].Trim();
                            instance.Client.Self.Chat(msg, 0, ChatType.Normal);
                            return true;
                        }
                        case "play_gesture":
                        {
                            if (args.Length < 2)
                            {
                                return false;
                            }

                            string gesture_name = args[1].Trim();
                            UUID gesture_uuid;
                            if(UUID.TryParse(gesture_name, out gesture_uuid))
                            {
                                Output("Playing gesture: " + gesture_name);
                                instance.Client.Self.PlayGesture(gesture_uuid);
                                return true;
                            }

                            Output("Invalid gesture: " + gesture_name);
                            return false;
                        }
                    }
                }
                break;
            }

            return false;
        }
    }
}
