using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TrackedShipmentsAPI.Models;


namespace TrackedShipmentsAPI.Services
{
    public class WebhookService
    {
        public const string ACTUAL_CODE = "ACT";
        public const string PLANNED_CODE = "PLN";
        public const string ESTIMATED_CODE = "EST";
        public const string GATE_IN_AT_POL = "Gate in at first POL";
        public const string ARRIVAL_AT_POL = "Arrival at POL";
        public const string LOADED_AT_POL = "Loaded at first POL";
        public const string LOADED_AT_TSP = "Loaded at T/S port";
        public const string DISCHARGE_AT_TSP = "Discharge at T/S port";
        public const string DEPARTURE_FROM_TSP = "Departure from T/S port";
        public const string EMPTY_TO_SHIPPER = "Empty to shipper";
        public const string PICKUP_AT_SHIPPER = "Pickup at shipper";
        public const string GATE_OUT_OF_POD = "Gate out from final POD";
        public const string DISCHARGE_AT_POD = "Discharge at final POD";
        public const string EMPTY_RETURN = "Empty return to depot";
        public const string DELIVERY_TO_CONSIGNEE = "Delivery to consignee";
        public const string POL = "POL";
        public const string TSP = "TSP";
        public const string POD = "POD";
        public const int MIN_TSP_PHASES_INDEX = 4;
        public const int MIN_LEG_QUANTITY = 5;

        public Dictionary<string, string> wwEventsMapping = new Dictionary<string, string>
        {
            { GATE_IN_AT_POL, "origin" },
            { ARRIVAL_AT_POL, "pol_arrival" },
            { LOADED_AT_POL, "pol_loaded" },
            { EMPTY_TO_SHIPPER, "empty_pickup" },
            { PICKUP_AT_SHIPPER, "origin_pickup" },
            { GATE_OUT_OF_POD, "pod_departure" },
            { DISCHARGE_AT_POD, "pod_discharge" },
            { EMPTY_RETURN, "empty_return" },
            { DELIVERY_TO_CONSIGNEE, "dlv_delivery" },
        };

        // Converts json object to XML
        public XmlDocument? JsonToXML(string jsonData)
        {
            XmlDocument? doc = JsonConvert.DeserializeXmlNode(jsonData);

            return doc;
        }

