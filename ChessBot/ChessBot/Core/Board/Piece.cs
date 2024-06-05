namespace ChessBot.Core.Board;

// Contains definitions for each piece type (represented as integers),
// as well as various helper functions for dealing with pieces.
public static class Piece
{
	// Piece Types
	public const int None = 0;
	public const int Pawn = 1;
	public const int Knight = 2;
	public const int Bishop = 3;
	public const int Rook = 4;
	public const int Queen = 5;
	public const int King = 6;

	// Piece Colours
	public const int White = 0;
	public const int Black = 8;

	// Pieces
	public const int WhitePawn = Pawn | White; // 1
	public const int WhiteKnight = Knight | White; // 2
	public const int WhiteBishop = Bishop | White; // 3
	public const int WhiteRook = Rook | White; // 4
	public const int WhiteQueen = Queen | White; // 5
	public const int WhiteKing = King | White; // 6

	public const int BlackPawn = Pawn | Black; // 9
	public const int BlackKnight = Knight | Black; // 10
	public const int BlackBishop = Bishop | Black; // 11
	public const int BlackRook = Rook | Black; // 12
	public const int BlackQueen = Queen | Black; // 13
	public const int BlackKing = King | Black; // 14

	public const int MaxPieceIndex = BlackKing;

	public static readonly int[] PieceIndices =
	[
		WhitePawn, WhiteKnight, WhiteBishop, WhiteRook, WhiteQueen, WhiteKing,
		BlackPawn, BlackKnight, BlackBishop, BlackRook, BlackQueen, BlackKing
	];
	
		
	private static readonly Dictionary<int, string> PieceSymbols = new()
	{
		{None, "."},
		{WhitePawn, "♙"},
		{WhiteKnight, "♘"},
		{WhiteBishop, "♗"},
		{WhiteRook, "♖"},
		{WhiteQueen, "♕"},
		{WhiteKing, "♔"},
		{BlackPawn, "♙"},
		{BlackKnight, "♘"},
		{BlackBishop, "♗"},
		{BlackRook, "♖"},
		{BlackQueen, "♕"},
		{BlackKing, "♔"}
	};

	public static readonly Dictionary<int, string> PieceNames = new()
	{
		{WhitePawn, "WhitePawn"},
		{WhiteKnight, "WhiteKnight"},
		{WhiteBishop, "WhiteBishop"},
		{WhiteRook, "WhiteRook"},
		{WhiteQueen, "WhiteQueen"},
		{WhiteKing, "WhiteKing"},
		{BlackPawn, "BlackPawn"},
		{BlackKnight, "BlackKnight"},
		{BlackBishop, "BlackBishop"},
		{BlackRook, "BlackRook"},
		{BlackQueen, "BlackQueen"},
		{BlackKing, "BlackKing"}
	};

	// Bit Masks
	private const int TypeMask = 0b0111;
	private const int ColourMask = 0b1000;

	public static int GetPieceValue(int pieceType, int pieceColour) => pieceType | pieceColour;

	public static int GetPieceValue(int pieceType, bool pieceIsWhite) => GetPieceValue(pieceType, pieceIsWhite ? White : Black);

	// Returns true if given piece matches the given colour. If piece is of type 'none', result will always be false.
	public static bool IsColour(int piece, int colour) => (piece & ColourMask) == colour && piece != 0;

	public static bool IsWhite(int piece) => IsColour(piece, White);

	public static int PieceColour(int piece) => piece & ColourMask;

	public static int PieceType(int piece) => piece & TypeMask;

	// Rook or Queen
	public static bool IsOrthogonalSlider(int piece) => PieceType(piece) is Queen or Rook;

	// Bishop or Queen
	public static bool IsDiagonalSlider(int piece) => PieceType(piece) is Queen or Bishop;

	// Bishop, Rook, or Queen
	public static bool IsSlidingPiece(int piece) => PieceType(piece) is Queen or Bishop or Rook;

	public static char GetSymbol(int piece)
	{
		int pieceType = PieceType(piece);
		char symbol = pieceType switch
		{
			Rook => 'R',
			Knight => 'N',
			Bishop => 'B',
			Queen => 'Q',
			King => 'K',
			Pawn => 'P',
			_ => ' '
		};
		symbol = IsWhite(piece) ? symbol : char.ToLower(symbol);
		return symbol;
	}

	public static int GetPieceTypeFromSymbol(char symbol)
	{
		symbol = char.ToUpper(symbol);
		return symbol switch
		{
			'R' => Rook,
			'N' => Knight,
			'B' => Bishop,
			'Q' => Queen,
			'K' => King,
			'P' => Pawn,
			_ => None
		};
	}
		
	public static string GetUtf8Symbol(int piece) => PieceSymbols[piece];

}