/*
 * Copyright 2022 Capnode AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Securities;
using QuantConnect.Statistics;

namespace Algoloop.Algorithm.CSharp.Model
{
    internal class TrackerPortfolio : PortfolioConstructionModel
    {
        private const decimal InitialCash = 100;
        private readonly bool LogOrder = false;

        private readonly int _slots;
        private readonly decimal _rebalance;
        private decimal _cash = InitialCash;
        private List<Trade> _trades = new List<Trade>();
        private IDictionary<Symbol, Trade> _holdings = new Dictionary<Symbol, Trade>();
        private Insight[] _queue;

        public TrackerPortfolio(int slots, decimal rebalance)
        {
            _slots = slots;
            _rebalance = rebalance;
        }

        // Create list of PortfolioTarget objects from Insights
        public override IEnumerable<IPortfolioTarget> CreateTargets(QCAlgorithm algorithm, Insight[] actualInsights)
        {
            if (actualInsights == null)
            {
                LiquidateHoldings(algorithm);
                return null;
            }

            // Trade on yesterday insights
            Insight[] insights = _queue;
            _queue = actualInsights;
            if (insights == null) return null;

            IEnumerable<Insight> toplist = insights.Take(_slots);
//            LogInsights(algorithm, toplist);

            // Exit position if not i toplist
            foreach (KeyValuePair<Symbol, Trade> holding in _holdings.ToArray())
            {
                if (toplist.Any(m => m.Symbol.Equals(holding.Key))) continue;
                Security security = algorithm.Securities[holding.Key];
                Trade trade = holding.Value;
                trade.ExitPrice = security.Open;
                trade.ExitTime = algorithm.Time;
                decimal value = trade.ExitPrice * trade.Quantity;
                _trades.Add(trade);
                _cash += value;
                if (!_holdings.Remove(holding.Key)) throw new ApplicationException($"Can not remove {holding.Key}");
                if (LogOrder)
                {
                    algorithm.Log($"Sell {holding.Key} {trade.Quantity:0.0000} @ {trade.ExitPrice:0.00} cash={_cash:0.00}");
                }
            }

            // Add new position if new in toplist
            int freeSlots = _slots - _holdings.Count;
            decimal modelSize = InitialCash / _slots;
            decimal size = freeSlots > 0 ? Math.Min(modelSize, _cash / freeSlots) : 0;
            foreach (Insight insight in toplist)
            {
                if (_holdings.ContainsKey(insight.Symbol)) continue;
                if (size == 0) continue;
                Security security = algorithm.Securities[insight.Symbol];
                decimal target = size / security.Open;
                _cash -= size;
                var trade = new Trade
                {
                    Symbol = insight.Symbol,
                    EntryTime = algorithm.Time,
                    EntryPrice = security.Open,
                    Quantity = target,
                };

                _holdings.Add(insight.Symbol, trade);
                if (LogOrder)
                {
                    algorithm.Log($"Buy {trade.Symbol} {trade.Quantity:0.0000} @ {trade.EntryPrice:0.00} cash={_cash:0.00}");
                }
            }

            // Rebalance portfolio
            foreach (Insight insight in toplist)
            {
                Security security = algorithm.Securities[insight.Symbol];
                decimal modelTarget = modelSize / security.Open;
                if (_holdings.TryGetValue(insight.Symbol, out Trade trade))
                {
                    if (_rebalance > 0 && trade.Quantity <= (1 - _rebalance) * modelTarget)
                    {
                        // Rebalance up
                        decimal target = Math.Min(modelTarget, _cash / security.Open);
                        decimal diff = target - trade.Quantity;
                        decimal value = security.Open * diff;
                        _cash -= value;
                        trade.Quantity = target;
                        value += trade.Quantity * trade.EntryPrice;
                        trade.EntryPrice = value / target;
                        trade.Quantity = target;
                        if (LogOrder)
                        {
                            algorithm.Log($"Buy rebalance {insight.Symbol} {diff:0.0000} @ {security.Open:0.00} cash={_cash:0.00}");
                        }
                    }
                    else if (_rebalance > 0 && trade.Quantity >= (1 + _rebalance) * modelTarget)
                    {
                        // Rebalance down
                        decimal diff = modelTarget - trade.Quantity;
                        decimal value = security.Open * diff;
                        trade.Quantity = modelTarget;
                        _cash -= value;
                        var sellTrade = new Trade
                        {
                            Symbol = insight.Symbol,
                            EntryTime = trade.EntryTime,
                            EntryPrice = trade.EntryPrice,
                            Quantity = -diff,
                            ExitTime = algorithm.Time,
                            ExitPrice = security.Open,
                        };
                        _trades.Add(sellTrade);

                        if (LogOrder)
                        {
                            algorithm.Log($"Sell rebalance {sellTrade.Symbol} {sellTrade.Quantity:0.0000} @ {sellTrade.ExitPrice:0.00} cash={_cash:0.00}");
                        }
                    }
                }
            }

            if (_holdings.Count > _slots) throw new ApplicationException("Too many positions");
            if (_cash < 0) throw new ApplicationException($"Negative balance {_cash:0.00}");
            return null;
        }

        public decimal GetEquity(QCAlgorithm algorithm)
        {
            decimal equity = _cash;
            foreach (KeyValuePair<Symbol, Trade> holding in _holdings)
            {
                Security security = algorithm.Securities[holding.Key];
                decimal value = security.Price * holding.Value.Quantity;
                equity += value;
            }

            return equity.SmartRounding();
        }

        private void LiquidateHoldings(QCAlgorithm algorithm)
        {
            if (LogOrder)
            {
                algorithm.Log("Liquidate holdings:");
            }

            // Liquidate holdings
            foreach (KeyValuePair<Symbol, Trade> holding in _holdings)
            {
                Security security = algorithm.Securities[holding.Key];
                Trade trade = holding.Value;
                trade.ExitPrice = security.Close;
                trade.ExitTime = algorithm.UtcTime;
                decimal profit = trade.ExitPrice * trade.Quantity;
                _cash += profit;
                _trades.Add(trade);
                if (LogOrder)
                {
                    algorithm.Log($"Sell {holding.Key} {trade.Quantity:0.0000} @ {trade.ExitPrice:0.00} cash={_cash:0.00}");
                }
            }
            _holdings.Clear();

            decimal cash = InitialCash;
            foreach (Trade trade in _trades)
            {
                decimal profit = trade.Quantity * (trade.ExitPrice - trade.EntryPrice);
                cash += profit;
                if (LogOrder)
                {
                    algorithm.Log($"Trade {trade.EntryTime.ToShortDateString()} {trade.ExitTime.ToShortDateString()} {trade.Symbol} Size={trade.Quantity:0.0000} Entry={trade.EntryPrice:0.00} Exit={trade.ExitPrice:0.00} Profit={profit:0.0000}");
                }
            }

            if (LogOrder)
            {
                algorithm.Log($"Summary _cash={_cash:0.00} cash={cash:0.00} diff={cash - _cash:0.00}");
            }
        }
    }
}
