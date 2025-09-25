# Simple ES Opening Range Breakout Strategy
A NinjaTrader 8 strategy for trading E-mini S&P 500 futures using opening range breakouts with automated risk management.

# Overview
Trades breakouts from the 9:35-9:45 AM opening range. Goes long above the range high, short below the range low. Default is long only. Uses ATR-based stops and targets with automatic breakeven protection and force close.

# Technical Details
Runs on primary timeframe using 5-minute bars for calculations. Fully automated order management handles entries, exits, and stop adjustments. Visual components include blue range box, green midnight line, and red dashed ATR levels.

# Configuration
All parameters are adjustable - ATR periods, multipliers, trading hours, contract size, and filter settings. Designed for backtesting and optimization with extensive customization options.

# Usage
Built for day trading ES futures algorithmically. Load sufficient historical data, configure risk parameters, and let it run. Practice proper position sizing and understand the risks before trading live.

Markets change - past performance doesn't guarantee future results.

Any inquiries: https://www.linkedin.com/in/meronmkifle/
