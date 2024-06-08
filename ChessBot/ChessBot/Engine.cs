using ChessBot.Core.Board;
using ChessBot.Core.MoveGeneration;
using ChessBot.Core.Utilities;
using Spectre.Console;

namespace ChessBot;

public class Engine
{
	private readonly Bot _player;
	private readonly Board _board;
	private readonly MoveGenerator _moveGenerator;

	private bool _isQuit;
	private bool _isBotThinking;
	private const int MoveTimeMs = 7000;

	private readonly AutoResetEvent _autoResetEvent = new(false);

	public Engine()
	{
		_board = Board.CreateBoard();
		_player = new Bot(_board);
		_moveGenerator = new MoveGenerator();
		_player.OnMoveChosen += OnMoveChosen;
	}

	public void RunGame()
	{
		Console.WriteLine("Chess Console\n\t1 - New game\n\t2 - Load board");
		Console.Write(@"Enter command: ");
		int choice = int.Parse(Console.ReadLine() ?? throw new InvalidOperationException());
		if (choice == 2)
		{
			Console.WriteLine(@"Enter FEN: ");
			string fen = Console.ReadLine() ?? throw new InvalidOperationException();
			_player.SetPosition(fen);
			_isBotThinking = true;
		}
		else
		{
			AnsiConsole.MarkupLine("Choose your color: \n1 - [#F5E8C7]White[/]\n2 - [#03AED2]Black[/]");
			Console.Write(@"Enter command: ");
			_isBotThinking = int.Parse(Console.ReadLine() ?? throw new InvalidOperationException()) == 2;
		}

		_player.NotifyNewGame();

		while (_isQuit == false && CheckResult() == false)
		{
			BoardUtility.PrintDiagram(_board, _board.IsWhiteToMove);

			if (_isBotThinking)
			{
				_player.ThinkTimed(MoveTimeMs);
				_autoResetEvent.WaitOne();
			}
			else
			{
				GetUserMove();
				Console.WriteLine();
			}

			_isBotThinking = !_isBotThinking;
		}
	}

	private bool CheckResult()
	{

		if (!_isQuit && !IsEndGame())
		{
			if (_board.IsInCheck())
			{
				AnsiConsole.MarkupLine("[red]Check![/]");
				return false;
			}

			if (_board.IsThreeFoldRepetition())
			{
				var panel = new Panel("Draw by threefold repetition")
					.Header("[yellow]Draw![/]");
				AnsiConsole.Write(panel);
				return true;
			}

			if (_board.IsFiftyMoveDraw())
			{
				var panel = new Panel("Draw by 50 moves rule")
					.Header("[yellow]Draw![/]");
				AnsiConsole.Write(panel);
				return true;
			}

			return false;
		}

		BoardUtility.PrintDiagram(_board, _board.IsWhiteToMove);

		if (_board.IsInCheck())
		{
			var winner = _board.IsWhiteToMove ? "[#03AED2]Black wins![/]" : "[#F5E8C7]White wins![/]";
			var panel = new Panel(winner)
				.Header("[red]Checkmate![/]");
			AnsiConsole.Write(panel);
		}
		else
		{
			var panel = new Panel("Stalemate")
				.Header("[yellow]Draw![/]");
			AnsiConsole.Write(panel);
		}

		return true;
	}

	private void OnMoveChosen(string move)
	{
		AnsiConsole.MarkupLine(@$"Best move: [green]{move}[/]");
		_player.MakeMove(move);
		_autoResetEvent.Set();
	}

	private void GetUserMove()
	{
		List<(CoordAlphabet, CoordAlphabet)> availableMoves = GetAllMoves();

		// Print available moves

		Console.WriteLine();
		string userMove;
		while (true)
		{
			Console.Write(@"Input your move: ");
			userMove = Console.ReadLine() ?? throw new InvalidOperationException();

			if (userMove == "quit")
			{
				_isQuit = true;
				return;
			}

			if (userMove.Length == 2)
			{
				// user entered the target square
				List<string> possibleMoves = availableMoves
					.Where(move => move.Item2.ToString() == userMove)
					.Select(move => move.Item1.ToString() + move.Item2.ToString())
					.ToList();
				if (possibleMoves.Count == 1)
				{
					userMove = possibleMoves.First();
					break;
				}

				if (possibleMoves.Count > 1)
				{
					Console.Write(@"Input the start square: ");
					string startSquare = Console.ReadLine() ?? throw new InvalidOperationException();
					if (possibleMoves.Contains(startSquare + userMove))
					{
						userMove = startSquare + userMove;
						break;
					}
				}
			}

			if (userMove.Length == 4)
			{
				// user entered the start and target square, check if it's a valid move
				if (availableMoves.Any(move =>
						move.Item1.ToString() == userMove.Substring(0, 2) &&
						move.Item2.ToString() == userMove.Substring(2, 2)))
				{
					break;
				}
			}
			else
			{
				AnsiConsole.MarkupLine("[red]Invalid move, try again.[/]");
			}
		}

		_player.MakeMove(userMove);
	}

	private bool IsEndGame()
	{
		return GetAllMoves().Count == 0;
	}

	private List<(CoordAlphabet, CoordAlphabet)> GetAllMoves()
	{
		List<(CoordAlphabet, CoordAlphabet)> moveList = [];
		Span<Move> generateMoves = _moveGenerator.GenerateMoves(_board);

		foreach (Move move in generateMoves)
		{
			CoordAlphabet startSquare = new(move.StartSquare / 8, move.StartSquare % 8);
			CoordAlphabet targetSquare = new(move.TargetSquare / 8, move.TargetSquare % 8);
			moveList.Add((startSquare, targetSquare));
		}

		return moveList;
	}

	private struct CoordAlphabet(int rank, int file)
	{
		public override string ToString()
		{
			return $"{(char)('a' + file)}{rank + 1}";
		}
	}
}