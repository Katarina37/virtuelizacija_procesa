using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WeatherStationCommon;
using System.IO;
using System.ServiceModel;
using System.Configuration;


namespace WeatherStationServer
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class WeatherService : IServiceContract, IDisposable
    {
        private string _sessionId;
        private StreamWriter _measurementsWriter;
        private StreamWriter _rejectsWriter;
        private string _dataDirectory;
        private bool _disposed = false;

        //zadatak 8
        public event EventHandler<string> OnTransferStarted;
        public event EventHandler<WeatherSample> OnSampleReceived;
        public event EventHandler<string> OnTransferCompleted;
        public event EventHandler<string> OnWarningRaised;

        //zadatak 9
        private double _previousSh = double.NaN;
        private double _shMean = 0;
        private int _sampleCount = 0;
        private double _shThreshold;

        //zadatak 10
        private double _previousHi = double.NaN;
        private double _hiThreshold;


        protected virtual void RaiseOnTransferStarted(string sessionId)
        {
            Console.WriteLine($"Transfer started: {sessionId}");
            OnTransferStarted?.Invoke( this, sessionId );
        }

        protected virtual void RaiseOnSampleReceived(WeatherSample sample)
        {
            Console.WriteLine($"Sample received: T={sample.T}, Sh={sample.Sh}");
            OnSampleReceived?.Invoke( this, sample );
        }

        protected virtual void RaiseOnTransferCompleted(string sessionId)
        {
            Console.WriteLine($"Transfer completed: {sessionId}");
            OnTransferCompleted?.Invoke( this, sessionId );
        }

        protected virtual void RaiseOnWarningRaised(string warning)
        {
            Console.WriteLine($"Warning: {warning}");
            OnWarningRaised?.Invoke( this, warning );
        }

        public WeatherService()
        {
            _dataDirectory = ConfigurationManager.AppSettings["DataDirectory"];
            _shThreshold = double.Parse(ConfigurationManager.AppSettings["SH_threshold"]);
            _hiThreshold = double.Parse(ConfigurationManager.AppSettings["HI_max_threshold"]);
            Directory.CreateDirectory(_dataDirectory);
        }

        public string StartSession(SessionMetadata meta)
        {
            try
            {
                if (string.IsNullOrEmpty(meta.StationId))
                    throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "StationId is required" });
                
                if(meta.ExpectedSamples <= 0)
                    throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "ExpectedSamples must be greater than 0" });

                _sessionId = $"{meta.StationId}_{meta.StartTime:yyyyMMdd_HHmmss}";

                string measurementsFile = Path.Combine(_dataDirectory, $"{_sessionId}_measurements.csv");
                string rejectsFile = Path.Combine(_dataDirectory, $"{_sessionId}_rejects.csv");

                _measurementsWriter = new StreamWriter(measurementsFile, true);
                _rejectsWriter = new StreamWriter(rejectsFile, true);

                _measurementsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date");
                _rejectsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date,Reason");

                Console.WriteLine($"Session started: {_sessionId}");
                Console.WriteLine($"Measurement file: {measurementsFile}");
                Console.WriteLine($"Rejects file: {rejectsFile}");

                RaiseOnTransferStarted(_sessionId);

                return _sessionId;
            }
            catch (Exception ex)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = ex.Message });
            }
        }

        public bool PushSample(WeatherSample sample)
        {
            try
            {
                if (sample.Date == DateTime.MinValue)
                    throw new FaultException<ValidationFault>(new ValidationFault { Message = "Invalid date"});

                if (sample.Sh <= 0)
                    throw new FaultException<ValidationFault>(new ValidationFault { Message = "Specific humidity must be positive"});

                if (sample.Sh < 0 || sample.Sh > 100)
                    throw new FaultException<ValidationFault>(new ValidationFault { Message = "Relative humidity must be between 0 and 100"});

                AnalyzeSpecificHumidity(sample);
                AnalyzeHeatIndex(sample);

                _measurementsWriter.WriteLine($"{sample.T},{sample.Tpot},{sample.Tdew},{sample.Sh},{sample.Rh},{sample.Date:o}");
                _measurementsWriter.Flush();

                Console.WriteLine($"Sample received: {sample.Date}");

                RaiseOnSampleReceived(sample);

                return true;
            }
            catch (FaultException) 
            {
                throw;
            }
            catch(Exception ex)
            {
                _rejectsWriter.WriteLine($"{sample.T},{sample.Tpot},{sample.Tdew},{sample.Sh},{sample.Rh},{sample.Date:o},{ex.Message}");
                _rejectsWriter.Flush();

                Console.WriteLine($"Rejected sample: {ex.Message}");

                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = ex.Message });
            }
        }

        public bool EndSession(string sessionId)
        {
            try
            {
                if (_sessionId != sessionId)
                    throw new FaultException<DataFormatFault>(new DataFormatFault { Message = "Invalid session ID"});

                DisposeWriters();
                RaiseOnTransferCompleted(sessionId);

                return true;
            }
            catch(Exception ex)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault { Message = ex.Message});
            }
        }

        private void AnalyzeSpecificHumidity(WeatherSample sample)
        {
            _sampleCount++;
            _shMean = _shMean + (sample.Sh - _shMean) / _sampleCount;

            if (!double.IsNaN(_previousSh))
            {
                double delthaSh = sample.Sh - _previousSh;

                if(Math.Abs(delthaSh) > _shThreshold)
                {
                    string direction = delthaSh > 0 ? "above" : "below";
                    string message = $"SH spike detected: {Math.Abs(delthaSh):F2} ({direction} threshold)";
                    RaiseOnWarningRaised(message);
                }
                double lowerBound = 0.75 * _shMean;
                double upperBound = 1.25 * _shMean;

                if(sample.Sh < lowerBound)
                {
                    string message = $"SH below expected range: {sample.Sh:F2} < {lowerBound:F2} (25% below mean)";
                    RaiseOnWarningRaised(message);
                }else if(sample.Sh > upperBound)
                {
                    string message = $"SH above expected range: {sample.Sh:F2} > {upperBound:F2} (25% above mean)";
                    RaiseOnWarningRaised(message);
                }
            }
            _previousSh = sample.Sh;
        }

        private double CalculateHeatIndex(double temperature, double humidity)
        {
            return -8.78 + 1.61 * temperature + 2.34 * humidity
                - 0.15 * temperature * humidity - 0.01 * Math.Pow(temperature, 2)
                - 0.02 * Math.Pow(humidity, 2) + 0.00 * Math.Pow(temperature, 2) * humidity
                + 0.00 * temperature * Math.Pow(humidity, 2)
                - 0.00 * Math.Pow(temperature, 2) * Math.Pow(humidity, 2);
        }

        private void AnalyzeHeatIndex(WeatherSample sample)
        {
            double hi = CalculateHeatIndex(sample.T, sample.Rh);

            if(hi > _hiThreshold)
            {
                string message = $"Heat index exceeded threshold: {hi:F2} > {_hiThreshold}";
                RaiseOnWarningRaised(message);
            }

            if (!double.IsNaN(_previousHi))
            {
                double deltaHi = hi - _previousHi;

                if(Math.Abs(deltaHi) > _hiThreshold / 2)
                {
                    string direction = deltaHi > 0 ? "above" : "below";
                    string message = $"HI spike detected: {Math.Abs(deltaHi):F2} ({direction} expected)";
                    RaiseOnWarningRaised(message);
                }
            }
            _previousHi = hi;
        }

        private void DisposeWriters()
        {
            _measurementsWriter?.Close();
            _measurementsWriter?.Dispose();

            _rejectsWriter?.Close();
            _rejectsWriter?.Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    DisposeWriters();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~WeatherService()
        {
            Dispose(false);
        }
    }
}
