using ChessBot.Core.Board;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessBot.Core.Utilities;

public static class FenUtility
{
	public const string StartPositionFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

	public static PositionInfo PositionFromFen(string fen)
	{
		PositionInfo loadedPositionInfo = new(fen);
		return loadedPositionInfo;
	}

	public static string CurrentFen(Board.Board board, bool alwaysIncludeEpSquare = true)
	{
		string fen = "";
		for (int rank = 7; rank >= 0; rank--)
		{
			int numEmptyFiles = 0;
			for (int file = 0; file < 8; file++)
			{
				int i = rank * 8 + file;
				int piece = board.Squares[i];
				if (piece != 0)
				{
					if (numEmptyFiles != 0)
					{
						fen += numEmptyFiles;
						numEmptyFiles = 0;
					}

					bool isBlack = Piece.IsColour(piece, Piece.Black);
					int pieceType = Piece.PieceType(piece);
					char pieceChar = ' ';
					switch (pieceType)
					{
						case Piece.Rook:
							pieceChar = 'R';
							break;
						case Piece.Knight:
							pieceChar = 'N';
							break;
						case Piece.Bishop:
							pieceChar = 'B';
							break;
						case Piece.Queen:
							pieceChar = 'Q';
							break;
						case Piece.King:
							pieceChar = 'K';
							break;
						case Piece.Pawn:
							pieceChar = 'P';
							break;
					}

					fen += isBlack ? pieceChar.ToString().ToLower() : pieceChar.ToString();
				}
				else
				{
					numEmptyFiles++;
				}

			}

			if (numEmptyFiles != 0)
			{
				fen += numEmptyFiles;
			}

			if (rank != 0)
			{
				fen += '/';
			}
		}

		// Side to move
		fen += ' ';
		fen += board.IsWhiteToMove ? 'w' : 'b';

		// Castling
		bool whiteKingSide = (board.CurrentGameState.CastlingRights & 1) == 1;
		bool whiteQueenSide = (board.CurrentGameState.CastlingRights >> 1 & 1) == 1;
		bool blackKingSide = (board.CurrentGameState.CastlingRights >> 2 & 1) == 1;
		bool blackQueenSide = (board.CurrentGameState.CastlingRights >> 3 & 1) == 1;
		fen += ' ';
		fen += whiteKingSide ? "K" : string.Empty;
		fen += whiteQueenSide ? "Q" : string.Empty;
		fen += blackKingSide ? "k" : string.Empty;
		fen += blackQueenSide ? "q" : string.Empty;
		fen += board.CurrentGameState.CastlingRights == 0 ? "-" : string.Empty;

		// En-passant
		fen += ' ';
		int epFileIndex = board.CurrentGameState.EnPassantFile - 1;
		int epRankIndex = board.IsWhiteToMove ? 5 : 2;

		bool isEnPassant = epFileIndex != -1;
		bool includeEp = alwaysIncludeEpSquare || EnPassantCanBeCaptured(epFileIndex, epRankIndex, board);
		if (isEnPassant && includeEp)
		{
			fen += BoardUtility.SquareNameFromCoordinate(epFileIndex, epRankIndex);
		}
		else
		{
			fen += '-';
		}

		// 50 move counter
		fen += ' ';
		fen += board.CurrentGameState.FiftyMoveCounter;

		// Full-move count (should be one at start, and increase after each move by black)
		fen += ' ';
		fen += board.PlyCount / 2 + 1;

		return fen;
	}

	private static bool EnPassantCanBeCaptured(int epFileIndex, int epRankIndex, Board.Board board)
	{
		Coord captureFromA = new Coord(epFileIndex - 1, epRankIndex + (board.IsWhiteToMove ? -1 : 1));
		Coord captureFromB = new Coord(epFileIndex + 1, epRankIndex + (board.IsWhiteToMove ? -1 : 1));
		int epCaptureSquare = new Coord(epFileIndex, epRankIndex).SquareIndex;
		int friendlyPawn = Piece.GetPieceValue(Piece.Pawn, board.MoveColour);

		return CanCapture(captureFromA) || CanCapture(captureFromB);

		bool CanCapture(Coord source)
		{
			bool isPawnOnSquare = board.Squares[source.SquareIndex] == friendlyPawn;
			if (source.IsValidSquare() && isPawnOnSquare)
			{
				Move move = new Move(source.SquareIndex, epCaptureSquare, Move.EnPassantCaptureFlag);
				board.MakeMove(move);
				board.MakeNullMove();
				bool wasLegalMove = !board.CalculateInCheckState();

				board.UnmakeNullMove();
				board.UnmakeMove(move);
				return wasLegalMove;
			}

			return false;
		}
	}

