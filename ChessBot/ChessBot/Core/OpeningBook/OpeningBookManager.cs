using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ChessBot.Core.Utilities;

namespace ChessBot.Core.OpeningBook;

public class OpeningBookManager
{
	private readonly Dictionary<string, BookMove[]> _movesByPosition;
	private readonly Random _rng;

	public OpeningBookManager(string file)
	{
		_rng = new Random();
		Span<string> entries = file.Trim(' ', '\n').Split("pos").AsSpan(1);
		_movesByPosition = new Dictionary<string, BookMove[]>(entries.Length);

		foreach (var s in entries)
		{
			string[] entryData = s.Trim('\n').Split('\n');
			string positionFen = entryData[0].Trim();
			Span<string> allMoveData = entryData.AsSpan(1);

			BookMove[] bookMoves = new BookMove[allMoveData.Length];

			for (int moveIndex = 0; moveIndex < bookMoves.Length; moveIndex++)
			{
				string[] moveData = allMoveData[moveIndex].Split(' ');
				bookMoves[moveIndex] = new BookMove(moveData[0], int.Parse(moveData[1]));
			}

			_movesByPosition.Add(positionFen, bookMoves);
		}
		Console.WriteLine($@"Total book positions: {_movesByPosition.Count}");
	}

	// WeightPow is a value between 0 and 1.
	// 0 means all moves are picked with equal probability, 1 means moves are weighted by num times played.
	public bool TryGetBookMove(Board.Board board, out string moveString, double weightPow = 0.5)
	{
		string positionFen = FenUtility.CurrentFen(board, alwaysIncludeEpSquare: false);
		weightPow = Math.Clamp(weightPow, 0, 1); // this is to ensure that weightPow is between 0 and 1
		if (_movesByPosition.TryGetValue(RemoveMoveCountersFromFen(positionFen), out var moves))
		{
			int totalPlayCount = 0;
			foreach (BookMove move in moves)
			{
				totalPlayCount += WeightedPlayCount(move.NumTimesPlayed);
			}

			double[] weights = new double[moves.Length];
			double weightSum = 0;
			for (int i = 0; i < moves.Length; i++)
			{
				double weight = WeightedPlayCount(moves[i].NumTimesPlayed) / (double)totalPlayCount;
				weightSum += weight;
				weights[i] = weight;
			}

			double[] probCumul = new double[moves.Length];
			for (int i = 0; i < weights.Length; i++)
			{
				double prob = weights[i] / weightSum;
				probCumul[i] = probCumul[Math.Max(0, i - 1)] + prob;
			}

			double random = _rng.NextDouble();
			for (int i = 0; i < moves.Length; i++)
			{
				if (random <= probCumul[i])
				{
					moveString = moves[i].MoveString;
					Console.WriteLine($@"Found book move index: {i}");
					return true;
				}
			}
		}

		moveString = "Null";
		return false;

		int WeightedPlayCount(int playCount) => (int)Math.Ceiling(Math.Pow(playCount, weightPow));
	}

	private string RemoveMoveCountersFromFen(string fen)
	{
		string fenA = fen.Substring(0, fen.LastIndexOf(' '));
		return fenA.Substring(0, fenA.LastIndexOf(' '));
	}


	private readonly struct BookMove(string moveString, int numTimesPlayed)
	{
		public readonly string MoveString = moveString;
		public readonly int NumTimesPlayed = numTimesPlayed;
	}
}