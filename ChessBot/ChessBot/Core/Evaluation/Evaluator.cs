using ChessBot.Core.Board;
using ChessBot.Core.Utilities;
using ChessBot.Core.MoveGeneration;
using ChessBot.Core.MoveGeneration.Bitboards;

namespace ChessBot.Core.Evaluation;

public class Evaluator
{

	public const int PawnValue = 100;
	public const int KnightValue = 300;
	public const int BishopValue = 320;
	public const int RookValue = 500;
	public const int QueenValue = 900;

	private static readonly int[] PassedPawnBonuses = [0, 120, 80, 50, 30, 15, 15]; // 0-6 squares from promotion
	private static readonly int[] IsolatedPawnPenaltyByCount = [0, -10, -25, -50, -75, -75, -75, -75, -75];
	private static readonly int[] KingPawnShieldScores = [4, 7, 4, 3, 6, 3];

	private const float EndgameMaterialStart = RookValue * 2 + BishopValue + KnightValue;
	private Board.Board _board;

	private EvaluationData _whiteEval;
	private EvaluationData _blackEval;

	// Performs static evaluation of the current position.
	// The position is assumed to be 'quiet', i.e no captures are available that could drastically affect the evaluation.
	// The score that's returned is given from the perspective of whoever's turn it is to move.
	// So a positive score means the player who's turn it is to move has an advantage, while a negative score indicates a disadvantage.
	public int Evaluate(Board.Board board)
	{
		_board = board;
		_whiteEval = new EvaluationData();
		_blackEval = new EvaluationData();

		MaterialInfo whiteMaterial = GetMaterialInfo(Board.Board.WhiteIndex);
		MaterialInfo blackMaterial = GetMaterialInfo(Board.Board.BlackIndex);

		// Score based on number (and type) of pieces on board
		_whiteEval.MaterialScore = whiteMaterial.MaterialScore;
		_blackEval.MaterialScore = blackMaterial.MaterialScore;

		// Score based on positions of pieces
		_whiteEval.PieceSquareScore = EvaluatePieceSquareTables(true, blackMaterial.EndgameTrans);
		_blackEval.PieceSquareScore = EvaluatePieceSquareTables(false, whiteMaterial.EndgameTrans);
		// Encourage using own king to push enemy king to edge of board in winning endgame
		_whiteEval.MopUpScore = MopUpEval(true, whiteMaterial, blackMaterial);
		_blackEval.MopUpScore = MopUpEval(false, blackMaterial, whiteMaterial);

		_whiteEval.PawnScore = EvaluatePawns(Board.Board.WhiteIndex);
		_blackEval.PawnScore = EvaluatePawns(Board.Board.BlackIndex);

		_whiteEval.PawnShieldScore = KingPawnShield(Board.Board.WhiteIndex, blackMaterial, _blackEval.PieceSquareScore);
		_blackEval.PawnShieldScore = KingPawnShield(Board.Board.BlackIndex, whiteMaterial, _whiteEval.PieceSquareScore);

		int perspective = board.IsWhiteToMove ? 1 : -1;
		int eval = _whiteEval.Sum() - _blackEval.Sum();
		return eval * perspective;
	}

