using System.Text;
using ConsoleChess.Core.Board;
using ConsoleChess.Core.Helpers;
using ConsoleChess.Core.Move_Generation;
using Spectre.Console;

namespace ConsoleChess.Core.Search;

using static Math;

public class Searcher
{
    // Constants
    private const int TranspositionTableSizeMb = 64;
    private const int MaxExtentions = 16;

    private const int ImmediateMateScore = 100000;
    private const int PositiveInfinity = 9999999;
    private const int NegativeInfinity = -PositiveInfinity;

    public event Action<Move>? OnSearchComplete;

    // State
    private Move _bestMoveThisIteration;
    private int _bestEvalThisIteration;
    private Move _bestMove;
    private int _bestEval;
    private bool _hasSearchedAtLeastOneMove;
    private bool _searchCancelled;

    // Diagnostics
    private SearchDiagnostics _searchDiagnostics;
    private System.Diagnostics.Stopwatch _searchIterationTimer;
    private System.Diagnostics.Stopwatch _searchTotalTimer;
    private readonly StringBuilder _debugInfo;

    // References
    private readonly TranspositionTable _transpositionTable;
    private readonly RepetitionTable _repetitionTable;
    private readonly MoveGenerator _moveGenerator;
    private readonly MoveOrderer _moveOrderer;
    private readonly Evaluation.Evaluator _evaluator;
    private readonly Board.Board _board;

    public Searcher(Board.Board board)
    {
        _board = board;
        _debugInfo = new StringBuilder();
        _evaluator = new Evaluation.Evaluator();
        _moveGenerator = new MoveGenerator();
        _transpositionTable = new TranspositionTable(board, TranspositionTableSizeMb);
        _moveOrderer = new MoveOrderer();
        _repetitionTable = new RepetitionTable();

        // Run a depth 1 search so that JIT doesn't run during actual search (and mess up timing stats in editor)
        Search(1, 0, NegativeInfinity, PositiveInfinity);
    }

