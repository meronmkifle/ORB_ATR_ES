using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class KNQORBStrategy : Strategy
    {
        // ==================== INPUTS / PROPERTIES ====================
        #region Input Parameters

        public enum TradingDirection
        {
            [Description("Long Only")] LongOnly,
            [Description("Short Only")] ShortOnly,
            [Description("Both")] Both
        }

        [NinjaScriptProperty]
        [Display(Name = "Strategy Version", GroupName = "Settings", Order = 1)]
        public string Version { get; set; } = "v1.0";

        [NinjaScriptProperty, Display(Name = "Trading Direction", Order = 1, GroupName = "Strategy")]
        public TradingDirection TradeDirection { get; set; } = TradingDirection.LongOnly;

        // Day filters
        [NinjaScriptProperty, Display(Name = "Trade Monday", Order = 2, GroupName = "Day Filter")]
        public bool TradeMonday { get; set; } = true;
        [NinjaScriptProperty, Display(Name = "Trade Tuesday", Order = 3, GroupName = "Day Filter")]
        public bool TradeTuesday { get; set; } = true;
        [NinjaScriptProperty, Display(Name = "Trade Wednesday", Order = 4, GroupName = "Day Filter")]
        public bool TradeWednesday { get; set; } = true;
        [NinjaScriptProperty, Display(Name = "Trade Thursday", Order = 5, GroupName = "Day Filter")]
        public bool TradeThursday { get; set; } = true;
        [NinjaScriptProperty, Display(Name = "Trade Friday", Order = 6, GroupName = "Day Filter")]
        public bool TradeFriday { get; set; } = true;
        [NinjaScriptProperty, Display(Name = "Trade Saturday", Order = 7, GroupName = "Day Filter")]
        public bool TradeSaturday { get; set; } = false;
        [NinjaScriptProperty, Display(Name = "Trade Sunday", Order = 8, GroupName = "Day Filter")]
        public bool TradeSunday { get; set; } = false;

        // Core settings
        [NinjaScriptProperty, Range(1, int.MaxValue)]
        [Display(Name = "ATR Period", Order = 10, GroupName = "Core Settings")]
        public int AtrPeriod { get; set; } = 14;

        [NinjaScriptProperty, Range(0.1, double.MaxValue)]
        [Display(Name = "ATR Multiplier", Order = 11, GroupName = "Core Settings")]
        public double AtrMultiplier { get; set; } = 2.5;

        [NinjaScriptProperty, Range(0.1, double.MaxValue)]
        [Display(Name = "TP Multiplier", Order = 12, GroupName = "Core Settings")]
        public double TpMultiplier { get; set; } = 2.0;

        [NinjaScriptProperty, Range(1, int.MaxValue)]
        [Display(Name = "Core Contracts", Order = 13, GroupName = "Core Settings")]
        public int CoreContracts { get; set; } = 1;

        [NinjaScriptProperty, Range(1, int.MaxValue)]
        [Display(Name = "Add Contracts", Order = 14, GroupName = "Core Settings")]
        public int AddContracts { get; set; } = 2;

        // Scaling thresholds (points)
        [NinjaScriptProperty, Range(2.0, double.MaxValue)]
        [Display(Name = "Add Threshold (pts)", Order = 15, GroupName = "Scaling")]
        public double AddThreshold { get; set; } = 8.0;

        [NinjaScriptProperty, Range(5.0, double.MaxValue)]
        [Display(Name = "Partial Threshold (pts)", Order = 16, GroupName = "Scaling")]
        public double PartialThreshold { get; set; } = 100.0;

        // Trailing
        [NinjaScriptProperty, Range(1, double.MaxValue)]
        [Display(Name = "Trail Activation (pts)", Order = 17, GroupName = "Scaling")]
        public double TrailActivationPoints { get; set; } = 30.0;

        [NinjaScriptProperty, Range(1, double.MaxValue)]
        [Display(Name = "Trail Buffer (pts)", Order = 18, GroupName = "Scaling")]
        public double TrailBufferPoints { get; set; } = 6.0;

        // Time strings
        [NinjaScriptProperty, Display(Name = "OR Start", Description = "HH:mm, e.g. 09:30", Order = 20, GroupName = "Time Settings")]
        public string OrStartText { get; set; } = "09:30";
        [NinjaScriptProperty, Display(Name = "OR End", Description = "HH:mm, e.g. 09:45", Order = 21, GroupName = "Time Settings")]
        public string OrEndText { get; set; } = "09:45";
        [NinjaScriptProperty, Display(Name = "Session End", Description = "HH:mm, e.g. 10:30", Order = 22, GroupName = "Time Settings")]
        public string SessionEndText { get; set; } = "10:30";
        [NinjaScriptProperty, Display(Name = "Midnight Time", Description = "HH:mm, e.g. 01:00", Order = 23, GroupName = "Time Settings")]
        public string MidnightText { get; set; } = "01:00";

        // Confirmation
        [NinjaScriptProperty, Range(0.5, double.MaxValue)]
        [Display(Name = "Volume Multiplier", Order = 30, GroupName = "Confirmation")]
        public double VolumeThreshold { get; set; } = 1;

        [NinjaScriptProperty, Range(1.0, double.MaxValue)]
        [Display(Name = "Min Range Size (pts)", Order = 31, GroupName = "Confirmation")]
        public double MinRangeSize { get; set; } = 8.0;

        // Midnight filter
        [NinjaScriptProperty, Display(Name = "Enable Midnight Filter", Order = 40, GroupName = "Midnight Filter")]
        public bool EnableMidnightFilter { get; set; } = true;

        [NinjaScriptProperty, Range(0.0, double.MaxValue)]
        [Display(Name = "Points Above Midnight", Order = 41, GroupName = "Midnight Filter")]
        public double MidnightPointsAbove { get; set; } = 10;

        #endregion

        // ----- Parsed times -----
        [Browsable(false)] public TimeSpan OrStart => ParseHHmm(OrStartText);
        [Browsable(false)] public TimeSpan OrEnd => ParseHHmm(OrEndText);
        [Browsable(false)] public TimeSpan SessionEnd => ParseHHmm(SessionEndText);
        [Browsable(false)] public TimeSpan Midnight => ParseHHmm(MidnightText);

        // ==================== STATE ====================
        private double rangeHigh, rangeLow;
        private double midnightOpen;
        private DateTime midnightTimeStamp;

        private bool positionAdded, partialsTaken, trailingActive, tradeTakenToday;
        private double currentStop;
        private double originalEntryPrice;
        private int totalPositionSize;
        private int barsSinceAdd;

        private DateTime currentTradeDate;
        private ATR atr5;
        private VOL vol5;
        private SMA vol5sma20;

        private double prevRangeHigh, prevRangeLow;


        // ==================== HELPERS ====================
        private static TimeSpan ParseHHmm(string s)
        {
            if (TimeSpan.TryParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture, out var ts))
                return ts;
            if (TimeSpan.TryParse(s, out ts))
                return ts;
            if (int.TryParse(s, out var hhmm))
                return new TimeSpan(hhmm / 100, hhmm % 100, 0);
            throw new ArgumentException($"Invalid time '{s}'. Expected HH:mm.");
        }

        private bool IsTimeBetween(TimeSpan start, TimeSpan end, DateTime barTime)
        {
            var t = barTime.TimeOfDay;
            return start <= end ? (t >= start && t <= end) : (t >= start || t <= end);
        }

        private bool IsTradingDay()
        {
            var day = Times[0][0].DayOfWeek;
            return day switch
            {
                DayOfWeek.Monday => TradeMonday,
                DayOfWeek.Tuesday => TradeTuesday,
                DayOfWeek.Wednesday => TradeWednesday,
                DayOfWeek.Thursday => TradeThursday,
                DayOfWeek.Friday => TradeFriday,
                DayOfWeek.Saturday => TradeSaturday,
                DayOfWeek.Sunday => TradeSunday,
                _ => false,
            };
        }

        private void ResetDaily()
        {
            rangeHigh = rangeLow = midnightOpen = 0;
            midnightTimeStamp = DateTime.MinValue;

            tradeTakenToday = false;
            positionAdded = false;
            partialsTaken = false;
            trailingActive = false;
            currentStop = 0;
            originalEntryPrice = 0;
            totalPositionSize = 0;
            barsSinceAdd = 0;
        }

        // ==================== LIFECYCLE ====================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "NQ ORB - Optimized Enhanced";
                Name = "K_NQ_ORBStrategy+";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 5;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = false;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;

                AddPlot(new Stroke(Brushes.Pink, 3), PlotStyle.Line, "Range High");
                AddPlot(new Stroke(Brushes.Blue, 3), PlotStyle.Line, "Range Low");
            }
            else if (State == State.Configure)
            {
                // 5-minute secondary series (for ORB, midnight, ATR/volume)
                AddDataSeries(BarsPeriodType.Minute, 5);
            }
            else if (State == State.DataLoaded)
            {
                // Indicators on 5-min series
                atr5 = ATR(BarsArray[1], AtrPeriod);
                vol5 = VOL(BarsArray[1]);
                vol5sma20 = SMA(vol5, 20);
            }
        }

        protected override void OnBarUpdate()
        {
            // Work only on primary series updates, but require enough bars on both
            if (BarsInProgress != 0 || CurrentBars[0] < 20 || CurrentBars[1] < 20)
                return;

            // New trading day reset (by date of primary series)
            if (currentTradeDate != Times[0][0].Date)
            {
                currentTradeDate = Times[0][0].Date;
                ResetDaily();
            }

            // ----- Time windows (using 5-min timestamps) -----
            DateTime t5 = Times[1][0];
            bool inOR = IsTimeBetween(OrStart, OrEnd, t5);
            bool inSession = IsTimeBetween(OrStart, SessionEnd, t5);
            bool inMidnightWindow = IsTimeBetween(Midnight, Midnight.Add(new TimeSpan(0, 1, 0)), t5);

            bool tradingDay = IsTradingDay();

            // ----- Build OR (on 5-min bars) -----
            if (inOR && tradingDay)
            {
                double h = Highs[1][0];
                double l = Lows[1][0];
                if (rangeHigh == 0 && rangeLow == 0)
                {
                    rangeHigh = h;
                    rangeLow = l;
                }
                else
                {
                    rangeHigh = Math.Max(rangeHigh, h);
                    rangeLow = Math.Min(rangeLow, l);
                }
                prevRangeHigh = rangeHigh;
                prevRangeLow = rangeLow;
            }

            // ----- Capture midnight open (first time in window) -----
            if (inMidnightWindow && tradingDay && midnightOpen == 0)
            {
                midnightOpen = Opens[1][0];
                midnightTimeStamp = Times[1][0];
            }

            // ----- Confirmations -----
            // FIX: Compare 5-min volume to its own 20-SMA
            bool volumeOk = vol5[0] > vol5sma20[0] * VolumeThreshold;

            // Range size in PRICE units; MinRangeSize is in POINTS -> convert
            bool rangeSizeOk = (rangeHigh > 0 && rangeLow > 0) &&
                               ((rangeHigh - rangeLow) >= MinRangeSize);

            bool midnightFilterOk = !EnableMidnightFilter
                ? true
                : (midnightOpen > 0 && rangeHigh > 0 &&
                   (rangeHigh >= (midnightOpen + MidnightPointsAbove)));

            // ----- Current profit (in POINTS) -----
            double currentProfitPts = 0.0;
            if (Position.MarketPosition == MarketPosition.Long)
                currentProfitPts = (Close[0] - Position.AveragePrice) ;
            else if (Position.MarketPosition == MarketPosition.Short)
                currentProfitPts = (Position.AveragePrice - Close[0]);

            // ----- Entry conditions -----
            bool longOK =
                (TradeDirection == TradingDirection.LongOnly || TradeDirection == TradingDirection.Both) &&
                inSession && tradingDay && rangeHigh > 0 && rangeLow > 0 &&
                Close[0] > rangeHigh && Position.MarketPosition == MarketPosition.Flat &&
                volumeOk && rangeSizeOk && midnightFilterOk && !tradeTakenToday;

            bool shortOK =
                (TradeDirection == TradingDirection.ShortOnly || TradeDirection == TradingDirection.Both) &&
                inSession && tradingDay && rangeHigh > 0 && rangeLow > 0 &&
                Close[0] < rangeLow && Position.MarketPosition == MarketPosition.Flat &&
                volumeOk && rangeSizeOk && midnightFilterOk && !tradeTakenToday;

            // ==================== ENTRIES ====================
            if (longOK)
            {
                EnterLong(CoreContracts, "CoreLong");

                // Initial managed SL/TP for this signal (pricing in PRICE units)
                double sl = Close[0] - atr5[0] * AtrMultiplier;
                double tp = Close[0] + atr5[0] * AtrMultiplier * TpMultiplier;

                SetStopLoss("CoreLong", CalculationMode.Price, sl, false);
                SetProfitTarget("CoreLong", CalculationMode.Price, tp);

                originalEntryPrice = Close[0];
                currentStop = sl;
                totalPositionSize = CoreContracts;
                tradeTakenToday = true;

                positionAdded = partialsTaken = trailingActive = false;
            }

            if (shortOK)
            {
                EnterShort(CoreContracts, "CoreShort");

                double sl = Close[0] + atr5[0] * AtrMultiplier;
                double tp = Close[0] - atr5[0] * AtrMultiplier * TpMultiplier;

                SetStopLoss("CoreShort", CalculationMode.Price, sl, false);
                SetProfitTarget("CoreShort", CalculationMode.Price, tp);

                originalEntryPrice = Close[0];
                currentStop = sl;
                totalPositionSize = CoreContracts;
                tradeTakenToday = true;

                positionAdded = partialsTaken = trailingActive = false;
            }

            // ==================== ADD TO WINNERS ====================
            if (Position.MarketPosition == MarketPosition.Long && !positionAdded && currentProfitPts >= AddThreshold)
            {
                EnterLong(AddContracts, "AddLong");

                // Move stop to at least breakeven (never loosen)
                double newStop = Position.AveragePrice; // BE
                currentStop = Math.Max(currentStop, newStop);
                SetStopLoss("CoreLong", CalculationMode.Price, currentStop, false);
                SetStopLoss("AddLong", CalculationMode.Price, currentStop, false);

                // Optional: TP for add (same multiple as core)
                double addTp = Close[0] + atr5[0] * AtrMultiplier * TpMultiplier;
                SetProfitTarget("AddLong", CalculationMode.Price, addTp);

                totalPositionSize = CoreContracts + AddContracts;
                positionAdded = true;
                barsSinceAdd = 0;
            }
            else if (Position.MarketPosition == MarketPosition.Short && !positionAdded && currentProfitPts >= AddThreshold)
            {
                EnterShort(AddContracts, "AddShort");

                double newStop = Position.AveragePrice; // BE
                currentStop = Math.Min(currentStop, newStop);
                SetStopLoss("CoreShort", CalculationMode.Price, currentStop, false);
                SetStopLoss("AddShort", CalculationMode.Price, currentStop, false);

                double addTp = Close[0] - atr5[0] * AtrMultiplier * TpMultiplier;
                SetProfitTarget("AddShort", CalculationMode.Price, addTp);

                totalPositionSize = CoreContracts + AddContracts;
                positionAdded = true;
                barsSinceAdd = 0;
            }
            if (positionAdded) barsSinceAdd++;

            // ==================== PARTIALS (quantity, after delay) ====================
            if (Position.MarketPosition != MarketPosition.Flat &&
                currentProfitPts >= PartialThreshold &&
                !partialsTaken && positionAdded && currentProfitPts > 0 && barsSinceAdd >= 2)
            {
                int qtyToExit = Math.Min(AddContracts, Position.Quantity);
                if (qtyToExit > 0)
                {
                    if (Position.MarketPosition == MarketPosition.Long)
                        ExitLong(qtyToExit, "PartialTPLong", "");  // from any entry
                    else
                        ExitShort(qtyToExit, "PartialTPShort", "");

                    partialsTaken = true;
                    totalPositionSize = Math.Max(0, totalPositionSize - qtyToExit);
                }
            }

            // ==================== TRAILING STOP (ratchet managed stop) ====================
            if (Position.MarketPosition != MarketPosition.Flat &&
                !trailingActive && currentProfitPts >= TrailActivationPoints)
            {
                trailingActive = true;

                double trailPrice = (Position.MarketPosition == MarketPosition.Long)
                    ? Close[0] - TrailBufferPoints
                    : Close[0] + TrailBufferPoints;

                // do not loosen relative to currentStop
                if (Position.MarketPosition == MarketPosition.Long)
                    currentStop = Math.Max(currentStop, trailPrice);
                else
                    currentStop = (currentStop == 0) ? trailPrice : Math.Min(currentStop, trailPrice);

                // Push to broker
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    SetStopLoss("CoreLong", CalculationMode.Price, currentStop, false);
                    if (positionAdded) SetStopLoss("AddLong", CalculationMode.Price, currentStop, false);
                }
                else
                {
                    SetStopLoss("CoreShort", CalculationMode.Price, currentStop, false);
                    if (positionAdded) SetStopLoss("AddShort", CalculationMode.Price, currentStop, false);
                }
            }

            if (trailingActive && Position.MarketPosition != MarketPosition.Flat)
            {
                double trailBuffer = TrailBufferPoints;

                if (Position.MarketPosition == MarketPosition.Long)
                {
                    double newTrail = Close[0] - trailBuffer;
                    if (newTrail > currentStop)
                    {
                        currentStop = newTrail;
                        SetStopLoss("CoreLong", CalculationMode.Price, currentStop, false);
                        if (positionAdded) SetStopLoss("AddLong", CalculationMode.Price, currentStop, false);
                    }
                }
                else
                {
                    double newTrail = Close[0] + trailBuffer;
                    if (currentStop == 0 || newTrail < currentStop)
                    {
                        currentStop = newTrail;
                        SetStopLoss("CoreShort", CalculationMode.Price, currentStop, false);
                        if (positionAdded) SetStopLoss("AddShort", CalculationMode.Price, currentStop, false);
                    }
                }
            }

            // ==================== AUTO-TP (only if no partials) ====================
            if (!partialsTaken && originalEntryPrice > 0 && Position.MarketPosition != MarketPosition.Flat)
            {
                double tpLevel = originalEntryPrice + (atr5[0] * AtrMultiplier * TpMultiplier) *
                                 (Position.MarketPosition == MarketPosition.Long ? 1 : -1);

                if (Position.MarketPosition == MarketPosition.Long)
                {
                    SetProfitTarget("CoreLong", CalculationMode.Price, tpLevel);
                    if (positionAdded) SetProfitTarget("AddLong", CalculationMode.Price, tpLevel);
                }
                else
                {
                    SetProfitTarget("CoreShort", CalculationMode.Price, tpLevel);
                    if (positionAdded) SetProfitTarget("AddShort", CalculationMode.Price, tpLevel);
                }
            }

            // ==================== VISUALS ====================
            Values[0][0] = prevRangeHigh;
            Values[1][0] = prevRangeLow;

            // Single tag per day to avoid spam
            if (midnightOpen > 0 && midnightTimeStamp != DateTime.MinValue)
            {
                string moTag = $"MidnightOpen_{currentTradeDate:yyyyMMdd}";
                Draw.Line(this, moTag, false, midnightTimeStamp, midnightOpen, Time[0], midnightOpen,
                    Brushes.Gold, DashStyleHelper.Solid, 2);

                if (EnableMidnightFilter)
                {
                    string mfTag = $"MidnightFilter_{currentTradeDate:yyyyMMdd}";
                    double filterLevel = midnightOpen + MidnightPointsAbove ;
                    Draw.Line(this, mfTag, false, midnightTimeStamp, filterLevel, Time[0], filterLevel,
                        Brushes.Orange, DashStyleHelper.Dot, 2);
                }
            }

            // ==================== RESET FLAGS WHEN FLAT ====================
            if (Position.MarketPosition == MarketPosition.Flat && Position.Quantity == 0)
            {
                positionAdded = false;
                partialsTaken = false;
                trailingActive = false;
                currentStop = 0;
                originalEntryPrice = 0;
                totalPositionSize = 0;
            }
        }
    }
}

