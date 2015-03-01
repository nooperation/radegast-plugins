using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using OpenMetaverse;
using Radegast;
using System;
using System.Collections.Generic;
using System.Linq;
using Radegast.LSL;


namespace Ragegast.Plugin.GreedyBot
{
	[Radegast.Plugin(Name = "GreedyBot Plugin", Description = "Goal is to make a GreedyGreedy bot, but will most likely end up as a failure", Version = "1.0")]
	public class GreedyBotPlugin : IRadegastPlugin
	{
		private enum State
		{
			NotRunning,

			SearchingForGameBoard,
			WaitingForOurTurn,
			SelectingDie,
			RollingDice
		}

		/// <summary>
		/// Radegast Instance
		/// </summary>
		public static RadegastInstance Instance;
		/// <summary>
		/// Game logic. Determines which dice we're going to be picking.
		/// </summary>
		private GameLogic game;
		private GameComponents gameComponents;

		/// <summary>
		/// Primary synchronization object used for all network messages until I figure out what is i
		/// </summary>
		private readonly object greedyBotLock = new object();
		/// <summary>
		/// Current state of the bot.
		/// </summary>
		private State currentState = State.NotRunning;

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
		private bool isGameScoreUnknown = true;


		/// <summary>
		/// Status the set of dice need to be in to consider the roll to be complete. The server doesn't send
		/// us all of the dice updates at once so we need to keep applying our updates until currentDiceFaces
		/// matches the expected pattern. This pattern is either all 'new' dice (green) or a combination of
		/// 'new' and 'used' dice.
		/// </summary>
		private readonly Die.FaceStatus[] expectedDiceStatus = new Die.FaceStatus[6];

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
		
		//private UUID TEMPMasterId = new UUID("24036859-e20e-40c4-8088-be6b934c3891");

		// TODO: TEMP!
		private UUID sitTargetGame = new UUID("011cc6df-9d87-eb41-9127-0a6ecdd4561b");

		private UUID soundBust = new UUID("3deb1f4a-0f54-bafe-4101-7ae0816fd13f");
		private UUID soundRoll = new UUID("2b07b90c-f882-ad32-3646-4a021c5e3d72");
		private UUID soundAchievement = new UUID("6cf517c2-885d-2ed7-ebb1-aa22a5bb6134");
		private UUID soundClick = new UUID("8eae9c2b-3caa-477c-964d-c3752c23eddb");
		private UUID soundZilchWarningStartTurn = new UUID("4cb5b43f-1f75-38d9-fb70-849bdd706429");
		private UUID soundZilchWarningBustedTwice = new UUID("41f44567-cb05-890c-f0d0-869188c702a4");


		public void StartPlugin(RadegastInstance inst)
		{
			Instance = inst;
			ClearGame();
			ThreadPool.Instance.Init();

			inst.Client.Self.IM += Self_IM;
			inst.Client.Objects.TerseObjectUpdate += Objects_TerseObjectUpdate;
			inst.Client.Self.ChatFromSimulator += Self_ChatFromSimulator;
			inst.Client.Objects.ObjectProperties += Objects_ObjectProperties;
			inst.Client.Self.AvatarSitResponse += Self_AvatarSitResponse;
			inst.Client.Objects.ObjectPropertiesFamily += Objects_ObjectPropertiesFamily;
			inst.Client.Objects.AvatarSitChanged += Objects_AvatarSitChanged;

			inst.Client.Sound.SoundTrigger += Sound_SoundTrigger;
		}

		public void StopPlugin(RadegastInstance inst)
		{
			ThreadPool.Instance.KillPreviousThread();

			inst.Client.Self.IM -= Self_IM;
			inst.Client.Objects.TerseObjectUpdate -= Objects_TerseObjectUpdate;
			inst.Client.Self.ChatFromSimulator -= Self_ChatFromSimulator;
			inst.Client.Objects.ObjectProperties -= Objects_ObjectProperties;
			inst.Client.Self.AvatarSitResponse -= Self_AvatarSitResponse;
			inst.Client.Objects.ObjectPropertiesFamily -= Objects_ObjectPropertiesFamily;
			inst.Client.Objects.AvatarSitChanged -= Objects_AvatarSitChanged;

			// TODO: Trigger 'roll' on Stop -> score changed
			// TODO: Trigger 'roll' on Roll -> busted! plays
			inst.Client.Sound.SoundTrigger -= Sound_SoundTrigger;
		}


