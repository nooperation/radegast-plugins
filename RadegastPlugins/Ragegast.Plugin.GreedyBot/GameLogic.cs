using System;
using System.Collections.Generic;
using System.Text;

namespace Ragegast.Plugin.GreedyBot
{
	public class GameLogic
	{
		/// <summary>
		/// Determines if the full house rule is enabled.
		/// </summary>
		public static bool IsFullHouseEnabled { get; set; }
		/// <summary>
		/// Determines if the small straight rule is enabled.
		/// </summary>
		public static bool IsSmallStraightEnabled { get; set; }

		/// <summary>
		/// If we have made a three of a kind using a non-scoring die then that non-scoring die
		/// is now a scoring die of 0 points until we are done with the current board and the dice
		/// are all reset.
		/// </summary>
		public int FreeDieValue = -1;
		/// <summary>
		/// Determines if we should roll again after we've selected our scoring dice or
		/// if we should stop rolling and end our turn.
		/// </summary>
		public bool ShouldRollAgain { get; set; }

		private struct Points
		{
			/// <summary>
			/// A run of 6 sequencial dice
			/// Ex: 123456
			/// </summary>
			public const int Straight = 1800;
			/// <summary>
			/// Three pair of dice.
			/// Ex: 112233
			/// </summary>
			public const int ThreePair = 1000;
			/// <summary>
			/// A run of 5 sequencial dice.
			/// Ex: 12345 or 23456
			/// </summary>
			public const int SmallStraight = 900; // 1-5 or 2-6
			/// <summary>
			/// 4 of the same kind + 2 of the same kind. 
			/// Ex: 444422
			/// </summary>
			public const int FullHouse = 1000; // 4x of the same kind + 2x of the same kind

			/// <summary>
			/// Gets the point worth of the face value of a die (1-6).
			/// </summary>
			/// <param name="dieValue">Face value of a die (1-6)</param>
			/// <returns>Point value of the die.</returns>
			public static int GetWorth(int dieValue)
			{
				if (dieValue == 1)
				{
					return 100;
				}
				if (dieValue == 5)
				{
					return 50;
				}

				return 0;
			}

			/// <summary>
			/// Gets the number of points the specified set dice with the same face values.
			/// Ex: 111 = 1000, 2222 = 400, 333 = 300
			/// </summary>
			/// <param name="dieValue">Value of die.</param>
			/// <param name="dieCount">Number of times this die appears (Must be >= 3)</param>
			/// <returns></returns>
			public static int GetRunWorth(int dieValue, int dieCount)
			{
				if (dieCount < 3)
				{
					throw new ApplicationException("Must have a run of atleast 3 dice with the same face value");
				}

				if (dieValue == 1)
				{
					// 3x 1 = 1000 points
					return 1000 * (int)Math.Pow(2, dieCount - 3);
				}

				// 3x N = (N*100) points
				return (dieValue * 100) * (int)Math.Pow(2, dieCount - 3);
			}
		}

		private void OutputLine(string msg)
		{
			//GreedyBotPlugin.Instance.TabConsole.MainChatManger.TextPrinter.PrintTextLine("DEBUG: " + msg, Color.ForestGreen);
		}

		/// <summary>
		/// Checks to see if the current hand (dieCounts) contains three pair. If it does then
		/// the required dice are added to the selection set (toSelect) and the worth of the
		/// selected dice are returned.
		/// </summary>
		/// <param name="dieCounts">Number of each die in hand.</param>
		/// <param name="toSelect">[Out] Selected dice.</param>
		/// <returns>Number of points the selection is worth</returns>
		private int CheckThreePair(int[] dieCounts, ref int[] toSelect)
		{
			int pairCount = 0;

			for (int i = 0; i < 6; ++i)
			{
				if (dieCounts[i] == 2)
				{
					pairCount++;
				}
			}
			if (pairCount == 3)
			{
				OutputLine("Rule 1 - three pair");
				dieCounts.CopyTo(toSelect, 0);

				return Points.ThreePair;
			}

			return 0;
		}

