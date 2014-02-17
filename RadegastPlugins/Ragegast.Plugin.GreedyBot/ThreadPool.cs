using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ragegast.Plugin.GreedyBot
{
	class ThreadPool
	{

		private IThreadWork threadWork = null;
		private bool isClickerThreadRunning = true;
		bool isThreadComplete = false;
		Thread workerThread = null;
		private readonly object workerThreadLock = new object();

		public ThreadPool()
		{
			workerThread = new Thread(WorkerThreadLogic);
			workerThread.Name = "GreedyBot Worker";
			workerThread.Start();
		}

		public void SetWork(IThreadWork newWork)
		{
			lock (workerThreadLock)
			{
				Utils.OutputLine("ClickObjectFace: StopThreadWork", Utils.OutputLevel.Game);
				StopThreadWork();
				Utils.OutputLine("ClickObjectFace: PULSE because new threadwork...", Utils.OutputLevel.Threading);
				threadWork = newWork;
				Monitor.Pulse(workerThreadLock);
				Utils.OutputLine("ClickObjectFace: WAIT for thread to start working...", Utils.OutputLevel.Threading);
				Monitor.Wait(workerThreadLock);
				Utils.OutputLine("ClickObjectFace: RESUME", Utils.OutputLevel.Threading);
			}
		}

		public void StopThreadWork()
		{
			lock (workerThreadLock)
			{
				if (threadWork != null)
				{
					threadWork.Stop();
					Utils.OutputLine("StopThreadWork: PULSE to make the worker wake up and see that we've acknowledged the work has been completed", Utils.OutputLevel.Threading);
					Monitor.Pulse(workerThreadLock);

					while (threadWork != null)
					{
						Utils.OutputLine("StopThreadWork: WAIT for work to finish...", Utils.OutputLevel.Threading);
						Monitor.Wait(workerThreadLock);
						Utils.OutputLine("StopThreadWork: RESUME", Utils.OutputLevel.Threading);
					}

					Utils.OutputLine("StopThreadWork: Work finished, returning", Utils.OutputLevel.Threading);
				}
			}
		}

		public void KillPreviousThread()
		{
			if (workerThread == null)
			{
				return;
			}
			lock (workerThreadLock)
			{
				Utils.OutputLine("KillPreviousThread: Previous thread still running, joining it...", Utils.OutputLevel.Game);
				StopThreadWork();
				isClickerThreadRunning = false;
				Monitor.Pulse(workerThreadLock);
				while (!isThreadComplete)
				{
					Utils.OutputLine("KillPreviousThread: WAIT for thread to end...", Utils.OutputLevel.Threading);
					Monitor.Wait(workerThreadLock);
					Utils.OutputLine("KillPreviousThread: RESUME", Utils.OutputLevel.Threading);
				}
				threadWork = null;
			}
			workerThread.Join();
			workerThread = null;
		}

		private void WorkerThreadLogic()
		{
			lock (workerThreadLock)
			{
				while (true)
				{
					if (!isClickerThreadRunning)
					{
						// If clickerThreadRunning flag has been set to false then main is waiting for us to confirm that
						//   this thread has completed (main's waiting on isThreadComplete to be set to true)
						Utils.OutputLine("Worker: PULSE becuase we're finished", Utils.OutputLevel.Threading);
						isThreadComplete = true;
						Monitor.Pulse(workerThreadLock);
						return;
					}

					// Main must wait for 'threadWork' to be set to null before it can schedule more work. Let main know
					//   that we're done with the previous work.
					Utils.OutputLine("Worker: PULSE we set threadWork to null and we're waiting for work", Utils.OutputLevel.Threading);
					threadWork = null;
					Monitor.Pulse(workerThreadLock);

					while (isClickerThreadRunning && threadWork == null)
					{
						// Wait for main to schedule some more work for us *or* for main to stop this thread.
						Utils.OutputLine("Worker: WAIT for more work...", Utils.OutputLevel.Threading);
						Monitor.Wait(workerThreadLock);
						Utils.OutputLine("Worker: RESUME", Utils.OutputLevel.Threading);
						Utils.OutputLine("Worker: PULSE for acknowledgement that we woke up from the pulse", Utils.OutputLevel.Threading);
						Monitor.Pulse(workerThreadLock);
					}

					Utils.OutputLine("Worker: (debug) " + isClickerThreadRunning + " Threadwork: " + ((threadWork == null) ? "Null" : "Yes"), Utils.OutputLevel.Threading);

					while (isClickerThreadRunning && threadWork != null && threadWork.IsRunning())
					{
						// Keep executing our payload every 'retryDelayInMs' ms until main tells us to top
						//   executing it. Main must notify us whenever it stops executing.
						//OutputLine("Execute...");
						Utils.OutputLine("Worker: Execute...", Utils.OutputLevel.Threading);
						threadWork.Execute();
						Utils.OutputLine("Worker: WAIT 2000ms", Utils.OutputLevel.Threading);
						Monitor.Wait(workerThreadLock, 2000);
						Utils.OutputLine("Worker: RESUME", Utils.OutputLevel.Threading);
					}
					Utils.OutputLine("Worker: Done executing!", Utils.OutputLevel.Threading);
				}
			}
		}
	}
}
