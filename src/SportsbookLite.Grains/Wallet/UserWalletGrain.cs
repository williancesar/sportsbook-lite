using SportsbookLite.Contracts.Events;
using SportsbookLite.Contracts.Wallet;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Grains.Wallet;

public sealed class UserWalletGrain : Grain, IUserWalletGrain
{
    private WalletState _state = new();

    public UserWalletGrain()
    {
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.UserId == string.Empty)
        {
            _state.UserId = this.GetPrimaryKeyString();
        }
        
        await base.OnActivateAsync(cancellationToken);
    }

    public ValueTask<Money> GetBalanceAsync()
    {
        return ValueTask.FromResult(_state.GetBalance());
    }

    public ValueTask<Money> GetAvailableBalanceAsync()
    {
        return ValueTask.FromResult(_state.GetAvailableBalance());
    }

    public async ValueTask<TransactionResult> DepositAsync(Money amount, string transactionId)
    {
        if (amount.Amount <= 0)
            return TransactionResult.Failure("Deposit amount must be positive");

        if (_state.ProcessedTransactionIds.Contains(transactionId))
        {
            var existingTransaction = _state.Transactions.FirstOrDefault(t => t.ReferenceId == transactionId);
            if (existingTransaction.Status == TransactionStatus.Completed)
                return TransactionResult.Success(existingTransaction, _state.GetBalance());
        }

        var transaction = WalletTransaction.Create(
            _state.UserId,
            TransactionType.Deposit,
            amount,
            $"Deposit of {amount.Amount} {amount.Currency}",
            transactionId);

        try
        {
            var newBalance = _state.Balance + amount.Amount;
            var completedTransaction = transaction.WithStatus(TransactionStatus.Completed);

            _state.AddTransaction(completedTransaction);
            _state.UpdateBalance(newBalance);

            var creditEntry = TransactionEntry.CreateCredit(
                transactionId,
                amount,
                $"Deposit credit");

            var debitEntry = TransactionEntry.CreateDebit(
                transactionId,
                amount,
                $"External deposit debit");

            _state.AddLedgerEntry(creditEntry);
            _state.AddLedgerEntry(debitEntry);


            var creditedEvent = WalletCreditedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.Deposit,
                transactionId,
                completedTransaction.Description,
                _state.GetBalance());

            await PublishEventAsync(creditedEvent);

            return TransactionResult.Success(completedTransaction, _state.GetBalance());
        }
        catch (Exception ex)
        {
            var failedTransaction = transaction.WithStatus(TransactionStatus.Failed, ex.Message);
            _state.AddTransaction(failedTransaction);

            var failedEvent = TransactionFailedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.Deposit,
                transactionId,
                ex.Message,
                transaction.Description);

            await PublishEventAsync(failedEvent);

            return TransactionResult.Failure(ex.Message);
        }
    }

    public async ValueTask<TransactionResult> WithdrawAsync(Money amount, string transactionId)
    {
        if (amount.Amount <= 0)
            return TransactionResult.Failure("Withdrawal amount must be positive");

        if (_state.ProcessedTransactionIds.Contains(transactionId))
        {
            var existingTransaction = _state.Transactions.FirstOrDefault(t => t.ReferenceId == transactionId);
            if (existingTransaction.Status == TransactionStatus.Completed)
                return TransactionResult.Success(existingTransaction, _state.GetBalance());
        }

        if (!_state.HasSufficientBalance(amount))
            return TransactionResult.Failure("Insufficient available balance");

        var transaction = WalletTransaction.Create(
            _state.UserId,
            TransactionType.Withdrawal,
            amount,
            $"Withdrawal of {amount.Amount} {amount.Currency}",
            transactionId);

        try
        {
            var newBalance = _state.Balance - amount.Amount;
            var completedTransaction = transaction.WithStatus(TransactionStatus.Completed);

            _state.AddTransaction(completedTransaction);
            _state.UpdateBalance(newBalance);

            var debitEntry = TransactionEntry.CreateDebit(
                transactionId,
                amount,
                $"Withdrawal debit");

            var creditEntry = TransactionEntry.CreateCredit(
                transactionId,
                amount,
                $"External withdrawal credit");

            _state.AddLedgerEntry(debitEntry);
            _state.AddLedgerEntry(creditEntry);


            var debitedEvent = WalletDebitedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.Withdrawal,
                transactionId,
                completedTransaction.Description,
                _state.GetBalance());

            await PublishEventAsync(debitedEvent);

            return TransactionResult.Success(completedTransaction, _state.GetBalance());
        }
        catch (Exception ex)
        {
            var failedTransaction = transaction.WithStatus(TransactionStatus.Failed, ex.Message);
            _state.AddTransaction(failedTransaction);

            var failedEvent = TransactionFailedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.Withdrawal,
                transactionId,
                ex.Message,
                transaction.Description);

            await PublishEventAsync(failedEvent);

            return TransactionResult.Failure(ex.Message);
        }
    }

    public ValueTask<TransactionResult> ReserveAsync(Money amount, string betId)
    {
        if (amount.Amount <= 0)
            return ValueTask.FromResult(TransactionResult.Failure("Reservation amount must be positive"));

        if (_state.HasReservation(betId))
            return ValueTask.FromResult(TransactionResult.Failure($"Reservation for bet {betId} already exists"));

        if (!_state.HasSufficientBalance(amount))
            return ValueTask.FromResult(TransactionResult.Failure("Insufficient available balance for reservation"));

        var transaction = WalletTransaction.Create(
            _state.UserId,
            TransactionType.Reservation,
            amount,
            $"Reserve {amount.Amount} {amount.Currency} for bet {betId}",
            betId);

        try
        {
            _state.AddReservation(betId, amount.Amount);
            var completedTransaction = transaction.WithStatus(TransactionStatus.Completed);
            _state.AddTransaction(completedTransaction);

            return ValueTask.FromResult(TransactionResult.Success(completedTransaction, _state.GetAvailableBalance()));
        }
        catch (Exception ex)
        {
            var failedTransaction = transaction.WithStatus(TransactionStatus.Failed, ex.Message);
            _state.AddTransaction(failedTransaction);

            return ValueTask.FromResult(TransactionResult.Failure(ex.Message));
        }
    }

    public async ValueTask<TransactionResult> CommitReservationAsync(string betId)
    {
        if (!_state.HasReservation(betId))
            return TransactionResult.Failure($"No reservation found for bet {betId}");

        var reservationAmount = _state.GetReservationAmount(betId);
        var amount = new Money(reservationAmount, _state.Currency);

        var transaction = WalletTransaction.Create(
            _state.UserId,
            TransactionType.ReservationCommit,
            amount,
            $"Commit reservation for bet {betId}",
            $"{betId}-commit");

        try
        {
            var newBalance = _state.Balance - reservationAmount;
            _state.RemoveReservation(betId);
            _state.UpdateBalance(newBalance);

            var completedTransaction = transaction.WithStatus(TransactionStatus.Completed);
            _state.AddTransaction(completedTransaction);

            var debitEntry = TransactionEntry.CreateDebit(
                transaction.Id,
                amount,
                $"Bet placement debit for {betId}");

            var creditEntry = TransactionEntry.CreateCredit(
                transaction.Id,
                amount,
                $"Bet system credit for {betId}");

            _state.AddLedgerEntry(debitEntry);
            _state.AddLedgerEntry(creditEntry);


            var debitedEvent = WalletDebitedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.BetPlacement,
                transaction.Id,
                completedTransaction.Description,
                _state.GetBalance());

            await PublishEventAsync(debitedEvent);

            return TransactionResult.Success(completedTransaction, _state.GetBalance());
        }
        catch (Exception ex)
        {
            var failedTransaction = transaction.WithStatus(TransactionStatus.Failed, ex.Message);
            _state.AddTransaction(failedTransaction);

            return TransactionResult.Failure(ex.Message);
        }
    }

    public ValueTask<TransactionResult> ReleaseReservationAsync(string betId)
    {
        if (!_state.HasReservation(betId))
            return ValueTask.FromResult(TransactionResult.Failure($"No reservation found for bet {betId}"));

        var reservationAmount = _state.GetReservationAmount(betId);
        var amount = new Money(reservationAmount, _state.Currency);

        var transaction = WalletTransaction.Create(
            _state.UserId,
            TransactionType.ReservationRelease,
            amount,
            $"Release reservation for bet {betId}",
            $"{betId}-release");

        try
        {
            _state.RemoveReservation(betId);
            var completedTransaction = transaction.WithStatus(TransactionStatus.Completed);
            _state.AddTransaction(completedTransaction);

            return ValueTask.FromResult(TransactionResult.Success(completedTransaction, _state.GetAvailableBalance()));
        }
        catch (Exception ex)
        {
            var failedTransaction = transaction.WithStatus(TransactionStatus.Failed, ex.Message);
            _state.AddTransaction(failedTransaction);

            return ValueTask.FromResult(TransactionResult.Failure(ex.Message));
        }
    }

    public ValueTask<IReadOnlyList<WalletTransaction>> GetTransactionHistoryAsync(int limit = 50)
    {
        var transactions = _state.Transactions
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .ToList()
            .AsReadOnly();

        return ValueTask.FromResult<IReadOnlyList<WalletTransaction>>(transactions);
    }

    public ValueTask<IReadOnlyList<TransactionEntry>> GetLedgerEntriesAsync(int limit = 50)
    {
        var entries = _state.LedgerEntries
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList()
            .AsReadOnly();

        return ValueTask.FromResult<IReadOnlyList<TransactionEntry>>(entries);
    }

    public async ValueTask<TransactionResult> ProcessPayoutAsync(Money amount, string betId, string sagaId)
    {
        if (amount.Amount <= 0)
            return TransactionResult.Failure("Payout amount must be positive");

        var transactionId = $"{betId}-payout-{sagaId}";

        if (_state.ProcessedTransactionIds.Contains(transactionId))
        {
            var existingTransaction = _state.Transactions.FirstOrDefault(t => t.ReferenceId == transactionId);
            if (existingTransaction.Status == TransactionStatus.Completed)
                return TransactionResult.Success(existingTransaction, _state.GetBalance());
        }

        var transaction = WalletTransaction.Create(
            _state.UserId,
            TransactionType.BetPayout,
            amount,
            $"Bet payout for {betId} (saga: {sagaId})",
            transactionId);

        try
        {
            var newBalance = _state.Balance + amount.Amount;
            var completedTransaction = transaction.WithStatus(TransactionStatus.Completed);

            _state.AddTransaction(completedTransaction);
            _state.UpdateBalance(newBalance);

            var creditEntry = TransactionEntry.CreateCredit(
                transactionId,
                amount,
                $"Bet payout credit for {betId}");

            var debitEntry = TransactionEntry.CreateDebit(
                transactionId,
                amount,
                $"System payout debit for {betId}");

            _state.AddLedgerEntry(creditEntry);
            _state.AddLedgerEntry(debitEntry);

            var creditedEvent = WalletCreditedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.BetPayout,
                transactionId,
                completedTransaction.Description,
                _state.GetBalance());

            await PublishEventAsync(creditedEvent);

            return TransactionResult.Success(completedTransaction, _state.GetBalance());
        }
        catch (Exception ex)
        {
            var failedTransaction = transaction.WithStatus(TransactionStatus.Failed, ex.Message);
            _state.AddTransaction(failedTransaction);

            var failedEvent = TransactionFailedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.BetPayout,
                transactionId,
                ex.Message,
                transaction.Description);

            await PublishEventAsync(failedEvent);

            return TransactionResult.Failure(ex.Message);
        }
    }

    public async ValueTask<TransactionResult> ReversePayoutAsync(Money amount, string betId, string sagaId, string reason)
    {
        if (amount.Amount <= 0)
            return TransactionResult.Failure("Reversal amount must be positive");

        var transactionId = $"{betId}-payout-reversal-{sagaId}";

        if (_state.ProcessedTransactionIds.Contains(transactionId))
        {
            var existingTransaction = _state.Transactions.FirstOrDefault(t => t.ReferenceId == transactionId);
            if (existingTransaction.Status == TransactionStatus.Completed)
                return TransactionResult.Success(existingTransaction, _state.GetBalance());
        }

        if (!_state.HasSufficientBalance(amount))
            return TransactionResult.Failure("Insufficient balance to reverse payout");

        var transaction = WalletTransaction.Create(
            _state.UserId,
            TransactionType.PayoutReversal,
            amount,
            $"Reverse payout for {betId} (saga: {sagaId}) - {reason}",
            transactionId);

        try
        {
            var newBalance = _state.Balance - amount.Amount;
            var completedTransaction = transaction.WithStatus(TransactionStatus.Completed);

            _state.AddTransaction(completedTransaction);
            _state.UpdateBalance(newBalance);

            var debitEntry = TransactionEntry.CreateDebit(
                transactionId,
                amount,
                $"Payout reversal debit for {betId}");

            var creditEntry = TransactionEntry.CreateCredit(
                transactionId,
                amount,
                $"System reversal credit for {betId}");

            _state.AddLedgerEntry(debitEntry);
            _state.AddLedgerEntry(creditEntry);

            var debitedEvent = WalletDebitedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.PayoutReversal,
                transactionId,
                completedTransaction.Description,
                _state.GetBalance());

            await PublishEventAsync(debitedEvent);

            return TransactionResult.Success(completedTransaction, _state.GetBalance());
        }
        catch (Exception ex)
        {
            var failedTransaction = transaction.WithStatus(TransactionStatus.Failed, ex.Message);
            _state.AddTransaction(failedTransaction);

            var failedEvent = TransactionFailedEvent.Create(
                _state.UserId,
                amount,
                TransactionType.PayoutReversal,
                transactionId,
                ex.Message,
                transaction.Description);

            await PublishEventAsync(failedEvent);

            return TransactionResult.Failure(ex.Message);
        }
    }

    private ValueTask PublishEventAsync<T>(T domainEvent) where T : IDomainEvent
    {
        try
        {
            // In a real implementation, this would publish to Pulsar
            // For now, we'll just log or store the event
            // This is where you would inject and use IEventPublisher
        }
        catch (Exception)
        {
            // Log the error but don't fail the transaction
            // Events are important but shouldn't break the core operation
        }
        return ValueTask.CompletedTask;
    }
}