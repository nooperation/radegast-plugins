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
		/// Radegast Instance
		/// </summary>
		public static RadegastInstance Instance;

		/// <summary>
		/// Primary synchronization object used for all network messages until I figure out what is i
		/// </summary>
		private readonly object greedyBotLock = new object();
		/// <summary>
		/// Current state of the bot.
		/// </summary>
		private State currentState = State.NotRunning;


		/// <summary>
		/// Color of the light when it's off / Not our turn
		/// </summary>
		public static readonly Color4 LightColorOff = new Color4(0.0f, 0.0f, 0.0f, 1.0f);


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

		/// <summary>
		/// Index of player on game board. This index corresponds to an entry in 'playerLightIds'
		/// </summary>
		public int playerIndex = -1;

		private ThreadPool threadPool;
		private GameComponentIds gameComponentIds = new GameComponentIds();

		public void StopPlugin(RadegastInstance inst)
		{
			threadPool.KillPreviousThread();

			inst.Client.Objects.TerseObjectUpdate -= Objects_TerseObjectUpdate;
			inst.Client.Self.ChatFromSimulator -= Self_ChatFromSimulator;
			inst.Client.Objects.ObjectProperties -= Objects_ObjectProperties;
			inst.Client.Self.AvatarSitResponse -= Self_AvatarSitResponse;
			inst.Client.Objects.ObjectPropertiesFamily -= Objects_ObjectPropertiesFamily;
			inst.Client.Objects.AvatarSitChanged -= Objects_AvatarSitChanged;
		}

		public void StartPlugin(RadegastInstance inst)
		{
			Instance = inst;
			

			threadPool = new ThreadPool();

			inst.Client.Objects.AvatarSitChanged += Objects_AvatarSitChanged;
			inst.Client.Objects.TerseObjectUpdate += Objects_TerseObjectUpdate;
			inst.Client.Self.ChatFromSimulator += Self_ChatFromSimulator;
			inst.Client.Self.IM += Self_IM;
			inst.Client.Self.AvatarSitResponse += Self_AvatarSitResponse;
		}

		

		/// <summary>
		/// Clears all information about the game and game table.
		/// </summary>
		private void ClearGame()
		{
			currentState = State.NotRunning;

			gameComponentIds.Clear();

			dieIndexWereSelecting = -1;
			playerIndex = -1;
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
						threadPool.StopThreadWork();
						ClearGame();
						//Instance.Client.Self.RequestSit(sitTargetLamp, Vector3.Zero);
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
				Instance.Client.Objects.ObjectPropertiesFamily += Objects_ObjectPropertiesFamily;
				Instance.Client.Objects.RequestObjectPropertiesFamily(Instance.Client.Network.CurrentSim, e.ObjectID);
			}
		}


		void Objects_ObjectPropertiesFamily(object sender, ObjectPropertiesFamilyEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (currentState != State.SearchinForGameBoard)
				{
					Instance.Client.Objects.ObjectPropertiesFamily -= Objects_ObjectPropertiesFamily;
					return;
				}

				if (e.Properties.Name.StartsWith("Greedy Greedy Table"))
				{
					if (gameComponentIds.tableId != UUID.Zero)
					{
						return;
					}

					Instance.Client.Objects.ObjectPropertiesFamily -= Objects_ObjectPropertiesFamily;

					gameComponentIds.tableId = e.Properties.ObjectID;
					GameLogic.IsSmallStraightEnabled = tablesWithSmallStraightRule.Contains(gameComponentIds.tableId);
					GameLogic.IsFullHouseEnabled = tablesWithFullHouseRule.Contains(gameComponentIds.tableId);

					Primitive greedyGreedyTable = Instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == gameComponentIds.tableId);
					if (greedyGreedyTable == null)
					{
						Utils.OutputLine("Failed to find greedyGreedyTable!", Utils.OutputLevel.Error);
						return;
					}

					// Don't read cached values, they're wrong. We need the latest object name.
					uint[] greedyTableChildren = Instance.Client.Network.CurrentSim.ObjectsPrimitives.FindAll(n => n.ParentID == greedyGreedyTable.LocalID).Select(n => n.LocalID).ToArray();
					if (greedyTableChildren.Length > 0)
					{
						Utils.OutputLine("Requesting " + greedyTableChildren.Length + " properties", Utils.OutputLevel.Game);

						currentState = State.RetrievingGameObjectProperties;
						Instance.Client.Objects.ObjectProperties += Objects_ObjectProperties;
						Instance.Client.Objects.SelectObjects(Instance.Client.Network.CurrentSim, greedyTableChildren);
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
					Instance.Client.Objects.ObjectProperties -= Objects_ObjectProperties;
					return;
				}

				if (e.Properties.Name.StartsWith("Game Player"))
				{
					if (e.Properties.Description.Length == 0)
					{
						return;
					}

					int seatId = e.Properties.Description[0] - '0';
					if (seatId < 1 || seatId > 8)
					{
						return;
					}

					if (e.Properties.Name == "Game Player (" + GreedyBotPlugin.Instance.Client.Self.Name + ")")
					{
						playerIndex = seatId - 1;
						Utils.OutputLine("CheckPropsForGameComponents: We're player #" + playerIndex, Utils.OutputLevel.Game);
					}
				}

				gameComponentIds.CheckPropsForGameComponents(e.Properties);
				if (gameComponentIds.HasFoundAllGameComponents() && playerIndex != -1)
				{
					Instance.Client.Objects.ObjectProperties -= Objects_ObjectProperties;

					currentState = State.WaitingForOurTurn;
					Utils.OutputLine("Found everything!", Utils.OutputLevel.Game);
				}
			}
		}

		// TODO: TEMP!
		private UUID sitTargetGame = new UUID("011cc6df-9d87-eb41-9127-0a6ecdd4561b");

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


		void Self_ChatFromSimulator(object sender, ChatEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (e.Message.Trim().ToLower() == "shazbot!")
				{
					ClearGame();
					ToggleSeat();
				}
				if (e.SourceID != gameComponentIds.tableId)
				{
					return;
				}

				if (e.Message == "It is not your turn. Please wait your turn before attempting to change the playing pieces.")
				{
					Utils.OutputLine("Chat: Not our turn - StopThreadWork", Utils.OutputLevel.Game);
					threadPool.StopThreadWork();
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
			}
		}

		void Objects_TerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
		{
			lock (greedyBotLock)
			{
				if (e.Prim.ID == gameComponentIds.diceLeftId)
				{
					if (e.Update.Textures == null)
					{
						return;
					}
					OnDiceFaceUpdate(true, e.Update.Textures);
					return;
				}
				if (e.Prim.ID == gameComponentIds.diceRightId)
				{
					if (e.Update.Textures == null)
					{
						return;
					}
					OnDiceFaceUpdate(false, e.Update.Textures);
					return;
				}
				if (e.Prim.ID == gameComponentIds.scoreId)
				{
					OnScoreUpdate(e);
					return;
				}
				if (gameComponentIds.playerLightIds.Contains(e.Prim.ID))
				{
					UpdateLight(e.Prim);
					return;
				}
			}
		}
		#endregion

		#region TESTED_WORKING

		


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
			else if (dieFace.RGBA.CompareTo(Die.DieColorNormal) == 0)
				return FaceStatus.Normal;
			else if (dieFace.RGBA.CompareTo(Die.DieColorSelected) == 0)
				return FaceStatus.Selected;
			else if (dieFace.RGBA.CompareTo(Die.DieColorNew) == 0)
				return FaceStatus.New;
			else if (dieFace.RGBA.CompareTo(Die.DieColorUsed) == 0)
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
					Utils.OutputLine("GetActiveDice: Current faces contains invalid data!", Utils.OutputLevel.Error);
					return null;
				}

				FaceStatus status = GetDieStatus(currentDiceFaces[dieIndex]);
				if (status == FaceStatus.New || status == FaceStatus.Normal)
				{
					dice.Add(new Die(dieIndex, currentDiceFaces[dieIndex]));
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
			Primitive targetObject = Instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == objectId);
			if (targetObject == null)
			{
				Utils.OutputLine("ClickObjectFace: Failed to find object by UUID", Utils.OutputLevel.Error);
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
			threadPool.SetWork(new ThreadWorkClickFace(objectLocalId, faceIndex));
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
			ClickObjectFace(dieIndex < 3 ? gameComponentIds.diceLeftId : gameComponentIds.diceRightId, dieFaceIndices[dieIndex]);
		}

		/// <summary>
		/// Clicks on the 'roll' button
		/// </summary>
		private void ClickRoll()
		{
			currentState = State.RollingDice;
			ClickObjectFace(gameComponentIds.gameButtonsId, 6);
		}

		/// <summary>
		/// Clicks on the 'stop' button
		/// </summary>
		private void ClickStop()
		{
			currentState = State.WaitingForOurTurn;
			ClickObjectFace(gameComponentIds.gameButtonsId, 7);
			// TODO: score should change on stop, let's just use that as a quick hack to confirm we've stopped
		}

		/// <summary>
		/// Selects a die from our dice queue.
		/// </summary>
		private void SelectDieFromQueue()
		{
			if (diceQueue.Count <= 0)
			{
				return;
			}

			dieIndexWereSelecting = diceQueue.Dequeue();

			// We now expect this die to be 'used' during our next turn, but if
			//  if we've used all 6 dice then the next turn must consist of 6
			//  'new' dice.
			expectedDiceStatus[dieIndexWereSelecting] = FaceStatus.Used;
			if (expectedDiceStatus.All(n => n == FaceStatus.Used))
			{
				// TODO: Free die must be reset
				game.FreeDieValue = -1;
				for (int i = 0; i < expectedDiceStatus.Length; i++)
				{
					expectedDiceStatus[i] = FaceStatus.New;
				}
			}

			ClickDie(dieIndexWereSelecting);
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
			// TODO: this will likely cause some problems
			Utils.OutputLine("OnScoreUpdate: stopping thread work...", Utils.OutputLevel.Game);
			threadPool.StopThreadWork();
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
				//Utils.OutputLine("OnDiceFaceUpdate: SelectingDie -> StopThreadWork", OutputLevel.Game);
				Utils.OutputLine("OnDiceFaceUpdate: Detecting die change...", Utils.OutputLevel.Game);
				DetectDieChange(previousDiceFaces);
			}
			else if (currentState == State.RollingDice)
			{
				if (!IsBoardReady())
				{
					return;
				}
				Utils.OutputLine("OnDiceFaceUpdate: RollingDice -> StopThreadWork", Utils.OutputLevel.Game);
				threadPool.StopThreadWork();

				bool isBust = false;

				if (game == null)
				{
					Utils.OutputLine(" ***** GAME IS NULL ***** ", Utils.OutputLevel.Error);
					currentState = State.WaitingForOurTurn;
					return;
				}

				continueRolling = game.ChooseDiceToRoll(ref diceQueue, GetActiveDice(), ref pointsThisTurn, myGameScore, marksAgainstUs, ref isBust);
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
					Utils.OutputLine("DetectDieChange: Die " + i + " went from " + previousStatus + " to " + currentStatus + ". Is our die = " + (i == dieIndexWereSelecting), Utils.OutputLevel.Game);
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
						//Utils.OutputLine("Game logic said we need to stop rolling");
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

			Utils.OutputLine("StartOurTurn: Schedule Roll", Utils.OutputLevel.Game);
			ClickRoll();
		}

		/// <summary>
		/// Called whenever a light has been updated to indicate a change of turns.
		/// </summary>
		/// <param name="lightPrim">Light prim that was updated.</param>
		private void UpdateLight(Primitive lightPrim)
		{
			int lightIndex = -1;
			for (int i = 0; i < gameComponentIds.playerLightIds.Length; i++)
			{
				if (gameComponentIds.playerLightIds[i] == lightPrim.ID)
				{
					lightIndex = i;
					break;
				}
			}

			if (lightIndex != playerIndex)
			{
				return;
			}

			if (lightPrim.Text == null)
			{
				Utils.OutputLine("UpdateLight: Lightprim.Text == null!", Utils.OutputLevel.Error);
				return;
			}
			if (lightPrim.Textures == null)
			{
				Utils.OutputLine("UpdateLight: lightPrim.Textures == null", Utils.OutputLevel.Error);
				return;
			}

			string[] statusText = lightPrim.Text.Split(new char[] { '\n' });
			if (statusText.Length < 2)
			{
				Utils.OutputLine("UpdateLight: OutputText < 2!", Utils.OutputLevel.Error);
				return;
			}

			marksAgainstUs = statusText[1].Count(n => n == '*');

			if (lightPrim.Textures.GetFace(0).RGBA != LightColorOff)
			{
				Utils.OutputLine("UpdateLight: Turn started", Utils.OutputLevel.Game);
				StartOurTurn();
			}
			else
			{
				Utils.OutputLine("UpdateLight: Turn ended", Utils.OutputLevel.Game);
			}
		}
	}
}
