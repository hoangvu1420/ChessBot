using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessBot.Core.Board;

public static class Zobrist
{
	public static readonly ulong[,] piecesMatrix = new ulong[Piece.MaxPieceIndex + 1, 64];

	public static readonly ulong[] castlingRights = new ulong[16];

	public static readonly ulong[] enPassantFile = new ulong[9];
	public static readonly ulong sideToMove;


	static Zobrist()
	{

		const int seed = 29426028;  
		var rand = new Random(seed);

		for (int squareIndex = 0; squareIndex < 64; squareIndex++)
		{
			foreach (int piece in Piece.PieceIndices)
			{
				piecesMatrix[piece, squareIndex] = RandomUnsigned64BitNumber(rand);
			}
		}

		for (int i = 0; i < castlingRights.Length; i++)
		{
			castlingRights[i] = RandomUnsigned64BitNumber(rand);
		}

		for (int i = 0; i < enPassantFile.Length; i++)
		{
			enPassantFile[i] = (i == 0 ? 0 : RandomUnsigned64BitNumber(rand));
		}

		sideToMove = RandomUnsigned64BitNumber(rand);
	}

	public static ulong CalculateZobristKey(Board board)
	{
		ulong zobristKey = 0;

		for (int squareIndex = 0; squareIndex < 64; squareIndex++)
		{
			int piece = board.Squares[squareIndex];

			if (Piece.PieceType(piece) != Piece.None)
			{
				zobristKey ^= piecesMatrix[piece, squareIndex];
			}
		}

		zobristKey ^= enPassantFile[board.CurrentGameState.EnPassantFile];

		if (board.MoveColour == Piece.Black)
		{
			zobristKey ^= sideToMove;
		}

		zobristKey ^= castlingRights[board.CurrentGameState.CastlingRights];

		return zobristKey;
	}

	private static ulong RandomUnsigned64BitNumber(Random rand)
	{
		byte[] buffer = new byte[8];
		rand.NextBytes(buffer);
		return BitConverter.ToUInt64(buffer, 0);
	}
}