/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
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
 *
*/

using System;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using System.Collections.Generic;
using QuantConnect.Securities.Option.StrategyMatcher;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Regression algorithm exercising an equity Call Calendar Spread option strategy and asserting it's being detected by Lean and works as expected
    /// </summary>
    public class OptionEquityCallCalendarSpreadRegressionAlgorithm : OptionEquityBaseStrategyRegressionAlgorithm
    {
        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="slice">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice slice)
        {
            if (!Portfolio.Invested)
            {
                OptionChain chain;
                if (IsMarketOpen(_optionSymbol) && slice.OptionChains.TryGetValue(_optionSymbol, out chain))
                {
                    var contracts = chain
                        .Where(contract => contract.Right == OptionRight.Call)
                        .OrderBy(x => x.Expiry)
                        .ThenBy(x => x.Strike)
                        .ToList();

                    var shortCall = contracts.First();
                    var longCall = contracts.First(contract => contract.Expiry > shortCall.Expiry && contract.Strike == shortCall.Strike);

                    var initialMargin = Portfolio.MarginRemaining;

                    MarketOrder(shortCall.Symbol, -10);
                    AssertOptionStrategyIsPresent(OptionStrategyDefinitions.NakedCall.Name, 10);
                    MarketOrder(longCall.Symbol, +10);

                    AssertOptionStrategyIsPresent(OptionStrategyDefinitions.CallCalendarSpread.Name, 10);

                    var freeMarginPostTrade = Portfolio.MarginRemaining;
                    var expectedMarginUsage = 0;
                    if (expectedMarginUsage != Portfolio.TotalMarginUsed)
                    {
                        throw new RegressionTestException("Unexpect margin used!");
                    }

                    // we payed the ask and value using the assets price
                    var priceSpreadDifference = GetPriceSpreadDifference(shortCall.Symbol, longCall.Symbol);
                    if (initialMargin != (freeMarginPostTrade + expectedMarginUsage + _paidFees - priceSpreadDifference))
                    {
                        throw new RegressionTestException("Unexpect margin remaining!");
                    }
                }
            }
        }

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        public override long DataPoints => 471135;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public override int AlgorithmHistoryDataPoints => 0;

        /// <summary>
        /// Final status of the algorithm
        /// </summary>
        public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public override Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Orders", "2"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "0%"},
            {"Drawdown", "0%"},
            {"Expectancy", "0"},
            {"Start Equity", "200000"},
            {"End Equity", "199287"},
            {"Net Profit", "0%"},
            {"Sharpe Ratio", "0"},
            {"Sortino Ratio", "0"},
            {"Probabilistic Sharpe Ratio", "0%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0"},
            {"Beta", "0"},
            {"Annual Standard Deviation", "0"},
            {"Annual Variance", "0"},
            {"Information Ratio", "0"},
            {"Tracking Error", "0"},
            {"Treynor Ratio", "0"},
            {"Total Fees", "$13.00"},
            {"Estimated Strategy Capacity", "$24000.00"},
            {"Lowest Capacity Asset", "GOOCV W78ZERHAOVVQ|GOOCV VP83T1ZUHROL"},
            {"Portfolio Turnover", "26.19%"},
            {"OrderListHash", "4b1bd9b5d8b0dba8ddaceeb1dfbcbbc9"}
        };
    }
}