	public int KingPawnShield(int colourIndex, MaterialInfo enemyMaterial, float enemyPieceSquareScore)
	{
		/*
		 * This method calculates a penalty based on the state of the pawn shield in front of the king. The pawn shield
		 * is important for the king's safety, especially in the opening and middle game. The method checks the squares
		 * in front of the king and adds a penalty for each square that is not occupied by a friendly pawn. The penalty
		 * is higher if the enemy has developed their pieces and can potentially attack the king.
		 */
		if (enemyMaterial.EndgameTrans >= 1)
		{
			// If it's the endgame, the pawn shield is no longer as important
			return 0;
		}

		int penalty = 0;

		bool isWhite = colourIndex == Board.Board.WhiteIndex;
		int friendlyPawn = Piece.GetPieceValue(Piece.Pawn, isWhite);
		int kingSquare = _board.KingSquare[colourIndex];
		int kingFile = BoardUtility.FileIndex(kingSquare);

		//int filePenalty = kingOpeningFilePenalty[kingFile];
		int unCastledKingPenalty = 0;

		if (kingFile <= 2 || kingFile >= 5)
		{
			int[] squares = isWhite ? PrecomputedEvaluationData.PawnShieldSquaresWhite[kingSquare] : PrecomputedEvaluationData.PawnShieldSquaresBlack[kingSquare];

			for (int i = 0; i < squares.Length / 2; i++)
			{
				int shieldSquareIndex = squares[i];
				if (_board.Squares[shieldSquareIndex] != friendlyPawn)
				{
					if (squares.Length > 3 && _board.Squares[squares[i + 3]] == friendlyPawn)
					{
						penalty += KingPawnShieldScores[i + 3];
					}
					else
					{
						penalty += KingPawnShieldScores[i];
					}
				}
			}
			penalty *= penalty;
		}
		else
		{
			float enemyDevelopmentScore = Math.Clamp((enemyPieceSquareScore + 10) / 130f, 0, 1);
			unCastledKingPenalty = (int)(50 * enemyDevelopmentScore);
		}

		int openFileAgainstKingPenalty = 0;

		if (enemyMaterial.NumRooks > 1 || (enemyMaterial.NumRooks > 0 && enemyMaterial.NumQueens > 0))
		{

			int clampedKingFile = Math.Clamp(kingFile, 1, 6);
			ulong myPawns = enemyMaterial.EnemyPawns;
			for (int attackFile = clampedKingFile; attackFile <= clampedKingFile + 1; attackFile++)
			{
				ulong fileMask = Bits.FileMask[attackFile];
				bool isKingFile = attackFile == kingFile;
				if ((enemyMaterial.Pawns & fileMask) == 0)
				{
					openFileAgainstKingPenalty += isKingFile ? 25 : 15;
					if ((myPawns & fileMask) == 0)
					{
						openFileAgainstKingPenalty += isKingFile ? 15 : 10;
					}
				}

			}
		}

		float pawnShieldWeight = 1 - enemyMaterial.EndgameTrans;
		if (_board.Queens[1 - colourIndex].Count == 0)
		{
			pawnShieldWeight *= 0.6f;
		}

		return (int)((-penalty - unCastledKingPenalty - openFileAgainstKingPenalty) * pawnShieldWeight);
	}

	public int EvaluatePawns(int colourIndex)
	{
		/*
		 * This method evaluates the pawns of the specified color. It gives a bonus for passed pawns (pawns that have no
		 * enemy pawns in front of them on the same file or adjacent files) and a penalty for isolated pawns (pawns that
		 * have no friendly pawns on adjacent files).
		 */
		PieceList pawns = _board.Pawns[colourIndex];
		bool isWhite = colourIndex == Board.Board.WhiteIndex;
		ulong opponentPawns = _board.PieceBitboards[Piece.GetPieceValue(Piece.Pawn, isWhite ? Piece.Black : Piece.White)];
		ulong friendlyPawns = _board.PieceBitboards[Piece.GetPieceValue(Piece.Pawn, isWhite ? Piece.White : Piece.Black)];
		ulong[] masks = isWhite ? Bits.WhitePassedPawnMask : Bits.BlackPassedPawnMask;
		int bonus = 0;
		int numIsolatedPawns = 0;

		for (int i = 0; i < pawns.Count; i++)
		{
			int square = pawns[i];
			ulong passedMask = masks[square];
			// Is passed pawn
			if ((opponentPawns & passedMask) == 0)
			{
				int rank = BoardUtility.RankIndex(square);
				int numSquaresFromPromotion = isWhite ? 7 - rank : rank;
				bonus += PassedPawnBonuses[numSquaresFromPromotion];
			}

			// Is isolated pawn
			if ((friendlyPawns & Bits.AdjacentFileMasks[BoardUtility.FileIndex(square)]) == 0)
			{
				numIsolatedPawns++;
			}
		}

		return bonus + IsolatedPawnPenaltyByCount[numIsolatedPawns];
	}
	
