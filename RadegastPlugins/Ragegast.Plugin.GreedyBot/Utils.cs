using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Ragegast.Plugin.GreedyBot
{
	public static class Utils
	{
		[DllImport("kernel32.dll")]
		private static extern void OutputDebugString(string lpOutputString);

		/// <summary>
		/// Epsilon used to compare floating point values. The two values will be considered
		/// equal if the difference between them is less than Epsilon.
		/// </summary>
		private const float Epsilon = 0.0001f;

		public enum OutputLevel
		{
			Game,
			Threading,
			Error
		}

		/// <summary>
		/// Outputs specified line to text chat (only local chat)
		/// </summary>
		public static void OutputLine(string msg, OutputLevel outputType)
		{
			OutputDebugString(GreedyBotPlugin.Instance.Client.Self.Name + " [" + outputType + "] " + msg + "\n");
			//Instance.TabConsole.MainChatManger.TextPrinter.PrintTextLine("DEBUG: " + msg, Color.CadetBlue);
		}

		/// <summary>
		/// Determines if two floating point values are close enough to be considered equal using
		/// a predetermined Epsilon to account for errors.
		/// </summary>
		/// <param name="lhs">First value to compare</param>
		/// <param name="rhs">Second value to compare</param>
		/// <returns>True if values are close enough to be considered equal</returns>
		public static bool IsAboutEqual(float lhs, float rhs)
		{
			return (lhs + Epsilon) > rhs && ((lhs - Epsilon) < rhs);
		}
	}
}
