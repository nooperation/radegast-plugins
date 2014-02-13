using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using OpenMetaverse;
using Radegast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Radegast.LSL;


namespace Ragegast.Plugin.GreedyBot
{
	[Radegast.Plugin(Name = "GreedyBot Plugin", Description = "Goal is to make a GreedyGreedy bot, but will most likely end up as a failure", Version = "1.0")]
	public class GreedyBotPlugin : IRadegastPlugin
	{
		[DllImport("kernel32.dll")]
		static extern void OutputDebugString(string lpOutputString);

		private class UVEntry
		{
			public readonly float U;
			public readonly float V;

			public UVEntry(float u, float v)
			{
				this.U = u;
				this.V = v;
			}

			public override string ToString()
			{
				return string.Format("{0}, {1}", U, V);
			}
		}

		private enum FaceStatus
		{
			Unknown,
			Normal,
			Selected,
			New,
			Used
		};

		private enum State
		{
			NotRunning,

			SearchinForGameBoard,
			RetrievingGameObjectProperties,
			WaitingForOurTurn,
			SelectingDie,
			RollingDice
		}

		/// <summary>
		/// Radegast instance
		/// </summary>
		public static RadegastInstance instance;

		/// <summary>
		/// Primary synchronization object used for all network messages until I figure out what is i
		/// </summary>
		private readonly object greedyBotLock = new object();
		/// <summary>
		/// Current state of the bot.
		/// </summary>
		private State currentState = State.NotRunning;


		/// <summary>
		/// Epsilon used to compare floating point values. The two values will be considered
		/// equal if the difference between them is less than Epsilon.
		/// </summary>
		private const float Epsilon = 0.0001f;
		/// <summary>
		/// Color of the die when it's normal / White
		/// </summary>
		private readonly Color4 ColorNormal = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's selected / Red
		/// </summary>
		private readonly Color4 ColorSelected = new Color4(1.0f, 0.0f, 0.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's new / Green
		/// </summary>
		private readonly Color4 ColorNew = new Color4(0.0f, 1.0f, 0.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's used / Blue
		/// </summary>
		private readonly Color4 ColorUsed = new Color4(0.0f, 0.0f, 1.0f, 1.0f);
		/// <summary>
		/// Color of the light when it's off / Not our turn
		/// </summary>
		private readonly Color4 ColorLightOff = new Color4(0.0f, 0.0f, 0.0f, 1.0f);


		/// <summary>
		/// Queue of dice we will want to select for our turn.
		/// </summary>
		private Queue<int> diceQueue = new Queue<int>();
		/// <summary>
		/// The die we have requested to select. We will be waiting for the status of this die to enter
		/// the 'selected' state before moving on to the next die in our queue.
		/// </summary>
		private int dieIndexWereSelecting = -1;
		/// <summary>
		/// Determines if we should continue rolling or if we should end our turn and our points
		/// to our game score.
		/// </summary>
		private bool continueRolling = false;
		/// <summary>
		/// Determines if this is the last round. We must keep going until ??? points.
		/// </summary>
		private bool isLastRound = false;


		/// <summary>
		/// Game logic. Determines which dice we're going to be picking.
		/// </summary>
		private GameLogic game;
		/// <summary>
		/// Determiens if the game board is playable and we've started to select dice.
		/// </summary>
		private int pointsThisTurn = 0;
		/// <summary>
		/// The number of busts we've had in a row. If we have three busts in a row then our
		/// game score is reset to 0.
		/// </summary>
		private int marksAgainstUs = 0;
		/// <summary>
		/// Total game score.
		/// </summary>
		private int myGameScore = 0;
		/// <summary>
		/// Index of player on game board. This index corresponds to an entry in 'playerLightIds'
		/// </summary>
		private int playerIndex = -1;


		/// <summary>
		/// Current dice faces.
		/// </summary>
		private Primitive.TextureEntryFace[] currentDiceFaces = new Primitive.TextureEntryFace[6];
		/// <summary>
		/// Status the set of dice need to be in to consider the roll to be complete. The server doesn't send
		/// us all of the dice updates at once so we need to keep applying our updates until currentDiceFaces
		/// matches the expected pattern. This pattern is either all 'new' dice (green) or a combination of
		/// 'new' and 'used' dice.
		/// </summary>
		private readonly FaceStatus[] expectedDiceStatus = new FaceStatus[6];


		/// <summary>
		/// Table of expected UV corrdinates for each face of the score prim for each digit (0-9 followed by BLANK).
		/// There are faces: 10000's, 1000's, 100's, 10's, 1's
		/// Each face has 10 digits: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, BLANK
		/// </summary>
		private readonly UVEntry[,] perScoreFaceUVs = new UVEntry[5, 11];
		/// <summary>
		/// UUID of the creator of the GreedyGreedy board this bot is for. Helps filter out all the other
		/// objects we don't care about when we're looking for the board and it's children.
		/// </summary>
		private readonly UUID creatorId = new UUID("974cd5a0-16ca-42a9-bac6-8d583b7d7438");
		/// <summary>
		/// Not all games have the small straight rule enabled. Games found in this set have the Small Straight rule enabled.
		/// </summary>
		private HashSet<UUID> tablesWithSmallStraightRule = new HashSet<UUID>()
		{
			new UUID("011cc6df-9d87-eb41-9127-0a6ecdd4561b")
		};
		/// <summary>
		/// Not all games have the full house rule enabled. Games found in this set have the Full House rule enabled.
		/// </summary>
		private HashSet<UUID> tablesWithFullHouseRule = new HashSet<UUID>()
		{
			new UUID("011cc6df-9d87-eb41-9127-0a6ecdd4561b")
		};
		/// <summary>
		/// UUID of the game table.
		/// </summary>
		private UUID tableId = UUID.Zero;
		/// <summary>
		/// UUID of the prim containing the left three dice.
		/// </summary>
		private UUID diceLeftId = UUID.Zero;
		/// <summary>
		/// UUID of the prim containing the right three dice.
		/// </summary>
		private UUID diceRightId = UUID.Zero;
		/// <summary>
		/// UUID of the prim containing the Roll and Stop buttons.
		/// </summary>
		private UUID gameButtonsId = UUID.Zero;
		/// <summary>
		/// UUID of the prim containing our turn score.
		/// </summary>
		private UUID scoreId = UUID.Zero;
		/// <summary>
		/// UUIDs of the player lights to indicate the current turn.
		/// </summary>
		private UUID[] playerLightIds = new UUID[8] { UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero };

		private UUID TEMPMasterId = new UUID("24036859-e20e-40c4-8088-be6b934c3891");

		public void StopPlugin(RadegastInstance inst)
		{
			KillPreviousThread();

			inst.Client.Objects.TerseObjectUpdate -= Objects_TerseObjectUpdate;
			inst.Client.Self.ChatFromSimulator -= Self_ChatFromSimulator;
			inst.Client.Objects.ObjectProperties -= Objects_ObjectProperties;
			inst.Client.Self.AvatarSitResponse -= Self_AvatarSitResponse;
			inst.Client.Objects.ObjectPropertiesFamily -= Objects_ObjectPropertiesFamily;
			inst.Client.Objects.AvatarSitChanged -= Objects_AvatarSitChanged;
		}

		public void StartPlugin(RadegastInstance inst)
		{
			instance = inst;
			GenerateScoreFaceUVs();

			workerThread = new Thread(WorkerThreadLogic);
			workerThread.Name = "GreedyBot Worker";
			workerThread.Start();

			inst.Client.Objects.AvatarSitChanged += Objects_AvatarSitChanged;
			inst.Client.Objects.TerseObjectUpdate += Objects_TerseObjectUpdate;
			inst.Client.Self.ChatFromSimulator += Self_ChatFromSimulator;
			inst.Client.Self.IM += Self_IM;
			inst.Client.Self.AvatarSitResponse += Self_AvatarSitResponse;
		}


		private IThreadWork threadWork = null;
		private bool isClickerThreadRunning = true;
		bool isThreadComplete = false;
		Thread workerThread = null;
		private readonly object workerThreadLock = new object();

		private void StopThreadWork()
		{
			lock (workerThreadLock)
			{
				if (threadWork != null)
				{
					threadWork.Stop();
					OutputLine("StopThreadWork: PULSE to make the worker wake up and see that we've acknowledged the work has been completed", OutputLevel.Threading);
					Monitor.Pulse(workerThreadLock);

					while (threadWork != null)
					{
						OutputLine("StopThreadWork: WAIT for work to finish...", OutputLevel.Threading);
						Monitor.Wait(workerThreadLock);
						OutputLine("StopThreadWork: Wokeup", OutputLevel.Threading);
					}

					OutputLine("StopThreadWork: Work finished, returning", OutputLevel.Threading);
				}
			}
		}

		private void KillPreviousThread()
		{
			if (workerThread == null)
			{
				return;
			}
			lock (workerThreadLock)
			{
				OutputLine("KillPreviousThread: Previous thread still running, joining it...", OutputLevel.Game);
				StopThreadWork();
				isClickerThreadRunning = false;
				Monitor.Pulse(workerThreadLock);
				while (!isThreadComplete)
				{
					OutputLine("KillPreviousThread: WAIT for thread to end...", OutputLevel.Threading);
					Monitor.Wait(workerThreadLock);
					OutputLine("KillPreviousThread: Wokeup", OutputLevel.Threading);
				}
				threadWork = null;
			}
			workerThread.Join();
			workerThread = null;
		}

		private interface IThreadWork
		{
			void Execute();
			void Stop();
			bool IsRunning();
		}

		class ThreadWorkClickFace : IThreadWork
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
				instance.Client.Self.Grab(objectLocalId, Vector3.Zero, Vector3.Zero, Vector3.Zero, faceIndex, Vector3.Zero,
										  Vector3.Zero, Vector3.Zero);
				instance.Client.Self.DeGrab(objectLocalId, Vector3.Zero, Vector3.Zero, faceIndex, Vector3.Zero,
											Vector3.Zero, Vector3.Zero);
			}

			public void Stop()
			{
				OutputLine("ThreadWork: Stopped!", OutputLevel.Threading);
				isRunning = false;
			}

			public bool IsRunning()
			{
				return isRunning;
			}
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
						OutputLine("Worker: PULSE becuase we're finished", OutputLevel.Threading );
						isThreadComplete = true;
						Monitor.Pulse(workerThreadLock);
						return;
					}

