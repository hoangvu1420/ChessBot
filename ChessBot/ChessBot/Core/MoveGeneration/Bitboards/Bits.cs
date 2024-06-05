using ConsoleChess.Core.Helpers;
using static System.Math;

namespace ConsoleChess.Core.Move_Generation.Bitboards;
//Tập hợp các bitboard được tính toán trước
public static class Bits
{
	public const ulong FileA = 0x101010101010101;

	public const ulong WhiteKingSideMask = 1ul << BoardUtility.F1 | 1ul << BoardUtility.G1;
	public const ulong BlackKingSideMask = 1ul << BoardUtility.F8 | 1ul << BoardUtility.G8;

	public const ulong WhiteQueenSideMask2 = 1ul << BoardUtility.D1 | 1ul << BoardUtility.C1;
	public const ulong BlackQueenSideMask2 = 1ul << BoardUtility.D8 | 1ul << BoardUtility.C8;

	public const ulong WhiteQueenSideMask = WhiteQueenSideMask2 | 1ul << BoardUtility.B1;
	public const ulong BlackQueenSideMask = BlackQueenSideMask2 | 1ul << BoardUtility.B8;

	public static readonly ulong[] WhitePassedPawnMask;
	public static readonly ulong[] BlackPassedPawnMask;

	public static readonly ulong[] WhitePawnSupportMask;
	public static readonly ulong[] BlackPawnSupportMask;

	public static readonly ulong[] FileMask;
	public static readonly ulong[] AdjacentFileMasks;

	public static readonly ulong[] KingSafetyMask;

	public static readonly ulong[] WhiteForwardFileMask;
	public static readonly ulong[] BlackForwardFileMask;


	public static readonly ulong[] TripleFileMask;

	static Bits()
	{
		FileMask = new ulong[8];
		AdjacentFileMasks = new ulong[8];

		for (int i = 0; i < 8; i++)
		{
			FileMask[i] = FileA << i;
			ulong left = i > 0 ? FileA << (i - 1) : 0;
			ulong right = i < 7 ? FileA << (i + 1) : 0;
			AdjacentFileMasks[i] = left | right;
		}

		TripleFileMask = new ulong[8];
		for (int i = 0; i < 8; i++)
		{
			int clampedFile = Clamp(i, 1, 6);
			TripleFileMask[i] = FileMask[clampedFile] | AdjacentFileMasks[clampedFile];
		}

		WhitePassedPawnMask = new ulong[64];
		BlackPassedPawnMask = new ulong[64];
		WhitePawnSupportMask = new ulong[64];
		BlackPawnSupportMask = new ulong[64];
		WhiteForwardFileMask = new ulong[64];
		BlackForwardFileMask = new ulong[64];

		for (int square = 0; square < 64; square++)
		{
			int file = BoardUtility.FileIndex(square);
			int rank = BoardUtility.RankIndex(square);
			ulong adjacentFiles = FileA << Max(0, file - 1) | FileA << Min(7, file + 1);
			// Passed pawn mask
			ulong whiteForwardMask = ~(ulong.MaxValue >> (64 - 8 * (rank + 1)));
			ulong blackForwardMask = (1ul << 8 * rank) - 1;

			WhitePassedPawnMask[square] = (FileA << file | adjacentFiles) & whiteForwardMask;
			BlackPassedPawnMask[square] = (FileA << file | adjacentFiles) & blackForwardMask;
			// Pawn support mask
			ulong adjacent = (1ul << (square - 1) | 1ul << (square + 1)) & adjacentFiles;
			WhitePawnSupportMask[square] = adjacent | BitBoardUtility.Shift(adjacent, -8);
			BlackPawnSupportMask[square] = adjacent | BitBoardUtility.Shift(adjacent, +8);

			WhiteForwardFileMask[square] = whiteForwardMask & FileMask[file];
			BlackForwardFileMask[square] = blackForwardMask & FileMask[file];
		}

		KingSafetyMask = new ulong[64];
		for (int i = 0; i < 64; i++)
		{
			KingSafetyMask[i] = BitBoardUtility.KingMoves[i] | (1ul << i);
		}
	}

}