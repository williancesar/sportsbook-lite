using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using SportsbookLite.Api.Features.Betting.Requests;
using SportsbookLite.Api.Features.Betting.Responses;
using SportsbookLite.Contracts.Betting;
using SportsbookLite.GrainInterfaces;

namespace SportsbookLite.Api.Features.Betting.Endpoints;

public sealed class GetBetHistoryEndpoint : Endpoint<GetBetHistoryRequest, BetHistoryResponse>
{
    private readonly IGrainFactory _grainFactory;

    public GetBetHistoryEndpoint(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override void Configure()
    {
        Get("/api/bets/{betId}/history");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get bet history";
            s.Description = "Retrieves the complete event history for a specific bet";
            s.Params["betId"] = "The unique identifier of the bet";
            s.Response(200, "History retrieved successfully", example: new BetHistoryResponse
            {
                Events = new[]
                {
                    new BetHistoryEventDto
                    {
                        EventType = "BetPlaced",
                        Timestamp = DateTimeOffset.UtcNow.AddHours(-1),
                        Data = new Dictionary<string, object>
                        {
                            ["betId"] = Guid.NewGuid(),
                            ["amount"] = 100.00m,
                            ["odds"] = 1.5m
                        }
                    }
                }
            });
            s.Response(404, "Bet not found");
            s.Response(500, "Internal server error");
        });
    }

    public override async Task HandleAsync(GetBetHistoryRequest req, CancellationToken ct)
    {
        try
        {
            var betGrain = _grainFactory.GetGrain<IBetGrain>(req.BetId);
            var history = await betGrain.GetBetHistoryAsync();

            if (!history.Any())
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }

            Response = new BetHistoryResponse
            {
                Events = history.Select(bet => MapToBetHistoryEvent(bet, req.IncludeMetadata)).ToArray()
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get history for bet {BetId}", req.BetId);
            
            HttpContext.Response.StatusCode = 500;
            Response = new BetHistoryResponse();
        }
    }

    private static BetHistoryEventDto MapToBetHistoryEvent(Bet bet, bool includeMetadata)
    {
        var eventType = DetermineEventType(bet);
        var data = new Dictionary<string, object>
        {
            ["betId"] = bet.Id,
            ["userId"] = bet.UserId,
            ["eventId"] = bet.EventId,
            ["marketId"] = bet.MarketId,
            ["selectionId"] = bet.SelectionId,
            ["amount"] = bet.Amount.Amount,
            ["currency"] = bet.Amount.Currency,
            ["odds"] = bet.Odds,
            ["status"] = bet.Status.ToString(),
            ["type"] = bet.Type.ToString(),
            ["potentialPayout"] = bet.PotentialPayout.Amount
        };

        if (bet.Payout != null)
        {
            data["actualPayout"] = bet.Payout.Value.Amount;
        }

        if (!string.IsNullOrEmpty(bet.RejectionReason))
        {
            data["rejectionReason"] = bet.RejectionReason;
        }

        if (!string.IsNullOrEmpty(bet.VoidReason))
        {
            data["voidReason"] = bet.VoidReason;
        }

        Dictionary<string, object>? metadata = null;
        if (includeMetadata)
        {
            metadata = new Dictionary<string, object>
            {
                ["isSettled"] = bet.IsSettled,
                ["canBeVoided"] = bet.CanBeVoided,
                ["canBeCashedOut"] = bet.CanBeCashedOut
            };
        }

        return new BetHistoryEventDto
        {
            EventType = eventType,
            Timestamp = bet.SettledAt ?? bet.PlacedAt,
            Data = data,
            Metadata = metadata
        };
    }

    private static string DetermineEventType(Bet bet) => bet.Status switch
    {
        BetStatus.Pending => "BetPlaced",
        BetStatus.Accepted => "BetAccepted",
        BetStatus.Rejected => "BetRejected",
        BetStatus.Won => "BetWon",
        BetStatus.Lost => "BetLost",
        BetStatus.Void => "BetVoided",
        BetStatus.CashOut => "BetCashedOut",
        _ => "BetStatusChanged"
    };
}