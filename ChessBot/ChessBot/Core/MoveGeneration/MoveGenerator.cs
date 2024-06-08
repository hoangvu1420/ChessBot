using ChessBot.Core.Board;
using ChessBot.Core.Utilities;
using ConsoleChess.Core.Move_Generation;
using ConsoleChess.Core.Move_Generation.Bitboards;
using ConsoleChess.Core.Move_Generation.Magics;

namespace ChessBot.Core.MoveGeneration;

using static PrecomputedMoveData;

public class MoveGenerator
{
    private const int MaxMoves = 218;

    // ---- Instance variables ----
    private bool _isWhiteToMove;
    private int _friendlyColour; // color mask
    private int _opponentColour;
    private int _friendlyKingSquare;
    private int _friendlyIndex;
    private int _enemyIndex;

    private bool _isInCheck;
    private bool _isInDoubleCheck;

    private ulong _checkRayBitmask;

    private ulong _pinRays; 
    private ulong _notPinRays; 
    private ulong _opponentAttackMapNoPawns;
    private ulong _opponentSlidingAttackMap;

    public ulong OpponentAttackMap;
    public ulong OpponentPawnAttackMap;

    private bool _isGenerateQuietMoves;
    private Board.Board _board;
    private int _currMoveIndex;

    private ulong _enemyPieces;
    private ulong _friendlyPieces;
    private ulong _allPieces;
    private ulong _emptySquares;

    private ulong _emptyOrEnemySquares;


    private ulong _moveTypeMask;

    public Span<Move> GenerateMoves(Board.Board board, bool capturesOnly = false)
    {
        Span<Move> moves = new Move[MaxMoves];
        GenerateMoves(board, ref moves, capturesOnly);
        return moves;
    }


    public void GenerateMoves(Board.Board board, ref Span<Move> moves, bool capturesOnly = false)
    {
        _board = board;
        _isGenerateQuietMoves = !capturesOnly;

        Init();

        GenerateKingMoves(moves);

        if (!_isInDoubleCheck)
        {
            GenerateSlidingMoves(moves);
            GenerateKnightMoves(moves);
            GeneratePawnMoves(moves);
        }

        moves = moves.Slice(0, _currMoveIndex);
    }

    // Note, this will only return correct value after GenerateMoves() has been called in the current position
    public bool IsInCheck()
    {
        return _isInCheck;
    }

    private void Init()
    {
        // Reset state
        _currMoveIndex = 0;
        _isInCheck = false;
        _isInDoubleCheck = false;
        _checkRayBitmask = 0;
        _pinRays = 0;

        // Store some info for convenience
        _isWhiteToMove = _board.MoveColour == Piece.White;
        _friendlyColour = _board.MoveColour;
        _opponentColour = _board.OpponentColour;
        _friendlyKingSquare = _board.KingSquare[_board.MoveColourIndex];
        _friendlyIndex = _board.MoveColourIndex;
        _enemyIndex = 1 - _friendlyIndex;

        // Store some bitboards for convenience
        _enemyPieces = _board.ColourBitboards[_enemyIndex];
        _friendlyPieces = _board.ColourBitboards[_friendlyIndex];
        _allPieces = _board.AllPiecesBitboard;
        _emptySquares = ~_allPieces;
        _emptyOrEnemySquares = _emptySquares | _enemyPieces;
        _moveTypeMask = _isGenerateQuietMoves ? ulong.MaxValue : _enemyPieces;

        CalculateAttackData();
    }