    public void StartSearch()
    {
        // Initialize search
        _bestEvalThisIteration = _bestEval = 0;
        _bestMoveThisIteration = _bestMove = Move.NullMove;

        _moveOrderer.ClearHistory();
        _repetitionTable.Init(_board);
        _debugInfo.Clear();

        // Initialize debug info
        _debugInfo.AppendLine($"Starting search with FEN [green]{FenUtility.CurrentFen(_board)}[/]");
        _searchCancelled = false;
        _searchDiagnostics = new SearchDiagnostics();
        _searchIterationTimer = new System.Diagnostics.Stopwatch();
        _searchTotalTimer = System.Diagnostics.Stopwatch.StartNew();

        // Search
        RunIterativeDeepeningSearch();

        // Finish up
        // In the unlikely event that the search is cancelled before a best move can be found, take any move
        if (_bestMove.IsNull)
        {
            _bestMove = _moveGenerator.GenerateMoves(_board)[0];
        }

        _searchTotalTimer.Stop();
        _debugInfo.AppendLine($@"Search completed in {_searchTotalTimer.ElapsedMilliseconds}ms");
        _debugInfo.AppendLine($@"Search diagnostics: 
    {_searchDiagnostics.NumCompletedIterations} full iterations, 
    {_searchDiagnostics.NumPositionsEvaluated} positions evaluated, 
    {_searchDiagnostics.NumCutOffs} cutoffs,
    {_searchDiagnostics.NumRecursiveSearch} recursive searches,
    {_searchDiagnostics.NumReductions} reductions,
    {_searchDiagnostics.NumExtensions} extensions,
    {_searchDiagnostics.NumQuiescenceSearches} quiescence searches");

        AnsiConsole.MarkupLine(_debugInfo.ToString());
        
        OnSearchComplete?.Invoke(_bestMove);
        _searchCancelled = false;
    }

    // Run iterative deepening. This means doing a full search with a depth of 1, then with a depth of 2, and so on.
    // This allows the search to be cancelled at any time and still yield a useful result.
    // Thanks to the transposition table and move ordering, this idea is not nearly as terrible as it sounds.
    private void RunIterativeDeepeningSearch()
    {
        for (int searchDepth = 1; searchDepth <= 256; searchDepth++)
        {
            _hasSearchedAtLeastOneMove = false;
            _debugInfo.AppendLine("-- Starting Iteration: " + searchDepth);
            _searchIterationTimer.Restart();

            Search(searchDepth, 0, NegativeInfinity, PositiveInfinity);

            if (_searchCancelled)
            {
                if (_hasSearchedAtLeastOneMove)
                {
                    _bestMove = _bestMoveThisIteration;
                    _bestEval = _bestEvalThisIteration;
                    _debugInfo.AppendLine(
                        $@"Using best move from incomplete search: {MoveUtility.GetMoveNameUci(_bestMove)} Eval: {_bestEval}");
                }

                _debugInfo.AppendLine("Search aborted");
                break;
            }

            _bestMove = _bestMoveThisIteration;
            _bestEval = _bestEvalThisIteration;

            _debugInfo.AppendLine($">>> Iteration result: [green]Best move: [/]{MoveUtility.GetMoveNameUci(_bestMove)} [green]Eval: [/] {_bestEval}");
            if (IsMateScore(_bestEval))
            {
                _debugInfo.AppendLine("[yellow]- Mate in ply: [/]" + NumPlyToMateFromScore(_bestEval));
            }

            _bestEvalThisIteration = int.MinValue;
            _bestMoveThisIteration = Move.NullMove;

            // Update diagnostics
            _searchDiagnostics.NumCompletedIterations = searchDepth;

            // Exit search if found a mate within search depth.
            // A mate found outside of search depth (due to extensions) may not be the fastest mate.
            if (IsMateScore(_bestEval) && NumPlyToMateFromScore(_bestEval) <= searchDepth)
            {
                _debugInfo.AppendLine("[green]Exiting search due to mate found within search depth[/]");
                break;
            }
        }
    }

    public void EndSearch()
    {
        _searchCancelled = true;
    }

    private int Search(int plyRemaining, int plyFromRoot, int alpha, int beta, int numExtensions = 0,
        Move prevMove = default, bool prevWasCapture = false)
    {
        // plyRemaining: number of remaining plies (half-moves) that can be explored in the current search branch.
        // plyFromRoot: number of plies from the root of the search tree to the current position.
        // alpha: the best score that the maximizing player can guarantee so far.
        // beta: the best score that the minimizing player can guarantee so far.
        if (_searchCancelled)
        {
            return 0;
        }
        
        _searchDiagnostics.NumRecursiveSearch++;

        if (plyFromRoot > 0)
        {
            // Detect draw by three-fold repetition.
            // (Note: returns a draw score even if this position has only appeared once for sake of simplicity)
            if (_board.CurrentGameState.FiftyMoveCounter >= 100 ||
                _repetitionTable.Contains(_board.CurrentGameState.ZobristKey))
            {
                return 0;
            }

            // Skip this position if a mating sequence has already been found earlier in the search, which would be shorter
            // than any mate we could find from here. This is done by observing that alpha can't possibly be worse
            // (and likewise beta can't  possibly be better) than being mated in the current position.
            alpha = Max(alpha, -ImmediateMateScore + plyFromRoot);
            beta = Min(beta, ImmediateMateScore - plyFromRoot);
            if (alpha >= beta)
            {
                return alpha;
            }
        }

        // Try looking up the current position in the transposition table.
        // If the same position has already been searched to at least an equal depth
        // to the search we're doing now,we can just use the recorded evaluation.
        int ttVal = _transpositionTable.LookupEvaluation(plyRemaining, plyFromRoot, alpha, beta);
        if (ttVal != TranspositionTable.LookupFailed)
        {
            if (plyFromRoot == 0)
            {
                _bestMoveThisIteration = _transpositionTable.TryGetStoredMove();
                _bestEvalThisIteration = _transpositionTable.Entries[_transpositionTable.Index].Value;
            }

            return ttVal;
        }

        if (plyRemaining == 0)
        {
            // when we reach the end of the search tree, we use quiescence search to get a more accurate evaluation
            int evaluation = QuiescenceSearch(alpha, beta);
            return evaluation;
        }

        Span<Move> moves = stackalloc Move[256];
        _moveGenerator.GenerateMoves(_board, ref moves, capturesOnly: false);
        Move prevBestMove = plyFromRoot == 0 ? _bestMove : _transpositionTable.TryGetStoredMove();

        _moveOrderer.OrderMoves(prevBestMove, _board, moves, _moveGenerator.OpponentAttackMap,
            _moveGenerator.OpponentPawnAttackMap, false, plyFromRoot);

        // Detect checkmate and stalemate when no legal moves are available
        if (moves.Length == 0)
        {
            if (_moveGenerator.IsInCheck())
            {
                int mateScore = ImmediateMateScore - plyFromRoot;
                return -mateScore;
            }

            return 0;
        }

        if (plyFromRoot > 0)
        {
            bool wasPawnMove = Piece.PieceType(_board.Squares[prevMove.TargetSquare]) == Piece.Pawn;
            _repetitionTable.Push(_board.CurrentGameState.ZobristKey, prevWasCapture || wasPawnMove);
        }

        int evaluationBound = TranspositionTable.UpperBound;
        Move bestMoveInThisPosition = Move.NullMove;

        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            int capturedPieceType = Piece.PieceType(_board.Squares[move.TargetSquare]);
            bool isCapture = capturedPieceType != Piece.None;
            _board.MakeMove(moves[i], isInSearch: true); // Make the move on the board (_board.MakeMove)

            // Potentially extend the search depth in interesting cases.
            int extension = 0;
            if (numExtensions < MaxExtentions)
            {
                int movedPieceType = Piece.PieceType(_board.Squares[move.TargetSquare]);
                int targetRank = BoardUtility.RankIndex(move.TargetSquare);
                // if the move is in check, or a pawn move to the 7th rank, extend the search depth
                if (_board.IsInCheck())
                {
                    extension = 1;
                    _searchDiagnostics.NumExtensions++;
                }
                else if (movedPieceType == Piece.Pawn && (targetRank == 1 || targetRank == 6))
                {
                    extension = 1;
                    _searchDiagnostics.NumExtensions++;
                }
            }

            bool needsFullSearch = true;
            int eval = 0;

            // Since we applied the MoveOrderer, we can be confident that the first move we look at is the best move.
            // So we can use a reduced window search to speed up the search as the later moves are likely to be worse.
            if (extension == 0 && plyRemaining >= 3 && i >= 3 && !isCapture)
            {
                const int reduceDepth = 1;
                // slightly increase our expectation about the opponent's ability to find a good move by narrowing the
                // window from (-beta, -alpha) to (-alpha - 1, -alpha).
                _searchDiagnostics.NumReductions++;
                eval = -Search(plyRemaining - 1 - reduceDepth, plyFromRoot + 1, -alpha - 1, -alpha, numExtensions, move,
                    isCapture);

                // If the evaluation is better than expected, we'd better to a full-depth search to get a more accurate evaluation
                // It's so good that even if our opponent was a bit better than we initially expected, they wouldn't be able to find a refutation.
                needsFullSearch = eval > alpha; // alpha here is beta in the recursive call
            }

            // Perform a full-depth search if the move seems better than expected or if no depth-reduction was applied.
            if (needsFullSearch)
            {
                eval = -Search(plyRemaining - 1 + extension, plyFromRoot + 1, -beta, -alpha, numExtensions + extension,
                    move, isCapture);
            }

            // Negating alpha and beta mirrors the switch in perspective.
            _board.UnmakeMove(moves[i], inSearch: true);

            if (_searchCancelled)
            {
                return 0;
            }

            // Beta Cutoff: 
            if (eval >= beta)
            {
                // Store evaluation in transposition table. Note that since we're exiting the search early, there may be an
                // even better move we haven't looked at yet, and so the current eval is a lower bound on the actual eval.
                _transpositionTable.StoreEvaluation(plyRemaining, plyFromRoot, beta, TranspositionTable.LowerBound,
                    moves[i]);

                // Update killer moves and history heuristic (note: don't include captures as theres are ranked highly anyway)
                if (!isCapture)
                {
                    if (plyFromRoot < MoveOrderer.MaxKillerMovePly)
                    {
                        _moveOrderer.KillerMoves[plyFromRoot].Add(move);
                    }

                    int historyScore = plyRemaining * plyRemaining;
                    _moveOrderer.History[_board.MoveColourIndex, moves[i].StartSquare, moves[i].TargetSquare] +=
                        historyScore;
                }

                if (plyFromRoot > 0)
                {
                    _repetitionTable.TryPop();
                }

                _searchDiagnostics.NumCutOffs++;
                return beta;
            }

            // Alpha Update: If the evaluation is better than the current alpha (the best score we're guaranteed so far), update alpha 
            //and record the best move.
            if (eval > alpha)
            {
                evaluationBound = TranspositionTable.Exact;
                bestMoveInThisPosition = moves[i];

                alpha = eval;
                if (plyFromRoot == 0)
                {
                    _bestMoveThisIteration = moves[i];
                    _bestEvalThisIteration = eval;
                    _hasSearchedAtLeastOneMove = true;
                }
            }
        }

        if (plyFromRoot > 0)
        {
            _repetitionTable.TryPop();
        }

        _transpositionTable.StoreEvaluation(plyRemaining, plyFromRoot, alpha, evaluationBound, bestMoveInThisPosition);

        return alpha;
    }

