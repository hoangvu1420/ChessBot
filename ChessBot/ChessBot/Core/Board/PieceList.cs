namespace ChessBot.Core.Board;

public class PieceList(int maxPieceCount = 16)
{

	// Indices of squares occupied by given piece type (only elements up to Count are valid, the rest are unused/garbage)
	public int[] occupiedSquares = new int[maxPieceCount];
	// Map to go from index of a square, to the index in the occupiedSquares array where that square is stored
	private int[] _map = new int[64];
	private int _numPieces = 0;

	public int Count => _numPieces;

	public void AddPieceAtSquare(int square)
	{
		occupiedSquares[_numPieces] = square;
		_map[square] = _numPieces;
		_numPieces++;
	}

	public void RemovePieceAtSquare(int square)
	{
		int pieceIndex = _map[square]; // get the index of this element in the occupiedSquares array
		occupiedSquares[pieceIndex] = occupiedSquares[_numPieces - 1]; // move last element in array to the place of the removed element
		_map[occupiedSquares[pieceIndex]] = pieceIndex; // update map to point to the moved element's new location in the array
		_numPieces--;
	}

	public void MovePiece(int startSquare, int targetSquare)
	{
		int pieceIndex = _map[startSquare]; // get the index of this element in the occupiedSquares array
		occupiedSquares[pieceIndex] = targetSquare;
		_map[targetSquare] = pieceIndex;
	}

	public int this[int index] => occupiedSquares[index];

}