		/// <summary>
		/// Checks to see if the current hand (dieCounts) contains a straight. If it does then
		/// the required dice are added to the selection set (toSelect) and the worth of the
		/// selected dice are returned.
		/// </summary>
		/// <param name="dieCounts">Number of each die in hand.</param>
		/// <param name="toSelect">[Out] Selected dice.</param>
		/// <returns>Number of points the selection is worth</returns>
		private int CheckStraight(int[] dieCounts, ref int[] toSelect)
		{

			if (dieCounts[1] > 0 && dieCounts[2] > 0 && dieCounts[3] > 0 && dieCounts[4] > 0)
			{
				bool hasStraight = false;

				if (!IsSmallStraightEnabled)
				{
					hasStraight = dieCounts[0] > 0 && dieCounts[5] > 0;
				}
				else
				{
					hasStraight = dieCounts[0] > 0 || dieCounts[5] > 0;
				}

				if (hasStraight)
				{
					// select all of 1, 5 and one of 2-4
					toSelect[0] = dieCounts[0];
					toSelect[1] = 1;
					toSelect[2] = 1;
					toSelect[3] = 1;
					toSelect[4] = dieCounts[4];

					// Select die '6' if it exists. We're handing both small and full straight here
					//  so we may of already selected all of our dice (1-5)
					if (dieCounts[5] > 0)
					{
						toSelect[5] = 1;
					}


					if (dieCounts[0] > 0 && dieCounts[5] > 0)
					{
						OutputLine("Rule 2A - Straight");
						return Points.Straight;
					}
					else
					{
						OutputLine("Rule 2B - Small Straight");
						return Points.SmallStraight;
					}
				}
			}

			return 0;
		}


		/// <summary>
		/// Checks to see if the current hand (dieCounts) contains one or more runs of 3x dice
		/// of the same face value, or if the hand contains a full house. The required dice are
		/// added to the selection set (toSelect) and the worth of the selected dice are returned.
		/// </summary>
		/// <param name="dieCounts">Number of each die in hand.</param>
		/// <param name="toSelect">[Out] Selected dice.</param>
		/// <returns>Number of points the selection is worth</returns>
		private int CheckThreeOfAKindAndFullhouse(int[] dieCounts, ref int[] toSelect)
		{
			bool hasSinglePair = false;
			bool hasFourOfAKind = false;
			int indexOfSinglePair = 0;
			int pointsWorth = 0;

			for (int i = 0; i < 6; i++)
			{
				if (dieCounts[i] >= 3)
				{
					if (dieCounts[i] == 4)
					{
						hasFourOfAKind = true;
					}
					if (i != 0 && i != 4)
					{
						FreeDieValue = i + 1;
					}

					pointsWorth += Points.GetRunWorth(i + 1, dieCounts[i]);

					toSelect[i] = dieCounts[i];
				}
				else if (dieCounts[i] == 2)
				{
					indexOfSinglePair = i;
					hasSinglePair = true;
				}
			}

			if (hasFourOfAKind && hasSinglePair && IsFullHouseEnabled)
			{
				toSelect[indexOfSinglePair] = 2;

				OutputLine("Rule 3B - Full House");
				return Points.FullHouse;
			}

			if (pointsWorth != 0)
			{
				OutputLine("Rule 3A - One or two 3x of same value");
			}

			return pointsWorth;
		}

		/// <summary>
		/// Checks to see if the current hand (dieCounts) contains a special set. If it does then
		/// the required dice are added to the selection set (toSelect) and the worth of the
		/// selected dice are returned.
		/// </summary>
		/// <param name="dieCounts">Number of each die in hand.</param>
		/// <param name="toSelect">[Out] Selected dice.</param>
		/// <returns>Number of points the selection is worth</returns>
		private int CheckSpecialRuns(int[] dieCounts, ref int[] toSelect)
		{
			int scoreThisHand = 0;

			scoreThisHand = CheckThreePair(dieCounts, ref toSelect);
			if (scoreThisHand != 0)
			{
				return scoreThisHand;
			}

			scoreThisHand = CheckStraight(dieCounts, ref toSelect);
			if (scoreThisHand != 0)
			{
				return scoreThisHand;
			}

			return CheckThreeOfAKindAndFullhouse(dieCounts, ref toSelect);
		}

		/// <summary>
		/// Determines if the specified die value (1-6) can be scored (Selected).
		/// </summary>
		/// <param name="faceValue">Face value of the die (1-6).</param>
		/// <returns>True if the die can be scored (Selected).</returns>
		private bool IsScoringValue(int faceValue)
		{
			return Points.GetWorth(faceValue) != 0 || faceValue == FreeDieValue;
		}

