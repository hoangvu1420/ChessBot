namespace ConsoleChess.Core.Board;

public readonly struct GameState(
	int capturedPieceType,
	int enPassantFile,
	int castlingRights,
	int fiftyMoveCounter,
	ulong zobristKey)
{
	public readonly int CapturedPieceType = capturedPieceType;
	public readonly int EnPassantFile = enPassantFile;
	public readonly int CastlingRights = castlingRights;
	public readonly int FiftyMoveCounter = fiftyMoveCounter;
	public readonly ulong ZobristKey = zobristKey;

	public const int ClearWhiteKingsideMask = 0b1110;
	public const int ClearWhiteQueensideMask = 0b1101;
	public const int ClearBlackKingsideMask = 0b1011;
	public const int ClearBlackQueensideMask = 0b0111;

	public bool HasKingsideCastleRight(bool white)
	{
		int mask = white ? 1 : 4;
		return (CastlingRights & mask) != 0;
	}

	public bool HasQueensideCastleRight(bool white)
	{
		int mask = white ? 2 : 8;
		return (CastlingRights & mask) != 0;
	}

}