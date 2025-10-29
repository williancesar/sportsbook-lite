namespace SportsbookLite.Grains.Betting;

public sealed class BetAggregate
{
    private readonly BetState _state;

    public BetAggregate(BetState state)
    {
        _state = state;
    }

    public static BetAggregate Create(Guid betId)
    {
        var state = new BetState { BetId = betId };
        return new BetAggregate(state);
    }

    public BetState GetState() => _state;

    public void PlaceBet(PlaceBetRequest request)
    {
        if (_state.Status != BetStatus.Pending)
            throw new InvalidOperationException("Bet has already been processed");

        _state.BetId = request.BetId;
        _state.UserId = request.UserId;
        _state.EventId = request.EventId;
        _state.MarketId = request.MarketId;
        _state.SelectionId = request.SelectionId;
        _state.Amount = request.Amount.Amount;
        _state.Currency = request.Amount.Currency;
        _state.Type = request.Type;
        _state.PlacedAt = DateTimeOffset.UtcNow;

        var domainEvent = new BetPlacedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            request.BetId.ToString(),
            request.BetId,
            request.UserId,
            request.EventId,
            request.MarketId,
            request.SelectionId,
            request.Amount,
            request.AcceptableOdds,
            request.Type
        );

        _state.AddUncommittedEvent(domainEvent);
    }

    public void AcceptBet(decimal finalOdds)
    {
        if (_state.Status != BetStatus.Pending)
            throw new InvalidOperationException("Bet can only be accepted from pending status");

        _state.Status = BetStatus.Accepted;
        _state.Odds = finalOdds;

        var potentialPayout = Money.Create(_state.Amount * finalOdds, _state.Currency);

        var domainEvent = new BetAcceptedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            _state.BetId.ToString(),
            _state.BetId,
            _state.UserId,
            finalOdds,
            potentialPayout
        );

        _state.AddUncommittedEvent(domainEvent);
    }

    public void RejectBet(string reason)
    {
        if (_state.Status != BetStatus.Pending)
            throw new InvalidOperationException("Bet can only be rejected from pending status");

        _state.Status = BetStatus.Rejected;
        _state.RejectionReason = reason;

        var domainEvent = new BetRejectedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            _state.BetId.ToString(),
            _state.BetId,
            _state.UserId,
            reason
        );

        _state.AddUncommittedEvent(domainEvent);
    }

    public void SettleBet(BetStatus finalStatus, Money? payout = null)
    {
        if (_state.Status != BetStatus.Accepted)
            throw new InvalidOperationException("Only accepted bets can be settled");

        if (finalStatus is not (BetStatus.Won or BetStatus.Lost))
            throw new ArgumentException("Final status must be Won or Lost", nameof(finalStatus));

        _state.Status = finalStatus;
        _state.SettledAt = DateTimeOffset.UtcNow;
        _state.PayoutAmount = payout?.Amount;

        var domainEvent = new BetSettledEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            _state.BetId.ToString(),
            _state.BetId,
            _state.UserId,
            finalStatus,
            payout
        );

        _state.AddUncommittedEvent(domainEvent);
    }

    public void VoidBet(string reason)
    {
        if (!_state.ToBet().CanBeVoided)
            throw new InvalidOperationException("Bet cannot be voided in current status");

        _state.Status = BetStatus.Void;
        _state.VoidReason = reason;
        _state.SettledAt = DateTimeOffset.UtcNow;

        var domainEvent = new BetVoidedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            _state.BetId.ToString(),
            _state.BetId,
            _state.UserId,
            reason
        );

        _state.AddUncommittedEvent(domainEvent);
    }

    public void Apply(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case BetPlacedEvent betPlaced:
                ApplyBetPlacedEvent(betPlaced);
                break;
            case BetAcceptedEvent betAccepted:
                ApplyBetAcceptedEvent(betAccepted);
                break;
            case BetRejectedEvent betRejected:
                ApplyBetRejectedEvent(betRejected);
                break;
            case BetSettledEvent betSettled:
                ApplyBetSettledEvent(betSettled);
                break;
            case BetVoidedEvent betVoided:
                ApplyBetVoidedEvent(betVoided);
                break;
        }
    }

    private void ApplyBetPlacedEvent(BetPlacedEvent domainEvent)
    {
        _state.BetId = domainEvent.BetId;
        _state.UserId = domainEvent.UserId;
        _state.EventId = domainEvent.EventId;
        _state.MarketId = domainEvent.MarketId;
        _state.SelectionId = domainEvent.SelectionId;
        _state.Amount = domainEvent.Amount.Amount;
        _state.Currency = domainEvent.Amount.Currency;
        _state.Type = domainEvent.Type;
        _state.Status = BetStatus.Pending;
        _state.PlacedAt = domainEvent.Timestamp;
    }

    private void ApplyBetAcceptedEvent(BetAcceptedEvent domainEvent)
    {
        _state.Status = BetStatus.Accepted;
        _state.Odds = domainEvent.FinalOdds;
    }

    private void ApplyBetRejectedEvent(BetRejectedEvent domainEvent)
    {
        _state.Status = BetStatus.Rejected;
        _state.RejectionReason = domainEvent.Reason;
    }

    private void ApplyBetSettledEvent(BetSettledEvent domainEvent)
    {
        _state.Status = domainEvent.FinalStatus;
        _state.SettledAt = domainEvent.Timestamp;
        _state.PayoutAmount = domainEvent.Payout?.Amount;
    }

    private void ApplyBetVoidedEvent(BetVoidedEvent domainEvent)
    {
        _state.Status = BetStatus.Void;
        _state.VoidReason = domainEvent.Reason;
        _state.SettledAt = domainEvent.Timestamp;
    }
}