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
using QuantConnect.Configuration;
using QuantConnect.ToolBox.YahooDownloader;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Algoloop.Provider
{
    public class Yahoo : IProvider
    {
        private bool _isDisposed;

        public void Register(SettingModel settings)
        {
        }

        public void Download(MarketModel model, SettingModel settings)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Config.Set("log-handler", "QuantConnect.Logging.CompositeLogHandler");
            Config.Set("data-directory", settings.DataFolder);

            IList<string> symbols = model.Symbols.Select(m => m.Id).ToList();
            string resolution = Resolution.Daily.ToString(); // Yahoo only support daily
            YahooDownloaderProgram.YahooDownloader(symbols, resolution, model.LastDate, model.LastDate);
        }

        public void Abort()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