	public static string FlipFen(string fen)
	{
		string flippedFen = "";
		string[] sections = fen.Split(' ');

		List<char> invertedFenChars = [];
		string[] fenRanks = sections[0].Split('/');

		for (int i = fenRanks.Length - 1; i >= 0; i--)
		{
			string rank = fenRanks[i];
			foreach (char c in rank)
			{
				flippedFen += InvertCase(c);
			}

			if (i != 0)
			{
				flippedFen += '/';
			}
		}

		flippedFen += " " + (sections[1][0] == 'w' ? 'b' : 'w');
		string castlingRights = sections[2];
		string flippedRights = "";
		foreach (char c in "kqKQ")
		{
			if (castlingRights.Contains(c))
			{
				flippedRights += InvertCase(c);
			}
		}

		flippedFen += " " + (flippedRights.Length == 0 ? "-" : flippedRights);

		string ep = sections[3];
		string flippedEp = ep[0] + "";
		if (ep.Length > 1)
		{
			flippedEp += ep[1] == '6' ? '3' : '6';
		}

		flippedFen += " " + flippedEp;
		flippedFen += " " + sections[4] + " " + sections[5];


		return flippedFen;

		char InvertCase(char c)
		{
			if (char.IsLower(c))
			{
				return char.ToUpper(c);
			}

			return char.ToLower(c);
		}
	}

	public readonly struct PositionInfo
	{
		public readonly string Fen;
		public readonly ReadOnlyCollection<int> Squares;

		// Castling rights
		public readonly bool WhiteCastleKingSide;
		public readonly bool WhiteCastleQueenSide;
		public readonly bool BlackCastleKingSide;
		public readonly bool BlackCastleQueenSide;

		public readonly int EpFile;
		public readonly bool WhiteToMove;

		public readonly int FiftyMovePlyCount;

		public readonly int MoveCount;

		public PositionInfo(string fen)
		{
			this.Fen = fen;
			int[] squarePieces = new int[64];

			string[] sections = fen.Split(' ');

			int file = 0;
			int rank = 7;

			foreach (char symbol in sections[0])
			{
				if (symbol == '/')
				{
					file = 0;
					rank--;
				}
				else
				{
					if (char.IsDigit(symbol))
					{
						file += (int)char.GetNumericValue(symbol);
					}
					else
					{
						int pieceColour = char.IsUpper(symbol) ? Piece.White : Piece.Black;
						int pieceType = char.ToLower(symbol) switch
						{
							'k' => Piece.King,
							'p' => Piece.Pawn,
							'n' => Piece.Knight,
							'b' => Piece.Bishop,
							'r' => Piece.Rook,
							'q' => Piece.Queen,
							_ => Piece.None
						};

						squarePieces[rank * 8 + file] = pieceType | pieceColour;
						file++;
					}
				}
			}

			Squares = new(squarePieces);

			WhiteToMove = sections[1] == "w";

			string castlingRights = sections[2];
			WhiteCastleKingSide = castlingRights.Contains('K');
			WhiteCastleQueenSide = castlingRights.Contains('Q');
			BlackCastleKingSide = castlingRights.Contains('k');
			BlackCastleQueenSide = castlingRights.Contains('q');

			// Default values
			EpFile = 0;
			FiftyMovePlyCount = 0;
			MoveCount = 0;

			if (sections.Length > 3)
			{
				string enPassantFileName = sections[3][0].ToString();
				if (BoardUtility.FileNames.Contains(enPassantFileName))
				{
					EpFile = BoardUtility.FileNames.IndexOf(enPassantFileName, StringComparison.Ordinal) + 1;
				}
			}

			// Half-move clock
			if (sections.Length > 4)
			{
				int.TryParse(sections[4], out FiftyMovePlyCount);
			}

			// Full move number
			if (sections.Length > 5)
			{
				int.TryParse(sections[5], out MoveCount);
			}
		}
	}
}