using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ragegast.Plugin.GreedyBot;

namespace LogicTester
{
	class Program
	{
		static void Main(string[] args)
		{
			new Program();
		}

		private Random rand = new Random();

		private List<Die> GetActiveDice(int dieCount)
		{
			//OutuputLine("");
			//OutuputLine("------- Roll --------");

			List<Die> activeDice = new List<Die>();

			//Console.Write("Dice: ");
			for (int i = 0; i < dieCount; i++)
			{
				int dieValue = 1 + (rand.Next()%6);

			//	Console.Write(dieValue);
				activeDice.Add(new Die(i, dieValue, Die.FaceStatus.New));
			}
			//OutuputLine("");
			//OutuputLine("---------------------");

			return activeDice;
		}

		public Program()
		{
			int totalTurnToWin = 0;
			int numRuns = 100000;

			for (int i = 0; i < numRuns; ++i)
			{
				//int turnsToWin = PlayGame();
				totalTurnToWin += PlayGame();
				//OutuputLine("Won in " + turnsToWin + " turns");
			}

			OutuputLine("Average turns to win: " + totalTurnToWin / (float)numRuns);
		}

		private int PlayGame()
		{
			GameLogic game = new GameLogic();
			GameLogic.IsFullHouseEnabled = true;
			GameLogic.IsSmallStraightEnabled = true;

			int myGameScore = 0;
			int marksAgainstUs = 0;
			int totalTurns = 0;

			while (myGameScore < 10000)
			{
				myGameScore = TakeTurn(game, myGameScore, ref marksAgainstUs);

				++totalTurns;
				//OutuputLine();
				//OutuputLine("Game score: " + myGameScore);
			}

			return totalTurns;
		}

		private void OutuputLine(string str)
		{
			Console.WriteLine(str);
		}

		private int TakeTurn(GameLogic game, int myGameScore, ref int marksAgainstUs)
		{
			game = new GameLogic();

			int score = 0;
			int diceToRoll = 6;
			List<Die> dice = GetActiveDice(diceToRoll);

			while (true)
			{
				Queue<int> diceQueue = new Queue<int>();
				bool isBust = false;

				int previousScore = score;
				bool rollAgain = game.ChooseDiceToRoll(ref diceQueue, dice, ref score, myGameScore, marksAgainstUs, ref isBust);

				if (isBust)
				{
					// We busted :x
					//OutuputLine("*** Bust!");

					if (marksAgainstUs == 2)
					{
						//OutuputLine("Busted when marksAgainstUs == 2 (wtf)");
						myGameScore = 0;
						marksAgainstUs = 0;
					}
					if (myGameScore != 0)
					{
						++marksAgainstUs;
					}

					break;
				}

				//OutuputLine("New score " + score + " (+" + (score - previousScore) + ")");

				//Console.Write("Pick: ");
				while (diceQueue.Count > 0)
				{
					int dieValue = dice[diceQueue.Dequeue()].Value;
				//		Console.Write(dieValue);
					--diceToRoll;
				}
				//OutuputLine("");

				if (!rollAgain)
				{
					myGameScore += score;
					score = 0;
					break;
				}

				if (diceToRoll == 0)
				{
					game = new GameLogic();
					diceToRoll = 6;
				}

				dice = GetActiveDice(diceToRoll);
			}
			return myGameScore;
		}
	}
}
