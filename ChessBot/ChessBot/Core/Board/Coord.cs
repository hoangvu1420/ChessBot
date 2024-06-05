using ChessBot.Core.Utilities;

namespace ChessBot.Core.Board;

public readonly struct Coord : IComparable<Coord>
{
	public readonly int fileIndex;
	public readonly int rankIndex;

	public Coord(int fileIndex, int rankIndex)
	{
		this.fileIndex = fileIndex;
		this.rankIndex = rankIndex;
	}

	public Coord(int squareIndex)
	{
		fileIndex = BoardUtility.FileIndex(squareIndex);
		rankIndex = BoardUtility.RankIndex(squareIndex);
	}

	public bool IsLightSquare()
	{
		return (fileIndex + rankIndex) % 2 != 0;
	}

	public int CompareTo(Coord other)
	{
		return fileIndex == other.fileIndex && rankIndex == other.rankIndex ? 0 : 1;
	}

	public static Coord operator +(Coord a, Coord b) => new(a.fileIndex + b.fileIndex, a.rankIndex + b.rankIndex);
	public static Coord operator -(Coord a, Coord b) => new(a.fileIndex - b.fileIndex, a.rankIndex - b.rankIndex);
	public static Coord operator *(Coord a, int m) => new(a.fileIndex * m, a.rankIndex * m);
	public static Coord operator *(int m, Coord a) => a * m;

	public bool IsValidSquare() => fileIndex >= 0 && fileIndex < 8 && rankIndex >= 0 && rankIndex < 8;
	public int SquareIndex => BoardUtility.IndexFromCoord(this);
}