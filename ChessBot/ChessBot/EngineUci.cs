﻿using ChessBot.Core.Utilities;

namespace ChessBot;

public class EngineUci
{
	readonly Bot _player;
	static readonly bool UseLogToFile = false;

	static readonly string[] PositionLabels = ["position", "fen", "moves"];
	static readonly string[] GoLabels = ["go", "movetime", "wtime", "btime", "winc", "binc", "movestogo"];

	public EngineUci()
	{
		_player = new Bot();
		_player.OnMoveChosen += OnMoveChosen;
	}

	public void ReceiveCommand(string message)
	{
		//Console.WriteLine(message);
		LogToFile("Command received: " + message);
		message = message.Trim();
		string messageType = message.Split(' ')[0].ToLower();

		switch (messageType)
		{
			case "uci":
				Respond("uciok");
				break;
			case "isready":
				Respond("readyok");
				break;
			case "ucinewgame":
				_player.NotifyNewGame();
				break;
			case "position":
				ProcessPositionCommand(message);
				break;
			case "go":
				ProcessGoCommand(message);
				break;
			case "stop":
				if (_player.IsThinking)
				{
					_player.StopThinking();
				}
				break;
			case "quit":
				_player.Quit();
				break;
			case "d":
				Console.WriteLine(_player.GetBoardDiagram());
				break;
			default:
				LogToFile($"Unrecognized command: {messageType}");
				break;
		}
	}

	void OnMoveChosen(string move)
	{
		LogToFile("OnMoveChosen: book move = " + _player.LatestMoveIsBookMove);
		Respond("bestmove " + move);
	}

	void ProcessGoCommand(string message)
	{
		if (message.Contains("movetime"))
		{
			int moveTimeMs = TryGetLabelledValueInt(message, "movetime", GoLabels, 0);
			_player.ThinkTimed(moveTimeMs);
		}
		else
		{
			int timeRemainingWhiteMs = TryGetLabelledValueInt(message, "wtime", GoLabels, 0);
			int timeRemainingBlackMs = TryGetLabelledValueInt(message, "btime", GoLabels, 0);
			int incrementWhiteMs = TryGetLabelledValueInt(message, "winc", GoLabels, 0);
			int incrementBlackMs = TryGetLabelledValueInt(message, "binc", GoLabels, 0);

			int thinkTime = _player.ChooseThinkTime(timeRemainingWhiteMs, timeRemainingBlackMs, incrementWhiteMs, incrementBlackMs);
			LogToFile("Thinking for: " + thinkTime + " ms.");
			_player.ThinkTimed(thinkTime);
		}

	}

	// Format: 'position startpos moves e2e4 e7e5'
	// Or: 'position fen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1 moves e2e4 e7e5'
	// Note: 'moves' section is optional
	void ProcessPositionCommand(string message)
	{
		// FEN
		if (message.ToLower().Contains("startpos"))
		{
			_player.SetPosition(FenUtility.StartPositionFen);
		}
		else if (message.ToLower().Contains("fen"))
		{
			string customFen = TryGetLabelledValue(message, "fen", PositionLabels);
			_player.SetPosition(customFen);
		}
		else
		{
			Console.WriteLine("Invalid position command (expected 'startpos' or 'fen')");
		}

		// Moves
		string allMoves = TryGetLabelledValue(message, "moves", PositionLabels);
		if (!string.IsNullOrEmpty(allMoves))
		{
			string[] moveList = allMoves.Split(' ');
			foreach (string move in moveList)
			{
				_player.MakeMove(move);
			}

			LogToFile($"Make moves after setting position: {moveList.Length}");
		}
	}

	void Respond(string reponse)
	{
		Console.WriteLine(reponse);
		LogToFile("Response sent: " + reponse);
	}

	static int TryGetLabelledValueInt(string text, string label, string[] allLabels, int defaultValue = 0)
	{
		string valueString = TryGetLabelledValue(text, label, allLabels, defaultValue + "");
		if (int.TryParse(valueString.Split(' ')[0], out int result))
		{
			return result;
		}
		return defaultValue;
	}

	static string TryGetLabelledValue(string text, string label, string[] allLabels, string defaultValue = "")
	{
		text = text.Trim();
		if (text.Contains(label))
		{
			int valueStart = text.IndexOf(label) + label.Length;
			int valueEnd = text.Length;
			foreach (string otherId in allLabels)
			{
				if (otherId != label && text.Contains(otherId))
				{
					int otherIdStartIndex = text.IndexOf(otherId);
					if (otherIdStartIndex > valueStart && otherIdStartIndex < valueEnd)
					{
						valueEnd = otherIdStartIndex;
					}
				}
			}

			return text.Substring(valueStart, valueEnd - valueStart).Trim();
		}
		return defaultValue;
	}

	void LogToFile(string text)
	{
		if (UseLogToFile)
		{
			Directory.CreateDirectory(AppDataPath);
			string path = Path.Combine(AppDataPath, "UCI_Log.txt");

			using (StreamWriter writer = new StreamWriter(path, true))
			{
				writer.WriteLine(text);
			}
		}
	}

	public static string AppDataPath
	{
		get
		{
			string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			return Path.Combine(dir, "Chess-Coding-Adventure");
		}
	}

}