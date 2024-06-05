using ChessBot.Core.Utilities;
using ChessBot.Core.MoveGeneration;
using ChessBot.Core.MoveGeneration.Bitboards;
using ChessBot.Core.MoveGeneration.Magics;

namespace ChessBot.Core.Board;

public sealed class Board
{
	public const int WhiteIndex = 0;
	public const int BlackIndex = 1;

	// Stores piece code for each square on the board
	public readonly int[] Squares;
	
	// Square index of white and black king
	public int[] KingSquare;
	
	// Bitboard for each piece type and colour (white pawns, white knights, ... black pawns, etc.)
	public ulong[] PieceBitboards;
	
	// Bitboards for all pieces of either colour (all white pieces, all black pieces)
	public ulong[] ColourBitboards;
	public ulong AllPiecesBitboard;
	public ulong FriendlyOrthogonalSlidePieces;
	public ulong FriendlyDiagonalSlidePieces;
	public ulong EnemyOrthogonalSlidePieces; // position of enemy rooks and queens
	public ulong EnemyDiagonalSlidePieces; // position of enemy bishops and queens
	// Piece count excluding pawns and kings
	public int TotalPieceCountWithoutPawnsAndKings;
	
	// # Piece lists
	public PieceList[] Rooks;
	public PieceList[] Bishops;
	public PieceList[] Queens;
	public PieceList[] Knights;
	public PieceList[] Pawns;

	// # Side to move info
	public bool IsWhiteToMove;
	public int MoveColour => IsWhiteToMove ? Piece.White : Piece.Black;
	public int OpponentColour => IsWhiteToMove ? Piece.Black : Piece.White;
	public int MoveColourIndex => IsWhiteToMove ? WhiteIndex : BlackIndex;
	public int OpponentColourIndex => IsWhiteToMove ? BlackIndex : WhiteIndex;
	
	// List of (hashed) positions since last pawn move or capture (for detecting repetitions)
	public Stack<ulong> RepetitionPositionHistory;

	// Total plies (half-moves) played in game
	public int PlyCount;
	public int FiftyMoveCounter => CurrentGameState.FiftyMoveCounter;
	public GameState CurrentGameState;
	public ulong ZobristKey => CurrentGameState.ZobristKey;
	public string CurrentFen => FenUtility.CurrentFen(this);
	public string GameStartFen => _startPositionInfo.Fen;
	public List<Move> AllGameMoves;


	// # Private stuff
	private PieceList[] _allPieceLists;
	private Stack<GameState> _gameStateHistory;
	private FenUtility.PositionInfo _startPositionInfo;
	private bool _cachedInCheckValue;
	private bool _hasCachedInCheckValue;

	public Board()
	{
		Squares = new int[64];
	}