		void Sound_SoundTrigger(object sender, SoundTriggerEventArgs e)
		{
			if (e.ObjectID != gameComponents.tableId)
			{
				return;
			}

			if (e.SoundID == soundBust)
			{
				Utils.OutputLine("BUST sound played from: " + e.ObjectID.ToString(), Utils.OutputLevel.Game);
			}
			else if (e.SoundID == soundRoll)
			{
				Utils.OutputLine("Dice roll sound played from: " + e.ObjectID.ToString(), Utils.OutputLevel.Game);
			}

			Utils.OutputLine("Object " + e.ObjectID.ToString() + " plays sound " + e.SoundID, Utils.OutputLevel.Game);
		}



		private void ToggleSeat()
		{
			ClearGame();

			if (Instance.Client.Self.SittingOn != 0)
			{
				Instance.Client.Self.Stand();
			}
			else
			{
				Instance.Client.Self.RequestSit(sitTargetGame, Vector3.Zero);
			}
		}

		/// <summary>
		/// Begins our game logic - must be called at the beginning of our turn before we roll - usually
		/// indicated by our player light turning on.
		/// </summary>
		private void StartOurTurn()
		{
			Utils.OutputLine("StartOurTurn", Utils.OutputLevel.Game);

			game = new GameLogic();
			gameComponents.StartNewTurn();

			// We expect the game to give us a clean board of 6 'new' dice.
			for (int i = 0; i < expectedDiceStatus.Length; i++)
			{
				expectedDiceStatus[i] = Die.FaceStatus.New;
			}

			Utils.OutputLine("StartOurTurn: Schedule Roll", Utils.OutputLevel.Game);
			ClickRoll();
		}

		/// <summary>
		/// Clears all information about the game and game table.
		/// </summary>
		private void ClearGame()
		{
			Utils.OutputLine("ClearGame", Utils.OutputLevel.Game);
			currentState = State.NotRunning;

			gameComponents = new GameComponents();
			gameComponents.LightChanged = LightChanged;
			gameComponents.DiceStatusChanged = DiceChanged;
			gameComponents.AllComponentsFound = AllComponentsFound;
			gameComponents.ScoreChanged = ScoreChanged;

			dieIndexWereSelecting = -1;

			diceQueue = new Queue<int>();

			pointsThisTurn = 0;
			marksAgainstUs = 0;
			myGameScore = 0;
			isGameScoreUnknown = true;
			continueRolling = false;

			game = null;
		}

		/// <summary>
		/// Clicks on the specified die (0-5)
		/// </summary>
		/// <param name="dieIndex">Index of die to click (0-5)</param>
		private void ClickDie(int dieIndex)
		{
			Utils.OutputLine("ClickDie", Utils.OutputLevel.Game);
			currentState = State.SelectingDie;
			gameComponents.ClickDie(dieIndex, 10, ClickDieFailed);
		}


		/// <summary>
		/// Clicks on the 'roll' button
		/// </summary>
		private void ClickRoll()
		{
			Utils.OutputLine("ClickRoll", Utils.OutputLevel.Game);
			currentState = State.RollingDice;
			gameComponents.ClickRoll(10, ClickRollFailed);
		}

		/// <summary>
		/// Clicks on the 'stop' button
		/// </summary>
		private void ClickStop()
		{
			Utils.OutputLine("ClickStop", Utils.OutputLevel.Game);
			currentState = State.WaitingForOurTurn;
			gameComponents.ClickStop(5, ClickStopFailed);
		}

		private void ClickDieFailed()
		{
			lock (greedyBotLock)
			{
				Utils.OutputLine("ClickDieFailed: Aborting!", Utils.OutputLevel.Game);
				Instance.Client.Self.Stand();
			}
		}

		private void ClickRollFailed()
		{
			lock (greedyBotLock)
			{
				Utils.OutputLine("ClickRollFailed: Aborting!", Utils.OutputLevel.Game);
				Instance.Client.Self.Stand();
			}
		}

		private void ClickStopFailed()
		{
			lock (greedyBotLock)
			{
				Utils.OutputLine("ClickStopFailed: It failed, attempting to startOurTurn...", Utils.OutputLevel.Game);
				StartOurTurn();
			}
		}

		/// <summary>
		/// Selects a die from our dice queue.
		/// </summary>
		private void SelectDieFromQueue()
		{
			Utils.OutputLine("SelectDieFromQueue", Utils.OutputLevel.Game);
			if (diceQueue.Count <= 0)
			{
				return;
			}

			dieIndexWereSelecting = diceQueue.Dequeue();

			// We now expect this die to be 'used' during our next turn, but if
			//  if we've used all 6 dice then the next turn must consist of 6
			//  'new' dice.
			expectedDiceStatus[dieIndexWereSelecting] = Die.FaceStatus.Used;
			if (expectedDiceStatus.All(n => n == Die.FaceStatus.Used))
			{
				// TODO: Free die must be reset
				game.FreeDieValue = -1;
				for (int i = 0; i < expectedDiceStatus.Length; i++)
				{
					expectedDiceStatus[i] = Die.FaceStatus.New;
				}
			}

			ClickDie(dieIndexWereSelecting);
		}

