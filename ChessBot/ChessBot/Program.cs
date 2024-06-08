namespace ChessBot;

public static class Program
{
	public static void Main(string[] args)
	{
		if (Static.UseUci)
		{
			EngineUci engineUci = new();

			string command = String.Empty;
			while (command != "quit")
			{
				command = Console.ReadLine();
				engineUci.ReceiveCommand(command);
			}
		}
		else
		{
			Engine engine = new();

			// use UTF-8 encoding
			Console.OutputEncoding = System.Text.Encoding.UTF8;

			engine.RunGame();
		}
	}
}