    // Search capture moves until a 'quiet' position is reached.
    private int QuiescenceSearch(int alpha, int beta)
    {
        // quiet position: a position where no captures are available, or a position where the only captures available are bad.
        // The Quiescence search is used to get a more accurate evaluation of the position in these cases, avoiding the
        // horizon effect where a seemingly good position is actually bad due to a forced capture.
        
        if (_searchCancelled)
        {
            return 0;
        }
        
        _searchDiagnostics.NumQuiescenceSearches++;

        // A player isn't forced to make a capture (typically), so see what the evaluation is without capturing anything.
        // This prevents situations where a player ony has bad captures available from being evaluated as bad,
        // when the player might have good non-capture moves available.
        int eval = _evaluator.Evaluate(_board);
        _searchDiagnostics.NumPositionsEvaluated++;
        if (eval >= beta)
        {
            _searchDiagnostics.NumCutOffs++;
            return beta;
        }

        alpha = Max(eval, alpha);

        Span<Move> moves = stackalloc Move[128];
        _moveGenerator.GenerateMoves(_board, ref moves, capturesOnly: true);
        _moveOrderer.OrderMoves(Move.NullMove, _board, moves, _moveGenerator.OpponentAttackMap,
            _moveGenerator.OpponentPawnAttackMap, true, 0);
        foreach (var move in moves)
        {
            _board.MakeMove(move, true);
            eval = -QuiescenceSearch(-beta, -alpha);
            _board.UnmakeMove(move, true);

            if (eval >= beta)
            {
                _searchDiagnostics.NumCutOffs++;
                return beta;
            }

            if (eval > alpha)
            {
                alpha = eval;
            }
        }

        return alpha;
    }

    public static bool IsMateScore(int score)
    {
        if (score == int.MinValue)
        {
            return false;
        }

        const int maxMateDepth = 1000;
        return Abs(score) > ImmediateMateScore - maxMateDepth;
    }

    public static int NumPlyToMateFromScore(int score)
    {
        return ImmediateMateScore - Abs(score);
    }

    public void ClearForNewPosition()
    {
        _transpositionTable.Clear();
        _moveOrderer.ClearKillers();
    }

    public TranspositionTable GetTranspositionTable() => _transpositionTable;

    [Serializable]
    public struct SearchDiagnostics
    {
        public int NumCompletedIterations;
        public int NumPositionsEvaluated;
        public ulong NumCutOffs;
        public ulong NumRecursiveSearch;
        public ulong NumReductions;
        public ulong NumExtensions;
        public ulong NumQuiescenceSearches;
    }
}