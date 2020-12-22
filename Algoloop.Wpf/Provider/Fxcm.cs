﻿/*
 * Copyright 2019 Capnode AB
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

using Algoloop.Model;
using QuantConnect;
using QuantConnect.Brokerages.Fxcm;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Algoloop.Provider
{
    public class Fxcm : ProviderBase
    {
        private readonly DateTime _firstDate = new DateTime(2003, 05, 05);

        public override void Download(MarketModel market, SettingModel settings)
        {
            if (market == null) throw new ArgumentNullException(nameof(market));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            IList<string> symbols = market.Symbols.Where(x => x.Active).Select(m => m.Id).ToList();
            if (!symbols.Any())
            {
                market.Active = false;
                UpdateSymbols(market);
                return;
            }

            string resolution = market.Resolution.Equals(Resolution.Tick) ? "all" : market.Resolution.ToString();
            DateTime fromDate = market.LastDate.Date.AddDays(1);
            if (fromDate >= DateTime.Today)
            {
                // Do not download today data
                market.Active = false;
                return;
            }

            if (fromDate < _firstDate)
            {
                fromDate = _firstDate;
            }

            DateTime toDate = fromDate;
            string from = fromDate.ToString("yyyyMMdd-HH:mm:ss", CultureInfo.InvariantCulture);
            string to = toDate.ToString("yyyyMMdd-HH:mm:ss", CultureInfo.InvariantCulture);
            string[] args =
            {
                "--app=FxcmDownloader",
                $"--from-date={from}",
                $"--to-date={to}",
                $"--resolution={resolution}",
                $"--tickers={string.Join(",", symbols)}"
            };

            IDictionary<string, string> config = new Dictionary<string, string>
            {
                ["data-directory"] = settings.DataFolder,
                ["data-folder"] = settings.DataFolder,
                ["fxcm-user-name"] = market.Login,
                ["fxcm-password"] = market.Password,
                ["fxcm-terminal"] = market.Access.ToStringInvariant()
            };

            // Download active symbols
            bool ok = base.RunProcess("QuantConnect.ToolBox.exe", args, config);
            if (!ok)
            {
                market.Active = false;
                return;
            }

            market.LastDate = toDate;
            UpdateSymbols(market);
        }

        private void UpdateSymbols(MarketModel market)
        {
            List<Symbol> symbols = FxcmSymbolMapper.KnownSymbols;
            IEnumerable<SymbolModel> actual = symbols.Select(
                m => new SymbolModel(m.ID.Symbol, m.ID.Market, m.ID.SecurityType) { Active = false } );
            UpdateSymbols(market, actual, false);
        }
    }
}
