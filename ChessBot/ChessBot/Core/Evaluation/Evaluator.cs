﻿using System;
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

	private MaterialInfo GetMaterialInfo(int colourIndex) {
		int numPawns = _board.Pawns[colourIndex].Count;
		int numKnights = _board.Knights[colourIndex].Count;
		int numBishops = _board.Bishops[colourIndex].Count;
		int numRooks = _board.Rooks[colourIndex].Count;
		int numQueens = _board.Queens[colourIndex].Count;

		bool isWhite = colourIndex == Board.Board.WhiteIndex;
		ulong myPawns = _board.PieceBitboards[Piece.GetPieceValue(Piece.Pawn, isWhite)];
		ulong enemyPawns = _board.PieceBitboards[Piece.GetPieceValue(Piece.Pawn, !isWhite)];

		return new MaterialInfo(numPawns, numKnights, numBishops, numQueens, numRooks, myPawns, enemyPawns);
	}

	private static int EvaluatePieceSquareTable(int[] table, PieceList pieceList, bool isWhite)
	{
		int value = 0;
		for (int i = 0; i < pieceList.Count; i++)
		{
			value += PieceSquareTable.GetSquareValue(table, pieceList[i], isWhite);
		}
		return value;
	}

	private struct EvaluationData
	{
		

		public int Sum()
		{
			return 0;	
		}
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

        public readonly float EndgameTrans;

        public MaterialInfo (int numPawns, int numKnights, int numBishops, int numQueens, int numRooks, ulong myPawns, ulong enemyPawns) {
            NumPawns = numPawns;
			NumBishops = numBishops;
			NumQueens = numQueens;
			NumRooks = numRooks;
			Pawns = myPawns;
			EnemyPawns = enemyPawns;

			NumMajors = numRooks + numQueens;
			NumMinors = numBishops + numKnights;

			MaterialScore = 0;
			MaterialScore += numPawns * PawnValue;
			MaterialScore += numKnights * KnightValue;
			MaterialScore += numBishops * BishopValue;
			MaterialScore += numRooks * RookValue;
			MaterialScore += numQueens * QueenValue;

			// Endgame Transition (0->1)
			const int queenEndgameWeight = 45;
			const int rookEndgameWeight = 20;
			const int bishopEndgameWeight = 10;
			const int knightEndgameWeight = 10;

			const int endgameStartWeight = 2 * rookEndgameWeight + 2 * bishopEndgameWeight + 2 * knightEndgameWeight + queenEndgameWeight;
			int endgameWeightSum = numQueens * queenEndgameWeight + numRooks * rookEndgameWeight + numBishops * bishopEndgameWeight + numKnights * knightEndgameWeight;
			EndgameTrans = 1 - Math.Min(1, endgameWeightSum / (float)endgameStartWeight);
			// Endgame transition = 0 means it's the start of the game, 1 means it's the end of the game
			// Endgame transition is calculated based on the number of pieces on the board and their type
			// endgameStartWeight is the sum of the weights of all pieces at the start of the game
        }
    }
}