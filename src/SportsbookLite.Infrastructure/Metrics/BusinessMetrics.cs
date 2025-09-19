using Prometheus;

namespace SportsbookLite.Infrastructure.Metrics;

/// <summary>
/// Central repository for all business metrics in the sportsbook system
/// </summary>
public static class BusinessMetrics
{

    // Betting Metrics
    public static readonly Counter BetsPlaced = Prometheus.Metrics
        .CreateCounter("sportsbook_bets_placed_total", "Total number of bets placed",
            new CounterConfiguration
            {
                LabelNames = new[] { "status", "event_type", "sport", "market_type" }
            });

    public static readonly Histogram BetAmount = Prometheus.Metrics
        .CreateHistogram("sportsbook_bet_amount", "Distribution of bet amounts",
            new HistogramConfiguration
            {
                LabelNames = new[] { "currency", "event_type", "sport" },
                Buckets = Histogram.ExponentialBuckets(1, 2, 15) // 1, 2, 4, 8... up to 16384
            });

    public static readonly Counter BetsSettled = Prometheus.Metrics
        .CreateCounter("sportsbook_bets_settled_total", "Total number of bets settled",
            new CounterConfiguration
            {
                LabelNames = new[] { "result", "event_type", "sport" }
            });

    public static readonly Histogram SettlementDuration = Prometheus.Metrics
        .CreateHistogram("sportsbook_settlement_duration_seconds", "Time taken to settle bets",
            new HistogramConfiguration
            {
                LabelNames = new[] { "event_type", "batch_size" },
                Buckets = Histogram.LinearBuckets(0.1, 0.5, 20) // 0.1s to 10s
            });

    // Odds Metrics
    public static readonly Counter OddsChanges = Prometheus.Metrics
        .CreateCounter("sportsbook_odds_changes_total", "Total number of odds changes",
            new CounterConfiguration
            {
                LabelNames = new[] { "market_id", "event_type", "trigger" }
            });

    public static readonly Gauge CurrentOdds = Prometheus.Metrics
        .CreateGauge("sportsbook_current_odds", "Current odds for active markets",
            new GaugeConfiguration
            {
                LabelNames = new[] { "market_id", "selection", "event_id" }
            });

    public static readonly Histogram OddsMovement = Prometheus.Metrics
        .CreateHistogram("sportsbook_odds_movement", "Distribution of odds movements",
            new HistogramConfiguration
            {
                LabelNames = new[] { "direction", "market_type" },
                Buckets = Histogram.LinearBuckets(0.01, 0.05, 20) // 0.01 to 1.0
            });

    // Wallet Metrics
    public static readonly Gauge WalletBalance = Prometheus.Metrics
        .CreateGauge("sportsbook_wallet_balance", "Current wallet balance",
            new GaugeConfiguration
            {
                LabelNames = new[] { "user_id", "currency" }
            });

    public static readonly Counter WalletTransactions = Prometheus.Metrics
        .CreateCounter("sportsbook_wallet_transactions_total", "Total wallet transactions",
            new CounterConfiguration
            {
                LabelNames = new[] { "type", "status", "currency" }
            });

    public static readonly Histogram TransactionAmount = Prometheus.Metrics
        .CreateHistogram("sportsbook_transaction_amount", "Distribution of transaction amounts",
            new HistogramConfiguration
            {
                LabelNames = new[] { "type", "currency" },
                Buckets = Histogram.ExponentialBuckets(1, 2, 15)
            });

    // Event Metrics
    public static readonly Gauge ActiveEvents = Prometheus.Metrics
        .CreateGauge("sportsbook_active_events", "Number of active sporting events",
            new GaugeConfiguration
            {
                LabelNames = new[] { "sport", "competition", "status" }
            });

    public static readonly Counter EventsProcessed = Prometheus.Metrics
        .CreateCounter("sportsbook_events_processed_total", "Total events processed",
            new CounterConfiguration
            {
                LabelNames = new[] { "sport", "action", "result" }
            });

