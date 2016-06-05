using System;
using OpenMetaverse;
using System.Windows.Forms;

namespace Radegast.Plugin.ParcelScanner
{
    [Radegast.Plugin(Name = "Radegast.Plugin.ParcelScanner", Description = "Example plugin.", Version = "1.0")]
    public class FollowDistance : IRadegastPlugin
    {
        RadegastInstance Instance;

        private void Output(string message)
        {
            Instance.TabConsole.DisplayNotificationInChat(message, ChatBufferTextStyle.StatusBlue);
        }

        public void StartPlugin(RadegastInstance inst)
        {
            Instance = inst;

            inst.Client.Network.SimChanged += Network_SimChanged;
            inst.Client.Parcels.ParcelProperties += Parcels_ParcelProperties;
            inst.Client.Parcels.ParcelInfoReply += Parcels_ParcelInfoReply;
        }

        public void StopPlugin(RadegastInstance inst)
        {
            inst.Client.Network.SimChanged -= Network_SimChanged;
            inst.Client.Parcels.ParcelProperties -= Parcels_ParcelProperties;
            inst.Client.Parcels.ParcelInfoReply -= Parcels_ParcelInfoReply;
        }

        void Parcels_ParcelInfoReply(object sender, ParcelInfoReplyEventArgs e)
        {
            
        }

        void Parcels_ParcelProperties(object sender, ParcelPropertiesEventArgs e)
        {
            
        }

        void Network_SimChanged(object sender, SimChangedEventArgs e)
        {
            Instance.Client.Parcels.RequestAllSimParcels(Instance.Client.Network.CurrentSim);

        }
    }
}