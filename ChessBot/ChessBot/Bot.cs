using ChessBot.Core.Board;
using ChessBot.Core.MoveGeneration;
using ChessBot.Core.OpeningBook;
using ChessBot.Core.Search;
using ChessBot.Core.Utilities;

using static System.Math;

namespace ChessBot;

public class Bot
{
	private const bool UseOpeningBook = true;

	private const int MaxBookPly = 16;

	private const bool UseMaxThinkTime = false;
	private const int MaxThinkTimeMs = 30000;

	// Public stuff
	public event Action<string>? OnMoveChosen;
	public bool IsThinking { get; set; }
	public bool LatestMoveIsBookMove { get; private set; }

	// References
	private readonly Searcher _searcher;
	private readonly Board _board;
	private readonly OpeningBookManager _bookManager;
	private readonly AutoResetEvent _searchWaitHandle;
	private CancellationTokenSource? _cancelSearchTimer;

	// State
	private int _currentSearchId;
	private bool _isQuitting;

	public Bot(Board board)
	{
		_board = board;
		_searcher = new Searcher(board);
		_searcher.OnSearchComplete += OnSearchComplete;

		_bookManager = new OpeningBookManager(Properties.Resources.Book);
		_searchWaitHandle = new(false);

		Task.Factory.StartNew(SearchThread, TaskCreationOptions.LongRunning);
	}

	public Bot()
	{
		_board = Board.CreateBoard();
		_searcher = new Searcher(_board);
		_searcher.OnSearchComplete += OnSearchComplete;

		_bookManager = new OpeningBookManager(Properties.Resources.Book);
		_searchWaitHandle = new(false);

		Task.Factory.StartNew(SearchThread, TaskCreationOptions.LongRunning);
	}

	public void NotifyNewGame()
	{
		_searcher.ClearForNewPosition();
	}

	public void SetPosition(string fen)
	{
		_board.LoadPosition(fen);
	}

	public void MakeMove(string moveString)
	{
		Move move = MoveUtility.GetMoveFromUciName(moveString, _board);
		_board.MakeMove(move);
	}

	public int ChooseThinkTime(int timeRemainingWhiteMs, int timeRemainingBlackMs, int incrementWhiteMs,
		int incrementBlackMs)
	{
		int myTimeRemainingMs = _board.IsWhiteToMove ? timeRemainingWhiteMs : timeRemainingBlackMs;
		int myIncrementMs = _board.IsWhiteToMove ? incrementWhiteMs : incrementBlackMs;
		double thinkTimeMs = myTimeRemainingMs / 40.0;
		if (UseMaxThinkTime)
		{
			thinkTimeMs = Min(MaxThinkTimeMs, thinkTimeMs);
		}

		// Add increment
		if (myTimeRemainingMs > myIncrementMs * 2)
		{
			thinkTimeMs += myIncrementMs * 0.8;
		}

		double minThinkTime = Min(50, myTimeRemainingMs * 0.25);
		return (int)Ceiling(Max(minThinkTime, thinkTimeMs));
	}

	public void ThinkTimed(int timeMs)
	{
		LatestMoveIsBookMove = false;
		IsThinking = true;
		_cancelSearchTimer?.Cancel();

		if (TryGetOpeningBookMove(out Move bookMove))
		{
			if (!Static.UseUci)
			{
				Console.WriteLine(@"--Book move--");
			}

			LatestMoveIsBookMove = true;
			OnSearchComplete(bookMove);
		}
		else
		{
			if (!Static.UseUci)
			{
				Console.WriteLine(@$"Thinking time: {timeMs / 1000}s");
				Console.WriteLine(@"--Thinking--");
			}

			StartSearch(timeMs);
		}
	}

	private void StartSearch(int timeMs)
	{
		_currentSearchId++;
		_searchWaitHandle.Set();
		_cancelSearchTimer = new CancellationTokenSource();
		Task.Delay(timeMs, _cancelSearchTimer.Token).ContinueWith(_ => EndSearch(_currentSearchId));
	}

	private void SearchThread()
	{
		while (!_isQuitting)
		{
			_searchWaitHandle.WaitOne();
			_searcher.StartSearch();
		}
	}

	public void StopThinking()
	{
		EndSearch();
	}

	public void Quit()
	{
		_isQuitting = true;
		EndSearch();
	}

	// public string GetBoardDiagram() => _board.ToString();

	private void EndSearch()
	{
		_cancelSearchTimer?.Cancel();
		if (IsThinking)
		{
			_searcher.EndSearch();
		}
	}

	private void EndSearch(int searchId)
	{
		// If search timer has been cancelled, the search will have been stopped already
		if (_cancelSearchTimer != null && _cancelSearchTimer.IsCancellationRequested)
		{
			return;
		}

		if (_currentSearchId == searchId)
		{
			EndSearch();
		}
	}

	private void OnSearchComplete(Move move)
	{
		IsThinking = false;

		string moveName = MoveUtility.GetMoveNameUci(move).Replace("=", "");

		OnMoveChosen?.Invoke(moveName);
	}

	private bool TryGetOpeningBookMove(out Move bookMove)
	{
		if (UseOpeningBook && _board.PlyCount <= MaxBookPly &&
			_bookManager.TryGetBookMove(_board, out string moveString))
		{
			bookMove = MoveUtility.GetMoveFromUciName(moveString, _board);
			return true;
		}

		bookMove = Move.NullMove;
		return false;
	}

	private static string GetResourcePath(params string[] localPath)
	{
		return Path.Combine(Directory.GetCurrentDirectory(), "resources", Path.Combine(localPath));
	}

	public static string ReadResourceFile(string localPath)
	{
		return File.ReadAllText(GetResourcePath(localPath));
	}

	public string GetBoardDiagram() => _board.ToString()!;
}