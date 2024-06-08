using ChessBot.Core.MoveGeneration;

namespace ChessBot.Core.Search;

public class TranspositionTable
{
	public const int LookupFailed = -1;

	public const int Exact = 0;
	public const int LowerBound = 1;
	public const int UpperBound = 2;

	public readonly Entry[] Entries;

	private readonly ulong _count;
	private readonly bool _enabled = true;
	private readonly Board.Board _board;

	public TranspositionTable(Board.Board board, int sizeMb)
	{
		_board = board;

		int ttEntrySizeBytes = System.Runtime.InteropServices.Marshal.SizeOf<Entry>();
		int desiredTableSizeInBytes = sizeMb * 1024 * 1024;
		int numEntries = desiredTableSizeInBytes / ttEntrySizeBytes;

		_count = (ulong)numEntries;
		Entries = new Entry[numEntries];
	}

	public void Clear()
	{
		for (int i = 0; i < Entries.Length; i++)
		{
			Entries[i] = new Entry();
		}
	}

	public ulong Index => _board.CurrentGameState.ZobristKey % _count;

	public Move TryGetStoredMove()
	{
		return Entries[Index].Move;
	}

	public bool TryLookupEvaluation(int depth, int plyFromRoot, int alpha, int beta, out int eval)
	{
		eval = 0;
		return false;
	}

	public int LookupEvaluation(int depth, int plyFromRoot, int alpha, int beta)
	{
		if (!_enabled)
		{
			return LookupFailed;
		}

		Entry entry = Entries[Index];

		if (entry.Key != _board.CurrentGameState.ZobristKey) return LookupFailed;
		if (entry.Depth < depth) return LookupFailed;
		int correctedScore = CorrectRetrievedMateScore(entry.Value, plyFromRoot);
		return entry.NodeType switch
		{
			Exact => correctedScore,
			UpperBound when correctedScore <= alpha => correctedScore,
			LowerBound when correctedScore >= beta => correctedScore,
			_ => LookupFailed
		};
	}

	public void StoreEvaluation(int depth, int numPlySearched, int eval, int evalType, Move move)
	{
		if (!_enabled)
		{
			return;
		}

		Entry entry = new Entry(_board.CurrentGameState.ZobristKey, CorrectMateScoreForStorage(eval, numPlySearched),
			(byte)depth, (byte)evalType, move);
		Entries[Index] = entry;
	}

	private int CorrectMateScoreForStorage(int score, int numPlySearched)
	{
		if (!Searcher.IsMateScore(score)) return score;
		int sign = Math.Sign(score);
		return (score * sign + numPlySearched) * sign;
	}

	private int CorrectRetrievedMateScore(int score, int numPlySearched)
	{
		if (Searcher.IsMateScore(score))
		{
			int sign = Math.Sign(score);
			return (score * sign - numPlySearched) * sign;
		}

		return score;
	}

	public struct Entry(ulong key, int value, byte depth, byte nodeType, Move move)
	{
		public readonly ulong Key = key; // the zobrist key of the position
		public readonly int Value = value; // the evaluation of the position
		public readonly Move Move = move; // the move that was played to reach this position
		public readonly byte Depth = depth; // depth is how many ply were searched ahead from this position
		public readonly byte NodeType = nodeType;

		public static int GetSize()
		{
			return System.Runtime.InteropServices.Marshal.SizeOf<Entry>();
		}
	}
}