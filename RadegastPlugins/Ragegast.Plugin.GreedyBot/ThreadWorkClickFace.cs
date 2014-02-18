using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenMetaverse;

namespace Ragegast.Plugin.GreedyBot
{
	public class ThreadWorkClickFace : IThreadWork
	{
		private readonly uint objectLocalId;
		private readonly int faceIndex;
		private bool isComplete;

		private Action failureFunc;
		private int maxRetries;

		public ThreadWorkClickFace(uint objectLocalId, int faceIndex, int maxRetries = 0, Action failureFunc = null)
		{
			this.objectLocalId = objectLocalId;
			this.faceIndex = faceIndex;
			this.isComplete = true;
			this.maxRetries = maxRetries;
			this.failureFunc = failureFunc;
		}

		public void Execute(int retryCount)
		{
			if (maxRetries != 0 && retryCount >= maxRetries)
			{
				Utils.OutputLine("Execute: Max retry count reached, aborting", Utils.OutputLevel.Threading);
				if (failureFunc != null)
				{
					new Thread(new ThreadStart(failureFunc)).Start();
				}
				Confirm();
				return;
			}

			GreedyBotPlugin.Instance.Client.Self.Grab(objectLocalId, Vector3.Zero, Vector3.Zero, Vector3.Zero, faceIndex, Vector3.Zero,
									  Vector3.Zero, Vector3.Zero);
			GreedyBotPlugin.Instance.Client.Self.DeGrab(objectLocalId, Vector3.Zero, Vector3.Zero, faceIndex, Vector3.Zero,
										Vector3.Zero, Vector3.Zero);
		}

		public void Confirm()
		{
			Utils.OutputLine("ThreadWork: STOP requested", Utils.OutputLevel.Threading);
			isComplete = false;
		}

		public bool IsComplete()
		{
			return isComplete;
		}

		public int GetRetryRate()
		{
			return 1000;
		}
	}
}