    private void GenerateKingMoves(Span<Move> moves)
    {
        ulong legalMask = ~(OpponentAttackMap | _friendlyPieces); // king should not move into check or friendly pieces

        ulong kingMoves = BitBoardUtility.KingMoves[_friendlyKingSquare] & legalMask & _moveTypeMask;
        while (kingMoves != 0)
        {
            int targetSquare = BitBoardUtility.PopLsb(ref kingMoves);
            moves[_currMoveIndex++] = new Move(_friendlyKingSquare, targetSquare);
        }

        // Castling
        if (!_isInCheck && _isGenerateQuietMoves) // no castling when in check or generating captures
        {
            ulong castleBlockers = OpponentAttackMap | _board.AllPiecesBitboard;
            if (_board.CurrentGameState.HasKingsideCastleRight(_board.IsWhiteToMove))
            {
                ulong castleMask = _board.IsWhiteToMove ? Bits.WhiteKingSideMask : Bits.BlackKingSideMask;
                if ((castleMask & castleBlockers) == 0)
                {
                    int targetSquare = _board.IsWhiteToMove ? BoardUtility.G1 : BoardUtility.G8;
                    moves[_currMoveIndex++] = new Move(_friendlyKingSquare, targetSquare, Move.CastleFlag);
                }
            }

            if (_board.CurrentGameState.HasQueensideCastleRight(_board.IsWhiteToMove))
            {
                ulong castleMask = _board.IsWhiteToMove ? Bits.WhiteQueenSideMask2 : Bits.BlackQueenSideMask2;
                ulong castleBlockMask = _board.IsWhiteToMove ? Bits.WhiteQueenSideMask : Bits.BlackQueenSideMask;
                if ((castleMask & castleBlockers) == 0 && (castleBlockMask & _board.AllPiecesBitboard) == 0)
                {
                    int targetSquare = _board.IsWhiteToMove ? BoardUtility.C1 : BoardUtility.C8;
                    moves[_currMoveIndex++] = new Move(_friendlyKingSquare, targetSquare, Move.CastleFlag);
                }
            }
        }
    }

    private void GenerateSlidingMoves(Span<Move> moves)
    {
        ulong moveMask = _emptyOrEnemySquares & _checkRayBitmask & _moveTypeMask;

        ulong orthogonalSlidePieces = _board.FriendlyOrthogonalSlidePieces;
        ulong diagonalSlidePieces = _board.FriendlyDiagonalSlidePieces;

        // Pinned pieces cannot move if king is in check
        if (_isInCheck)
        {
            orthogonalSlidePieces &= ~_pinRays;
            diagonalSlidePieces &= ~_pinRays;
        }

        // Orthogonal
        while (orthogonalSlidePieces != 0)
        {
            int startSquare = BitBoardUtility.PopLsb(ref orthogonalSlidePieces);
            ulong moveSquares = Magic.GetRookAttacks(startSquare, _allPieces) & moveMask;

            // If piece is pinned, it can only move along the pin ray
            if (IsPinned(startSquare))
            {
                moveSquares &= AlignMask[startSquare, _friendlyKingSquare];
            }

            while (moveSquares != 0)
            {
                int targetSquare = BitBoardUtility.PopLsb(ref moveSquares);
                moves[_currMoveIndex++] = new Move(startSquare, targetSquare);
            }
        }

        // Diagonal
        while (diagonalSlidePieces != 0)
        {
            int startSquare = BitBoardUtility.PopLsb(ref diagonalSlidePieces);
            ulong moveSquares = Magic.GetBishopAttacks(startSquare, _allPieces) & moveMask;

            // If piece is pinned, it can only move along the pin ray
            if (IsPinned(startSquare))
            {
                moveSquares &= AlignMask[startSquare, _friendlyKingSquare];
            }

            while (moveSquares != 0)
            {
                int targetSquare = BitBoardUtility.PopLsb(ref moveSquares);
                moves[_currMoveIndex++] = new Move(startSquare, targetSquare);
            }
        }
    }

    private void GenerateKnightMoves(Span<Move> moves)
    {
        int friendlyKnightPiece = Piece.GetPieceValue(Piece.Knight, _board.MoveColour);
        // bitboard of all non-pinned knights
        ulong knights = _board.PieceBitboards[friendlyKnightPiece] & _notPinRays;
        ulong moveMask = _emptyOrEnemySquares & _checkRayBitmask & _moveTypeMask;

        while (knights != 0)
        {
            int knightSquare = BitBoardUtility.PopLsb(ref knights);
            ulong moveSquares = BitBoardUtility.KnightAttacks[knightSquare] & moveMask;

            while (moveSquares != 0)
            {
                int targetSquare = BitBoardUtility.PopLsb(ref moveSquares);
                moves[_currMoveIndex++] = new Move(knightSquare, targetSquare);
            }
        }
    }