	public void MakeMove(Move move, bool isInSearch = false)
	{
		// Get info about move
		int startSquare = move.StartSquare;
		int targetSquare = move.TargetSquare;
		int moveFlag = move.MoveFlag;
		bool isPromotion = move.IsPromotion;
		bool isEnPassant = moveFlag is Move.EnPassantCaptureFlag;

		int movedPiece = Squares[startSquare];
		int movedPieceType = Piece.PieceType(movedPiece);
		int capturedPiece = isEnPassant ? Piece.GetPieceValue(Piece.Pawn, OpponentColour) : Squares[targetSquare];
		int capturedPieceType = Piece.PieceType(capturedPiece);

		int prevCastleState = CurrentGameState.CastlingRights;
		int prevEnPassantFile = CurrentGameState.EnPassantFile;
		ulong newZobristKey = CurrentGameState.ZobristKey;
		int newCastlingRights = CurrentGameState.CastlingRights;
		int newEnPassantFile = 0;

		// Update bitboard of moved piece (pawn promotion is a special case and is corrected later)
		MovePiece(movedPiece, startSquare, targetSquare);

		// Handle captures
		if (capturedPieceType != Piece.None)
		{
			int captureSquare = targetSquare;

			if (isEnPassant)
			{
				captureSquare = targetSquare + (IsWhiteToMove ? -8 : 8);
				Squares[captureSquare] = Piece.None;
			}
			if (capturedPieceType != Piece.Pawn)
			{
				TotalPieceCountWithoutPawnsAndKings--;
			}

			// Remove captured piece from bitboards/piece list
			_allPieceLists[capturedPiece].RemovePieceAtSquare(captureSquare);
			BitBoardUtility.ToggleSquare(ref PieceBitboards[capturedPiece], captureSquare);
			BitBoardUtility.ToggleSquare(ref ColourBitboards[OpponentColourIndex], captureSquare);
			newZobristKey ^= Zobrist.piecesMatrix[capturedPiece, captureSquare];
		}

		// Handle king
		if (movedPieceType == Piece.King)
		{
			KingSquare[MoveColourIndex] = targetSquare;
			newCastlingRights &= IsWhiteToMove ? 0b1100 : 0b0011;

			// Handle castling
			if (moveFlag == Move.CastleFlag)
			{
				int rookPiece = Piece.GetPieceValue(Piece.Rook, MoveColour);
				bool kingSide = targetSquare == BoardUtility.G1 || targetSquare == BoardUtility.G8;
				int castlingRookFromIndex = kingSide ? targetSquare + 1 : targetSquare - 2;
				int castlingRookToIndex = kingSide ? targetSquare - 1 : targetSquare + 1;

				// Update rook position
				BitBoardUtility.ToggleSquares(ref PieceBitboards[rookPiece], castlingRookFromIndex, castlingRookToIndex);
				BitBoardUtility.ToggleSquares(ref ColourBitboards[MoveColourIndex], castlingRookFromIndex, castlingRookToIndex);
				_allPieceLists[rookPiece].MovePiece(castlingRookFromIndex, castlingRookToIndex);
				Squares[castlingRookFromIndex] = Piece.None;
				Squares[castlingRookToIndex] = Piece.Rook | MoveColour;

				newZobristKey ^= Zobrist.piecesMatrix[rookPiece, castlingRookFromIndex];
				newZobristKey ^= Zobrist.piecesMatrix[rookPiece, castlingRookToIndex];
			}
		}

		// Handle promotion
		if (isPromotion)
		{
			TotalPieceCountWithoutPawnsAndKings++;
			int promotionPieceType = moveFlag switch
			{
				Move.PromoteToQueenFlag => Piece.Queen,
				Move.PromoteToRookFlag => Piece.Rook,
				Move.PromoteToKnightFlag => Piece.Knight,
				Move.PromoteToBishopFlag => Piece.Bishop,
				_ => 0
			};

			int promotionPiece = Piece.GetPieceValue(promotionPieceType, MoveColour);

			// Remove pawn from promotion square and add promoted piece instead
			BitBoardUtility.ToggleSquare(ref PieceBitboards[movedPiece], targetSquare);
			BitBoardUtility.ToggleSquare(ref PieceBitboards[promotionPiece], targetSquare);
			_allPieceLists[movedPiece].RemovePieceAtSquare(targetSquare);
			_allPieceLists[promotionPiece].AddPieceAtSquare(targetSquare);
			Squares[targetSquare] = promotionPiece;
		}

		// Pawn has moved two forwards, mark file with en-passant flag
		if (moveFlag == Move.PawnTwoUpFlag)
		{
			int file = BoardUtility.FileIndex(startSquare) + 1;
			newEnPassantFile = file;
			newZobristKey ^= Zobrist.enPassantFile[file];
		}

		// Update castling rights
		if (prevCastleState != 0)
		{
			// Any piece moving to/from rook square removes castling right for that side
			if (targetSquare == BoardUtility.H1 || startSquare == BoardUtility.H1)
			{
				newCastlingRights &= GameState.ClearWhiteKingsideMask;
			}
			else if (targetSquare == BoardUtility.A1 || startSquare == BoardUtility.A1)
			{
				newCastlingRights &= GameState.ClearWhiteQueensideMask;
			}
			if (targetSquare == BoardUtility.H8 || startSquare == BoardUtility.H8)
			{
				newCastlingRights &= GameState.ClearBlackKingsideMask;
			}
			else if (targetSquare == BoardUtility.A8 || startSquare == BoardUtility.A8)
			{
				newCastlingRights &= GameState.ClearBlackQueensideMask;
			}
		}

		// Update zobrist key with new piece position and side to move
		newZobristKey ^= Zobrist.sideToMove;
		newZobristKey ^= Zobrist.piecesMatrix[movedPiece, startSquare];
		newZobristKey ^= Zobrist.piecesMatrix[Squares[targetSquare], targetSquare];
		newZobristKey ^= Zobrist.enPassantFile[prevEnPassantFile];

		if (newCastlingRights != prevCastleState)
		{
			newZobristKey ^= Zobrist.castlingRights[prevCastleState]; // remove old castling rights state
			newZobristKey ^= Zobrist.castlingRights[newCastlingRights]; // add new castling rights state
		}

		// Change side to move
		IsWhiteToMove = !IsWhiteToMove;

		PlyCount++;
		int newFiftyMoveCounter = CurrentGameState.FiftyMoveCounter + 1;

		// Update extra bitboards
		AllPiecesBitboard = ColourBitboards[WhiteIndex] | ColourBitboards[BlackIndex];
		UpdateSliderBitboards();

		// Pawn moves and captures reset the fifty move counter and clear 3-fold repetition history
		if (movedPieceType == Piece.Pawn || capturedPieceType != Piece.None)
		{
			if (!isInSearch)
			{
				RepetitionPositionHistory.Clear();
			}
			newFiftyMoveCounter = 0;
		}

		GameState newState = new(capturedPieceType, newEnPassantFile, newCastlingRights, newFiftyMoveCounter, newZobristKey);
		_gameStateHistory.Push(newState);
		CurrentGameState = newState;
		_hasCachedInCheckValue = false;

		if (!isInSearch)
		{
			RepetitionPositionHistory.Push(newState.ZobristKey);
			AllGameMoves.Add(move);
		}
	}

