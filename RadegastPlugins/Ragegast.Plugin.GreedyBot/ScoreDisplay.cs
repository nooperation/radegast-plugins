using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace Ragegast.Plugin.GreedyBot
{
	class ScoreDisplay
	{
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

		public Action<int> ScoreChanged;

		/// <summary>
		/// Table of expected UV corrdinates for each face of the score prim for each digit (0-9 followed by BLANK).
		/// There are faces: 10000's, 1000's, 100's, 10's, 1's
		/// Each face has 10 digits: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, BLANK
		/// </summary>
		private static readonly UVEntry[,] perScoreFaceUVs = GenerateScoreFaceUVs();

		public void OnUpdate(Primitive.TextureEntry textures)
		{
			List<Primitive.TextureEntryFace> faces = new List<Primitive.TextureEntryFace>()
			{
				textures.GetFace(3), // Ten-thousands
				textures.GetFace(7), // Thousands
				textures.GetFace(4), // Hundreds
				textures.GetFace(6), // Tens
				textures.GetFace(1), // Ones
			};
			
			int score = 0;
			for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
			{
				UVEntry currentUv = new UVEntry(faces[faceIndex].OffsetU, faces[faceIndex].OffsetV);
				bool foundDigit = false;
				score *= 10;
			
				for (int digit = 0; digit < perScoreFaceUVs.GetLength(1); digit++)
				{
					if (Utils.IsAboutEqual(currentUv.U, perScoreFaceUVs[faceIndex, digit].U) && Utils.IsAboutEqual(currentUv.V, perScoreFaceUVs[faceIndex, digit].V))
					{
						if (digit <= 9)
						{
							score += digit;
						}
			
						foundDigit = true;
						break;
					}
				}
			
				if (!foundDigit)
				{
					Utils.OutputLine("ScoreDisplay: ***  ERROR: Failed to find digit", Utils.OutputLevel.Error);
				}
			}

			ScoreChanged(score);
		}

		/// <summary>
		/// Generates the table of UV corrdinates for each face of the score should have
		/// for each digit (0-9 followed by BLANK).
		/// These values are not exact so a slightly larger epsilon (0.0001 seems to work)
		/// will be needed when comparing these values against the actual UV coordinates
		/// the faces have.
		/// </summary>
		private static UVEntry[,] GenerateScoreFaceUVs()
		{
			UVEntry[,] perScoreFaceUVs = new UVEntry[5, 11];

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

			return perScoreFaceUVs;
		}
	}
}
