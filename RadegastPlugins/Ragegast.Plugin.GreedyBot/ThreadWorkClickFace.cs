using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace Ragegast.Plugin.GreedyBot
{
	public class ThreadWorkClickFace : IThreadWork
	{
		private readonly uint objectLocalId;
		private readonly int faceIndex;
		private bool isRunning;

		public ThreadWorkClickFace(uint objectLocalId, int faceIndex)
		{
			this.objectLocalId = objectLocalId;
			this.faceIndex = faceIndex;
			this.isRunning = true;
		}

		public void Execute()
		{
			GreedyBotPlugin.Instance.Client.Self.Grab(objectLocalId, Vector3.Zero, Vector3.Zero, Vector3.Zero, faceIndex, Vector3.Zero,
									  Vector3.Zero, Vector3.Zero);
			GreedyBotPlugin.Instance.Client.Self.DeGrab(objectLocalId, Vector3.Zero, Vector3.Zero, faceIndex, Vector3.Zero,
										Vector3.Zero, Vector3.Zero);
		}

		public void Stop()
		{
			Utils.OutputLine("ThreadWork: STOP requested", Utils.OutputLevel.Threading);
			isRunning = false;
		}

		public bool IsRunning()
		{
			return isRunning;
		}
	}
}
