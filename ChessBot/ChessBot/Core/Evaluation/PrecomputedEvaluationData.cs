using ChessBot.Core.Board;

namespace ChessBot.Core.Evaluation;

public static class PrecomputedEvaluationData {
    public static readonly int[][] PawnShieldSquaresWhite;
	public static readonly int[][] PawnShieldSquaresBlack;

    static PrecomputedEvaluationData() {
		PawnShieldSquaresWhite = new int[64][];
		PawnShieldSquaresBlack = new int[64][];
		
        for (int squareIndex = 0; squareIndex < 64; squareIndex++)
		{
			CreatePawnShieldSquare(squareIndex);
		}
	}

    private static void CreatePawnShieldSquare(int squareIndex) {
		List<int> shieldIndicesWhite = [];
		List<int> shieldIndicesBlack = [];
		Coord coord = new Coord(squareIndex);
		int rank = coord.rankIndex;
		int file = Math.Clamp(coord.fileIndex, 1, 6);

		for (int fileOffset = -1; fileOffset <= 1; fileOffset++)
		{
			AddIfValid(new Coord(file + fileOffset, rank + 1), shieldIndicesWhite);
			AddIfValid(new Coord(file + fileOffset, rank - 1), shieldIndicesBlack);
		}

		for (int fileOffset = -1; fileOffset <= 1; fileOffset++)
		{
			AddIfValid(new Coord(file + fileOffset, rank + 2), shieldIndicesWhite);
			AddIfValid(new Coord(file + fileOffset, rank - 2), shieldIndicesBlack);
		}

		PawnShieldSquaresWhite[squareIndex] = shieldIndicesWhite.ToArray();
		PawnShieldSquaresBlack[squareIndex] = shieldIndicesBlack.ToArray();

		void AddIfValid(Coord coord, List<int> list)
		{
			if (coord.IsValidSquare())
			{
				list.Add(coord.SquareIndex);
			}
		}
	}
}