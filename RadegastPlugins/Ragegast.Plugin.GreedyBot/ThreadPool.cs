using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ragegast.Plugin.GreedyBot
{
	class ThreadPool
	{
		private static readonly ThreadPool instance = new ThreadPool();
		public static ThreadPool Instance
		{
			get
			{
				return instance;
			}
		}

		/// <summary>
		/// The work to be executed. We will execute this work until we're told to stop via StopThreadWork.
		/// </summary>
		private IThreadWork threadWork = null;
		/// <summary>
		/// Determines if the worker thread should continue running or if it should start coming to and end.
		/// </summary>
		private bool isClickerThreadRunning = true;
		/// <summary>
		/// Determiens if the worker thread has completed its execution and is coming to and end.
		/// </summary>
		bool isThreadComplete = false;
		/// <summary>
		/// Thread used to execute jobs. This is the only thread that will execute jobs and only one job at a time can be scheduled.
		/// </summary>
		Thread workerThread = null;
		/// <summary>
		/// Primary synchronization object for the treadpool
		/// </summary>
		private readonly object workerThreadLock = new object();

		static ThreadPool()
		{

		}

		private ThreadPool()
		{

		}

		/// <summary>
		/// Initializes the thread pool. Must be called prior to attempting to schedule work.
		/// </summary>
		public void Init()
		{
			KillPreviousThread();
			isClickerThreadRunning = true;
			workerThread = new Thread(WorkerThreadLogic);
			workerThread.Name = "GreedyBot Worker";
			workerThread.Start();
		}

		/// <summary>
		/// Schedules a job. This function will BLOCK until the previously scheduled job has finished.
		/// </summary>
		/// <param name="newWork"></param>
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

		/// <summary>
		/// Stops any scheduled jobs. This function will BLOCK until the scheduled job has finished. This must be called
		/// to confirm the execution of the job. If it's not called then the job will be re-executed after a certain
		/// delay (See: IThreadWork.GetRetryRate() )
		/// </summary>
		public void StopThreadWork()
		{
			lock (workerThreadLock)
			{
				if (threadWork != null)
				{
					threadWork.Confirm();
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

		/// <summary>
		/// Kills the thread pool thread if it's running. This function will BLOCK until the thread has
		/// completed. Must be called prior to shutting down to guarentee a clean exit.
		/// </summary>
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

		/// <summary>
		/// Handles executing the thread work. 
		/// Thread logic:
		///		While isClickerThreadRunning:
		///			Wake anyone waiting for us to finish our current job
		///			Wait for someone to set a new job and wake us up
		///			Wake anyone waiting for us to confirm that we've recieved a job.
		///			While threadWork.IsComplete():
		///				Execute the job payload
		///				Wait for either a confirmation to stop working or for a given time limit.
		/// </summary>
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

					int retryCount = 0;
					while (isClickerThreadRunning && threadWork != null && threadWork.IsComplete())
					{
						// Keep executing our payload every 'retryDelayInMs' ms until main tells us to top
						//   executing it. Main must notify us whenever it stops executing.
						//OutputLine("Execute...");
						Utils.OutputLine("Worker: Execute...", Utils.OutputLevel.Threading);
						threadWork.Execute(retryCount);
						Utils.OutputLine("Worker: WAIT " + threadWork.GetRetryRate() + "ms", Utils.OutputLevel.Threading);
						Monitor.Wait(workerThreadLock, threadWork.GetRetryRate());
						Utils.OutputLine("Worker: RESUME", Utils.OutputLevel.Threading);

						++retryCount;
					}
					Utils.OutputLine("Worker: Done executing!", Utils.OutputLevel.Threading);
				}
			}
		}
	}
}