					// Main must wait for 'threadWork' to be set to null before it can schedule more work. Let main know
					//   that we're done with the previous work.
					OutputLine("Worker: PULSE we set threadWork to null and we're waiting for work", OutputLevel.Threading);
					threadWork = null;
					Monitor.Pulse(workerThreadLock);

					while (isClickerThreadRunning && threadWork == null)
					{
						// Wait for main to schedule some more work for us *or* for main to stop this thread.
						OutputLine("Worker: WAIT for more work...", OutputLevel.Threading);
						Monitor.Wait(workerThreadLock);
						OutputLine("Worker: Woke up", OutputLevel.Threading);
						OutputLine("Worker: PULSE for acknowledgement that we woke up from the pulse", OutputLevel.Threading);
						Monitor.Pulse(workerThreadLock);
					}

					OutputLine("Worker: (debug) " + isClickerThreadRunning + " Threadwork: " + ((threadWork == null) ? "Null" : "Yes"), OutputLevel.Threading);

					while (isClickerThreadRunning && threadWork != null && threadWork.IsRunning())
					{
						// Keep executing our payload every 'retryDelayInMs' ms until main tells us to top
						//   executing it. Main must notify us whenever it stops executing.
						//OutputLine("Execute...");
						OutputLine("Worker: Execute...", OutputLevel.Threading);
						threadWork.Execute();
						OutputLine("Worker: WAIT 2000ms", OutputLevel.Threading);
						Monitor.Wait(workerThreadLock, 2000);
						OutputLine("Worker: Wokeup", OutputLevel.Threading);
					}
					OutputLine("Worker: Done executing!", OutputLevel.Threading);
				}
			}
		}

		/// <summary>
		/// Determines if all necessary game objects have been found to be able to start playing the game.
		/// </summary>
		/// <returns>True if we've found all necessary game objects.</returns>
		private bool HasFoundAllGameComponents()
		{
			return diceLeftId != UUID.Zero 
				&& diceRightId != UUID.Zero 
				&& gameButtonsId != UUID.Zero 
				&& scoreId != UUID.Zero 
				&& playerLightIds.All(n => n != UUID.Zero) 
				&& playerIndex != -1;
		}

		private void CheckPropsForGameComponents(Primitive.ObjectProperties props)
		{
			if (props.CreatorID != creatorId)
			{
				return;
			}

			if (HasFoundAllGameComponents())
			{
				return;
			}

			if (props.Name == "Dice")
			{
				if (props.Description == "1,3;2,0;3,1")
				{
					diceLeftId = props.ObjectID;
				}
				else if (props.Description == "4,3;5,0;6,1")
				{
					diceRightId = props.ObjectID;
				}
			}
			else if (props.Name == "Action Buttons")
			{
				gameButtonsId = props.ObjectID;
			}
			else if (props.Name == "PointsDisplay")
			{
				scoreId = props.ObjectID;
			}
			else if (props.Name == "Player Light")
			{
				if (props.Description.Length == 0)
				{
					return;
				}

				int seatId = props.Description[0] - '0';
				if (seatId < 1 || seatId > 8)
				{
					return;
				}

				playerLightIds[seatId - 1] = props.ObjectID;
			}
			else if (props.Name.StartsWith("Game Player"))
			{
				if (props.Description.Length == 0)
				{
					return;
				}

				int seatId = props.Description[0] - '0';
				if (seatId < 1 || seatId > 8)
				{
					return;
				}

				if (props.Name == "Game Player (" + instance.Client.Self.Name +")")
				{
					playerIndex = seatId - 1;
					OutputLine("We're player #" + playerIndex, OutputLevel.Game);
				}
			}

			if (HasFoundAllGameComponents())
			{
				instance.Client.Objects.ObjectProperties -= Objects_ObjectProperties;

				currentState = State.WaitingForOurTurn;
				OutputLine("Found everything!", OutputLevel.Game);
			}
		}

		/// <summary>
		/// Clears all information about the game and game table.
		/// </summary>
		private void ClearGame()
		{
			currentState = State.NotRunning;

			diceLeftId = UUID.Zero;
			diceRightId = UUID.Zero;
			gameButtonsId = UUID.Zero;
			scoreId = UUID.Zero;
			tableId = UUID.Zero;
			playerLightIds = new UUID[playerLightIds.Length];

			playerIndex = -1;
			dieIndexWereSelecting = -1;

			diceQueue = new Queue<int>();

			pointsThisTurn = 0;
			marksAgainstUs = 0;
			myGameScore = 0;
			continueRolling = false;

			game = null;
		}

		#region Events
		void Self_IM(object sender, InstantMessageEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (e.IM.Message.Trim().ToLower() == "shazbot!")
				{
					ToggleSeat();
				}
			}
		}

		void Objects_AvatarSitChanged(object sender, AvatarSitChangedEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (e.Avatar.ID != instance.Client.Self.AgentID)
				{
					return;
				}

				// We got up, forget everything we know about the game.
				if (e.SittingOn == 0)
				{
					if (currentState != State.NotRunning)
					{
						OutputLine("Objects_AvatarSitChanged: Stopping thread work...", OutputLevel.Game);
						StopThreadWork();
						ClearGame();
						//instance.Client.Self.RequestSit(sitTargetLamp, Vector3.Zero);
					}
				}
			}
		}

		void Self_AvatarSitResponse(object sender, AvatarSitResponseEventArgs e)
		{
			lock (greedyBotLock)
			{
				// We sat down, forget everything we know about previous games.
				ClearGame();

				currentState = State.SearchinForGameBoard;
				instance.Client.Objects.ObjectPropertiesFamily += Objects_ObjectPropertiesFamily;
				instance.Client.Objects.RequestObjectPropertiesFamily(instance.Client.Network.CurrentSim, e.ObjectID);
			}
		}

		void Objects_ObjectPropertiesFamily(object sender, ObjectPropertiesFamilyEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (currentState != State.SearchinForGameBoard)
				{
					instance.Client.Objects.ObjectPropertiesFamily -= Objects_ObjectPropertiesFamily;
					return;
				}

				if (e.Properties.Name.StartsWith("Greedy Greedy Table"))
				{
					if (tableId != UUID.Zero)
					{
						return;
					}

					instance.Client.Objects.ObjectPropertiesFamily -= Objects_ObjectPropertiesFamily;

					tableId = e.Properties.ObjectID;
					GameLogic.IsSmallStraightEnabled = tablesWithSmallStraightRule.Contains(tableId);
					GameLogic.IsFullHouseEnabled = tablesWithFullHouseRule.Contains(tableId);

					Primitive greedyGreedyTable = instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == tableId);
					if (greedyGreedyTable == null)
					{
						OutputLine("Failed to find greedyGreedyTable!", OutputLevel.Error);
						return;
					}

					// Don't read cached values, they're wrong. We need the latest object name.
					uint[] greedyTableChildren = instance.Client.Network.CurrentSim.ObjectsPrimitives.FindAll(n => n.ParentID == greedyGreedyTable.LocalID).Select(n => n.LocalID).ToArray();
					if (greedyTableChildren.Length > 0)
					{
						OutputLine("Requesting " + greedyTableChildren.Length + " properties", OutputLevel.Game);

						currentState = State.RetrievingGameObjectProperties;
						instance.Client.Objects.ObjectProperties += Objects_ObjectProperties;
						instance.Client.Objects.SelectObjects(instance.Client.Network.CurrentSim, greedyTableChildren);
					}
				}
			}
		}

		void Objects_ObjectProperties(object sender, ObjectPropertiesEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (currentState != State.RetrievingGameObjectProperties)
				{
					instance.Client.Objects.ObjectProperties -= Objects_ObjectProperties;
					return;
				}

				CheckPropsForGameComponents(e.Properties);
			}
		}

		// TODO: TEMP!
		private UUID sitTargetGame = new UUID("011cc6df-9d87-eb41-9127-0a6ecdd4561b");
		private void ToggleSeat()
		{
			ClearGame();

			if (instance.Client.Self.SittingOn != 0)
			{
				instance.Client.Self.Stand();
			}
			else
			{
				instance.Client.Self.RequestSit(sitTargetGame, Vector3.Zero);
			}
		}


		void Self_ChatFromSimulator(object sender, ChatEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (e.Message.Trim().ToLower() == "shazbot!")
				{
					ClearGame();
					ToggleSeat();
				}
				if (e.SourceID != tableId)
				{
					return;
				}

				if (e.Message == "It is not your turn. Please wait your turn before attempting to change the playing pieces.")
				{
					OutputLine("Chat: Not our turn - StopThreadWork", OutputLevel.Game);
					StopThreadWork();
					game = null;
					currentState = State.WaitingForOurTurn;
				}
				else if (e.Message == "You must accumulate at least 1000 points before you can begin scoring. Keep on truckin'.")
				{
					// TODO: This is a hack... but it semes to be an OKAY hack! If we get this message then we actually have 0 points
					//  and because we don't have a very reliable way to detect the end of the game yet to reset to 0 points we're just
					//  going to use this to do it for us!
					OutputLine("Chat: must get 1k points - scheduling roll", OutputLevel.Game);
					myGameScore = 0;
					ClickRoll();
				}
				else if (e.Message == "This is the final round, you must keep rolling until you beat the current top score or bust. Good luck, citizen.")
				{
					// TODO: Detect that this is the last round so we can use the correct algorithm in the game logic to pick our dice
					//   for now we just keep trying to roll until we either exceed the opponets score or bust.
					OutputLine("MSG 3: End of game must keep rolling till we win or bust - scheduling roll", OutputLevel.Game);
					ClickRoll();
				}
			}
		}

		void Objects_TerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (e.Prim.ID == diceLeftId)
				{
					if (e.Update.Textures == null)
					{
						return;
					}
					OnDiceFaceUpdate(true, e.Update.Textures);
					return;
				}
				if (e.Prim.ID == diceRightId)
				{
					if (e.Update.Textures == null)
					{
						return;
					}
					OnDiceFaceUpdate(false, e.Update.Textures);
					return;
				}
				if (e.Prim.ID == scoreId)
				{
					OnScoreUpdate(e);
					return;
				}
				if (playerLightIds.Contains(e.Prim.ID))
				{
					UpdateLight(e.Prim);
					return;
				}
			}
		}
		#endregion

		#region TESTED_WORKING

		internal enum OutputLevel
		{
			Game,
			Threading,
			Error
		}

		/// <summary>
		/// Outputs specified line to text chat (only local chat)
		/// </summary>
		internal static void OutputLine(string msg, OutputLevel outputType)
		{
			OutputDebugString(instance.Client.Self.Name +  " [" + outputType + "] " + msg + "\n");
			//instance.TabConsole.MainChatManger.TextPrinter.PrintTextLine("DEBUG: " + msg, Color.CadetBlue);
		}
		
		/// <summary>
		/// Determines if two floating point values are close enough to be considered equal using
		/// a predetermined Epsilon to account for errors.
		/// </summary>
		/// <param name="lhs">First value to compare</param>
		/// <param name="rhs">Second value to compare</param>
		/// <returns>True if values are close enough to be considered equal</returns>
		private static bool IsAboutEqual(float lhs, float rhs)
		{
			return (lhs + Epsilon) > rhs && ((lhs - Epsilon) < rhs);
		}

		/// <summary>
		/// Generates the table of UV corrdinates for each face of the score should have
		/// for each digit (0-9 followed by BLANK).
		/// These values are not exact so a slightly larger epsilon (0.0001 seems to work)
		/// will be needed when comparing these values against the actual UV coordinates
		/// the faces have.
		/// </summary>
		private void GenerateScoreFaceUVs()
		{
			// Character width in UV coordinates (all characters are square)
			const float baseScoreWidth = 0.1000091f;

			// 'V' offset to get to row containing digits 0-7
			const float faceVOffset = -0.04998932f;

			// 'U' offset for the first digit (zero) for the faces of ones, tens, thousands, ten thousands. Each face
			//   seems to have a different starting 'U' offset, but the same 'V' offset.
			float[] perFaceUOffset = new float[]
			{
				-0.205023236f,	// 10000's U offset for '0' digit
				-0.2500076f,	// 1000's U offset for '0' digit
				-0.5900143f,	// 100's U offset for '0' digit
				-0.2500076f,	// 10's U offset for '0' digit
				-0.2949919f		// 1's U offset for '0' digit
			};

			// The number of characters we need to travel horizontally to reach the image
			//   for 0-9 and BLANK. Index 0 represents digit 0, 1 represents digit 1 and so
			//   on. The last index represents the blank character.
			//
			// finalUOffset = baseUOffset + (CharacterWidth * uOffsets[digit])
			int[] uOffsets = new int[]
			{
				0,1,2,3,4,5,6,7,-2,-1,2
			};

			// The number of characters we need to travel vertically to reach the image
			//   for 0-9 and BLANK. Index 0 represents digit 0, 1 represents digit 1 and so
			//   on. The last index represents the blank character.
			int[] vOffsets = new int[]
			{
				0,0,0,0,0,0,0,0,-1,-1,-4
			};

			//             RAW OFFSETS                          Generalized offsets
			// Digit: Uoffset | vOffset			      Uoffset            , VOffset
			// 0: -0.5900143  | -0.04998932        || BaseU + (Width * 0), BaseV + (Width * 0) 
			// 1: -0.4900052  | -0.04998932        || BaseU + (Width * 1), BaseV + (Width * 0) 
			// 2: -0.389996   | -0.04998932        || BaseU + (Width * 2), BaseV + (Width * 0) 
			// 3: -0.2899869  | -0.04998932        || BaseU + (Width * 3), BaseV + (Width * 0) 
			// 4: -0.1900082  | -0.04998932        || BaseU + (Width * 4), BaseV + (Width * 0) 
			// 5: -0.08999909 | -0.04998932        || BaseU + (Width * 5), BaseV + (Width * 0) 
			// 6: 0.01001007  | -0.04998932        || BaseU + (Width * 6), BaseV + (Width * 0) 
			// 7: 0.1099887   | -0.04998932        || BaseU + (Width * 7), BaseV + (Width * 0) 
			// 8: -0.7900021  | -0.1499985         || BaseU + (Width * -2), BaseV + (Width * -1)
			// 9: -0.689993   | -0.1499985         || BaseU + (Width * -1), BaseV + (Width * -1)
			// x: -0.389996   | -0.4499954         || BaseU + (Width * 2), BaseV + (Width * -4) 
			//
			// where BaseV = -0.04998932
			//       Width =  0.1000091  (about)
			//       BaseU = (See perFaceUOffset)

			for (int faceIndex = 0; faceIndex < perScoreFaceUVs.GetLength(0); faceIndex++)
			{
				for (int digitIndex = 0; digitIndex < perScoreFaceUVs.GetLength(1); digitIndex++)
				{
					perScoreFaceUVs[faceIndex, digitIndex] = new UVEntry(perFaceUOffset[faceIndex] + (baseScoreWidth * uOffsets[digitIndex]), faceVOffset + (baseScoreWidth * vOffsets[digitIndex]));
				}
			}
		}

		/// <summary>
		/// Determines the value of a die from the specified face.
		/// </summary>
		/// <param name="dieFace">Face of the die to get the value of.</param>
		/// <returns>Value of die (1-6)</returns>
		private int GetDieValueFromFace(Primitive.TextureEntryFace dieFace)
		{
			// <summary>
			// 'U' offsets for each side of the die.
			// All faces are on the same horizontal strip so no V offset is needed
			// </summary>
			float[] dieFaceUOffsets = new float[]
			{
				-0.4499954f,  // Side with value '1'
				-0.3499863f,  // Side with value '2'
				-0.2500076f,  // Side with value '3'
				-0.1499985f,  // Side with value '4'
				-0.04998932f, // Side with value '5'
				0.04998932f	  // Side with value '6'
			};

			for (int dieIndex = 0; dieIndex < dieFaceUOffsets.Length; ++dieIndex)
			{
				if (IsAboutEqual(dieFace.OffsetU, dieFaceUOffsets[dieIndex]))
				{
					return dieIndex + 1;
				}
			}

			throw new ApplicationException("Invalid die U offset: " + dieFace.OffsetU);
		}

		/// <summary>
		/// Determines the status of the specified die face. This is based off of the current
		/// color of the face.
		/// </summary>
		/// <param name="dieFace">Face of the die to determine the status of</param>
		/// <returns>Current status of the die.</returns>
		private FaceStatus GetDieStatus(Primitive.TextureEntryFace dieFace)
		{
			if (dieFace == null)
				return FaceStatus.Unknown;
			else if (dieFace.RGBA.CompareTo(ColorNormal) == 0)
				return FaceStatus.Normal;
			else if (dieFace.RGBA.CompareTo(ColorSelected) == 0)
				return FaceStatus.Selected;
			else if (dieFace.RGBA.CompareTo(ColorNew) == 0)
				return FaceStatus.New;
			else if (dieFace.RGBA.CompareTo(ColorUsed) == 0)
				return FaceStatus.Used;
			else
				return FaceStatus.Unknown;
		}

		/// <summary>
		/// Retrieves a list of dice that we can pick.
		/// </summary>
		/// <returns>List of dice we can pick.</returns>
		private List<Die> GetActiveDice()
		{
			List<Die> dice = new List<Die>();

			for (int dieIndex = 0; dieIndex < currentDiceFaces.Length; dieIndex++)
			{
				if (currentDiceFaces[dieIndex] == null)
				{
					OutputLine("Current faces contains invalid data!", OutputLevel.Error);
					return null;
				}

				FaceStatus status = GetDieStatus(currentDiceFaces[dieIndex]);
				if (status == FaceStatus.New || status == FaceStatus.Normal)
				{
					dice.Add(new Die(dieIndex, GetDieValueFromFace(currentDiceFaces[dieIndex])));
				}
			}

			return dice;
		}

		/// <summary>
		/// Clicks on the specified face of an object by UUID
		/// </summary>
		/// <param name="objectId">UUID of the object to click</param>
		/// <param name="faceIndex">Index of face to click on the object</param>
		private void ClickObjectFace(UUID objectId, int faceIndex)
		{
			Primitive targetObject = instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == objectId);
			if (targetObject == null)
			{
				OutputLine("Failed to find object by UUID", OutputLevel.Error);
				return;
			}

			ClickObjectFace(targetObject.LocalID, faceIndex);
		}

		/// <summary>
		/// Clicks on the specified face of an object by local ID
		/// </summary>
		/// <param name="objectLocalId">Local id of the object to click</param>
		/// <param name="faceIndex">Index of face to click on the object</param>
		private void ClickObjectFace(uint objectLocalId, int faceIndex)
		{
			lock (workerThreadLock)
			{
				OutputLine("ClickObjectFace: StopThreadWork", OutputLevel.Game);
				StopThreadWork();
				OutputLine("ClickObjectFace: PULSE because new threadwork...", OutputLevel.Threading);
				threadWork = new ThreadWorkClickFace(objectLocalId, faceIndex);
				Monitor.Pulse(workerThreadLock);
				OutputLine("ClickObjectFace: WAIT for thread to start working...", OutputLevel.Threading);
				Monitor.Wait(workerThreadLock);
				OutputLine("ClickObjectFace: woke up", OutputLevel.Threading);
			}
		}

		/// <summary>
		/// Clicks on the specified die (0-5)
		/// </summary>
		/// <param name="dieIndex">Index of die to click (0-5)</param>
		private void ClickDie(int dieIndex)
		{
			// <summary>
			// Lookup table to determine which face we need to look at to view die N.
			// From left to right: Die 1 is face 3, Die 2 is face 0, and Die 3 is face 1
			// TODO: I think we can pull these from the dice description...
			// </summary>
			int[] dieFaceIndices = new int[]
			{
				3, // Die 1	(Set 1)
				0, // Die 2	(Set 1)
				1, // Die 3	(Set 1)
				3, // Die 4	(Set 2)
				0, // Die 5	(Set 2)
				1  // Die 6	(Set 2)
			};

			currentState = State.SelectingDie;
			ClickObjectFace(dieIndex < 3 ? diceLeftId : diceRightId, dieFaceIndices[dieIndex]);
		}

		/// <summary>
		/// Clicks on the 'roll' button
		/// </summary>
		private void ClickRoll()
		{
			currentState = State.RollingDice;
			ClickObjectFace(gameButtonsId, 6);
		}

		/// <summary>
		/// Clicks on the 'stop' button
		/// </summary>
		private void ClickStop()
		{
			currentState = State.WaitingForOurTurn;
			ClickObjectFace(gameButtonsId, 7);
			// TODO: score should change on stop, let's just use that as a quick hack to confirm we've stopped XD
			//OutputLine("ClickStop: StopThreadWork");
			//StopThreadWork();
		}

		/// <summary>
		/// Selects a die from our dice queue.
		/// </summary>
		private void SelectDieFromQueue()
		{
			if (diceQueue.Count > 0)
			{
				dieIndexWereSelecting = diceQueue.Dequeue();

				// We now expect this die to be 'used' during our next turn, but if
				//  if we've used all 6 dice then the next turn must consist of 6
				//  'new' dice.
				expectedDiceStatus[dieIndexWereSelecting] = FaceStatus.Used;
				if (expectedDiceStatus.All(n => n == FaceStatus.Used))
				{
					// TODO: Free die must be reset
					game.freeDieValue = -1;
					for (int i = 0; i < expectedDiceStatus.Length; i++)
					{
						expectedDiceStatus[i] = FaceStatus.New;
					}
				}

				ClickDie(dieIndexWereSelecting);
			}
		}
		#endregion

		/// <summary>
		/// Determines if it's our turn and the game board is in its expected state. This
		/// function should work pretty well because the expectedDieStatus for each die will
		/// either be 'used' or 'new'. When the server gives us a new set of dice then all the
		/// dice will have to be either 'used' or 'new'. 'used' aren't updated and the only
		/// way a die can be in the 'new' state is if the server sends us the updated dice
		/// withe the 'new' status.
		/// </summary>
		/// <returns>True if we're ready to board is done rolling the dice and we're ready to start selecting</returns>
		private bool IsBoardReady()
		{
			for (int i = 0; i < expectedDiceStatus.Length; i++)
			{
				if (currentDiceFaces[i] == null)
				{
					return false;
				}
				if (GetDieStatus(currentDiceFaces[i]) != expectedDiceStatus[i])
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// The current turn score has been updated f
		/// TODO: Remove me
		/// </summary>
		/// <param name="e"></param>
		private void OnScoreUpdate(TerseObjectUpdateEventArgs e)
		{
			//List<Primitive.TextureEntryFace> faces = new List<Primitive.TextureEntryFace>()
			//{
			//	e.Update.Textures.GetFace(3), // Ten-thousands
			//	e.Update.Textures.GetFace(7), // Thousands
			//	e.Update.Textures.GetFace(4), // Hundreds
			//	e.Update.Textures.GetFace(6), // Tens
			//	e.Update.Textures.GetFace(1), // Ones
			//};
			//
			//int realPointsThisTurn = 0;
			//for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
			//{
			//	UVEntry currentUv = new UVEntry(faces[faceIndex].OffsetU, faces[faceIndex].OffsetV);
			//	bool foundDigit = false;
			//	realPointsThisTurn *= 10;
			//
			//	for (int digit = 0; digit < perScoreFaceUVs.GetLength(1); digit++)
			//	{
			//		if (IsAboutEqual(currentUv.U, perScoreFaceUVs[faceIndex, digit].U) && IsAboutEqual(currentUv.V, perScoreFaceUVs[faceIndex, digit].V))
			//		{
			//			if (digit <= 9)
			//			{
			//				realPointsThisTurn += digit;
			//			}
			//
			//			foundDigit = true;
			//			break;
			//		}
			//	}
			//
			//	if (!foundDigit)
			//	{
			//		OutputLine("**************  ERROR: Failed to find digit!!!");
			//		OutputLine("**************  ERROR: Failed to find digit!!!");
			//		OutputLine("**************  ERROR: Failed to find digit!!!");
			//	}
			//}
			//if (pointsThisTurn != realPointsThisTurn)
			//{
			//	OutputLine("Discrepency detected in points: Expected " + pointsThisTurn + " but displaying: " + realPointsThisTurn);
			//	pointsThisTurn = realPointsThisTurn;
			//}

			//OutputLine("Score: " + pointsThisTurn);
			//instance.Client.Self.Chat(pointsThisTurn.ToString(), 407407407, ChatType.Normal);

			// TODO: this will likely cause some problems
			OutputLine("OnScoreUpdate: stopping thread work...", OutputLevel.Game);
			StopThreadWork();
		}

		
		/// <summary>
		/// A die on the game board has been updated.
		/// </summary>
		/// <param name="isLeftSet">Determines which set of dice have been updated.</param>
		/// <param name="textures">Updated face texture(s).</param>
		private void OnDiceFaceUpdate(bool isLeftSet, Primitive.TextureEntry textures)
		{	
			int diceOffset = isLeftSet ? 0 : 3;

			Primitive.TextureEntryFace[] previousDiceFaces = new Primitive.TextureEntryFace[6];
			currentDiceFaces.CopyTo(previousDiceFaces, 0);

			currentDiceFaces[0 + diceOffset] = textures.GetFace(3);
			currentDiceFaces[1 + diceOffset] = textures.GetFace(0);
			currentDiceFaces[2 + diceOffset] = textures.GetFace(1);

			if (currentState == State.SelectingDie)
			{
				//OutputLine("OnDiceFaceUpdate: SelectingDie -> StopThreadWork", OutputLevel.Game);
				OutputLine("OnDiceFaceUpdate: Detecting die change...", OutputLevel.Game);
				DetectDieChange(previousDiceFaces);
			}
			else if (currentState == State.RollingDice)
			{
				if (!IsBoardReady())
				{
					return;
				}
				OutputLine("OnDiceFaceUpdate: RollingDice -> StopThreadWork", OutputLevel.Game);
				StopThreadWork();

				bool isBust = false;

				if (game == null)
				{
					OutputLine(" ***** GAME IS NULL ***** ", OutputLevel.Error);
					currentState = State.WaitingForOurTurn;
					return;
				}

				continueRolling = game.ChooseDiceToRoll(ref diceQueue, GetActiveDice(), ref pointsThisTurn, myGameScore, marksAgainstUs, ref isBust);
				if (diceQueue.Count == 0)
				{
					pointsThisTurn = 0;
					OutputLine("Busted!", OutputLevel.Game);
					game = null;

					currentState = State.WaitingForOurTurn;
					return;
				}

				SelectDieFromQueue();
			}
		}


		private void DetectDieChange(Primitive.TextureEntryFace[] previousDiceFaces)
		{
			for (int i = 0; i < currentDiceFaces.Length; i++)
			{
				if (currentDiceFaces[i] == null)
				{
					continue;
				}

				FaceStatus previousStatus = GetDieStatus(previousDiceFaces[i]);
				FaceStatus currentStatus = GetDieStatus(currentDiceFaces[i]);
				if (previousStatus != currentStatus)
				{
					OutputLine("DetectDieChange: Die " + i + " went from " + previousStatus + " to " + currentStatus + ". Is our die = " + (i == dieIndexWereSelecting), OutputLevel.Game);
					OnDieStatusChange(i, previousStatus, currentStatus);
				}
			}
		}

		/// <summary>
		/// Status of atleast one die has changed.
		/// </summary>
		/// <param name="dieIndex">Index of the die that changed.</param>
		/// <param name="previousStatus">Previous status of the die.</param>
		/// <param name="currentStatus">Current status of the die.</param>
		private void OnDieStatusChange(int dieIndex, FaceStatus previousStatus, FaceStatus currentStatus)
		{
			if (dieIndex == dieIndexWereSelecting && currentStatus == FaceStatus.Selected)
			{
				if (diceQueue.Count == 0)
				{
					if (!continueRolling)
					{
						//OutputLine("Game logic said we need to stop rolling");
						myGameScore += pointsThisTurn;
						pointsThisTurn = 0;
						OutputLine("OnDieStatusChange: Schedule stop", OutputLevel.Game);
						ClickStop();
					}
					else
					{
						OutputLine("OnDieStatusChange: Schedule roll", OutputLevel.Game);
						ClickRoll();
					}
				}
				else
				{
					OutputLine("OnDieStatusChange: Schedule select", OutputLevel.Game);
					SelectDieFromQueue();
				}
			}
			else
			{
				OutputLine("OnDieStatusChange: Don't care", OutputLevel.Game);
			}
		}

		/// <summary>
		/// Begins our game logic - must be called at the beginning of our turn before we roll - usually
		/// indicated by our player light turning on.
		/// </summary>
		private void StartOurTurn()
		{
			currentDiceFaces = new Primitive.TextureEntryFace[6];
			game = new GameLogic();

			// We expect the game to give us a clean board of 6 'new' dice.
			for (int i = 0; i < expectedDiceStatus.Length; i++)
			{
				expectedDiceStatus[i] = FaceStatus.New;
			}

			OutputLine("StartOurTurn: Schedule Roll", OutputLevel.Game);
			ClickRoll();
		}

		/// <summary>
		/// Called whenever a light has been updated to indicate a change of turns.
		/// </summary>
		/// <param name="lightPrim">Light prim that was updated.</param>
		private void UpdateLight(Primitive lightPrim)
		{
			int lightIndex = -1;
			for (int i = 0; i < playerLightIds.Length; i++)
			{
				if (playerLightIds[i] == lightPrim.ID)
				{
					lightIndex = i;
				}
			}

			if (lightIndex == playerIndex)
			{
				if (lightPrim.Text == null)
				{
					OutputLine("UpdateLight: Lightprim.Text == null!", OutputLevel.Error);
					return;
				}
				if (lightPrim.Textures == null)
				{
					OutputLine("UpdateLight: lightPrim.Textures == null", OutputLevel.Error);
					return;
				}

				string[] statusText = lightPrim.Text.Split(new char[] { '\n' });
				if (statusText.Length < 2)
				{
					OutputLine("UpdateLight: OutputText < 2!", OutputLevel.Error);
					return;
				}

				marksAgainstUs = statusText[1].Count(n => n == '*');

				if (lightPrim.Textures.GetFace(0).RGBA != ColorLightOff)
				{
					OutputLine("UpdateLight: Turn started", OutputLevel.Game);
					StartOurTurn();
				}
				else
				{
					OutputLine("UpdateLight: Turn ended", OutputLevel.Game);
				}
			}
		}
	}

	public class Die
	{
		/// <summary>
		/// Index of this die.
		/// </summary>
		public int Index;
		/// <summary>
		/// Value of this die.
		/// </summary>
		public int Value;

		public Die(int index, int value)
		{
			this.Index = index;
			this.Value = value;
		}
	}

	public class GameLogic
	{
		/// <summary>
		/// If we have made a three of a kind using a non-scoring die then that non-scoring die
		/// is now a scoring die of 0 points until we are done with the current board and the dice
		/// are all reset.
		/// </summary>
		public int freeDieValue = -1;

		/// <summary>
		/// Determines if the full house rule is enabled.
		/// </summary>
		public static bool IsFullHouseEnabled { get; set; }
		/// <summary>
		/// Determines if the small straight rule is enabled.
		/// </summary>
		public static bool IsSmallStraightEnabled { get; set; }
		/// <summary>
		/// Determines if we should roll again after we've selected our scoring dice or
		/// if we should stop rolling and end our turn.
		/// </summary>
		public bool ShouldRollAgain { get; set; }

		private class Points
		{
			/// <summary>
			/// A run of 6 sequencial dice
			/// Ex: 123456
			/// </summary>
			public const int Straight = 1800;
			/// <summary>
			/// Three pair of dice.
			/// Ex: 112233
			/// </summary>
			public const int ThreePair = 1000;
			/// <summary>
			/// A run of 5 sequencial dice.
			/// Ex: 12345 or 23456
			/// </summary>
			public const int SmallStraight = 900; // 1-5 or 2-6
			/// <summary>
			/// 4 of the same kind + 2 of the same kind. 
			/// Ex: 444422
			/// </summary>
			public const int FullHouse = 1000; // 4x of the same kind + 2x of the same kind

			/// <summary>
			/// Gets the point worth of the face value of a die (1-6).
			/// </summary>
			/// <param name="dieValue">Face value of a die (1-6)</param>
			/// <returns>Point value of the die.</returns>
			public static int GetWorth(int dieValue)
			{
				if (dieValue == 1)
				{
					return 100;
				}
				if (dieValue == 5)
				{
					return 50;
				}

				return 0;
			}

			/// <summary>
			/// Gets the number of points the specified set dice with the same face values.
			/// Ex: 111 = 1000, 2222 = 400, 333 = 300
			/// </summary>
			/// <param name="dieValue">Value of die.</param>
			/// <param name="dieCount">Number of times this die appears (Must be >= 3)</param>
			/// <returns></returns>
			public static int GetRunWorth(int dieValue, int dieCount)
			{
				if (dieCount < 3)
				{
					throw new ApplicationException("Must have a run of atleast 3 dice with the same face value");
				}

				if (dieValue == 1)
				{
					// 3x 1 = 1000 points
					return 1000 * (int)Math.Pow(2, dieCount - 3);
				}

				// 3x N = (N*100) points
				return (dieValue * 100) * (int)Math.Pow(2, dieCount - 3);
			}
		}

		private void OutputLine(string msg)
		{
			//GreedyBotPlugin.instance.TabConsole.MainChatManger.TextPrinter.PrintTextLine("DEBUG: " + msg, Color.ForestGreen);
		}

		/// <summary>
		/// Checks to see if the current hand (dieCounts) contains three pair. If it does then
		/// the required dice are added to the selection set (toSelect) and the worth of the
		/// selected dice are returned.
		/// </summary>
		/// <param name="dieCounts">Number of each die in hand.</param>
		/// <param name="toSelect">[Out] Selected dice.</param>
		/// <returns>Number of points the selection is worth</returns>
		private int CheckThreePair(int[] dieCounts, ref int[] toSelect)
		{
			int pairCount = 0;

			for (int i = 0; i < 6; ++i)
			{
				if (dieCounts[i] == 2)
				{
					pairCount++;
				}
			}
			if (pairCount == 3)
			{
				OutputLine("Rule 1 - three pair");
				dieCounts.CopyTo(toSelect, 0);

				return Points.ThreePair;
			}

			return 0;
		}

		/// <summary>
		/// Checks to see if the current hand (dieCounts) contains a straight. If it does then
		/// the required dice are added to the selection set (toSelect) and the worth of the
		/// selected dice are returned.
		/// </summary>
		/// <param name="dieCounts">Number of each die in hand.</param>
		/// <param name="toSelect">[Out] Selected dice.</param>
		/// <returns>Number of points the selection is worth</returns>
		private int CheckStraight(int[] dieCounts, ref int[] toSelect)
		{

			if (dieCounts[1] > 0 && dieCounts[2] > 0 && dieCounts[3] > 0 && dieCounts[4] > 0)
			{
				bool hasStraight = false;

				if (!IsSmallStraightEnabled)
				{
					hasStraight = dieCounts[0] > 0 && dieCounts[5] > 0;
				}
				else
				{
					hasStraight = dieCounts[0] > 0 || dieCounts[5] > 0;
				}

				if (hasStraight)
				{
					// select all of 1, 5 and one of 2-4
					toSelect[0] = dieCounts[0];
					toSelect[1] = 1;
					toSelect[2] = 1;
					toSelect[3] = 1;
					toSelect[4] = dieCounts[4];

					// Select die '6' if it exists. We're handing both small and full straight here
					//  so we may of already selected all of our dice (1-5)
					if (dieCounts[5] > 0)
					{
						toSelect[5] = 1;
					}


					if (dieCounts[0] > 0 && dieCounts[5] > 0)
					{
						OutputLine("Rule 2A - Straight");
						return Points.Straight;
					}
					else
					{
						OutputLine("Rule 2B - Small Straight");
						return Points.SmallStraight;
					}
				}
			}

			return 0;
		}


		/// <summary>
		/// Checks to see if the current hand (dieCounts) contains one or more runs of 3x dice
		/// of the same face value, or if the hand contains a full house. The required dice are
		/// added to the selection set (toSelect) and the worth of the selected dice are returned.
		/// </summary>
		/// <param name="dieCounts">Number of each die in hand.</param>
		/// <param name="toSelect">[Out] Selected dice.</param>
		/// <returns>Number of points the selection is worth</returns>
		private int CheckThreeOfAKindAndFullhouse(int[] dieCounts, ref int[] toSelect)
		{
			bool hasSinglePair = false;
			bool hasFourOfAKind = false;
			int indexOfSinglePair = 0;
			int pointsWorth = 0;

			for (int i = 0; i < 6; i++)
			{
				if (dieCounts[i] >= 3)
				{
					if (dieCounts[i] == 4)
					{
						hasFourOfAKind = true;
					}
					if (i != 0 && i != 4)
					{
						freeDieValue = i + 1;
					}

					pointsWorth += Points.GetRunWorth(i + 1, dieCounts[i]);

					toSelect[i] = dieCounts[i];
				}
				else if (dieCounts[i] == 2)
				{
					indexOfSinglePair = i;
					hasSinglePair = true;
				}
			}

			if (hasFourOfAKind && hasSinglePair && IsFullHouseEnabled)
			{
				toSelect[indexOfSinglePair] = 2;

				OutputLine("Rule 3B - Full House");
				return Points.FullHouse;
			}

			if (pointsWorth != 0)
			{
				OutputLine("Rule 3A - One or two 3x of same value");
			}

			return pointsWorth;
		}

		/// <summary>
		/// Checks to see if the current hand (dieCounts) contains a special set. If it does then
		/// the required dice are added to the selection set (toSelect) and the worth of the
		/// selected dice are returned.
		/// </summary>
		/// <param name="dieCounts">Number of each die in hand.</param>
		/// <param name="toSelect">[Out] Selected dice.</param>
		/// <returns>Number of points the selection is worth</returns>
		private int CheckSpecialRuns(int[] dieCounts, ref int[] toSelect)
		{
			int scoreThisHand = 0;

			scoreThisHand = CheckThreePair(dieCounts, ref toSelect);
			if (scoreThisHand != 0)
			{
				return scoreThisHand;
			}

			scoreThisHand = CheckStraight(dieCounts, ref toSelect);
			if (scoreThisHand != 0)
			{
				return scoreThisHand;
			}

			return CheckThreeOfAKindAndFullhouse(dieCounts, ref toSelect);
		}

		/// <summary>
		/// Determines if the specified die value (1-6) can be scored (Selected).
		/// </summary>
		/// <param name="faceValue">Face value of the die (1-6).</param>
		/// <returns>True if the die can be scored (Selected).</returns>
		private bool IsScoringValue(int faceValue)
		{
			return Points.GetWorth(faceValue) != 0 || faceValue == freeDieValue;
		}

		/// <summary>
		/// Chooses which dice to select from activeDice.
		/// </summary>
		/// <param name="diceQueue">[Out] Queue of dice that we want to select.</param>
		/// <param name="activeDice">Our current hand of dice.</param>
		/// <param name="score">[In,Out] Current turn score.</param>
		/// <param name="myGameScore">Current game score.</param>
		/// <param name="marksAgainstUs">Number of consecutive busts we've had.</param>
		/// <param name="isBust">[Out] Determines if we busted (TODO: REMOVE ME - DEBUG PURPOSES ONLY)</param>
		/// <returns>True if we should continue rolling. False if we should select all the dice from diceQueue and en our turn.</returns>
		public bool ChooseDiceToRoll(ref Queue<int> diceQueue, List<Die> activeDice, ref int score, int myGameScore, int marksAgainstUs, ref bool isBust)
		{
			OutputLine("ChooseDiceToRoll(Score = " + score + ", myGameScore = " + myGameScore + ", marksAgainstUs =" + marksAgainstUs + ")");
			diceQueue.Clear();
			StringBuilder sb = new StringBuilder("Dice: ");

			int[] dieCounts = new int[6];
			int[] toSelect = new int[6];

			foreach (var die in activeDice)
			{
				dieCounts[die.Value - 1]++;
			}

			score += CheckSpecialRuns(dieCounts, ref toSelect);

			List<Die> unusedDice = new List<Die>();
			foreach (var die in activeDice)
			{
				if (toSelect[die.Value - 1] > 0)
				{
					diceQueue.Enqueue(die.Index);
					toSelect[die.Value - 1]--;
					dieCounts[die.Value - 1]--;
				}
				else
				{
					unusedDice.Add(die);
				}
			}
			if (unusedDice.Count == 0)
			{
				return true;
			}

			// See if we only have scorable dice left. If we do then we should probably re-roll.

			Die bestUnusedDie = null;
			int totalUnusedScoringDiePoints = 0;
			int scorableDiceRemaining = 0;

			foreach (var die in unusedDice)
			{
				if (IsScoringValue(die.Value))
				{
					int dieWorth = Points.GetWorth(die.Value);
					totalUnusedScoringDiePoints += dieWorth;

					if (bestUnusedDie == null || dieWorth > Points.GetWorth(bestUnusedDie.Value))
					{
						bestUnusedDie = die;
					}

					++scorableDiceRemaining;
				}
			}

			if (scorableDiceRemaining == unusedDice.Count)
			{
				OutputLine("Adding all remainign scoring dice and rolling again...");
				foreach (var unusedDie in unusedDice)
				{
					score += Points.GetWorth(unusedDie.Value);
					diceQueue.Enqueue(unusedDie.Index);
				}

				return true;
			}

			if (scorableDiceRemaining > 0)
			{
				bool selectAllUnusedDice = false;
				bool continueRolling = false;

				bool hasFreeDie = freeDieValue != -1 && freeDieValue != 1 && freeDieValue != 5;
				int pointThreshold;

				if (!IsSmallStraightEnabled)
				{
					pointThreshold = 200;
				}
				else
				{
					pointThreshold = 250;
				}

				// we *have* to roll until we hit 1000...
				if (myGameScore == 0 && score < 1000)
				{
					selectAllUnusedDice = false;
					continueRolling = true;

					OutputLine("Rule: We must exceed 1000 points until our game score != 0");
				}
				else if (marksAgainstUs == 2)
				{
					selectAllUnusedDice = true;
					continueRolling = false;

					OutputLine("Rule: We have 2 marks against us, we must not take the risk!");
				}
				else if (score + totalUnusedScoringDiePoints >= pointThreshold)
				{
					// We have enough points
					selectAllUnusedDice = true;
					continueRolling = false;

					OutputLine("Rule: Scoring dice remain and " + (score + totalUnusedScoringDiePoints) + " >= " + pointThreshold);
				}
				else
				{
					// We need some risk, pick the best die and hope for the best
					selectAllUnusedDice = false;
					continueRolling = true;

					OutputLine("Rule: Scoring dice remain and point < pointThreshhold");
				}

				if (selectAllUnusedDice)
				{
					foreach (var die in unusedDice)
					{
						if (IsScoringValue(die.Value))
						{
							score += Points.GetWorth(die.Value);
							diceQueue.Enqueue(die.Index);
						}
					}
				}
				else
				{
					score += Points.GetWorth(bestUnusedDie.Value);
					diceQueue.Enqueue(bestUnusedDie.Index);
				}

				return continueRolling;
			}
			else
			{
				if (diceQueue.Count == 0)
				{
					isBust = true;
					return false;
				}

				// we *have* to roll until we hit 1000...
				if (myGameScore == 0 && score < 1000)
				{
					OutputLine("Rule: We must exceed 1000 points until our game score != 0");
					return true;
				}

				// Not going to risk it, if we bust we're screwed
				if (marksAgainstUs == 2)
				{
					OutputLine("Rule: We have 2 marks against us, we must not take the risk!");
					return false;
				}

				if (score >= 400)
				{
					OutputLine("Rule: No scorable dice left, but we do have >= 400 points");
					return false;
				}

				return unusedDice.Count >= 4;
			}
		}
	}
}