		/// <summary>
		/// Chooses which dice to select from activeDice.
		/// </summary>
		/// <param name="diceQueue">[Out] Queue of dice that we want to select.</param>
		/// <param name="activeDice">Our current hand of dice.</param>
		/// <param name="score">[In,Out] Current turn score.</param>
		/// <param name="myGameScore">Current game score.</param>
		/// <param name="marksAgainstUs">Number of consecutive busts we've had.</param>
		/// <param name="isBust">[Out] Determines if we busted (TODO: REMOVE ME - DEBUG PURPOSES ONLY)</param>
		/// <returns>True if we should continue rolling. False if we should select all the dice from diceQueue and en our turn.</returns>
		public bool ChooseDiceToRoll(ref Queue<int> diceQueue, IEnumerable<Die> activeDice, ref int score, int myGameScore, int marksAgainstUs, ref bool isBust)
		{
			OutputLine("ChooseDiceToRoll(Score = " + score + ", myGameScore = " + myGameScore + ", marksAgainstUs =" + marksAgainstUs + ")");
			diceQueue.Clear();
			StringBuilder sb = new StringBuilder("Dice: ");

			int[] dieCounts = new int[6];
			int[] toSelect = new int[6];

			foreach (var die in activeDice)
			{
				dieCounts[die.Value - 1]++;
			}

			score += CheckSpecialRuns(dieCounts, ref toSelect);

			List<Die> unusedDice = new List<Die>();
			foreach (var die in activeDice)
			{
				if (toSelect[die.Value - 1] > 0)
				{
					diceQueue.Enqueue(die.Index);
					toSelect[die.Value - 1]--;
					dieCounts[die.Value - 1]--;
				}
				else
				{
					unusedDice.Add(die);
				}
			}
			if (unusedDice.Count == 0)
			{
				return true;
			}

			// See if we only have scorable dice left. If we do then we should probably re-roll.

			Die bestUnusedDie = null;
			int totalUnusedScoringDiePoints = 0;
			int scorableDiceRemaining = 0;

			foreach (var die in unusedDice)
			{
				if (IsScoringValue(die.Value))
				{
					int dieWorth = Points.GetWorth(die.Value);
					totalUnusedScoringDiePoints += dieWorth;

					if (bestUnusedDie == null || dieWorth > Points.GetWorth(bestUnusedDie.Value))
					{
						bestUnusedDie = die;
					}

					++scorableDiceRemaining;
				}
			}

			if (scorableDiceRemaining == unusedDice.Count)
			{
				OutputLine("Adding all remainign scoring dice and rolling again...");
				foreach (var unusedDie in unusedDice)
				{
					score += Points.GetWorth(unusedDie.Value);
					diceQueue.Enqueue(unusedDie.Index);
				}

				return true;
			}

			if (scorableDiceRemaining > 0)
			{
				bool selectAllUnusedDice = false;
				bool continueRolling = false;

				bool hasFreeDie = FreeDieValue != -1 && FreeDieValue != 1 && FreeDieValue != 5;
				int pointThreshold;

				if (!IsSmallStraightEnabled)
				{
					pointThreshold = 200;
				}
				else
				{
					pointThreshold = 250;
				}

				// we *have* to roll until we hit 1000...
				if (myGameScore == 0 && score < 1000)
				{
					selectAllUnusedDice = false;
					continueRolling = true;

					OutputLine("Rule: We must exceed 1000 points until our game score != 0");
				}
				else if (marksAgainstUs == 2)
				{
					selectAllUnusedDice = true;
					continueRolling = false;

					OutputLine("Rule: We have 2 marks against us, we must not take the risk!");
				}
				else if (score + totalUnusedScoringDiePoints >= pointThreshold)
				{
					// We have enough points
					selectAllUnusedDice = true;
					continueRolling = false;

					OutputLine("Rule: Scoring dice remain and " + (score + totalUnusedScoringDiePoints) + " >= " + pointThreshold);
				}
				else
				{
					// We need some risk, pick the best die and hope for the best
					selectAllUnusedDice = false;
					continueRolling = true;

					OutputLine("Rule: Scoring dice remain and point < pointThreshhold");
				}

				if (selectAllUnusedDice)
				{
					foreach (var die in unusedDice)
					{
						if (IsScoringValue(die.Value))
						{
							score += Points.GetWorth(die.Value);
							diceQueue.Enqueue(die.Index);
						}
					}
				}
				else
				{
					score += Points.GetWorth(bestUnusedDie.Value);
					diceQueue.Enqueue(bestUnusedDie.Index);
				}

				return continueRolling;
			}
			else
			{
				if (diceQueue.Count == 0)
				{
					isBust = true;
					return false;
				}

				// we *have* to roll until we hit 1000...
				if (myGameScore == 0 && score < 1000)
				{
					OutputLine("Rule: We must exceed 1000 points until our game score != 0");
					return true;
				}

				// Not going to risk it, if we bust we're screwed
				if (marksAgainstUs == 2)
				{
					OutputLine("Rule: We have 2 marks against us, we must not take the risk!");
					return false;
				}

				if (score >= 400)
				{
					OutputLine("Rule: No scorable dice left, but we do have >= 400 points");
					return false;
				}

				return unusedDice.Count >= 4;
			}
		}
	}
}