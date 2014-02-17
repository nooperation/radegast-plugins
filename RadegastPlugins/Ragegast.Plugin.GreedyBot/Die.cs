using System;
using OpenMetaverse;

namespace Ragegast.Plugin.GreedyBot
{
	public class Die
	{
		/// <summary>
		/// Color of the die when it's normal / White
		/// </summary>
		public static readonly Color4 DieColorNormal = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's selected / Red
		/// </summary>
		public static readonly Color4 DieColorSelected = new Color4(1.0f, 0.0f, 0.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's new / Green
		/// </summary>
		public static readonly Color4 DieColorNew = new Color4(0.0f, 1.0f, 0.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's used / Blue
		/// </summary>
		public static readonly Color4 DieColorUsed = new Color4(0.0f, 0.0f, 1.0f, 1.0f);

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

		public Die(int index, Primitive.TextureEntryFace dieFace)
		{
			this.Index = index;
			this.Value = GetDieValueFromFace(dieFace);
		}

		/// <summary>
		/// Determines the value of a die from the specified face.
		/// </summary>
		/// <param name="dieFace">Face of the die to get the value of.</param>
		/// <returns>Value of die (1-6)</returns>
		private static int GetDieValueFromFace(Primitive.TextureEntryFace dieFace)
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
				if (Utils.IsAboutEqual(dieFace.OffsetU, dieFaceUOffsets[dieIndex]))
				{
					return dieIndex + 1;
				}
			}

			throw new ApplicationException("GetDieValueFromFace: Invalid die U offset: " + dieFace.OffsetU);
		}
	}
}