    private void GeneratePawnMoves(Span<Move> moves)
    {
        int pushDir = _board.IsWhiteToMove ? 1 : -1;
        int pushOffset = pushDir * 8;

        int friendlyPawnPiece = Piece.GetPieceValue(Piece.Pawn, _board.MoveColour);
        ulong pawns = _board.PieceBitboards[friendlyPawnPiece];

        ulong promotionRankMask = _board.IsWhiteToMove ? BitBoardUtility.Rank8 : BitBoardUtility.Rank1;

        ulong singlePush = BitBoardUtility.Shift(pawns, pushOffset) & _emptySquares;

        ulong pushPromotions = singlePush & promotionRankMask & _checkRayBitmask;


        ulong captureEdgeFileMask = _board.IsWhiteToMove ? BitBoardUtility.NotAFile : BitBoardUtility.NotHFile; // 
        ulong captureEdgeFileMask2 = _board.IsWhiteToMove ? BitBoardUtility.NotHFile : BitBoardUtility.NotAFile;
        ulong captureLeft = BitBoardUtility.Shift(pawns & captureEdgeFileMask, pushDir * 7) & _enemyPieces; // captures to the right
        ulong captureRight = BitBoardUtility.Shift(pawns & captureEdgeFileMask2, pushDir * 9) & _enemyPieces; // captures to the left

        ulong singlePushNoPromotions = singlePush & ~promotionRankMask & _checkRayBitmask;

        ulong capturePromotionsLeft = captureLeft & promotionRankMask & _checkRayBitmask;
        ulong capturePromotionsRight = captureRight & promotionRankMask & _checkRayBitmask;

        captureLeft &= _checkRayBitmask & ~promotionRankMask;
        captureRight &= _checkRayBitmask & ~promotionRankMask;

        // Single / double push
        if (_isGenerateQuietMoves)
        {
            // Generate single pawn pushes
            while (singlePushNoPromotions != 0)
            {
                int targetSquare = BitBoardUtility.PopLsb(ref singlePushNoPromotions);
                int startSquare = targetSquare - pushOffset;
                if (!IsPinned(startSquare) || AlignMask[startSquare, _friendlyKingSquare] ==
                    AlignMask[targetSquare, _friendlyKingSquare])
                {
                    moves[_currMoveIndex++] = new Move(startSquare, targetSquare);
                }
            }

            // Generate double pawn pushes
            ulong doublePushTargetRankMask = _board.IsWhiteToMove ? BitBoardUtility.Rank4 : BitBoardUtility.Rank5;
            ulong doublePush = BitBoardUtility.Shift(singlePush, pushOffset) & _emptySquares &
                               doublePushTargetRankMask & _checkRayBitmask;

            while (doublePush != 0)
            {
                int targetSquare = BitBoardUtility.PopLsb(ref doublePush);
                int startSquare = targetSquare - pushOffset * 2;
                if (!IsPinned(startSquare) || AlignMask[startSquare, _friendlyKingSquare] ==
                    AlignMask[targetSquare, _friendlyKingSquare])
                {
                    moves[_currMoveIndex++] = new Move(startSquare, targetSquare, Move.PawnTwoUpFlag);
                }
            }
        }

        // Captures
        while (captureLeft != 0)
        {
            int targetSquare = BitBoardUtility.PopLsb(ref captureLeft);
            int startSquare = targetSquare - pushDir * 7;

            if (!IsPinned(startSquare) || AlignMask[startSquare, _friendlyKingSquare] ==
                AlignMask[targetSquare, _friendlyKingSquare])
            {
                moves[_currMoveIndex++] = new Move(startSquare, targetSquare);
            }
        }

        while (captureRight != 0)
        {
            int targetSquare = BitBoardUtility.PopLsb(ref captureRight);
            int startSquare = targetSquare - pushDir * 9;

            if (!IsPinned(startSquare) || AlignMask[startSquare, _friendlyKingSquare] ==
                AlignMask[targetSquare, _friendlyKingSquare])
            {
                moves[_currMoveIndex++] = new Move(startSquare, targetSquare);
            }
        }


        // Promotions
        while (pushPromotions != 0)
        {
            int targetSquare = BitBoardUtility.PopLsb(ref pushPromotions);
            int startSquare = targetSquare - pushOffset;
            if (!IsPinned(startSquare))
            {
                GeneratePromotions(startSquare, targetSquare, moves);
            }
        }

        // Captures with promotions
        while (capturePromotionsLeft != 0)
        {
            int targetSquare = BitBoardUtility.PopLsb(ref capturePromotionsLeft);
            int startSquare = targetSquare - pushDir * 7;

            if (!IsPinned(startSquare) || AlignMask[startSquare, _friendlyKingSquare] ==
                AlignMask[targetSquare, _friendlyKingSquare])
            {
                GeneratePromotions(startSquare, targetSquare, moves);
            }
        }

        while (capturePromotionsRight != 0)
        {
            int targetSquare = BitBoardUtility.PopLsb(ref capturePromotionsRight);
            int startSquare = targetSquare - pushDir * 9;

            if (!IsPinned(startSquare) || AlignMask[startSquare, _friendlyKingSquare] ==
                AlignMask[targetSquare, _friendlyKingSquare])
            {
                GeneratePromotions(startSquare, targetSquare, moves);
            }
        }

        // En passant
        if (_board.CurrentGameState.EnPassantFile <= 0) return;
        {
            int epFileIndex = _board.CurrentGameState.EnPassantFile - 1;
            int epRankIndex = _board.IsWhiteToMove ? 5 : 2;
            int targetSquare = epRankIndex * 8 + epFileIndex;
            int capturedPawnSquare = targetSquare - pushOffset;

            if (!BitBoardUtility.ContainsSquare(_checkRayBitmask, capturedPawnSquare)) return;
            ulong pawnsThatCanCaptureEp =
                pawns & BitBoardUtility.PawnAttacks(1ul << targetSquare, !_board.IsWhiteToMove);

            while (pawnsThatCanCaptureEp != 0)
            {
                int startSquare = BitBoardUtility.PopLsb(ref pawnsThatCanCaptureEp);
                if (IsPinned(startSquare) && AlignMask[startSquare, _friendlyKingSquare] !=
                    AlignMask[targetSquare, _friendlyKingSquare]) continue;
                if (!InCheckAfterEnPassant(startSquare, targetSquare, capturedPawnSquare))
                {
                    moves[_currMoveIndex++] = new Move(startSquare, targetSquare, Move.EnPassantCaptureFlag);
                }
            }
        }
    }

