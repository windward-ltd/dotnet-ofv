using Newtonsoft.Json;

namespace TrackedShipmentsAPI.Models
{   
    public class TrackedShipmentModel
    {
        [JsonProperty("containerNumber")]
        public string? containerNumber { get; set; }

        [JsonProperty("scac")]
        public string? scac { get; set; }

        [JsonProperty("bol")]
        public string? bol { get; set; }

        [JsonProperty("carrierBookingReference")]
        public string? carrierBookingReference { get; set; }

        [JsonProperty("metadata")]
        public MetadataModel? metadata { get; set; } = new MetadataModel();
    }

    public class MetadataModel
    {
        [JsonProperty("jobNumber")]
        public string? jobNumber { get; set; }
    }
}