using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace Ragegast.Plugin.GreedyBot
{
	class Dice
	{
		public Action<int, Die.FaceStatus, Die.FaceStatus> DiceStatusChanged;

		/// <summary>
		/// Most current set of dice. Some may be NULL.
		/// </summary>
		private Die[] currentDice = new Die[6];

		public void Reset()
		{
			currentDice = new Die[6];
		}

		/// <summary>
		/// A die on the game board has been updated.
		/// </summary>
		/// <param name="isLeftSet">Determines which set of dice have been updated.</param>
		/// <param name="textures">Updated face texture(s).</param>
		public void OnDiceFaceUpdate(bool isLeftSet, Primitive.TextureEntry textures)
		{
			int diceOffset = isLeftSet ? 0 : 3;

			Die[] previousDice = new Die[6];
			currentDice.CopyTo(previousDice, 0);

			if (textures.GetFace(3) != null)
			{
				currentDice[0 + diceOffset] = new Die(0 + diceOffset, textures.GetFace(3));
			}
			if (textures.GetFace(0) != null)
			{
				currentDice[1 + diceOffset] = new Die(1 + diceOffset, textures.GetFace(0));
			}
			if (textures.GetFace(1) != null)
			{
				currentDice[2 + diceOffset] = new Die(2 + diceOffset, textures.GetFace(1));
			}

			Utils.OutputLine("OnDiceFaceUpdate: Detecting die change...", Utils.OutputLevel.Game);

			for (int i = 0; i < currentDice.Length; i++)
			{
				if (currentDice[i] == null)
				{
					continue;
				}

				if (previousDice[i] == null)
				{
					DiceStatusChanged(i, Die.FaceStatus.Unknown, currentDice[i].Status);
				}
				else
				{
					DiceStatusChanged(i, previousDice[i].Status, currentDice[i].Status);
				}
			}
		}

		/// <summary>
		/// Returns a collection of selectable dice (New or Normal status)
		/// </summary>
		/// <returns>Collection of selectable dice (New or Normal status)</returns>
		public IEnumerable<Die> GetActiveDice()
		{
			return from die in currentDice
				   where die != null && die.IsActive()
				   select die;
		}

		/// <summary>
		/// Determines if the dice have 'settled' and we can start selecting dice. This
		/// function should work pretty well because the expectedDieStatus for each die will
		/// either be 'used' or 'new'. When the server gives us a new set of dice then all the
		/// dice will have to be either 'used' or 'new'. 'used' aren't updated and the only
		/// way a die can be in the 'new' state is if the server sends us the updated dice
		/// withe the 'new' status.
		/// </summary>
		/// <returns>True if the dice are in a state where we're ready to start selecting.</returns>
		public bool AreDiceReady(Die.FaceStatus[] expectedDice)
		{
			if (expectedDice.Length != currentDice.Length)
			{
				return false;
			}

			for (int i = 0; i < expectedDice.Length; i++)
			{
				if (currentDice[i] == null)
				{
					return false;
				}
				if (currentDice[i].Status != expectedDice[i])
				{
					return false;
				}
			}

			return true;
		}
	}
}