    private void GeneratePromotions(int startSquare, int targetSquare, Span<Move> moves)
    {
        moves[_currMoveIndex++] = new Move(startSquare, targetSquare, Move.PromoteToQueenFlag);
        // Don't generate non-queen promotions in q-search
        if (!_isGenerateQuietMoves) return;

        moves[_currMoveIndex++] = new Move(startSquare, targetSquare, Move.PromoteToKnightFlag);
        moves[_currMoveIndex++] = new Move(startSquare, targetSquare, Move.PromoteToRookFlag);
        moves[_currMoveIndex++] = new Move(startSquare, targetSquare, Move.PromoteToBishopFlag);
    }

    private bool IsPinned(int square)
    {
        return ((_pinRays >> square) & 1) != 0;
    }

    private void GenSlidingAttackMap()
    {
        _opponentSlidingAttackMap = 0;

        UpdateSlideAttack(_board.EnemyOrthogonalSlidePieces, true);
        UpdateSlideAttack(_board.EnemyDiagonalSlidePieces, false);

        void UpdateSlideAttack(ulong pieceBoard, bool ortho)
        {
            ulong blockers = _board.AllPiecesBitboard & ~(1ul << _friendlyKingSquare);

            while (pieceBoard != 0)
            {
                int startSquare = BitBoardUtility.PopLsb(ref pieceBoard);
                ulong moveBoard = Magic.GetSliderAttacks(startSquare, blockers, ortho);

                _opponentSlidingAttackMap |= moveBoard; // the |= operator is used to combine the two values
            }
        }
    }

