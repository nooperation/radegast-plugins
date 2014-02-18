using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ragegast.Plugin.GreedyBot
{
	public interface IThreadWork
	{
		/// <summary>
		/// Payload to execute. This payload will be executed until 'IsComplete' return true.
		/// </summary>
		void Execute(int retryCount);
		/// <summary>
		/// Confirm that this work has been executed.
		/// </summary>
		void Confirm();
		/// <summary>
		/// Determines if we should execute our payload. This work
		/// will be repeatidly executed until this function returns false.
		/// </summary>
		/// <returns>True if this work has completed.</returns>
		bool IsComplete();
		/// <summary>
		/// Gets the number of milliseconds to wait before executing the payload again. The payload
		/// will keep executing until the user confirms that the payload was successfully executed.
		/// </summary>
		/// <returns>Number of milliseconds to wait before executing the payload again</returns>
		int GetRetryRate();
	}
}
