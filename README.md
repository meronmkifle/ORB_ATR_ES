# ES Opening Range Breakout Strategy

A NinjaTrader 8 strategy for trading E-mini S&P 500 futures using opening range breakouts with automated risk management.

## Overview

Trades breakouts from the 9:35-9:45 AM opening range. Goes long above the range high, short below the range low. Default is Longs only. Uses ATR-based stops and targets that adapt to volatility.

## Key Features

- **Position scaling**: Adds contracts to winning trades, moves stop to breakeven
- **Trailing stops**: Protects profits once trade reaches activation threshold  
- **Partial profit taking**: Systematically locks in gains
- **Smart filters**: Minimum range size, midnight level filter, day-of-week selection
- **Daily reset**: Fresh start each trading session

## Technical Details

Runs on primary timeframe using 5-minute bars for calculations. Fully automated order management handles entries, exits, scaling, and stops. Visual components display range levels and reference points.

## Configuration

All parameters are adjustable - ATR periods, multipliers, scaling thresholds, trading hours, and filter settings. Designed for backtesting and optimization.

## Usage

Built for day trading ES futures algorithmically. Load sufficient historical data, configure risk parameters, and let it run. Practice proper position sizing and understand the risks before trading live.

Markets change - past performance doesn't guarantee future results.

## Any emquireies :  meronmkifle@gmail.com
