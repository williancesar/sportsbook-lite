namespace SportsbookLite.Contracts.Wallet;

[GenerateSerializer]
public enum TransactionType
{
    Deposit,
    Withdrawal,
    BetPlacement,
    BetWin,
    BetLoss,
    BetRefund,
    Reservation,
    ReservationCommit,
    ReservationRelease,
    BetPayout,
    PayoutReversal
}