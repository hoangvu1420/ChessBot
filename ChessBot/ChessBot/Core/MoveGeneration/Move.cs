/*
Dùng 16 bit để biểu diễn

Định dạng: ffffttttttssssss)
Bits 0-5: chỉ số ô bắt đầu
Bits 6-11: chỉ số ô mục tiêu
Bits 12-15: chỉ số flag biểu diễn các nước đi đặc biệt
*/

using ChessBot.Core.Board;

namespace ChessBot.Core.MoveGeneration;

public readonly struct Move
{
	// Gtri 16bit
	private readonly ushort _moveValue;

	// Flags
	public const int NoFlag = 0b0000;
	public const int EnPassantCaptureFlag = 0b0001;
	public const int CastleFlag = 0b0010;
	public const int PawnTwoUpFlag = 0b0011;	

	public const int PromoteToQueenFlag = 0b0100;
	public const int PromoteToKnightFlag = 0b0101;
	public const int PromoteToRookFlag = 0b0110;
	public const int PromoteToBishopFlag = 0b0111;

	// Masks
	private const ushort StartSquareMask = 0b0000000000111111;
	private const ushort TargetSquareMask = 0b0000111111000000;
	private const ushort FlagMask = 0b1111000000000000;

	public Move(ushort moveValue)
	{
		_moveValue = moveValue;
	}

	public Move(int startSquare, int targetSquare)
	{
		_moveValue = (ushort)(startSquare | targetSquare << 6);
	}

	public Move(int startSquare, int targetSquare, int flag)
	{
		_moveValue = (ushort)(startSquare | targetSquare << 6 | flag << 12);
	}

	public ushort Value => _moveValue;
	public bool IsNull => _moveValue == 0;

	public int StartSquare => _moveValue & StartSquareMask;
	public int TargetSquare => (_moveValue & TargetSquareMask) >> 6;
	public bool IsPromotion => MoveFlag >= PromoteToQueenFlag;
	public int MoveFlag => _moveValue >> 12;

	public int PromotionPieceType
	{
		get
		{
			switch (MoveFlag)
			{
				case PromoteToRookFlag:
					return Piece.Rook;
				case PromoteToKnightFlag:
					return Piece.Knight;
				case PromoteToBishopFlag:
					return Piece.Bishop;
				case PromoteToQueenFlag:
					return Piece.Queen;
				default:
					return Piece.None;
			}
		}
	}

	public static Move NullMove => new(0);
	public static bool SameMove(Move a, Move b) => a._moveValue == b._moveValue;


}