	private int MopUpEval(bool isWhite, MaterialInfo myMaterial, MaterialInfo enemyMaterial)
	{
		/*
		 * This method encourages the player to use their king to push the enemy king to the edge of the board in the
		 * endgame, especially when the player has a material advantage.
		 */
		if (myMaterial.MaterialScore > enemyMaterial.MaterialScore + PawnValue * 2 && enemyMaterial.EndgameTrans > 0)
		{
			int mopUpScore = 0;
			int friendlyIndex = isWhite ? Board.Board.WhiteIndex : Board.Board.BlackIndex;
			int opponentIndex = isWhite ? Board.Board.BlackIndex : Board.Board.WhiteIndex;

			int friendlyKingSquare = _board.KingSquare[friendlyIndex];
			int opponentKingSquare = _board.KingSquare[opponentIndex];
			// Encourage moving king closer to opponent king
			mopUpScore += (14 - PrecomputedMoveData.OrthogonalDistance[friendlyKingSquare, opponentKingSquare]) * 4;
			// Encourage pushing opponent king to edge of board
			mopUpScore += PrecomputedMoveData.CentreManhattanDistance[opponentKingSquare] * 10;
			return (int)(mopUpScore * enemyMaterial.EndgameTrans);
		}
		return 0;
	}

	private int EvaluatePieceSquareTables(bool isWhite, float endgameTrans)
	{
		/*
		 * This method evaluates the positions of the pieces on the board using piece-square tables. Piece-square tables
		 * are a common heuristic in chess engines. They assign a score to each square of the board for each type of
		 * piece, representing how desirable it is for that piece to be on that square.
		 */
		int value = 0;
		int colourIndex = isWhite ? Board.Board.WhiteIndex : Board.Board.BlackIndex;
		//value += EvaluatePieceSquareTable(PieceSquareTable.Pawns, board.pawns[colourIndex], isWhite);
		value += EvaluatePieceSquareTable(PieceSquareTable.Rooks, _board.Rooks[colourIndex], isWhite);
		value += EvaluatePieceSquareTable(PieceSquareTable.Knights, _board.Knights[colourIndex], isWhite);
		value += EvaluatePieceSquareTable(PieceSquareTable.Bishops, _board.Bishops[colourIndex], isWhite);
		value += EvaluatePieceSquareTable(PieceSquareTable.Queens, _board.Queens[colourIndex], isWhite);

		int pawnEarly = EvaluatePieceSquareTable(PieceSquareTable.Pawns, _board.Pawns[colourIndex], isWhite);
		int pawnLate = EvaluatePieceSquareTable(PieceSquareTable.PawnsEnd, _board.Pawns[colourIndex], isWhite);

		value += (int)(pawnEarly * (1 - endgameTrans));
		value += (int)(pawnLate * endgameTrans);

		int kingEarlyPhase = PieceSquareTable.GetSquareValue(PieceSquareTable.KingStart, _board.KingSquare[colourIndex], isWhite);
		value += (int)(kingEarlyPhase * (1 - endgameTrans));
		int kingLatePhase = PieceSquareTable.GetSquareValue(PieceSquareTable.KingEnd, _board.KingSquare[colourIndex], isWhite);
		value += (int)(kingLatePhase * endgameTrans);

		return value;
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
		public int MaterialScore;
		public int MopUpScore;
		public int PieceSquareScore;
		public int PawnScore;
		public int PawnShieldScore;

		public int Sum()
		{
			return MaterialScore + MopUpScore + PieceSquareScore + PawnScore + PawnShieldScore;
		}
	}

	private MaterialInfo GetMaterialInfo(int colourIndex)
	{
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

		// Endgame Transition is a value between 0 and 1 that represents how close the position is to an endgame.
		public readonly float EndgameTrans;

		public MaterialInfo(int numPawns, int numKnights, int numBishops, int numQueens, int numRooks, ulong myPawns, ulong enemyPawns)
		{
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