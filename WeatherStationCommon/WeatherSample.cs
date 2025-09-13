using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WeatherStationCommon
{
    [DataContract]
    public class WeatherSample
    {
        [DataMember]
        public double T {  get; set; }

        [DataMember]
        public double Tpot { get; set; }

        [DataMember]
        public double Tdew { get; set; }

        [DataMember]
        public double Sh {  get; set; }

        [DataMember]
        public double Rh { get; set; }

        [DataMember]
        public DateTime Date { get; set; }
    }
}
