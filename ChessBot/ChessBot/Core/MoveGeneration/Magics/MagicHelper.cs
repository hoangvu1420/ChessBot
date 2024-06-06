using ConsoleChess.Core.Board;
using ConsoleChess.Core.Helpers;
using ConsoleChess.Core.Move_Generation.Bitboards;

namespace ConsoleChess.Core.Move_Generation.Magics;

public static class MagicHelper
{
	public static ulong[] CreateAllBlockerBitboards(ulong movementMask)
	{
		List<int> moveSquareIndices = [];
		for (int i = 0; i < 64; i++)
		{
			if (((movementMask >> i) & 1) == 1)
			{
				moveSquareIndices.Add(i);
			}
		}

		int numPatterns = 1 << moveSquareIndices.Count; 
		ulong[] blockerBitboards = new ulong[numPatterns];

		for (int patternIndex = 0; patternIndex < numPatterns; patternIndex++)
		{
			for (int bitIndex = 0; bitIndex < moveSquareIndices.Count; bitIndex++)
			{
				int bit = (patternIndex >> bitIndex) & 1;
				blockerBitboards[patternIndex] |= (ulong)bit << moveSquareIndices[bitIndex];
			}
		}

		return blockerBitboards;
	}


	public static ulong CreateMovementMask(int squareIndex, bool ortho)
	{
		ulong mask = 0;
		Coord[] directions = ortho ? BoardUtility.RookDirections : BoardUtility.BishopDirections;
		Coord startCoord = new Coord(squareIndex);

		foreach (Coord dir in directions)
		{
			for (int dst = 1; dst < 8; dst++)
			{
				Coord coord = startCoord + dir * dst;
				Coord nextCoord = startCoord + dir * (dst + 1);

				if (nextCoord.IsValidSquare())
				{
					BitBoardUtility.SetSquare(ref mask, coord.SquareIndex);
				}
				else { break; }
			}
		}
		return mask;
	}

	public static ulong LegalMoveBitboardFromBlockers(int startSquare, ulong blockerBitboard, bool ortho)
	{
		ulong bitboard = 0;

		Coord[] directions = ortho ? BoardUtility.RookDirections : BoardUtility.BishopDirections;
		Coord startCoord = new Coord(startSquare);

		foreach (Coord dir in directions)
		{
			for (int dst = 1; dst < 8; dst++)
			{
				Coord coord = startCoord + dir * dst;

				if (coord.IsValidSquare())
				{
					BitBoardUtility.SetSquare(ref bitboard, coord.SquareIndex);
					if (BitBoardUtility.ContainsSquare(blockerBitboard, coord.SquareIndex))
					{
						break;
					}
				}
				else { break; }
			}
		}

		return bitboard;
	}
}