using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ChessBot.Core.Board;

namespace ChessBot.Core.Evaluation;

public class Evaluator {
    public const int PawnValue = 100;
    public const int KnightValue = 300;
    public const int BishopValue = 320;
    public const int RookValue = 500;
    public const int QueenValue = 900;

    private Board.Board _board;
    public int Evaluate(Board.Board board) {
        _board = board;

        return 0;
    }

    public readonly struct MaterialInfo
	{
		public readonly int MaterialScore;
		public readonly int NumPawns;
		public readonly int NumMajors;
		public readonly int NumMinors;
		public readonly int NumBishops;
		public readonly int NumQueens;
		public readonly int NumRooks;

        public readonly ulong Pawns;
		public readonly ulong EnemyPawns;

        public MaterialInfo () {
            MaterialScore = 0;
        }
    }
}