        // Creates all leg data of at least 5 first legs (some can be empty if no data) to adhere to data structure
        public void AddLegsData(JObject json, Milestone[] milestones)
        {
            int maxLegLength = Math.Max(MIN_LEG_QUANTITY, milestones.Length - 1);
            JObject aggregatedJson = new JObject();

            for (int i = maxLegLength - 1; i >= 0; i--)
            {
                JObject legVesselObject = new JObject();
                legVesselObject["name"] = milestones[i]?.departure?.vessel?.name ?? "";
                legVesselObject["imo"] = milestones[i]?.departure?.vessel?.imo ?? "";
                legVesselObject["id"] = "";

                JObject legServiceObject = new JObject();
                legServiceObject["id"] = "";
                legServiceObject["name"] = "";
                legServiceObject["code"] = "";
                legServiceObject["direction"] = "";

                string prefix = $"leg{i + 1}";
                aggregatedJson[$"{prefix}_vessel"] = legVesselObject;
                aggregatedJson[$"{prefix}_voyage"] = milestones[i]?.departure?.voyage ?? "";
                aggregatedJson[$"{prefix}_service"] = legServiceObject;
            }

            JObject legsJson = JObject.Parse($@"
            {{
                ""Root"": {{
                    ""container"": {{
                        ""shipment"": {aggregatedJson.ToString()}
                    }}
                }}
            }}");

            json.Merge(legsJson); 
        }

        // Returns actual or estimated datetime of an event
        public string? GetEventData(Event[] events, string description, bool actualField)
        {
            Event? matchedEvent = events?.FirstOrDefault((Event item) => item?.description == description);

            return GetDateByActual(matchedEvent?.timestamps, actualField);
        }

        public void AddLocData(JObject json, Port? port, string fieldsPrefix)
        {
            JObject itemObject = new JObject();
            JObject aggregatedJson = new JObject();

            itemObject["name"] = port?.properties?.name ?? "";
            itemObject["locode"] = port?.properties?.locode ?? "";
            itemObject["timezone"] = port?.properties?.timezone ?? "";
            itemObject["latitude"] = port?.properties?.centroid?.geometry?.coordinates?[1] ?? "";
            itemObject["longitude"] = port?.properties?.centroid?.geometry?.coordinates?[0] ?? "";
            itemObject["city"] = "";
            itemObject["state"] = "";
            itemObject["country"] = port?.properties?.country ?? "";
            itemObject["country_iso2"] = "";
            itemObject["country_iso3"] = "";

            aggregatedJson[$"{fieldsPrefix}_loc"] = itemObject;

            JObject locJson = JObject.Parse($@"
            {{
                ""Root"": {{
                    ""container"": {{
                        ""shipment"": {aggregatedJson.ToString()}
                    }}
                }}
            }}");

            json.Merge(locJson);     
        }

        // Adds all non TSP related events data regarding actual or estimated datetime to json
        public void AddEventsData(JObject json, Event[] events)
        {
            Event[] filteredEvents = events
                .Where((item) => item?.description != DISCHARGE_AT_TSP && item?.description != DEPARTURE_FROM_TSP && item?.description != LOADED_AT_TSP)
                .ToArray();

            JObject parentJson = new JObject();
            JObject aggregatedEventsJson = new JObject();
            JObject eventLocJson = new JObject();

            foreach (string key in wwEventsMapping.Keys)
            {
                string prefix = wwEventsMapping[key];

                aggregatedEventsJson[$"{prefix}_planned_initial"] = "";
                aggregatedEventsJson[$"{prefix}_actual"] = GetEventData(events, key, true);
                aggregatedEventsJson[$"{prefix}_planned_last"] = GetEventData(events, key, false);

                Event? matchingEventByKey = filteredEvents.FirstOrDefault((item) => item.description == key);
                if (matchingEventByKey?.description == ARRIVAL_AT_POL)
                {
                    aggregatedEventsJson[$"pol_vsldeparture_planned_last"] = GetEventData(events, key, false);
                }

                AddLocData(json, matchingEventByKey?.port, prefix);
            }
            
            JObject eventsJson = JObject.Parse($@"
            {{
                ""Root"": {{
                    ""container"": {{
                        ""shipment"": {aggregatedEventsJson.ToString()}
                    }}
                }}
            }}");

            json.Merge(eventsJson);
        }

        public string? GetDateByActual(Timestamps? timestamps, bool actualField) {
            string? datetime = timestamps?.datetime;
            bool isActual = timestamps?.code == ACTUAL_CODE;

            if (actualField) {
                return isActual ? datetime : "";
            }

            return isActual ? "" : datetime;
        }

        public void AddTransshipmentsData(JObject json, Milestone[] milestones, Event[] events)
        {
            Milestone[] tspMilestones = milestones
                .Where((item) => item.type == TSP)
                .ToArray();

            Event[] tspEvents = events
                .Where((item) => item?.description == DISCHARGE_AT_TSP || item?.description == LOADED_AT_TSP)
                .ToArray();

            JObject aggregatedTransshipmentsJson = new JObject();

            for (int i = 0; i <= Math.Min(tspMilestones.Length - 1, 4); i++)
            {
                Milestone? currentIteratedMilestone = tspMilestones[i];

                Event? dischargeEvent = tspEvents.FirstOrDefault((item) => currentIteratedMilestone?.arrival?.vessel?.name == item.vessel?.name && item.description == DISCHARGE_AT_TSP);
                Event? loadedEvent = tspEvents.FirstOrDefault((item) => currentIteratedMilestone?.departure?.vessel?.name == item.vessel?.name && item.description == LOADED_AT_TSP);

                string prefix = $"tsp{i + 1}";

                aggregatedTransshipmentsJson[$"{prefix}_vslarrival_planned_initial"] = "";
                aggregatedTransshipmentsJson[$"{prefix}_vslarrival_planned_last"] = GetDateByActual(currentIteratedMilestone?.arrival?.timestamps?.carrier, false);
                aggregatedTransshipmentsJson[$"{prefix}_vslarrival_actual"] = GetDateByActual(currentIteratedMilestone?.arrival?.timestamps?.carrier, true);
                aggregatedTransshipmentsJson[$"{prefix}_vslarrival_detected"] = GetDateByActual(currentIteratedMilestone?.arrival?.timestamps?.predicted, true);
                aggregatedTransshipmentsJson[$"{prefix}_vslarrival_prediction"] = "";
                aggregatedTransshipmentsJson[$"{prefix}_discharge_planned_initial"] = "";
                aggregatedTransshipmentsJson[$"{prefix}_discharge_planned_last"] = GetDateByActual(dischargeEvent?.timestamps, false);
                aggregatedTransshipmentsJson[$"{prefix}_discharge_actual"] = GetDateByActual(dischargeEvent?.timestamps, true);
                aggregatedTransshipmentsJson[$"{prefix}_loaded_planned_initial"] = "";
                aggregatedTransshipmentsJson[$"{prefix}_loaded_planned_last"] = GetDateByActual(loadedEvent?.timestamps, false);
                aggregatedTransshipmentsJson[$"{prefix}_loaded_actual"] = GetDateByActual(loadedEvent?.timestamps, true);
                aggregatedTransshipmentsJson[$"{prefix}_vsldeparture_planned_initial"] = "";
                aggregatedTransshipmentsJson[$"{prefix}_vsldeparture_planned_last"] = GetDateByActual(currentIteratedMilestone?.departure?.timestamps?.carrier, false);
                aggregatedTransshipmentsJson[$"{prefix}_vsldeparture_actual"] = GetDateByActual(currentIteratedMilestone?.departure?.timestamps?.carrier, true);
                aggregatedTransshipmentsJson[$"{prefix}_vsldeparture_detected"] = GetDateByActual(currentIteratedMilestone?.departure?.timestamps?.predicted, true);

                AddLocData(json, currentIteratedMilestone?.port, prefix);
            }

            JObject eventsJson = JObject.Parse($@"
            {{
                ""Root"": {{
                    ""container"": {{
                        ""shipment"": {aggregatedTransshipmentsJson.ToString()}
                    }}
                }}
            }}");

            json.Merge(eventsJson); 
        }

        // Aggregates the relevant data from all functions to the JSON
        public string AddDataToJSON(dynamic data, string sentAt)
        {
            Event[] events = data?.shipment?.status?.events?.ToObject<Event[]>() ?? new Event[1];
            Milestone[] milestones = data?.shipment?.status?.milestones?.ToObject<Milestone[]>() ?? new Milestone[1];

            string? shipmentId = data?.shipment?.id;

            string? podVesselArrivalDetected = data?.shipments?.status?.predicted?.code == ACTUAL_CODE ? data?.shipments?.status?.predicted?.datetime : "";

            Milestone? podLocMilestone = milestones.FirstOrDefault((Milestone milestone) => milestone?.type == POD);
            Milestone? polLocMilestone = milestones.FirstOrDefault((Milestone milestone) => milestone?.type == POL);

            Vessel? currentVessel = data?.shipment?.status?.currentEvent?.vessel;

            // Parse the JSON structure
            JObject json = JObject.Parse($@"
            {{
                ""Root"": {{
                    ""container"": {{
                        ""event"": {{
                            ""id"": """",
                            ""shipment_id"": ""{shipmentId ?? ""}"",
                            ""master_shipment_id"": """",
                            ""master_shipment_leg_id"": """",
                            ""details"": {{
                                ""new_status"": """",
                                ""new_status_verbose"": ""{data?.shipment?.status?.currentEvent?.description}"",
                                ""message"": """"
                            }},
                            ""shipment"": """",
                            ""code"": """",
                            ""severity"": """",
                            ""walltime"": """",
                            ""created"": ""{sentAt}""
                        }},
                        ""shipment"": {{
                            ""id"": ""{shipmentId ?? ""}"",
                            ""shipmentsubscription_status"": """",
                            ""status_verbose"": """",
                            ""current_vessel_nextport"": """",
                            ""current_vessel_position"": """",
                            ""current_vessel"": ""{currentVessel?.name ?? ""}"",
                            ""container_type_iso"": ""{data?.shipment?.container?.isoCode ?? ""}"",
                            ""container_type_str"": """",
                            ""shipmentsubscription_status_verbose"": """",
                            ""shipmentsubscription_on_hold"": """",
                            ""shipmentsubscription_id"": """",
                            ""lifecycle_status_verbose"": """",
                            ""carrier_name"": ""{data?.shipment?.carrier?.longName ?? ""}"",
                            ""carrier_scac"": ""{data?.shipment?.carrier?.code ?? ""}"",
                            ""container_number"": ""{data?.shipment?.container?.number ?? ""}"",
                            ""descriptive_name"": """",
                            ""shipmentsubscription_descriptive_name"": """",
                            ""transport_modes_verbose"": """",
                            ""transport_modes"": """",
                            ""url"": """",
                            ""shipmentsubscription"": """",       
                            ""availability_loc"": """",
                            ""identifiers"": {{
                                ""type"": """",
                                ""reference_number"": """"
                            }},
                            ""booking_number"": ""{data?.shipment.carrierBookingRefernce}"",
                            ""bl_number"": ""{data?.shipment?.bol}"",
                            ""weight"": """",
                            ""status"": """",
                            ""lifecycle_status"": """",
                            ""id_date"": """",
                            ""pol_vsldeparture_planned_initial"": ""{data?.shipment?.initialCarrierETD}"",
                            ""pol_vsldeparture_planned_last"": """",
                            ""pol_vsldeparture_actual"": ""{polLocMilestone?.departure?.timestamps?.carrier?.datetime ?? ""}"",
                            ""pol_vsldeparture_detected"": ""{polLocMilestone?.departure?.timestamps?.predicted?.datetime ?? ""}"",
                            ""pod_vslarrival_planned_initial"": ""{data?.shipment.initialCarrierETA}"",
                            ""pod_vslarrival_planned_last"": ""{data?.shipment.status.estimatedArrivalAt}"",
                            ""pod_vslarrival_actual"": ""{data?.shipment.status.actualArrivalAt}"",
                            ""pod_vslarrival_detected"": ""{podVesselArrivalDetected}"",
                            ""pod_vslarrival_prediction"": """",
                            ""pod_discharge_prediction"": """",
                            ""lif_loc"": """",
                            ""lif_arrival_planned_initial"": """",
                            ""lif_arrival_planned_last"": """",
                            ""lif_arrival_actual"": """",
                            ""lif_departure_planned_initial"": """",
                            ""lif_departure_planned_last"": """",
                            ""lif_departure_actual"": """",
                            ""empty_return_customer"": """",
                            ""ts_count"": """",
                            ""customs_release_state"": """",
                            ""customs_release_date"": """",
                            ""carrier_release_state"": """",
                            ""carrier_release_date"": """",
                            ""availability_date"": """",
                            ""created"": """",
                            ""modified"": """"
                        }},
                        ""generated"": """",
                        ""secutiry_token"": """",
                        ""event_class"": """"
                    }}
                }}
            }}");

            AddLocData(json, polLocMilestone?.port, "pol");
            AddLocData(json, podLocMilestone?.port, "pod");
            AddLocData(json, null, "dlv");
            AddEventsData(json, events);
            AddLegsData(json, milestones);
            AddTransshipmentsData(json, milestones, events);

            // Convert the updated JSON structure back to a string
            string updatedJson = json.ToString();

            // return updatedJson;
            return updatedJson;
        }
    }
}