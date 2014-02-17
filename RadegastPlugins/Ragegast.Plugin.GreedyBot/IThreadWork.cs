using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ragegast.Plugin.GreedyBot
{
	public interface IThreadWork
	{
		void Execute();
		void Stop();
		bool IsRunning();
	}
}
