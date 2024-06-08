using ChessBot.Core.Board;
using ChessBot.Core.Utilities;

namespace ChessBot.Core.MoveGeneration;

using static Math;

public static class PrecomputedMoveData
{
	public static readonly ulong[,] AlignMask;
	public static readonly ulong[,] DirRayMask;

	// First 4 are orthogonal, last 4 are diagonals (N, S, W, E, NW, SE, NE, SW)
	public static readonly int[] DirectionOffsets = [8, -8, -1, 1, 7, -7, 9, -9];

	private static readonly Coord[] DirOffsets2D =
	[
		new Coord(0, 1),
		new Coord(0, -1),
		new Coord(-1, 0),
		new Coord(1, 0),
		new Coord(-1, 1),
		new Coord(1, -1),
		new Coord(1, 1),
		new Coord(-1, -1)
	];


	// Stores number of moves available in each of the 8 directions for every square on the board
	// Order of directions is: N, S, W, E, NW, SE, NE, SW
	// So for example, if availableSquares[0][1] == 7...
	// that means that there are 7 squares to the north of b1 (the square with index 1 in board array)
	public static readonly int[][] NumSquaresToEdge;

	// Stores array of indices for each square a knight can land on from any square on the board
	// So for example, knightMoves[0] is equal to {10, 17}, meaning a knight on a1 can jump to c2 and b3
	public static readonly byte[][] KnightMoves;
	public static readonly byte[][] KingMoves;

	// Pawn attack directions for white and black (NW, NE; SW SE)
	public static readonly byte[][] PawnAttackDirections =
	[
		[4, 6],
		[7, 5]
	];

	public static readonly int[][] PawnAttacksWhite;
	public static readonly int[][] PawnAttacksBlack;
	public static readonly int[] DirectionLookup;

	public static readonly ulong[] KingAttackBitboards;
	public static readonly ulong[] KnightAttackBitboards;
	public static readonly ulong[][] PawnAttackBitboards;

	public static readonly ulong[] RookMoves;
	public static readonly ulong[] BishopMoves;
	public static readonly ulong[] QueenMoves;

	// Aka manhattan distance (answers how many moves for a rook to get from square a to square b)
	public static int[,] OrthogonalDistance;
	// Aka chebyshev distance (answers how many moves for a king to get from square a to square b)
	public static int[,] KingDistance;
	public static int[] CentreManhattanDistance;

	public static int NumRookMovesToReachSquare(int startSquare, int targetSquare)
	{
		return OrthogonalDistance[startSquare, targetSquare];
	}

	public static int NumKingMovesToReachSquare(int startSquare, int targetSquare)
	{
		return KingDistance[startSquare, targetSquare];
	}

