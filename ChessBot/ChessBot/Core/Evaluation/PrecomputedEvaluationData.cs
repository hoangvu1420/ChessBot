using ChessBot.Core.Board;

namespace ChessBot.Core.Evaluation;

public static class PrecomputedEvaluationData
{
    public static readonly int[][] PawnShieldSquaresWhite;
	public static readonly int[][] PawnShieldSquaresBlack;

    static PrecomputedEvaluationData()
	{
		PawnShieldSquaresWhite = new int[64][];
		PawnShieldSquaresBlack = new int[64][];
		//
	}
}