    private void CalculateAttackData()
    {
        GenSlidingAttackMap();
        int startDirIndex = 0;
        int endDirIndex = 8;

        if (_board.Queens[_enemyIndex].Count == 0)
        {
            startDirIndex = _board.Rooks[_enemyIndex].Count > 0 ? 0 : 4;
            endDirIndex = _board.Bishops[_enemyIndex].Count > 0 ? 8 : 4;
        }

        for (int dir = startDirIndex; dir < endDirIndex; dir++)
        {
            bool isDiagonal = dir > 3;
            ulong slider = isDiagonal ? _board.EnemyDiagonalSlidePieces : _board.EnemyOrthogonalSlidePieces;
            if ((DirRayMask[dir, _friendlyKingSquare] & slider) == 0)
            {
                continue; // pass if no enemy sliding piece in this direction
            }

            int n = NumSquaresToEdge[_friendlyKingSquare][dir];
            int directionOffset = DirectionOffsets[dir];
            bool isFriendlyPieceAlongRay = false;
            ulong rayMask = 0;

            for (int i = 0; i < n; i++)
            {
                int squareIndex = _friendlyKingSquare + directionOffset * (i + 1);
                rayMask |= 1ul << squareIndex;
                int piece = _board.Squares[squareIndex];

                if (piece == Piece.None) continue;
                // This square contains a piece

                if (Piece.IsColour(piece, _friendlyColour)) // The piece is friendly
                {
                    if (!isFriendlyPieceAlongRay)
                    {
                        isFriendlyPieceAlongRay = true;
                    }
                    else
                    {
                        break;
                    }
                }
                // The piece is an enemy piece
                else
                {
                    int pieceType = Piece.PieceType(piece);

                    if (isDiagonal && Piece.IsDiagonalSlider(pieceType) ||
                        !isDiagonal && Piece.IsOrthogonalSlider(pieceType))
                    {
                        if (isFriendlyPieceAlongRay)
                        {
                            _pinRays |= rayMask;
                        }
                        else
                        {
                            _checkRayBitmask |= rayMask;
                            _isInDoubleCheck = _isInCheck; // if already in check, then this is double check
                            _isInCheck = true;
                        }
                    }

                    break;
                }
            }

            if (_isInDoubleCheck)
            {
                break;
            }
        }

        _notPinRays = ~_pinRays;

        ulong opponentKnightAttacks = 0;
        ulong opponentKnights = _board.PieceBitboards[Piece.GetPieceValue(Piece.Knight, _board.OpponentColour)];
        ulong friendlyKingBoard = _board.PieceBitboards[Piece.GetPieceValue(Piece.King, _board.MoveColour)];

        while (opponentKnights != 0)
        {
            int knightSquare = BitBoardUtility.PopLsb(ref opponentKnights);
            ulong knightAttacks = BitBoardUtility.KnightAttacks[knightSquare];
            opponentKnightAttacks |= knightAttacks;
            // calculates the squares that the knight can attack

            if ((knightAttacks & friendlyKingBoard) != 0)
            {
                // square is occupied by the friendly king
                _isInDoubleCheck = _isInCheck;
                _isInCheck = true;
                _checkRayBitmask |= 1ul << knightSquare;
            }
        }

        // Pawn attacks
        PieceList opponentPawns = _board.Pawns[_enemyIndex];
        OpponentPawnAttackMap = 0;

        ulong opponentPawnsBoard = _board.PieceBitboards[Piece.GetPieceValue(Piece.Pawn, _board.OpponentColour)];
        OpponentPawnAttackMap = BitBoardUtility.PawnAttacks(opponentPawnsBoard, !_isWhiteToMove);
        if (BitBoardUtility.ContainsSquare(OpponentPawnAttackMap, _friendlyKingSquare))
        {
            _isInDoubleCheck = _isInCheck; // if already in check, then this is double check
            _isInCheck = true;
            ulong possiblePawnAttackOrigins = _board.IsWhiteToMove
                ? BitBoardUtility.WhitePawnAttacks[_friendlyKingSquare]
                : BitBoardUtility.BlackPawnAttacks[_friendlyKingSquare];
            ulong pawnCheckMap = opponentPawnsBoard & possiblePawnAttackOrigins;
            _checkRayBitmask |= pawnCheckMap;
        }

        int enemyKingSquare = _board.KingSquare[_enemyIndex];

        _opponentAttackMapNoPawns = _opponentSlidingAttackMap | opponentKnightAttacks |
                                    BitBoardUtility.KingMoves[enemyKingSquare];
        OpponentAttackMap = _opponentAttackMapNoPawns | OpponentPawnAttackMap;

        if (!_isInCheck)
        {
            _checkRayBitmask = ulong.MaxValue;
        }
    }

    private bool InCheckAfterEnPassant(int startSquare, int targetSquare, int epCaptureSquare)
    {
        ulong enemyOrtho = _board.EnemyOrthogonalSlidePieces;

        if (enemyOrtho == 0) return false;
        ulong maskedBlockers = _allPieces ^ (1ul << epCaptureSquare | 1ul << startSquare | 1ul << targetSquare);
        ulong rookAttacks = Magic.GetRookAttacks(_friendlyKingSquare, maskedBlockers);
        return (rookAttacks & enemyOrtho) != 0;
    }
}