	// Undo a move previously made on the board
	public void UnmakeMove(Move move, bool inSearch = false)
	{
		// Swap colour to move
		IsWhiteToMove = !IsWhiteToMove;

		bool undoingWhiteMove = IsWhiteToMove;

		// Get move info
		int movedSource = move.StartSquare;
		int movedTarget = move.TargetSquare;
		int moveFlag = move.MoveFlag;

		bool undoingEnPassant = moveFlag == Move.EnPassantCaptureFlag;
		bool undoingPromotion = move.IsPromotion;
		bool undoingCapture = CurrentGameState.CapturedPieceType != Piece.None;

		int movedPiece = undoingPromotion ? Piece.GetPieceValue(Piece.Pawn, MoveColour) : Squares[movedTarget];
		int movedPieceType = Piece.PieceType(movedPiece);
		int capturedPieceType = CurrentGameState.CapturedPieceType;

		// If undoing promotion, then remove piece from promotion square and replace with pawn
		if (undoingPromotion)
		{
			int promotedPiece = Squares[movedTarget];
			int pawnPiece = Piece.GetPieceValue(Piece.Pawn, MoveColour);
			TotalPieceCountWithoutPawnsAndKings--;

			_allPieceLists[promotedPiece].RemovePieceAtSquare(movedTarget);
			_allPieceLists[movedPiece].AddPieceAtSquare(movedTarget);
			BitBoardUtility.ToggleSquare(ref PieceBitboards[promotedPiece], movedTarget);
			BitBoardUtility.ToggleSquare(ref PieceBitboards[pawnPiece], movedTarget);
		}

		MovePiece(movedPiece, movedTarget, movedSource);

		// Undo capture
		if (undoingCapture)
		{
			int captureSquare = movedTarget;
			int capturedPiece = Piece.GetPieceValue(capturedPieceType, OpponentColour);

			if (undoingEnPassant)
			{
				captureSquare = movedTarget + (undoingWhiteMove ? -8 : 8);
			}
			if (capturedPieceType != Piece.Pawn)
			{
				TotalPieceCountWithoutPawnsAndKings++;
			}

			// Add back captured piece
			BitBoardUtility.ToggleSquare(ref PieceBitboards[capturedPiece], captureSquare);
			BitBoardUtility.ToggleSquare(ref ColourBitboards[OpponentColourIndex], captureSquare);
			_allPieceLists[capturedPiece].AddPieceAtSquare(captureSquare);
			Squares[captureSquare] = capturedPiece;
		}


		// Update king
		if (movedPieceType is Piece.King)
		{
			KingSquare[MoveColourIndex] = movedSource;

			// Undo castling
			if (moveFlag is Move.CastleFlag)
			{
				int rookPiece = Piece.GetPieceValue(Piece.Rook, MoveColour);
				bool kingSide = movedTarget == BoardUtility.G1 || movedTarget == BoardUtility.G8;
				int rookSquareBeforeCastling = kingSide ? movedTarget + 1 : movedTarget - 2;
				int rookSquareAfterCastling = kingSide ? movedTarget - 1 : movedTarget + 1;

				// Undo castling by returning rook to original square
				BitBoardUtility.ToggleSquares(ref PieceBitboards[rookPiece], rookSquareAfterCastling, rookSquareBeforeCastling);
				BitBoardUtility.ToggleSquares(ref ColourBitboards[MoveColourIndex], rookSquareAfterCastling, rookSquareBeforeCastling);
				Squares[rookSquareAfterCastling] = Piece.None;
				Squares[rookSquareBeforeCastling] = rookPiece;
				_allPieceLists[rookPiece].MovePiece(rookSquareAfterCastling, rookSquareBeforeCastling);
			}
		}

		AllPiecesBitboard = ColourBitboards[WhiteIndex] | ColourBitboards[BlackIndex];
		UpdateSliderBitboards();

		if (!inSearch && RepetitionPositionHistory.Count > 0)
		{
			RepetitionPositionHistory.Pop();
		}
		if (!inSearch)
		{
			AllGameMoves.RemoveAt(AllGameMoves.Count - 1);
		}

		// Go back to previous state
		_gameStateHistory.Pop();
		CurrentGameState = _gameStateHistory.Peek();
		PlyCount--;
		_hasCachedInCheckValue = false;
	}