	// Initialize lookup data
	static PrecomputedMoveData()
	{
		PawnAttacksWhite = new int[64][];
		PawnAttacksBlack = new int[64][];
		NumSquaresToEdge = new int[8][];
		KnightMoves = new byte[64][];
		KingMoves = new byte[64][];
		NumSquaresToEdge = new int[64][];

		RookMoves = new ulong[64];
		BishopMoves = new ulong[64];
		QueenMoves = new ulong[64];

		// Calculate knight jumps and available squares for each square on the board.
		// See comments by variable definitions for more info.
		int[] allKnightJumps = [15, 17, -17, -15, 10, -6, 6, -10];
		KnightAttackBitboards = new ulong[64];
		KingAttackBitboards = new ulong[64];
		PawnAttackBitboards = new ulong[64][];

		for (int squareIndex = 0; squareIndex < 64; squareIndex++)
		{

			int y = squareIndex / 8;
			int x = squareIndex - y * 8;

			int north = 7 - y;
			int south = y;
			int west = x;
			int east = 7 - x;
			
			NumSquaresToEdge[squareIndex] = new int[8];
			NumSquaresToEdge[squareIndex][0] = north;
			NumSquaresToEdge[squareIndex][1] = south;
			NumSquaresToEdge[squareIndex][2] = west;
			NumSquaresToEdge[squareIndex][3] = east;
			NumSquaresToEdge[squareIndex][4] = Min(north, west);
			NumSquaresToEdge[squareIndex][5] = Min(south, east);
			NumSquaresToEdge[squareIndex][6] = Min(north, east);
			NumSquaresToEdge[squareIndex][7] = Min(south, west);

			// Calculate all squares knight can jump to from current square
			var legalKnightJumps = new List<byte>();
			ulong knightBitboard = 0;
			foreach (int knightJumpDelta in allKnightJumps)
			{
				int knightJumpSquare = squareIndex + knightJumpDelta;
				if (knightJumpSquare is < 0 or >= 64) continue;
				
				int knightSquareY = knightJumpSquare / 8;
				int knightSquareX = knightJumpSquare - knightSquareY * 8;
					
				// Ensure knight has moved max of 2 squares on x/y axis (to reject indices that have wrapped around side of board)
				int maxCoordMoveDst = Max(Abs(x - knightSquareX), Abs(y - knightSquareY));
				if (maxCoordMoveDst != 2) continue;
				
				legalKnightJumps.Add((byte)knightJumpSquare);
				knightBitboard |= 1ul << knightJumpSquare;
			}
			KnightMoves[squareIndex] = legalKnightJumps.ToArray();
			KnightAttackBitboards[squareIndex] = knightBitboard;

			// Calculate all squares king can move to from current square (not including castling)
			var legalKingMoves = new List<byte>();
			foreach (int kingMoveDelta in DirectionOffsets)
			{
				int kingMoveSquare = squareIndex + kingMoveDelta;
				if (kingMoveSquare >= 0 && kingMoveSquare < 64)
				{
					int kingSquareY = kingMoveSquare / 8;
					int kingSquareX = kingMoveSquare - kingSquareY * 8;
					// Ensure king has moved max of 1 square on x/y axis (to reject indices that have wrapped around side of board)
					int maxCoordMoveDst = Max(Abs(x - kingSquareX), Abs(y - kingSquareY));
					if (maxCoordMoveDst == 1)
					{
						legalKingMoves.Add((byte)kingMoveSquare);
						KingAttackBitboards[squareIndex] |= 1ul << kingMoveSquare;
					}
				}
			}
			KingMoves[squareIndex] = legalKingMoves.ToArray();

			// Calculate legal pawn captures for white and black
			List<int> pawnCapturesWhite = [];
			List<int> pawnCapturesBlack = [];
			PawnAttackBitboards[squareIndex] = new ulong[2];
			if (x > 0)
			{
				// If not on the left edge, add the squares to the left of the current square
				if (y < 7)
				{
					pawnCapturesWhite.Add(squareIndex + 7);
					PawnAttackBitboards[squareIndex][Board.Board.WhiteIndex] |= 1ul << (squareIndex + 7);
				}
				if (y > 0)
				{
					pawnCapturesBlack.Add(squareIndex - 9);
					PawnAttackBitboards[squareIndex][Board.Board.BlackIndex] |= 1ul << (squareIndex - 9);
				}
			}
			if (x < 7)
			{
				// if not on the right edge, add the squares to the right of the current square
				if (y < 7)
				{
					pawnCapturesWhite.Add(squareIndex + 9);
					PawnAttackBitboards[squareIndex][Board.Board.WhiteIndex] |= 1ul << (squareIndex + 9);
				}
				if (y > 0)
				{
					pawnCapturesBlack.Add(squareIndex - 7);
					PawnAttackBitboards[squareIndex][Board.Board.BlackIndex] |= 1ul << (squareIndex - 7);
				}
			}
			PawnAttacksWhite[squareIndex] = pawnCapturesWhite.ToArray();
			PawnAttacksBlack[squareIndex] = pawnCapturesBlack.ToArray();

			// Rook moves
			for (int directionIndex = 0; directionIndex < 4; directionIndex++)
			{
				int currentDirOffset = DirectionOffsets[directionIndex];
				for (int n = 0; n < NumSquaresToEdge[squareIndex][directionIndex]; n++)
				{
					int targetSquare = squareIndex + currentDirOffset * (n + 1);
					RookMoves[squareIndex] |= 1ul << targetSquare;
				}
			}
			
			// Bishop moves
			for (int directionIndex = 4; directionIndex < 8; directionIndex++)
			{
				int currentDirOffset = DirectionOffsets[directionIndex];
				for (int n = 0; n < NumSquaresToEdge[squareIndex][directionIndex]; n++)
				{
					int targetSquare = squareIndex + currentDirOffset * (n + 1);
					BishopMoves[squareIndex] |= 1ul << targetSquare;
				}
			}
			
			// Queen moves
			QueenMoves[squareIndex] = RookMoves[squareIndex] | BishopMoves[squareIndex];
		}

		DirectionLookup = new int[127];
		for (int i = 0; i < 127; i++)
		{
			int offset = i - 63;
			int absOffset = Abs(offset);
			int absDir = 1;
			if (absOffset % 9 == 0)
			{
				absDir = 9;
			}
			else if (absOffset % 8 == 0)
			{
				absDir = 8;
			}
			else if (absOffset % 7 == 0)
			{
				absDir = 7;
			}

			DirectionLookup[i] = absDir * Sign(offset);
		}

		// Distance lookup
		OrthogonalDistance = new int[64, 64];
		KingDistance = new int[64, 64];
		CentreManhattanDistance = new int[64];
		for (int squareA = 0; squareA < 64; squareA++)
		{
			Coord coordA = BoardUtility.CoordFromIndex(squareA);
			int fileDstFromCentre = Max(3 - coordA.fileIndex, coordA.fileIndex - 4);
			int rankDstFromCentre = Max(3 - coordA.rankIndex, coordA.rankIndex - 4);
			CentreManhattanDistance[squareA] = fileDstFromCentre + rankDstFromCentre;

			for (int squareB = 0; squareB < 64; squareB++)
			{

				Coord coordB = BoardUtility.CoordFromIndex(squareB);
				int rankDistance = Abs(coordA.rankIndex - coordB.rankIndex);
				int fileDistance = Abs(coordA.fileIndex - coordB.fileIndex);
				OrthogonalDistance[squareA, squareB] = fileDistance + rankDistance;
				KingDistance[squareA, squareB] = Max(fileDistance, rankDistance);
			}
		}

		AlignMask = new ulong[64, 64];
		for (int squareA = 0; squareA < 64; squareA++)
		{
			for (int squareB = 0; squareB < 64; squareB++)
			{
				Coord cA = BoardUtility.CoordFromIndex(squareA);
				Coord cB = BoardUtility.CoordFromIndex(squareB);
				Coord delta = cB - cA;
				Coord dir = new Coord(Sign(delta.fileIndex), Sign(delta.rankIndex));
				//Coord dirOffset = dirOffsets2D[dirIndex];

				for (int i = -8; i < 8; i++)
				{
					// take i steps along the direction
					Coord coord = BoardUtility.CoordFromIndex(squareA) + dir * i;
					if (coord.IsValidSquare())
					{
						AlignMask[squareA, squareB] |= 1ul << BoardUtility.IndexFromCoord(coord);
					}
				}
			}
		}

		DirRayMask = new ulong[8, 64];
		for (int dirIndex = 0; dirIndex < DirOffsets2D.Length; dirIndex++)
		{
			// 8 directions: N, S, W, E, NW, SE, NE, SW
			for (int squareIndex = 0; squareIndex < 64; squareIndex++)
			{
				Coord square = BoardUtility.CoordFromIndex(squareIndex);

				for (int i = 0; i < 8; i++)
				{
					// take i steps along the direction
					Coord coord = square + DirOffsets2D[dirIndex] * i;
					if (coord.IsValidSquare())
					{
						DirRayMask[dirIndex, squareIndex] |= 1ul << BoardUtility.IndexFromCoord(coord);
					}
					else
					{
						break;
					}
				}
			}
		}
		/*
		 * Each element of dirRayMask is a bitboard that represents the squares that a piece can move to in a given direction.
		 * If a bit is set to 1, it means that a move to the corresponding square is possible in the current direction.
		 * For example, dirRayMask[0][1] would give you a bitmask of all the squares that can be reached from square 1
		 * (b1) by moving North.
		 */
	}
}