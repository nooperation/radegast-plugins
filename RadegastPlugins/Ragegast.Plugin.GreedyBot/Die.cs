using System;
using OpenMetaverse;

namespace Ragegast.Plugin.GreedyBot
{
	public class Die
	{
		public enum FaceStatus
		{
			/// <summary>
			/// Die is in an unknown state.
			/// </summary>
			Unknown,
			/// <summary>
			/// Die is selectable (white die)
			/// </summary>
			Normal,
			/// <summary>
			/// Die is already selected (Red)
			/// </summary>
			Selected,
			/// <summary>
			/// Die is new (Green)
			/// </summary>
			New,
			/// <summary>
			/// Die is used from a previous round (Blue)
			/// </summary>
			Used
		};

		/// <summary>
		/// Color of the die when it's normal / White
		/// </summary>
		private static readonly Color4 DieColorNormal = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's selected / Red
		/// </summary>
		private static readonly Color4 DieColorSelected = new Color4(1.0f, 0.0f, 0.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's new / Green
		/// </summary>
		private static readonly Color4 DieColorNew = new Color4(0.0f, 1.0f, 0.0f, 1.0f);
		/// <summary>
		/// Color of the die when it's used / Blue
		/// </summary>
		private static readonly Color4 DieColorUsed = new Color4(0.0f, 0.0f, 1.0f, 1.0f);

		/// <summary>
		/// Index of this die.
		/// </summary>
		public int Index;
		/// <summary>
		/// Value of this die.
		/// </summary>
		public int Value;
		/// <summary>
		/// Status of this die.
		/// </summary>
		public FaceStatus Status;

		/// <summary>
		/// Constructs a new die
		/// </summary>
		/// <param name="index">Index of the die (0-5) from left to right.</param>
		/// <param name="value">Face value of the die (1-6).</param>
		/// <param name="status">State of the die.</param>
		public Die(int index, int value, FaceStatus status)
		{
			this.Index = index;
			this.Value = value;
			this.Status = status;
		}

		/// <summary>
		/// Constructs a new die.
		/// </summary>
		/// <param name="index">Index of the die (0-5) from left to right.</param>
		/// <param name="dieFace">TextureEntryFace for this die.</param>
		public Die(int index, Primitive.TextureEntryFace dieFace)
		{
			this.Index = index;
			this.Value = GetDieValueFromFace(dieFace);
			this.Status = GetDieStatus(dieFace);
		}

		/// <summary>
		/// Determines if this die is 'active'. All 'new' and 'normal' (green or white) dice
		/// are considered 'active'
		/// </summary>
		/// <returns>True if this die is active.</returns>
		public bool IsActive()
		{
			return Status == FaceStatus.Normal || Status == FaceStatus.New;
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

		/// <summary>
		/// Determines the status of the specified die face. This is based off of the current
		/// color of the face.
		/// </summary>
		/// <param name="dieFace">Face of the die to determine the status of</param>
		/// <returns>Current status of the die.</returns>
		private static FaceStatus GetDieStatus(Primitive.TextureEntryFace dieFace)
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
	}
}