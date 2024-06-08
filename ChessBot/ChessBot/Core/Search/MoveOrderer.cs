using ConsoleChess.Core.Board;
using ConsoleChess.Core.Evaluation;
using ConsoleChess.Core.Move_Generation;
using ConsoleChess.Core.Move_Generation.Bitboards;

namespace ConsoleChess.Core.Search;

/*
 * a killer move is a non-capturing move that caused a beta cutoff in the previous search. A beta cutoff occurs when
 * the search algorithm determines that it has found a move that is so good that it doesn't need to consider the
 * remaining moves.
 *
 * The idea behind killer moves is that if a non-capturing move caused a beta cutoff in the previous search, it is
 * likely to cause a beta cutoff again in the current search. Therefore, these moves are given a high priority in the
 * move ordering process, meaning they are examined early in the search. This can lead to more effective use of
 * alpha-beta pruning and a more efficient search process.
 */

public class MoveOrderer
{
	private int[] _moveScores = new int[MaxMoveCount];
	private const int MaxMoveCount = 218;

	public Killers[] KillerMoves = new Killers[MaxKillerMovePly];
	public int[,,] History = new int[2, 64, 64]; // 3D array to store history heuristic values
	// color of the piece making the move.
	// starting square of the piece making the move.
	// target square of the piece making the move.
	
	public const int MaxKillerMovePly = 32;

	private const int Million = 1000000;
	private const int HashMoveScore = 100 * Million;
	private const int WinningCaptureBias = 8 * Million;
	private const int PromoteBias = 6 * Million;
	private const int KillerBias = 4 * Million;
	private const int LosingCaptureBias = 2 * Million;
	private const int RegularBias = 0;

	public void ClearHistory()
	{
		History = new int[2, 64, 64];
	}

	public void ClearKillers()
	{
		KillerMoves = new Killers[MaxKillerMovePly];
	}

	public void OrderMoves(Move hashMove, Board.Board board, Span<Move> moves, ulong oppAttacks, ulong oppPawnAttacks, bool inQSearch, int ply)
	{
		for (int i = 0; i < moves.Length; i++)
		{
			// for each move, calculate a score based on the move type, piece value, and history heuristic value

			Move move = moves[i];

			if (Move.SameMove(move, hashMove))
			{
				_moveScores[i] = HashMoveScore;
				continue;
			}
			int score = 0;
			int sourceSquare = move.StartSquare;
			int targetSquare = move.TargetSquare;

			int movePiece = board.Squares[sourceSquare];
			int movePieceType = Piece.PieceType(movePiece);
			int capturePieceType = Piece.PieceType(board.Squares[targetSquare]);
			bool isCapture = capturePieceType != Piece.None;
			int flag = moves[i].MoveFlag;
			int pieceValue = GetPieceValue(movePieceType);

			if (isCapture)
			{
				// Order moves to try capturing the most valuable opponent piece with least valuable of own pieces first
				int captureMaterialDelta = GetPieceValue(capturePieceType) - pieceValue;
				bool opponentCanRecapture = BitBoardUtility.ContainsSquare(oppPawnAttacks | oppAttacks, targetSquare);
				if (opponentCanRecapture)
				{
					score += (captureMaterialDelta >= 0 ? WinningCaptureBias : LosingCaptureBias) + captureMaterialDelta;
				}
				else
				{
					score += WinningCaptureBias + captureMaterialDelta;
				}
			}

			if (movePieceType == Piece.Pawn)
			{
				if (flag == Move.PromoteToQueenFlag && !isCapture)
				{
					score += PromoteBias;
				}
			}
			else if (movePieceType == Piece.King)
			{
				// don't move king into danger
			}
			else
			{
				int sourceScore = PieceSquareTable.GetSquareValue(movePiece, sourceSquare);
				int targetScore = PieceSquareTable.GetSquareValue(movePiece, targetSquare);
				score += targetScore - sourceScore;

				if (BitBoardUtility.ContainsSquare(oppPawnAttacks, targetSquare))
				{
					score -= 50;
				}
				else if (BitBoardUtility.ContainsSquare(oppAttacks, targetSquare))
				{
					score -= 25;
				}
			}

			if (!isCapture)
			{
				bool isKiller = !inQSearch && ply < MaxKillerMovePly && KillerMoves[ply].Match(move);
				score += isKiller ? KillerBias : RegularBias;
				score += History[board.MoveColourIndex, move.StartSquare, move.TargetSquare];
			}

			_moveScores[i] = score;
		}

		//Sort(moves, moveScores);
		Quicksort(moves, _moveScores, 0, moves.Length - 1);
	}

	private static int GetPieceValue(int pieceType)
	{
		switch (pieceType)
		{
			case Piece.Queen:
				return Evaluation.Evaluator.QueenValue;
			case Piece.Rook:
				return Evaluation.Evaluator.RookValue;
			case Piece.Knight:
				return Evaluation.Evaluator.KnightValue;
			case Piece.Bishop:
				return Evaluation.Evaluator.BishopValue;
			case Piece.Pawn:
				return Evaluation.Evaluator.PawnValue;
			default:
				return 0;
		}
	}

	public static void Quicksort(Span<Move> values, int[] scores, int low, int high)
	{
		if (low < high)
		{
			int pivotIndex = Partition(values, scores, low, high);
			Quicksort(values, scores, low, pivotIndex - 1);
			Quicksort(values, scores, pivotIndex + 1, high);
		}
	}

	private static int Partition(Span<Move> values, int[] scores, int low, int high)
	{
		int pivotScore = scores[high];
		int i = low - 1;

		for (int j = low; j <= high - 1; j++)
		{
			if (scores[j] > pivotScore)
			{
				i++;
				(values[i], values[j]) = (values[j], values[i]);
				(scores[i], scores[j]) = (scores[j], scores[i]);
			}
		}
		(values[i + 1], values[high]) = (values[high], values[i + 1]);
		(scores[i + 1], scores[high]) = (scores[high], scores[i + 1]);

		return i + 1;
	}
}


public struct Killers
{
	/*
	 * By storing two killer moves instead of one, the engine can keep track of two potentially good moves for each ply,
	 * increasing the chances of one of them causing a beta cutoff and improving the efficiency of the search.
	 */
	private Move _moveA;
	private Move _moveB;

	public void Add(Move move)
	{
		if (move.Value != _moveA.Value)
		{
			_moveB = _moveA;
			_moveA = move;
		}
	}

	public bool Match(Move move) => move.Value == _moveA.Value || move.Value == _moveB.Value;

}