	// Switch side to play without making a move (NOTE: must not be in check when called)
	public void MakeNullMove()
	{
		IsWhiteToMove = !IsWhiteToMove;

		PlyCount++;

		ulong newZobristKey = CurrentGameState.ZobristKey;
		newZobristKey ^= Zobrist.sideToMove;
		newZobristKey ^= Zobrist.enPassantFile[CurrentGameState.EnPassantFile];

		GameState newState = new(Piece.None, 0, CurrentGameState.CastlingRights, CurrentGameState.FiftyMoveCounter + 1, newZobristKey);
		CurrentGameState = newState;
		_gameStateHistory.Push(CurrentGameState);
		UpdateSliderBitboards();
		_hasCachedInCheckValue = true;
		_cachedInCheckValue = false;
	}

	public void UnmakeNullMove()
	{
		IsWhiteToMove = !IsWhiteToMove;
		PlyCount--;
		_gameStateHistory.Pop();
		CurrentGameState = _gameStateHistory.Peek();
		UpdateSliderBitboards();
		_hasCachedInCheckValue = true;
		_cachedInCheckValue = false;
	}

	public bool IsInCheck()
	{
		if (_hasCachedInCheckValue)
		{
			return _cachedInCheckValue;
		}
		_cachedInCheckValue = CalculateInCheckState();
		_hasCachedInCheckValue = true;

		return _cachedInCheckValue;
	}

	// Calculate in check value
	// Call IsInCheck instead for automatic caching of value
	public bool CalculateInCheckState()
	{
		int kingSquare = KingSquare[MoveColourIndex];
		ulong blockers = AllPiecesBitboard;

		if (EnemyOrthogonalSlidePieces != 0)
		{
			ulong rookAttacks = Magic.GetRookAttacks(kingSquare, blockers);
			if ((rookAttacks & EnemyOrthogonalSlidePieces) != 0)
			{
				return true;
			}
		}
		if (EnemyDiagonalSlidePieces != 0)
		{
			ulong bishopAttacks = Magic.GetBishopAttacks(kingSquare, blockers);
			if ((bishopAttacks & EnemyDiagonalSlidePieces) != 0)
			{
				return true;
			}
		}

		ulong enemyKnights = PieceBitboards[Piece.GetPieceValue(Piece.Knight, OpponentColour)];
		if ((BitBoardUtility.KnightAttacks[kingSquare] & enemyKnights) != 0)
		{
			return true;
		}

		ulong enemyPawns = PieceBitboards[Piece.GetPieceValue(Piece.Pawn, OpponentColour)];
		ulong pawnAttackMask = IsWhiteToMove ? BitBoardUtility.WhitePawnAttacks[kingSquare] : BitBoardUtility.BlackPawnAttacks[kingSquare];
		
		return (pawnAttackMask & enemyPawns) != 0;
	}

