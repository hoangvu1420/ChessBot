using ChessBot.Core.Board;
using ChessBot.Core.MoveGeneration;

namespace ChessBot.Core.Utilities;

public static class MoveUtility
{
	public static Move GetMoveFromUciName(string moveName, Board.Board board)
	{
		int startSquare = BoardUtility.SquareIndexFromName(moveName.Substring(0, 2));
		int targetSquare = BoardUtility.SquareIndexFromName(moveName.Substring(2, 2));

		int movedPieceType = Piece.PieceType(board.Squares[startSquare]);
		Coord startCoord = new(startSquare);
		Coord targetCoord = new(targetSquare);

		// Figure out move flag
		int flag = Move.NoFlag;

		if (movedPieceType == Piece.Pawn)
		{
			// A promotion move is like: "e7e8q" which means pawn moves from e7 to e8 and promotes to queen
			// Promotion
			if (moveName.Length > 4)
			{
				flag = moveName[^1] switch
				{
					'q' => Move.PromoteToQueenFlag,
					'r' => Move.PromoteToRookFlag,
					'n' => Move.PromoteToKnightFlag,
					'b' => Move.PromoteToBishopFlag,
					_ => Move.NoFlag
				};
			}
			// Double pawn push
			else if (Math.Abs(targetCoord.rankIndex - startCoord.rankIndex) == 2)
			{
				flag = Move.PawnTwoUpFlag;
			}
			// En-passant
			else if (startCoord.fileIndex != targetCoord.fileIndex && board.Squares[targetSquare] == Piece.None)
			{
				flag = Move.EnPassantCaptureFlag;
			}
		}
		else if (movedPieceType == Piece.King)
		{
			// A castle move is like: "e1g1" which means king moves from e1 to g1 and rook moves from h1 to f1
			if (Math.Abs(startCoord.fileIndex - targetCoord.fileIndex) > 1)
			{
				flag = Move.CastleFlag;
			}
		}

		return new Move(startSquare, targetSquare, flag);
	}

	public static string GetMoveNameUci(Move move)
	{
		string startSquareName = BoardUtility.SquareNameFromIndex(move.StartSquare);
		string endSquareName = BoardUtility.SquareNameFromIndex(move.TargetSquare);
		string moveName = startSquareName + endSquareName;
		if (move.IsPromotion)
		{
			switch (move.MoveFlag)
			{
				case Move.PromoteToRookFlag:
					moveName += "r";
					break;
				case Move.PromoteToKnightFlag:
					moveName += "n";
					break;
				case Move.PromoteToBishopFlag:
					moveName += "b";
					break;
				case Move.PromoteToQueenFlag:
					moveName += "q";
					break;
			}
		}
		return moveName;
	}
}