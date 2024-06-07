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

}