	public void LoadPosition(string fen)
	{
		FenUtility.PositionInfo posInfo = FenUtility.PositionFromFen(fen);
		LoadPosition(posInfo);
	}

	private void LoadPosition(FenUtility.PositionInfo posInfo)
	{
		_startPositionInfo = posInfo;
		Initialize();

		// Load pieces into board array and piece lists
		for (int squareIndex = 0; squareIndex < 64; squareIndex++)
		{
			int piece = posInfo.Squares[squareIndex];
			int pieceType = Piece.PieceType(piece);
			int colourIndex = Piece.IsWhite(piece) ? WhiteIndex : BlackIndex;
			Squares[squareIndex] = piece;

			if (piece == Piece.None) continue;
			
			BitBoardUtility.SetSquare(ref PieceBitboards[piece], squareIndex);
			BitBoardUtility.SetSquare(ref ColourBitboards[colourIndex], squareIndex);

			if (pieceType == Piece.King)
			{
				KingSquare[colourIndex] = squareIndex;
			}
			else
			{
				_allPieceLists[piece].AddPieceAtSquare(squareIndex);
			}
			TotalPieceCountWithoutPawnsAndKings += pieceType is Piece.Pawn or Piece.King ? 0 : 1;
		}

		// Side to move
		IsWhiteToMove = posInfo.WhiteToMove;

		// Set extra bitboards
		AllPiecesBitboard = ColourBitboards[WhiteIndex] | ColourBitboards[BlackIndex];
		UpdateSliderBitboards();

		// Create gamestate
		int whiteCastle = (posInfo.WhiteCastleKingSide ? 1 << 0 : 0) | (posInfo.WhiteCastleQueenSide ? 1 << 1 : 0);
		int blackCastle = (posInfo.BlackCastleKingSide ? 1 << 2 : 0) | (posInfo.BlackCastleQueenSide ? 1 << 3 : 0);
		int castlingRights = whiteCastle | blackCastle;

		PlyCount = (posInfo.MoveCount - 1) * 2 + (IsWhiteToMove ? 0 : 1);

		// Set game state (note: calculating zobrist key relies on current game state)
		CurrentGameState = new GameState(Piece.None, posInfo.EpFile, castlingRights, posInfo.FiftyMovePlyCount, 0);
		ulong zobristKey = Zobrist.CalculateZobristKey(this);
		CurrentGameState = new GameState(Piece.None, posInfo.EpFile, castlingRights, posInfo.FiftyMovePlyCount, zobristKey);

		RepetitionPositionHistory.Push(zobristKey);

		_gameStateHistory.Push(CurrentGameState);
	}

	// public override string ToString()
	// {
	// 	return BoardHelper.CreateDiagram(this, IsWhiteToMove);
	// }

	public static Board CreateBoard(string fen = FenUtility.StartPositionFen)
	{
		Board board = new();
		board.LoadPosition(fen);
		return board;
	}

	public static Board CreateBoard(Board source)
	{
		Board board = new();
		board.LoadPosition(source._startPositionInfo);

		for (int i = 0; i < source.AllGameMoves.Count; i++)
		{
			board.MakeMove(source.AllGameMoves[i]);
		}
		return board;
	}

	private void MovePiece(int piece, int startSquare, int targetSquare)
	{
		BitBoardUtility.ToggleSquares(ref PieceBitboards[piece], startSquare, targetSquare);
		BitBoardUtility.ToggleSquares(ref ColourBitboards[MoveColourIndex], startSquare, targetSquare);

		_allPieceLists[piece].MovePiece(startSquare, targetSquare);
		Squares[startSquare] = Piece.None;
		Squares[targetSquare] = piece;
	}

