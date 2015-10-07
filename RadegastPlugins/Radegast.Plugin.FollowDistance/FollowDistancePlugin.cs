using System;
using OpenMetaverse;
using System.Windows.Forms;

namespace Radegast.Plugin.FollowdistancePlugin
{
    [Radegast.Plugin(Name = "Followdistance Plugin", Description = "Followdistance plugin.", Version = "1.0")]
    public class FollowDistance : IRadegastPlugin
    {
        float originalFollowDistance;
        
        public void StartPlugin(RadegastInstance inst)
        {
            originalFollowDistance = inst.State.FollowDistance;
            inst.State.FollowDistance = 10.0f;
        }

        public void StopPlugin(RadegastInstance inst)
        {
            inst.State.FollowDistance = originalFollowDistance;
        }
    }
}