		/// <summary>
		/// Raised whenever the score for the current turn has changed.
		/// </summary>
		/// <param name="score">New score.</param>
		void ScoreChanged(int score)
		{
			lock (greedyBotLock)
			{
				// TODO: this will likely cause some problems
				//Utils.OutputLine("ScoreChanged: stopping thread work...", Utils.OutputLevel.Game);
				//ThreadPool.Instance.StopThreadWork();
			}
		}

		/// <summary>
		/// Raised whenever all components have been found and we're ready to start playing.
		/// </summary>
		void AllComponentsFound()
		{
			lock (greedyBotLock)
			{
				Utils.OutputLine("AllComponentsFound", Utils.OutputLevel.Game);
				currentState = State.WaitingForOurTurn;

				GameLogic.IsSmallStraightEnabled = tablesWithSmallStraightRule.Contains(gameComponents.tableId);
				GameLogic.IsFullHouseEnabled = tablesWithFullHouseRule.Contains(gameComponents.tableId);

				Utils.OutputLine("AllComponentsFound: Found everything!", Utils.OutputLevel.Game);

				StartOurTurn();
			}
		}


		/// <summary>
		/// Raised whenever a game light changes. GameComponents.PlayerIndex is a lightIndex.
		/// </summary>
		/// <param name="lightIndex">Index of the light being changed. See GameComponents.PlayerIndex to see if it's our light.</param>
		/// <param name="bustCount">Number of busts in a row this player has.</param>
		/// <param name="isLightOn">Determines if the light is on or off. The light turns on to indicate whose turn it is.</param>
		void LightChanged(int lightIndex, int gameScore, int bustCount, bool isLightOn)
		{
			lock (greedyBotLock)
			{
				Utils.OutputLine("LightChanged", Utils.OutputLevel.Game);
				if (lightIndex != gameComponents.PlayerIndex)
				{
					Utils.OutputLine("LightChanged: don't care -> " + isLightOn, Utils.OutputLevel.Game);
					return;
				}

				Utils.OutputLine("GameScore: " + gameScore + "    myGameScore: " + myGameScore, Utils.OutputLevel.Game);
				marksAgainstUs = bustCount;
				if (isGameScoreUnknown)
				{
					// We will manage our own game score after we know our initial score
					Utils.OutputLine("Initial game score is: " + myGameScore, Utils.OutputLevel.Game);
					myGameScore = gameScore;
					isGameScoreUnknown = false;
				}

				if (isLightOn)
				{
					Utils.OutputLine("LightChanged: Turn started", Utils.OutputLevel.Game);
					StartOurTurn();
				}
				else
				{
					Utils.OutputLine("LightChanged: Turn ended", Utils.OutputLevel.Game);
					ThreadPool.Instance.StopThreadWork();
				}
			}
		}

		/// <summary>
		/// Raised whenever the state of a die has changed.
		/// </summary>
		/// <param name="dieIndex">Die index (0-5) from left to right.</param>
		/// <param name="previousStatus">Previous state of the die.</param>
		/// <param name="currentStatus">New state of the die.</param>
		private void DiceChanged(int dieIndex, Die.FaceStatus previousStatus, Die.FaceStatus currentStatus)
		{
			lock (greedyBotLock)
			{
				Utils.OutputLine("DiceChanged", Utils.OutputLevel.Game);
				if (currentState == State.SelectingDie)
				{
					SelectingDieCompleted(dieIndex, previousStatus, currentStatus);
				}
				else if (currentState == State.RollingDice)
				{
					if (!gameComponents.AreDiceReady(expectedDiceStatus))
					{
						return;
					}

					RollingDiceCompleted();
				}
			}
		}