	private void UpdateSliderBitboards()
	{
		int friendlyRook = Piece.GetPieceValue(Piece.Rook, MoveColour);
		int friendlyQueen = Piece.GetPieceValue(Piece.Queen, MoveColour);
		int friendlyBishop = Piece.GetPieceValue(Piece.Bishop, MoveColour);
		FriendlyOrthogonalSlidePieces = PieceBitboards[friendlyRook] | PieceBitboards[friendlyQueen];
		FriendlyDiagonalSlidePieces = PieceBitboards[friendlyBishop] | PieceBitboards[friendlyQueen];

		int enemyRook = Piece.GetPieceValue(Piece.Rook, OpponentColour);
		int enemyQueen = Piece.GetPieceValue(Piece.Queen, OpponentColour);
		int enemyBishop = Piece.GetPieceValue(Piece.Bishop, OpponentColour);
		EnemyOrthogonalSlidePieces = PieceBitboards[enemyRook] | PieceBitboards[enemyQueen];
		EnemyDiagonalSlidePieces = PieceBitboards[enemyBishop] | PieceBitboards[enemyQueen];
	}
	
	public bool IsThreeFoldRepetition()
	{
		if (RepetitionPositionHistory.Count < 6)
		{
			return false;
		}

		ulong currentZobristKey = CurrentGameState.ZobristKey;
		int repetitions = 0;
		foreach (ulong key in RepetitionPositionHistory)
		{
			if (key == currentZobristKey)
			{
				repetitions++;
			}
			if (repetitions >= 2)
			{
				return true;
			}
		}
		return false;
	}
	
	public bool IsFiftyMoveDraw()
	{
		return CurrentGameState.FiftyMoveCounter >= 100;
	}
	
	public bool IsInsufficientMaterialDraw()
	{
		// if there are no pieces on the board, then the position is a draw by insufficient material
		if (TotalPieceCountWithoutPawnsAndKings == 0)
		{
			return true;
		}

		// if there is only one piece on the board, then the position is a draw by insufficient material
		if (TotalPieceCountWithoutPawnsAndKings == 1)
		{
			return true;
		}

		// if there are only two knights or two bishops on the board, then the position is a draw by insufficient material
		if (TotalPieceCountWithoutPawnsAndKings == 2)
		{
			if (Knights[WhiteIndex].Count == 2 || Knights[BlackIndex].Count == 2)
			{
				return true;
			}

			if (Bishops[WhiteIndex].Count == 2 || Bishops[BlackIndex].Count == 2)
			{
				return true;
			}
		}

		return false;
	}

	private void Initialize()
	{
		AllGameMoves = [];
		KingSquare = new int[2];
		Array.Clear(Squares);

		RepetitionPositionHistory = new Stack<ulong>(capacity: 64);
		_gameStateHistory = new Stack<GameState>(capacity: 64);

		CurrentGameState = new GameState();
		PlyCount = 0;

		Knights = [new PieceList(10), new PieceList(10)];
		Pawns = [new PieceList(8), new PieceList(8)];
		Rooks = [new PieceList(10), new PieceList(10)];
		Bishops = [new PieceList(10), new PieceList(10)];
		Queens = [new PieceList(9), new PieceList(9)];

		_allPieceLists = new PieceList[Piece.MaxPieceIndex + 1];
		_allPieceLists[Piece.WhitePawn] = Pawns[WhiteIndex];
		_allPieceLists[Piece.WhiteKnight] = Knights[WhiteIndex];
		_allPieceLists[Piece.WhiteBishop] = Bishops[WhiteIndex];
		_allPieceLists[Piece.WhiteRook] = Rooks[WhiteIndex];
		_allPieceLists[Piece.WhiteQueen] = Queens[WhiteIndex];
		_allPieceLists[Piece.WhiteKing] = new PieceList(1);

		_allPieceLists[Piece.BlackPawn] = Pawns[BlackIndex];
		_allPieceLists[Piece.BlackKnight] = Knights[BlackIndex];
		_allPieceLists[Piece.BlackBishop] = Bishops[BlackIndex];
		_allPieceLists[Piece.BlackRook] = Rooks[BlackIndex];
		_allPieceLists[Piece.BlackQueen] = Queens[BlackIndex];
		_allPieceLists[Piece.BlackKing] = new PieceList(1);

		TotalPieceCountWithoutPawnsAndKings = 0;

		// Initialize bitboards
		PieceBitboards = new ulong[Piece.MaxPieceIndex + 1];
		ColourBitboards = new ulong[2];
		AllPiecesBitboard = 0;
	}

}