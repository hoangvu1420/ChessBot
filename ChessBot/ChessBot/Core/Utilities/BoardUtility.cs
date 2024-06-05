using ChessBot.Core.Board;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console;

namespace ChessBot.Core.Utilities;

public static class BoardUtility
{
	public static readonly Coord[] RookDirections =
		[new Coord(-1, 0), new Coord(1, 0), new Coord(0, 1), new Coord(0, -1)];

	public static readonly Coord[] BishopDirections =
		[new Coord(-1, 1), new Coord(1, 1), new Coord(1, -1), new Coord(-1, -1)];

	public const string FileNames = "abcdefgh";
	public const string RankNames = "12345678";

	public const int A1 = 0;
	public const int B1 = 1;
	public const int C1 = 2;
	public const int D1 = 3;
	public const int E1 = 4;
	public const int F1 = 5;
	public const int G1 = 6;
	public const int H1 = 7;

	public const int A8 = 56;
	public const int B8 = 57;
	public const int C8 = 58;
	public const int D8 = 59;
	public const int E8 = 60;
	public const int F8 = 61;
	public const int G8 = 62;
	public const int H8 = 63;


	// Rank (0 to 7) of square 
	public static int RankIndex(int squareIndex)
	{
		return squareIndex >> 3;
	}

	// File (0 to 7) of square 
	public static int FileIndex(int squareIndex)
	{
		return squareIndex & 0b000111;
	}

	public static int IndexFromCoord(int fileIndex, int rankIndex)
	{
		return rankIndex * 8 + fileIndex;
	}

	public static int IndexFromCoord(Coord coord)
	{
		return IndexFromCoord(coord.fileIndex, coord.rankIndex);
	}

	public static Coord CoordFromIndex(int squareIndex)
	{
		return new Coord(FileIndex(squareIndex), RankIndex(squareIndex));
	}

	public static bool LightSquare(int fileIndex, int rankIndex)
	{
		return (fileIndex + rankIndex) % 2 != 0;
	}

	public static bool LightSquare(int squareIndex)
	{
		return LightSquare(FileIndex(squareIndex), RankIndex(squareIndex));
	}

	public static string SquareNameFromCoordinate(int fileIndex, int rankIndex)
	{
		return FileNames[fileIndex] + "" + (rankIndex + 1);
	}

	public static string SquareNameFromIndex(int squareIndex)
	{
		return SquareNameFromCoordinate(CoordFromIndex(squareIndex));
	}

	public static string SquareNameFromCoordinate(Coord coord)
	{
		return SquareNameFromCoordinate(coord.fileIndex, coord.rankIndex);
	}

	public static int SquareIndexFromName(string name)
	{
		char fileName = name[0];
		char rankName = name[1];
		int fileIndex = FileNames.IndexOf(fileName);
		int rankIndex = RankNames.IndexOf(rankName);
		return IndexFromCoord(fileIndex, rankIndex);
	}

	public static bool IsValidCoordinate(int x, int y) => x >= 0 && x < 8 && y >= 0 && y < 8;

	public static void PrintDiagram(Board.Board board, bool blackAtTop = true, bool includeFen = false,
		bool includeZobristKey = false)
	{
		AnsiConsole.WriteLine();

		int lastMoveSquare = board.AllGameMoves.Count > 0 ? board.AllGameMoves[^1].TargetSquare : -1;

		// Print the board with pieces
		for (int y = 0; y < 8; y++)
		{
			int rankIndex = blackAtTop ? 7 - y : y;

			var rank = new Table().Border(TableBorder.None).Collapse();

			for (int x = 0; x < 8; x++)
			{
				int fileIndex = blackAtTop ? x : 7 - x;
				int squareIndex = IndexFromCoord(fileIndex, rankIndex);
				bool highlight = squareIndex == lastMoveSquare;
				int piece = board.Squares[squareIndex];
				string pieceSymbol = Piece.GetUtf8Symbol(piece);
				string color = Piece.IsWhite(piece) ? "#F5E8C7" : "#03AED2";
				if (highlight)
				{
					rank.AddColumn(new TableColumn(new Markup($"[bold red]{pieceSymbol}[/]")));
				}
				else if (piece == Piece.None)
				{
					rank.AddColumn(new TableColumn(new Markup($"[grey]{pieceSymbol}[/]")));
				}
				else
				{
					rank.AddColumn(new TableColumn(new Markup($"[{color}]{pieceSymbol}[/]")));
				}
			}

			// Add rank index at the end of the row
			rank.AddColumn(new TableColumn(new Markup($"{rankIndex + 1}")).Centered());

			AnsiConsole.Write(rank);
		}

		// Show file names
		var fileNamesTable = new Table().Border(TableBorder.None).Collapse();
		string fileNames = blackAtTop ? "abcdefgh" : "hgfedcba";
		foreach (char fileName in fileNames)
		{
			fileNamesTable.AddColumn(new TableColumn(new Markup($"{fileName}")).Centered());
		}

		fileNamesTable.AddColumn(new TableColumn(new Markup(" "))); // Extra column for alignment
		AnsiConsole.Write(fileNamesTable);

		// Add an empty line
		AnsiConsole.WriteLine();
		// Include FEN and Zobrist Key if requested
		if (includeFen)
		{
			AnsiConsole.Markup($"[bold green]FEN: [/] {FenUtility.CurrentFen(board)}\n");
		}

		if (includeZobristKey)
		{
			AnsiConsole.Markup($"[bold green]Zobrist key: [/] {board.ZobristKey}\n");
		}
	}

}