		/// <summary>
		/// Raised whenever we've successfully selected a die.
		/// </summary>
		/// <param name="dieIndex">Index of the die that was selected (0-5) from left to right.</param>
		/// <param name="previousStatus">Previous state of the die.</param>
		/// <param name="currentStatus">Current state of the die.</param>
		private void SelectingDieCompleted(int dieIndex, Die.FaceStatus previousStatus, Die.FaceStatus currentStatus)
		{
			// greedyBotLock already locked...
			Utils.OutputLine("SelectingDieCompleted", Utils.OutputLevel.Game);
			if (dieIndex == dieIndexWereSelecting && currentStatus == Die.FaceStatus.Selected)
			{
				if (diceQueue.Count == 0)
				{
					if (!continueRolling)
					{
						myGameScore += pointsThisTurn;
						pointsThisTurn = 0;
						Utils.OutputLine("OnDieStatusChange: Schedule stop", Utils.OutputLevel.Game);
						ClickStop();
					}
					else
					{
						Utils.OutputLine("OnDieStatusChange: Schedule roll", Utils.OutputLevel.Game);
						ClickRoll();
					}
				}
				else
				{
					Utils.OutputLine("OnDieStatusChange: Schedule select", Utils.OutputLevel.Game);
					SelectDieFromQueue();
				}
			}
			else
			{
				Utils.OutputLine("OnDieStatusChange: Don't care", Utils.OutputLevel.Game);
			}
		}

		/// <summary>
		/// Raised whenever we've successfully rolled.
		/// </summary>
		private void RollingDiceCompleted()
		{
			// greedyBotLock already locked...
			Utils.OutputLine("RollingDiceCompleted", Utils.OutputLevel.Game);
			Utils.OutputLine("OnDiceFaceUpdate: RollingDice -> StopThreadWork", Utils.OutputLevel.Game);
			ThreadPool.Instance.StopThreadWork();

			bool isBust = false;

			if (game == null)
			{
				Utils.OutputLine(" ***** GAME IS NULL ***** ", Utils.OutputLevel.Error);
				currentState = State.WaitingForOurTurn;
				return;
			}

			continueRolling = game.ChooseDiceToRoll(ref diceQueue, gameComponents.GetActiveDice(), ref pointsThisTurn, myGameScore, marksAgainstUs, ref isBust);
			if (diceQueue.Count == 0)
			{
				pointsThisTurn = 0;
				Utils.OutputLine("Busted!", Utils.OutputLevel.Game);
				game = null;

				currentState = State.WaitingForOurTurn;
				return;
			}

			SelectDieFromQueue();
		}

		/// <summary>
		/// Raised when an ImprovedInstantMessage packet is recieved from the simulator, this is used for everything from
		/// private messaging to friendship offers. The Dialog field defines what type of message has arrived
		/// </summary>
		/// <see cref="http://lib.openmetaverse.org/docs/trunk/html/T_OpenMetaverse_InstantMessageEventArgs.htm"/>
		/// <param name="sender">Source of this event.</param>
		/// <param name="e">The date received from an ImprovedInstantMessage</param>
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

		/// <summary>
		/// Raised when the simulator sends us data containing updated sit information for our Avatar
		/// </summary>
		/// <see cref="http://lib.openmetaverse.org/docs/trunk/html/T_OpenMetaverse_AvatarSitChangedEventArgs.htm"/>
		/// <param name="sender">Source of this event.</param>
		/// <param name="e">Provides updates sit position data </param>
		void Objects_AvatarSitChanged(object sender, AvatarSitChangedEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (e.Avatar.ID != Instance.Client.Self.AgentID)
				{
					return;
				}

				// We got up, forget everything we know about the game.
				if (e.SittingOn == 0)
				{
					if (currentState != State.NotRunning)
					{
						Utils.OutputLine("Objects_AvatarSitChanged: Stopping thread work...", Utils.OutputLevel.Game);
						ThreadPool.Instance.StopThreadWork();
						ClearGame();
						//Instance.Client.Self.RequestSit(sitTargetLamp, Vector3.Zero);
					}
				}
			}
		}

		/// <summary>
		/// Raised in response to a RequestSit request
		/// </summary>
		/// <see cref="http://lib.openmetaverse.org/docs/trunk/html/M_OpenMetaverse_AgentManager_RequestSit.htm"/>
		/// <param name="sender">Source of this event.</param>
		/// <param name="e">Contains the response data returned from the simulator in response to a RequestSit</param>
		void Self_AvatarSitResponse(object sender, AvatarSitResponseEventArgs e)
		{
			lock (greedyBotLock)
			{
				Utils.OutputLine("Self_AvatarSitResponse", Utils.OutputLevel.Game);
				// We sat down, forget everything we know about previous games.
				ClearGame();

				currentState = State.SearchingForGameBoard;
				Instance.Client.Objects.RequestObjectPropertiesFamily(Instance.Client.Network.CurrentSim, e.ObjectID);
			}
		}

