using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace Ragegast.Plugin.GreedyBot
{
	class GameComponents
	{
		/// <summary>
		/// UUID of the creator of the GreedyGreedy board this bot is for. Helps filter out all the other
		/// objects we don't care about when we're looking for the board and it's children.
		/// </summary>
		private readonly UUID creatorId = new UUID("974cd5a0-16ca-42a9-bac6-8d583b7d7438");
		/// <summary>
		/// UUID of the game table.
		/// </summary>
		public UUID tableId = UUID.Zero;
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
		/// <summary>
		/// Index of player on game board. This index corresponds to an entry in 'playerLightIds'
		/// </summary>
		public int PlayerIndex = -1;

		private Dice gameDice = new Dice();
		private ScoreDisplay gameScore = new ScoreDisplay();
		private Light gameLight = new Light();

		public Action<int, Die.FaceStatus, Die.FaceStatus> DiceStatusChanged;
		public Action<int, int, int, bool> LightChanged;
		public Action<int> ScoreChanged;
		public Action AllComponentsFound;

		/// <summary>
		/// Determines if all necessary game objects have been found to be able to start playing the game.
		/// </summary>
		/// <returns>True if we've found all necessary game objects.</returns>
		public bool HasFoundAllGameComponents()
		{
			return diceLeftId != UUID.Zero
			       && diceRightId != UUID.Zero
			       && gameButtonsId != UUID.Zero
			       && scoreId != UUID.Zero
			       && playerLightIds.All(n => n != UUID.Zero)
			       && PlayerIndex != -1;
		}

		public GameComponents()
		{
			gameScore.ScoreChanged = ScoreChangedHandler;
			gameDice.DiceStatusChanged = DiceChangedHandler;
			gameLight.LightChanged = LightChangedHandler;
		}

		private void DiceChangedHandler(int dieIndex, Die.FaceStatus previousStatus, Die.FaceStatus currentStatus)
		{
			if(DiceStatusChanged != null)
				DiceStatusChanged(dieIndex, previousStatus, currentStatus);
		}

		private void LightChangedHandler(int lightIndex, int gameScore, int bustCount, bool isLightOn)
		{
			if(LightChanged != null)
				LightChanged(lightIndex, gameScore, bustCount, isLightOn);
		}

		private void ScoreChangedHandler(int score)
		{
			if(ScoreChanged != null)
				ScoreChanged(score);
		}

		/// <summary>
		/// Clears the game and forgets about the game board.
		/// </summary>
		public void Clear()
		{
			diceLeftId = UUID.Zero;
			diceRightId = UUID.Zero;
			gameButtonsId = UUID.Zero;
			scoreId = UUID.Zero;
			tableId = UUID.Zero;
			playerLightIds = new UUID[playerLightIds.Length];
			PlayerIndex = -1;

			gameDice.Reset();
		}

		public void CheckPropsForGameComponents(Primitive.ObjectProperties props)
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
		}

		public void Objects_TerseObjectUpdate(object sender, TerseObjectUpdateEventArgs e)
		{
			if (e.Prim.ID == diceLeftId)
			{
				if (e.Update.Textures == null)
				{
					return;
				}
				gameDice.OnDiceFaceUpdate(true, e.Update.Textures);
				return;
			}
			if (e.Prim.ID == diceRightId)
			{
				if (e.Update.Textures == null)
				{
					return;
				}
				gameDice.OnDiceFaceUpdate(false, e.Update.Textures);
				return;
			}
			if (e.Prim.ID == scoreId)
			{
				if (e.Update.Textures == null)
				{
					return;

				}
				gameScore.OnUpdate(e.Prim.Textures);
				return;
			}
			if (playerLightIds.Contains(e.Prim.ID))
			{
				Utils.OutputLine("Updating light", Utils.OutputLevel.Game);
				int lightIndex = -1;
				for (int i = 0; i < playerLightIds.Length; i++)
				{
					if (playerLightIds[i] == e.Prim.ID)
					{
						lightIndex = i;
						break;
					}
				}

				gameLight.UpdateLight(e.Prim, lightIndex);
				return;
			}
		}

		public void Objects_ObjectPropertiesFamily(object sender, ObjectPropertiesFamilyEventArgs e)
		{
			if (!e.Properties.Name.StartsWith("Greedy Greedy Table"))
			{
				return;
			}
			if (tableId != UUID.Zero)
			{
				return;
			}

			Primitive greedyGreedyTable = GreedyBotPlugin.Instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == e.Properties.ObjectID);
			if (greedyGreedyTable == null)
			{
				Utils.OutputLine("Failed to find greedyGreedyTable!", Utils.OutputLevel.Error);
				return;
			}

			tableId = e.Properties.ObjectID;

			// Don't read cached values, they're wrong. We need the latest object name.
			uint[] greedyTableChildren = GreedyBotPlugin.Instance.Client.Network.CurrentSim.ObjectsPrimitives.FindAll(n => n.ParentID == greedyGreedyTable.LocalID).Select(n => n.LocalID).ToArray();
			if (greedyTableChildren.Length > 0)
			{
				Utils.OutputLine("Requesting " + greedyTableChildren.Length + " properties", Utils.OutputLevel.Game);
				GreedyBotPlugin.Instance.Client.Objects.SelectObjects(GreedyBotPlugin.Instance.Client.Network.CurrentSim, greedyTableChildren);
			}
		}

		public void Objects_ObjectProperties(object sender, ObjectPropertiesEventArgs e)
		{
			//if (currentState != GreedyBotPlugin.State.RetrievingGameObjectProperties)
			//{
			//	return;
			//}

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
					PlayerIndex = seatId - 1;
					Utils.OutputLine("CheckPropsForGameComponents: We're player #" + PlayerIndex, Utils.OutputLevel.Game);
				}
			}

			CheckPropsForGameComponents(e.Properties);
			if (HasFoundAllGameComponents())
			{
				AllComponentsFound();
			}
		}

		/// <summary>
		/// Must be called prior to starting a new turn to reset the state of the dice.
		/// </summary>
		public void StartNewTurn()
		{
			gameDice.Reset();
		}

		/// <summary>
		/// Determines if our dice are in the expected state. Primarly used to determine if our
		/// previous action has completed. If we just rolled then all of our unused dice must be
		/// 'new'. If we just selected a die then that die must be 'selected'.
		/// </summary>
		/// <param name="expectedDiceStatus">Array of expected die statuses. Must be an array of size 6, one for each die (left to right on game board)</param>
		/// <returns>True if all of our dice match the expected status.</returns>
		public bool AreDiceReady(Die.FaceStatus[] expectedDiceStatus)
		{
			return gameDice.AreDiceReady(expectedDiceStatus);
		}

		/// <summary>
		/// Returns a collection of selectable dice (New or Normal status)
		/// </summary>
		/// <returns>Collection of selectable dice (New or Normal status)</returns>
		public IEnumerable<Die> GetActiveDice()
		{
			return gameDice.GetActiveDice();
		}

		private uint GetLocalId(UUID objectId)
		{
			Primitive targetObject = GreedyBotPlugin.Instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == objectId);
			if (targetObject == null)
			{
				Utils.OutputLine("ClickObjectFace: Failed to find object by UUID", Utils.OutputLevel.Error);
				return 0;
			}

			return targetObject.LocalID;
		}

		///// <summary>
		///// Clicks on the specified face of an object by UUID
		///// </summary>
		///// <param name="objectId">UUID of the object to click</param>
		///// <param name="faceIndex">Index of face to click on the object</param>
		///// <param name="threadWork"></param>
		//private void ClickObjectFace(UUID objectId, int faceIndex, IThreadWork threadWork)
		//{
		//	Primitive targetObject = GreedyBotPlugin.Instance.Client.Network.CurrentSim.ObjectsPrimitives.Find(n => n.ID == objectId);
		//	if (targetObject == null)
		//	{
		//		Utils.OutputLine("ClickObjectFace: Failed to find object by UUID", Utils.OutputLevel.Error);
		//		return;
		//	}
		//
		//	ClickObjectFace(targetObject.LocalID, faceIndex, threadWork);
		//}
		//
		///// <summary>
		///// Clicks on the specified face of an object by local ID
		///// </summary>
		///// <param name="objectLocalId">Local id of the object to click</param>
		///// <param name="faceIndex">Index of face to click on the object</param>
		///// <param name="threadWork"></param>
		//private void ClickObjectFace(uint objectLocalId, int faceIndex, IThreadWork threadWork)
		//{
		//	ThreadPool.Instance.SetWork(threadWork);
		//}

		/// <summary>
		/// Clicks on the specified die (0-5)
		/// </summary>
		/// <param name="dieIndex">Index of die to click (0-5)</param>
		public void ClickDie(int dieIndex, int maxRetries = 0, Action failureFunc = null)
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

			uint localId = GetLocalId(dieIndex < 3 ? diceLeftId : diceRightId);
			ThreadPool.Instance.SetWork(new ThreadWorkClickFace(localId, dieFaceIndices[dieIndex], maxRetries, failureFunc));
		}

		/// <summary>
		/// Clicks on the 'roll' button
		/// </summary>
		public void ClickRoll(int maxRetries = 0, Action failureFunc = null)
		{
			uint localId = GetLocalId(gameButtonsId);
			ThreadPool.Instance.SetWork(new ThreadWorkClickFace(localId, 6, maxRetries, failureFunc));
		}

		/// <summary>
		/// Clicks on the 'stop' button
		/// </summary>
		public void ClickStop(int maxRetries = 0, Action failureFunc = null)
		{
			uint localId = GetLocalId(gameButtonsId);
			ThreadPool.Instance.SetWork(new ThreadWorkClickFace(localId, 7, maxRetries, failureFunc));
		}
	}
}
