using Newtonsoft.Json;

namespace TrackedShipmentsAPI.Models
{
    public class WebhookModel
    {
        [JsonProperty("data")]
        public Shipment? shipment { get; set; }

        [JsonProperty("metadata")]
        public Metadata? metadata { get; set; }
    }

    public class Shipment
    {
        [JsonProperty("identifiers")]
        public Identifiers? identifiers { get; set; }

        [JsonProperty("initialCarrierETA")]
        public string? initialCarrierETA { get; set; }

        [JsonProperty("initialCarrierETD")]
        public string? initialCarrierETD { get; set; }

        [JsonProperty("predicted")]
        public ScheduleTimestamps? predicted { get; set; }

        [JsonProperty("milestones")]
        public List<Milestone>? milestones { get; set; }

        [JsonProperty("events")]
        public List<Event>? events { get; set; }

        [JsonProperty("vessels")]
        public List<Vessel>? vessels { get; set; }

        [JsonProperty("ports")]
        public List<Port>? ports { get; set; }
    }

    public class Metadata
    {
        [JsonProperty("sentAt")]
        public string? sentAt { get; set; }

        [JsonProperty("businessData")]
        public object? businessData { get; set; }
        
        [JsonProperty("jobNumber")]
        public string? jobNumber { get; set; }
    }

    public class Identifiers
    {
        [JsonProperty("trackedShipmentId")]
        public string? trackedShipmentId { get; set; }

        [JsonProperty("shipmentId")]
        public string? shipmentId { get; set; }

        [JsonProperty("containerNumber")]
        public string? containerNumber { get; set; }

        [JsonProperty("bolNumber")]
        public string? bolNumber { get; set; }

        [JsonProperty("carrierBookingReference")]
        public string? carrierBookingReference { get; set; }

        [JsonProperty("carrier")]
        public Carrier? carrier { get; set; }

        [JsonProperty("ISOEquipmentCode")]
        public string? ISOEquipmentCode { get; set; }
    }

    public class Carrier
    {
        [JsonProperty("SCAC")]
        public string? scac { get; set; }

        [JsonProperty("name")]
        public string? name { get; set; }
    }

    public class Status
    {
        [JsonProperty("description")]
        public string? description { get; set; }

        [JsonProperty("type")]
        public string? type { get; set; }
    }

    public class Event
    {
        [JsonProperty("description")]
        public string? description { get; set; }

        [JsonProperty("timestamps")]
        public Timestamps? timestamps { get; set; }

        [JsonProperty("portId")]
        public string? portId { get; set; }

        [JsonProperty("vesselId")]
        public string? vesselId { get; set; }
    }

    public class CarrierLatestStatus
    {
        [JsonProperty("status")]
        public Status? status { get; set; }

        [JsonProperty("voyage")]
        public string? voyage { get; set; }

        [JsonProperty("portId")]
        public string? portId { get; set; }

        [JsonProperty("vesselId")]
        public string? vesselId { get; set; }
    }

    public class Timestamps
    {
        [JsonProperty("datetime")]
        public string? datetime { get; set; }

        [JsonProperty("code")]
        public string? code { get; set; }
    }

    public class Port
    {
        [JsonProperty("name")]
        public string? name { get; set; }

        [JsonProperty("locode")]
        public string? locode { get; set; }

        [JsonProperty("country")]
        public string? country { get; set; }

        [JsonProperty("timezone")]
        public string? timezone { get; set; }

        [JsonProperty("coordinates")]
        public string[]? coordinates { get; set; }
    }

    public class Vessel
    {
        [JsonProperty("name")]
        public string? name { get; set; }

        [JsonProperty("imo")]
        public string? imo { get; set; }
    }

    public class Milestone
    {
        [JsonProperty("portId")]
        public string? portId { get; set; }

        [JsonProperty("type")]
        public string? type { get; set; }

        [JsonProperty("utcOffset")]
        public string? utcOffset { get; set; }

        [JsonProperty("departure")]
        public MilestoneSchedule? departure { get; set; }

        [JsonProperty("arrival")]
        public MilestoneSchedule? arrival { get; set; }
    }

    public class MilestoneSchedule
    {
        [JsonProperty("voyage")]
        public string? voyage { get; set; }

        [JsonProperty("vesselId")]
        public string? vesselId { get; set; }

        [JsonProperty("timestamps")]
        public MilestoneScheduleTimestamps? timestamps { get; set; }
    }

    public class MilestoneScheduleTimestamps
    {
        [JsonProperty("carrier")]
        public Timestamps? carrier { get; set; }

        [JsonProperty("predicted")]
        public Timestamps? predicted { get; set; }
    }

    public class ScheduleTimestamps : Timestamps
    {
        [JsonProperty("diffFromInitialCarrierDays")]
        public int? diffFromInitialCarrierDays { get; set; }
    }
}