		/// <summary>
		/// Raised when a scripted object or agent within range sends a public message
		/// </summary>
		/// <see cref="http://lib.openmetaverse.org/docs/trunk/html/E_OpenMetaverse_AgentManager_ChatFromSimulator.htm"/>
		/// <param name="sender">Source of this event.</param>
		/// <param name="e"></param>
		void Self_ChatFromSimulator(object sender, ChatEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (e.Message.Trim().ToLower() == "shazbot!")
				{
					ClearGame();
					ToggleSeat();
				}
				if (e.SourceID != gameComponents.tableId)
				{
					return;
				}

				if (e.Message == "It is not your turn. Please wait your turn before attempting to change the playing pieces.")
				{
					Utils.OutputLine("Chat: Not our turn - StopThreadWork", Utils.OutputLevel.Game);
					ThreadPool.Instance.StopThreadWork();
					game = null;
					currentState = State.WaitingForOurTurn;
				}
				else if (e.Message == "You must accumulate at least 1000 points before you can begin scoring. Keep on truckin'.")
				{
					// TODO: This is a hack... but it semes to be an OKAY hack! If we get this message then we actually have 0 points
					//  and because we don't have a very reliable way to detect the end of the game yet to reset to 0 points we're just
					//  going to use this to do it for us!
					Utils.OutputLine("Chat: must get 1k points - scheduling roll", Utils.OutputLevel.Game);
					myGameScore = 0;
					ClickRoll();
				}
				else if (e.Message == "This is the final round, you must keep rolling until you beat the current top score or bust. Good luck, citizen.")
				{
					// TODO: Detect that this is the last round so we can use the correct algorithm in the game logic to pick our dice
					//   for now we just keep trying to roll until we either exceed the opponets score or bust.
					Utils.OutputLine("MSG 3: End of game must keep rolling till we win or bust - scheduling roll", Utils.OutputLevel.Game);
					ClickRoll();
				}
				else if (e.Message.EndsWith(" has left the game."))
				{
					// TODO: Check to see if we're the last player?
				}
				else if (e.Message.EndsWith(" has joined the game."))
				{
					// TODO: Check to see if we're the only player?
				}
				else if (e.Message.StartsWith("Welcome to the game,"))
				{
					// TODO: do anything?
				}
			}
		}

		/// <summary>
		/// Raised when the simulator sends us data containing additional and Avatar details
		/// The ObjectPropertiesFamily event occurs when the simulator sends an ObjectPropertiesPacket containing additional details for a Primitive, Foliage data or Attachment. This includes Permissions, Sale info, and other basic details on an object
		/// The ObjectProperties event is also raised when a RequestObjectPropertiesFamily(Simulator, UUID) request is made, the viewer equivalent is hovering the mouse cursor over an object
		/// </summary>
		/// <see cref="http://lib.openmetaverse.org/docs/trunk/html/E_OpenMetaverse_ObjectManager_ObjectPropertiesFamily.htm"/>
		/// <param name="sender">Source of this event.</param>
		/// <param name="e">Provides additional primitive data, permissions and sale info for the ObjectPropertiesFamily event</param>
		void Objects_ObjectPropertiesFamily(object sender, ObjectPropertiesFamilyEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (currentState == State.SearchingForGameBoard)
				{
					gameComponents.Objects_ObjectPropertiesFamily(sender, e);
				}
			}
		}

		/// <summary>
		/// Raised when the simulator sends us data containing additional information
		/// The ObjectProperties event occurs when the simulator sends an ObjectPropertiesPacket containing additional details for a Primitive, Foliage data or Attachment data
		/// The ObjectProperties event is also raised when a SelectObject(Simulator, UInt32) request is made.
		/// </summary>
		/// <see cref="http://lib.openmetaverse.org/docs/trunk/html/E_OpenMetaverse_ObjectManager_ObjectProperties.htm"/>
		/// <param name="sender">Source of this event.</param>
		/// <param name="e">Provides additional primitive data for the ObjectProperties event</param>
		void Objects_ObjectProperties(object sender, ObjectPropertiesEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (currentState == State.SearchingForGameBoard)
				{
					gameComponents.Objects_ObjectProperties(sender, e);
				}
			}
		}

		/// <summary>
		/// Raised when the simulator sends us data containing Primitive and Avatar movement changes
		/// </summary>
		/// <see cref="http://lib.openmetaverse.org/docs/trunk/html/E_OpenMetaverse_ObjectManager_TerseObjectUpdate.htm"/>
		/// <param name="sender">Source of this event.</param>
		/// <param name="e">Provides primitive data containing updated location, velocity, rotation, textures for the TerseObjectUpdate event.</param>
		void Objects_TerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
		{
			lock (greedyBotLock)
			{
				gameComponents.Objects_TerseObjectUpdate(sender, e);
			}
		}
	}
}
