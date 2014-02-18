using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace Ragegast.Plugin.GreedyBot
{
	public class Light
	{
		public Action<int, int, int, bool> LightChanged;

		/// <summary>
		/// Color of the light when it's off / Not our turn
		/// </summary>
		public static readonly Color4 LightColorOff = new Color4(0.0f, 0.0f, 0.0f, 1.0f);
		/// <summary>
		/// Called whenever a light has been updated to indicate a change of turns.
		/// </summary>
		/// <param name="lightPrim">Light prim that was updated.</param>
		public void UpdateLight(Primitive lightPrim, int lightIndex)
		{
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
			int gameScore = 0;
			int.TryParse(statusText[1], out gameScore);

			int marksAgainstUs = statusText[1].Count(n => n == '*');
			bool isLightOn = lightPrim.Textures.GetFace(0).RGBA != LightColorOff;
			LightChanged(lightIndex, gameScore, marksAgainstUs, isLightOn);
		}
	}
}
