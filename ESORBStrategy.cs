using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
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
    public class SimpleES : Strategy
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
        public string Version { get; set; } = "v1.SimpleES";

        [NinjaScriptProperty, Display(Name = "Trading Direction", Order = 1, GroupName = "Strategy")]
        public TradingDirection TradeDirection { get; set; } = TradingDirection.LongOnly;

        // Day filters
        [NinjaScriptProperty, Display(Name = "Trade Monday", Order = 2, GroupName = "Day Filter")]
        public bool TradeMonday { get; set; } = true;
        [NinjaScriptProperty, Display(Name = "Trade Tuesday", Order = 3, GroupName = "Day Filter")]
        public bool TradeTuesday { get; set; } = true;
        [NinjaScriptProperty, Display(Name = "Trade Wednesday", Order = 4, GroupName = "Day Filter")]
        public bool TradeWednesday { get; set; } = false;
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
        public int AtrPeriod { get; set; } = 4;

        [NinjaScriptProperty, Range(0.1, double.MaxValue)]
        [Display(Name = "ATR Multiplier", Order = 11, GroupName = "Core Settings")]
        public double AtrMultiplier { get; set; } = 1;

        [NinjaScriptProperty, Range(0.1, double.MaxValue)]
        [Display(Name = "TP Multiplier", Order = 12, GroupName = "Core Settings")]
        public double TpMultiplier { get; set; } = 3.5;

        [NinjaScriptProperty, Range(1, int.MaxValue)]
        [Display(Name = "Contracts", Order = 13, GroupName = "Core Settings")]
        public int Contracts { get; set; } = 1;

        // Breakeven settings
        [NinjaScriptProperty, Display(Name = "Enable Breakeven", Order = 14, GroupName = "Core Settings")]
        public bool EnableBreakeven { get; set; } = true;

        [NinjaScriptProperty, Range(0.1, double.MaxValue)]
        [Display(Name = "Breakeven Trigger (ATR)", Order = 15, GroupName = "Core Settings")]
        public double BreakevenTriggerATR { get; set; } = 1;

        // Time strings
        [NinjaScriptProperty, Display(Name = "OR Start", Description = "HH:mm, e.g. 09:30", Order = 20, GroupName = "Time Settings")]
        public string OrStartText { get; set; } = "09:35";
        [NinjaScriptProperty, Display(Name = "OR End", Description = "HH:mm, e.g. 09:45", Order = 21, GroupName = "Time Settings")]
        public string OrEndText { get; set; } = "09:45";
        [NinjaScriptProperty, Display(Name = "Session End", Description = "HH:mm, e.g. 10:30", Order = 22, GroupName = "Time Settings")]
        public string SessionEndText { get; set; } = "10:50";
        [NinjaScriptProperty, Display(Name = "Midnight Time", Description = "HH:mm, e.g. 01:00", Order = 23, GroupName = "Time Settings")]
        public string MidnightText { get; set; } = "01:00";

        // Confirmation
        [NinjaScriptProperty, Range(1.0, double.MaxValue)]
        [Display(Name = "Min Range Size (pts)", Order = 31, GroupName = "Confirmation")]
        public double MinRangeSize { get; set; } = 5.0;

        // Midnight filter
        [NinjaScriptProperty, Display(Name = "Enable Midnight Filter", Order = 40, GroupName = "Midnight Filter")]
        public bool EnableMidnightFilter { get; set; } = true;

        [NinjaScriptProperty, Range(0.0, double.MaxValue)]
        [Display(Name = "Points Above Midnight", Order = 41, GroupName = "Midnight Filter")]
        public double MidnightPointsAbove { get; set; } = 6.0;

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
        private bool tradeTakenToday;
        private bool breakevenMoved;
        private double entryPrice;
        private DateTime currentTradeDate;
        private ATR atr5;
        private bool rangeBoxDrawn;
        private bool midnightLineDrawn;
        private DateTime orStartTime, orEndTime;

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
            breakevenMoved = false;
            entryPrice = 0;
            rangeBoxDrawn = false;
            midnightLineDrawn = false;
            orStartTime = DateTime.MinValue;
            orEndTime = DateTime.MinValue;
        }

        private void DrawRangeBox()
        {
            if (rangeHigh > 0 && rangeLow > 0 && orStartTime != DateTime.MinValue && orEndTime != DateTime.MinValue && !rangeBoxDrawn)
            {
                // Draw the ORB range box in blue
                Draw.Rectangle(this, $"ORB_Box_{currentTradeDate:yyyyMMdd}", 
                    false, orStartTime, rangeLow, orEndTime, rangeHigh, 
                    Brushes.Transparent, Brushes.Blue, 20);
                
                rangeBoxDrawn = true;
            }
        }

        private void DrawMidnightOpenLine()
        {
            if (midnightOpen > 0 && !midnightLineDrawn)
            {
                // Draw horizontal line across the entire trading day in green
                DateTime startOfDay = currentTradeDate.Date.Add(OrStart);
                DateTime endOfDay = currentTradeDate.Date.Add(SessionEnd);
                
                Draw.Line(this, $"MidnightOpen_{currentTradeDate:yyyyMMdd}", 
                    false, startOfDay, midnightOpen, endOfDay, midnightOpen, 
                    Brushes.Green, DashStyleHelper.Solid, 2);
                
                midnightLineDrawn = true;
            }
        }

        private void DrawStopAndTargetLevels(double stopPrice, double targetPrice, string entryType)
        {
            // Draw 5 dots for stop loss level in black
            for (int i = 0; i < 5; i++)
            {
                Draw.Dot(this, $"StopLevel_{entryType}_{i}_{CurrentBar}", 
                    false, i, stopPrice, Brushes.Black);
            }
            
            // Draw 5 dots for take profit level in black
            for (int i = 0; i < 5; i++)
            {
                Draw.Dot(this, $"TargetLevel_{entryType}_{i}_{CurrentBar}", 
                    false, i, targetPrice, Brushes.Black);
            }
        }

        // ==================== LIFECYCLE ====================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Simple ES Strategy - Basic Entry Only with Visual Elements";
                Name = "SimpleES";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
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
            }
            else if (State == State.Configure)
            {
                // 5-minute secondary series
                AddDataSeries(BarsPeriodType.Minute, 5);
            }
            else if (State == State.DataLoaded)
            {
                // ATR indicator on 5-min series
                atr5 = ATR(BarsArray[1], AtrPeriod);
            }
        }

        protected override void OnBarUpdate()
        {
            // Work only on primary series updates
            if (BarsInProgress != 0 || CurrentBars[0] < 20 || CurrentBars[1] < 20)
                return;

            // New trading day reset
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

            // ----- Build Opening Range -----
            if (inOR && tradingDay)
            {
                // Capture start time on first OR bar
                if (orStartTime == DateTime.MinValue)
                    orStartTime = Times[1][0];

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
                
                // Capture end time
                orEndTime = Times[1][0];
            }

            // Draw range box when OR is complete
            if (!inOR && rangeHigh > 0 && rangeLow > 0 && !rangeBoxDrawn && tradingDay)
            {
                DrawRangeBox();
            }

            // ----- Capture midnight open -----
            if (inMidnightWindow && tradingDay && midnightOpen == 0)
            {
                midnightOpen = Opens[1][0];
                midnightTimeStamp = Times[1][0];
                
                // Draw the midnight open line
                DrawMidnightOpenLine();
            }

            // ----- Confirmations -----
            bool rangeSizeOk = (rangeHigh > 0 && rangeLow > 0) &&
                               ((rangeHigh - rangeLow) >= MinRangeSize);

            bool midnightFilterOk = !EnableMidnightFilter
                ? true
                : (midnightOpen > 0 && rangeHigh > 0 &&
                   (rangeHigh >= (midnightOpen + MidnightPointsAbove)));

            // ----- Entry conditions -----
            bool longOK =
                (TradeDirection == TradingDirection.LongOnly || TradeDirection == TradingDirection.Both) &&
                inSession && tradingDay && rangeHigh > 0 && rangeLow > 0 &&
                Close[0] > rangeHigh && Position.MarketPosition == MarketPosition.Flat &&
                rangeSizeOk && midnightFilterOk && !tradeTakenToday;

            bool shortOK =
                (TradeDirection == TradingDirection.ShortOnly || TradeDirection == TradingDirection.Both) &&
                inSession && tradingDay && rangeHigh > 0 && rangeLow > 0 &&
                Close[0] < rangeLow && Position.MarketPosition == MarketPosition.Flat &&
                rangeSizeOk && midnightFilterOk && !tradeTakenToday;

            // ==================== ENTRIES ====================
            if (longOK)
            {
                EnterLong(Contracts, "Long");

                // Set initial stop loss and take profit
                double sl = Close[0] - atr5[0] * AtrMultiplier;
                double tp = Close[0] + atr5[0] * AtrMultiplier * TpMultiplier;

                SetStopLoss("Long", CalculationMode.Price, sl, false);
                SetProfitTarget("Long", CalculationMode.Price, tp);

                // Draw stop and target level dots
                DrawStopAndTargetLevels(sl, tp, "Long");

                entryPrice = Close[0];
                tradeTakenToday = true;
                breakevenMoved = false;
            }

            if (shortOK)
            {
                EnterShort(Contracts, "Short");

                // Set initial stop loss and take profit
                double sl = Close[0] + atr5[0] * AtrMultiplier;
                double tp = Close[0] - atr5[0] * AtrMultiplier * TpMultiplier;

                SetStopLoss("Short", CalculationMode.Price, sl, false);
                SetProfitTarget("Short", CalculationMode.Price, tp);

                // Draw stop and target level dots
                DrawStopAndTargetLevels(sl, tp, "Short");

                entryPrice = Close[0];
                tradeTakenToday = true;
                breakevenMoved = false;
            }

            // ==================== BREAKEVEN STOP ====================
            if (EnableBreakeven && Position.MarketPosition != MarketPosition.Flat && !breakevenMoved && entryPrice > 0)
            {
                double currentProfit = 0;
                
                if (Position.MarketPosition == MarketPosition.Long)
                    currentProfit = Close[0] - entryPrice;
                else if (Position.MarketPosition == MarketPosition.Short)
                    currentProfit = entryPrice - Close[0];

                // Move stop to breakeven when profit reaches configured ATR multiple
                if (currentProfit >= (atr5[0] * BreakevenTriggerATR))
                {
                    if (Position.MarketPosition == MarketPosition.Long)
                        SetStopLoss("Long", CalculationMode.Price, entryPrice, false);
                    else
                        SetStopLoss("Short", CalculationMode.Price, entryPrice, false);
                    
                    breakevenMoved = true;
                }
            }
        }
    }
}
