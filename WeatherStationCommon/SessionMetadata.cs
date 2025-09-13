using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WeatherStationCommon
{
    [DataContract]
    public  class SessionMetadata
    {
        [DataMember]
        public string StationId { get; set; }

        [DataMember]
        public DateTime StartTime { get; set; }

        [DataMember]
        public int ExpectedSamples { get; set; }

    }
}
