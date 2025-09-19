using SportsbookLite.Contracts.Wallet;

namespace SportsbookLite.GrainInterfaces;

public interface IUserWalletGrain : IGrainWithStringKey
{
    ValueTask<Money> GetBalanceAsync();
    ValueTask<Money> GetAvailableBalanceAsync();
    ValueTask<TransactionResult> DepositAsync(Money amount, string transactionId);
    ValueTask<TransactionResult> WithdrawAsync(Money amount, string transactionId);
    ValueTask<TransactionResult> ReserveAsync(Money amount, string betId);
    ValueTask<TransactionResult> CommitReservationAsync(string betId);
    ValueTask<TransactionResult> ReleaseReservationAsync(string betId);
    ValueTask<IReadOnlyList<WalletTransaction>> GetTransactionHistoryAsync(int limit = 50);
    ValueTask<IReadOnlyList<TransactionEntry>> GetLedgerEntriesAsync(int limit = 50);
    ValueTask<TransactionResult> ProcessPayoutAsync(Money amount, string betId, string sagaId);
    ValueTask<TransactionResult> ReversePayoutAsync(Money amount, string betId, string sagaId, string reason);
}