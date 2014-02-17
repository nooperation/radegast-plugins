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
		public UUID diceLeftId = UUID.Zero;
		/// <summary>
		/// UUID of the prim containing the right three dice.
		/// </summary>
		public UUID diceRightId = UUID.Zero;
		/// <summary>
		/// UUID of the prim containing the Roll and Stop buttons.
		/// </summary>
		public UUID gameButtonsId = UUID.Zero;
		/// <summary>
		/// UUID of the prim containing our turn score.
		/// </summary>
		public UUID scoreId = UUID.Zero;
		/// <summary>
		/// UUIDs of the player lights to indicate the current turn.
		/// </summary>
		public UUID[] playerLightIds = new UUID[8] { UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero, UUID.Zero };


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
			       && playerLightIds.All(n => n != UUID.Zero);
		}

		public void Clear()
		{
			diceLeftId = UUID.Zero;
			diceRightId = UUID.Zero;
			gameButtonsId = UUID.Zero;
			scoreId = UUID.Zero;
			tableId = UUID.Zero;
			playerLightIds = new UUID[playerLightIds.Length];
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
	}
}