    public static readonly Histogram EventProcessingLatency = Prometheus.Metrics
        .CreateHistogram("sportsbook_event_processing_latency_seconds", "Event processing latency",
            new HistogramConfiguration
            {
                LabelNames = new[] { "event_type", "priority" },
                Buckets = Histogram.LinearBuckets(0.001, 0.005, 20) // 1ms to 100ms
            });

    // Risk Management Metrics
    public static readonly Gauge ExposureByMarket = Prometheus.Metrics
        .CreateGauge("sportsbook_exposure_by_market", "Current exposure per market",
            new GaugeConfiguration
            {
                LabelNames = new[] { "market_id", "event_id", "sport" }
            });

    public static readonly Counter RiskAlerts = Prometheus.Metrics
        .CreateCounter("sportsbook_risk_alerts_total", "Risk management alerts triggered",
            new CounterConfiguration
            {
                LabelNames = new[] { "severity", "type", "market_id" }
            });

    // System Health Metrics
    public static readonly Gauge SystemHealth = Prometheus.Metrics
        .CreateGauge("sportsbook_system_health", "Overall system health score (0-100)",
            new GaugeConfiguration
            {
                LabelNames = new[] { "component" }
            });

    /// <summary>
    /// Records a bet placement with all relevant dimensions
    /// </summary>
    public static void RecordBetPlacement(string status, string eventType, string sport, string marketType, decimal amount, string currency)
    {
        BetsPlaced.WithLabels(status, eventType, sport, marketType).Inc();
        BetAmount.WithLabels(currency, eventType, sport).Observe((double)amount);
    }

    /// <summary>
    /// Records bet settlement
    /// </summary>
    public static void RecordBetSettlement(string result, string eventType, string sport, double durationSeconds)
    {
        BetsSettled.WithLabels(result, eventType, sport).Inc();
        SettlementDuration.WithLabels(eventType, "single").Observe(durationSeconds);
    }

    /// <summary>
    /// Records odds change
    /// </summary>
    public static void RecordOddsChange(string marketId, string eventType, string trigger, double oldOdds, double newOdds)
    {
        OddsChanges.WithLabels(marketId, eventType, trigger).Inc();
        var movement = Math.Abs(newOdds - oldOdds);
        var direction = newOdds > oldOdds ? "up" : "down";
        OddsMovement.WithLabels(direction, eventType).Observe(movement);
    }

    /// <summary>
    /// Updates wallet balance
    /// </summary>
    public static void UpdateWalletBalance(string userId, string currency, decimal balance)
    {
        WalletBalance.WithLabels(userId, currency).Set((double)balance);
    }

    /// <summary>
    /// Records wallet transaction
    /// </summary>
    public static void RecordWalletTransaction(string type, string status, string currency, decimal amount)
    {
        WalletTransactions.WithLabels(type, status, currency).Inc();
        TransactionAmount.WithLabels(type, currency).Observe((double)amount);
    }

    /// <summary>
    /// Updates active events count
    /// </summary>
    public static void UpdateActiveEvents(string sport, string competition, string status, int count)
    {
        ActiveEvents.WithLabels(sport, competition, status).Set(count);
    }

    /// <summary>
    /// Records event processing
    /// </summary>
    public static void RecordEventProcessing(string sport, string action, string result, double latencySeconds)
    {
        EventsProcessed.WithLabels(sport, action, result).Inc();
        EventProcessingLatency.WithLabels(action, "normal").Observe(latencySeconds);
    }

    /// <summary>
    /// Updates market exposure
    /// </summary>
    public static void UpdateMarketExposure(string marketId, string eventId, string sport, decimal exposure)
    {
        ExposureByMarket.WithLabels(marketId, eventId, sport).Set((double)exposure);
    }

    /// <summary>
    /// Records risk alert
    /// </summary>
    public static void RecordRiskAlert(string severity, string type, string marketId)
    {
        RiskAlerts.WithLabels(severity, type, marketId).Inc();
    }

    /// <summary>
    /// Updates system health score
    /// </summary>
    public static void UpdateSystemHealth(string component, double healthScore)
    {
        SystemHealth.WithLabels(component).Set(healthScore);
    }
}