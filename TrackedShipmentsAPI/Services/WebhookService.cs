using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TrackedShipmentsAPI.Models;
using System.Text.Json;


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
        public const int MIN_TSP_MILESTONES_LENGTH = 4;
        public const int MIN_LEG_LENGTH = 5;

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

        public void MergeToMainJson(JObject mainJson, JObject jsonToMerge)
        {
            JObject parsedJson = JObject.Parse($@"
            {{
                ""Root"": {{
                    ""container"": {{
                        ""shipment"": {jsonToMerge.ToString()}
                    }}
                }}
            }}");

            mainJson.Merge(parsedJson);
        }

        // Converts json object to XML
        public XmlDocument? JsonToXML(string jsonData)
        {
            XmlDocument? doc = JsonConvert.DeserializeXmlNode(jsonData);

            return doc;
        }

        public Dictionary<string, T> CreateDictionaryFromJson<T>(dynamic arrayElement, string keyName)
        {
            Dictionary<string, T> dictionary = new Dictionary<string, T>();

            foreach (dynamic element in arrayElement)
            {
                string keyId = element[keyName].ToString();
                T item = element.ToObject<T>();
                dictionary[keyId] = item;
            }

            return dictionary;
        }

        // Creates all leg data of at least 5 first legs (some can be empty if no data) to adhere to data structure
        public void AddLegsData(JObject json, Milestone[] milestones, Dictionary<string, Vessel> vesselsDict)
        {
            int maxLegLength = Math.Max(MIN_LEG_LENGTH, milestones.Length - 1);
            JObject aggregatedJson = new JObject();

            for (int i = 0; i < maxLegLength; i++)
            {
                Milestone? currentIteratedMilestone = new Milestone();

                if (i < milestones?.Length) {
                    currentIteratedMilestone = milestones[i];
                }

                Vessel? currentDepartureVessel = currentIteratedMilestone?.departure?.vesselId != null ? vesselsDict[currentIteratedMilestone?.departure?.vesselId ?? ""] : new Vessel();
                
                JObject legVesselObject = new JObject();
                legVesselObject["name"] = currentDepartureVessel?.name ?? "";
                legVesselObject["imo"] = currentDepartureVessel?.imo ?? "";
                legVesselObject["id"] = "";


                JObject legServiceObject = new JObject();
                legServiceObject["id"] = "";
                legServiceObject["name"] = "";
                legServiceObject["code"] = "";
                legServiceObject["direction"] = "";

                string prefix = $"leg{i + 1}";
                aggregatedJson[$"{prefix}_vessel"] = legVesselObject;
                aggregatedJson[$"{prefix}_service"] = legServiceObject;
                aggregatedJson[$"{prefix}_voyage"] = currentIteratedMilestone?.departure?.voyage ?? "";
            }

            MergeToMainJson(json, aggregatedJson);
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

            itemObject["name"] = port?.name ?? "";
            itemObject["locode"] = port?.locode ?? "";
            itemObject["timezone"] = port?.timezone ?? "";
            itemObject["latitude"] = port?.coordinates?[1] ?? null;
            itemObject["longitude"] = port?.coordinates?[0] ?? null;
            itemObject["city"] = "";
            itemObject["state"] = "";
            itemObject["country"] = port?.country ?? "";
            itemObject["country_iso2"] = "";
            itemObject["country_iso3"] = "";

            aggregatedJson[$"{fieldsPrefix}_loc"] = itemObject;

            MergeToMainJson(json, aggregatedJson);     
        }

        // Adds all non TSP related events data regarding actual or estimated datetime to json
        public void AddEventsData(JObject json, Event[] events, Dictionary<string, Port> portsDict)
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

                Port? port = matchingEventByKey?.portId != null ? portsDict[matchingEventByKey?.portId ?? ""] : new Port();
                AddLocData(json, port, prefix);
            }
            
            MergeToMainJson(json, aggregatedEventsJson);
        }

        public string? GetDateByActual(Timestamps? timestamps, bool actualField) {
            string? datetime = timestamps?.datetime;
            bool isActual = timestamps?.code == ACTUAL_CODE;

            if (actualField) {
                return isActual ? datetime : "";
            }

            return isActual ? "" : datetime;
        }

        public void AddTransshipmentsData(JObject json, Milestone[] milestones, Event[] events, Dictionary<string, Port> portsDict, Dictionary<string, Vessel> vesselsDict)
        {
            Milestone[] tspMilestones = milestones
                .Where((item) => item.type == TSP)
                .ToArray();

            Event[] tspEvents = events
                .Where((item) => item?.description == DISCHARGE_AT_TSP || item?.description == LOADED_AT_TSP)
                .ToArray();

            JObject aggregatedTransshipmentsJson = new JObject();

            int maxTspLength = Math.Max(tspMilestones.Length - 1, MIN_TSP_MILESTONES_LENGTH);

            for (int i = 0; i <= maxTspLength; i++)
            {
                Milestone? currentIteratedMilestone = new Milestone();

                if (i < tspMilestones.Length)
                {
                    currentIteratedMilestone = tspMilestones[i];
                }

                Port? currentMilestonePort = currentIteratedMilestone?.portId != null ? portsDict[currentIteratedMilestone?.portId ?? ""] : new Port();

                Vessel? currentArrivalVessel = currentIteratedMilestone?.arrival?.vesselId != null ? vesselsDict[currentIteratedMilestone?.arrival?.vesselId ?? ""] : new Vessel();
                Vessel? currentDepartureVessel = currentIteratedMilestone?.departure?.vesselId != null ? vesselsDict[currentIteratedMilestone?.departure?.vesselId ?? ""] : new Vessel();

                Event? dischargeEvent = tspEvents.FirstOrDefault((item) => {
                    Vessel? currentItemVessel = item?.vesselId != null ? vesselsDict[item?.vesselId ?? ""] : new Vessel();
                    return ((currentArrivalVessel?.name != null && currentArrivalVessel?.name == currentItemVessel?.name) || (currentArrivalVessel?.imo != null && currentArrivalVessel?.imo == currentItemVessel?.imo)) && item?.description == DISCHARGE_AT_TSP;
                });

                Event? loadedEvent = tspEvents.FirstOrDefault((item) => {
                    Vessel? currentItemVessel = item?.vesselId != null ? vesselsDict[item?.vesselId ?? ""] : new Vessel();
                    return ((currentDepartureVessel?.name != null && currentDepartureVessel?.name == currentItemVessel?.name) || (currentDepartureVessel?.imo != null && currentDepartureVessel?.imo == currentItemVessel?.imo)) && item?.description == DISCHARGE_AT_TSP;
                });

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

                AddLocData(json, currentMilestonePort, prefix);
            }

            MergeToMainJson(json, aggregatedTransshipmentsJson);
        }

        // Aggregates the relevant data from all functions to the JSON
        public string AddDataToJSON(dynamic data, string sentAt)
        {
            Event[] events = data?.shipment?.events?.ToObject<Event[]>() ?? new Event[1];
            Milestone[] milestones = data?.shipment?.milestones?.ToObject<Milestone[]>() ?? new Milestone[1];

            Dictionary<string, Port> portsDict = CreateDictionaryFromJson<Port>(data?.shipment?.ports, "portId");
            Dictionary<string, Vessel> vesselsDict = CreateDictionaryFromJson<Vessel>(data?.shipment?.vessels, "vesselId");

            string? shipmentId = data?.shipment?.identifiers?.shipmentId;

            string? podVesselArrivalDetected = data?.shipments?.predicted?.code == ACTUAL_CODE ? data?.shipments?.predicted?.datetime : "";

            Milestone? podLocMilestone = milestones.FirstOrDefault((Milestone milestone) => milestone?.type == POD);
            Milestone? polLocMilestone = milestones.FirstOrDefault((Milestone milestone) => milestone?.type == POL);

            CarrierLatestStatus? carrierLatestStatus = data?.shipment?.carrierLatestStatus?.ToObject<CarrierLatestStatus>();
            Vessel? currentVessel = carrierLatestStatus?.vesselId != null ? vesselsDict[carrierLatestStatus?.vesselId ?? ""] : new Vessel();

            Milestone? nextMilestone = milestones.FirstOrDefault((Milestone milestone) => milestone?.arrival?.timestamps?.carrier?.datetime != null && milestone?.arrival?.timestamps?.carrier?.code == PLANNED_CODE);
            Port? nextPort = currentVessel != null & nextMilestone?.arrival?.vesselId == currentVessel?.vesselId ? portsDict[nextMilestone?.portId ?? ""] : new Port();

            string? podVesselArrivalPlannedLast = GetDateByActual(podLocMilestone?.arrival?.timestamps?.carrier, false);
            string? podVesselArrivalActual = GetDateByActual(podLocMilestone?.arrival?.timestamps?.carrier, true);

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
                                ""new_status_verbose"": ""{carrierLatestStatus?.status?.description}"",
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
                            ""status_verbose"": ""{carrierLatestStatus?.status?.description}"",
                            ""current_vessel_nextport"": {{
                                 ""name"": ""{nextPort?.name}"",
                                 ""locode"": ""{nextPort?.locode}"",
                                 ""eta"": ""{GetDateByActual(nextMilestone?.arrival?.timestamps?.predicted, false) ?? GetDateByActual(nextMilestone?.arrival?.timestamps?.carrier, false)}"",
                            }},
                            ""current_vessel_position"": {{
                                ""latitude"": ""{currentVessel?.lastPosition?.coordinates?[1]}"",
                                ""longitude"": ""{currentVessel?.lastPosition?.coordinates?[0]}"",
                                ""timestamp"": ""{currentVessel?.lastPosition?.datetime}"",
                                ""heading"": ""{currentVessel?.lastPosition?.course}"",
                            }},
                            ""current_vessel"": {{
                                ""name"": ""{currentVessel?.name ?? ""}"",
                                ""imo"": ""{currentVessel?.imo ?? ""}"",
                                ""id"": ""{currentVessel?.vesselId ?? ""}"",
                            }},
                            ""container_type_iso"": ""{data?.shipment?.identifiers?.ISOEquipmentCode ?? ""}"",
                            ""container_type_str"": """",
                            ""shipmentsubscription_status_verbose"": """",
                            ""shipmentsubscription_on_hold"": """",
                            ""shipmentsubscription_id"": """",
                            ""lifecycle_status_verbose"": """",
                            ""carrier_name"": ""{data?.shipment?.identifiers?.carrier?.name ?? ""}"",
                            ""carrier_scac"": ""{data?.shipment?.identifiers?.carrier?.SCAC ?? ""}"",
                            ""container_number"": ""{data?.shipment?.identifiers?.containerNumber ?? ""}"",
                            ""descriptive_name"": """",
                            ""shipmentsubscription_descriptive_name"": """",
                            ""transport_modes_verbose"": """",
                            ""transport_modes"": """",
                            ""url"": """",
                            ""shipmentsubscription"": """",       
                            ""availability_loc"": """",
                            ""identifiers"": {{
                                ""type"": """",
                                ""reference_number"": ""{data?.metadata?.jobNumber}""
                            }},
                            ""booking_number"": ""{data?.shipment?.identifiers?.carrierBookingReference}"",
                            ""bl_number"": ""{data?.shipment?.identifiers?.bolNumber}"",
                            ""weight"": """",
                            ""status"": """",
                            ""lifecycle_status"": """",
                            ""id_date"": ""{carrierLatestStatus?.timestamps?.datetime}"",
                            ""pol_vsldeparture_planned_initial"": ""{data?.shipment?.initialCarrierETD}"",
                            ""pol_vsldeparture_planned_last"": """",
                            ""pol_vsldeparture_actual"": ""{polLocMilestone?.departure?.timestamps?.carrier?.datetime ?? ""}"",
                            ""pol_vsldeparture_detected"": ""{polLocMilestone?.departure?.timestamps?.predicted?.datetime ?? ""}"",
                            ""pod_vslarrival_planned_initial"": ""{data?.shipment?.initialCarrierETA}"",
                            ""pod_vslarrival_planned_last"": ""{podVesselArrivalPlannedLast}"",
                            ""pod_vslarrival_actual"": ""{podVesselArrivalActual}"",
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

            Port? polPort = polLocMilestone?.portId != null ? portsDict[polLocMilestone?.portId ?? ""] : new Port();
            Port? podPort = podLocMilestone?.portId != null ? portsDict[podLocMilestone?.portId ?? ""] : new Port();

            AddLocData(json, polPort, "pol");
            AddLocData(json, podPort, "pod");
            AddLocData(json, null, "dlv");

            AddEventsData(json, events, portsDict);
            AddLegsData(json, milestones, vesselsDict);
            AddTransshipmentsData(json, milestones, events, portsDict, vesselsDict);

            // Convert the updated JSON structure back to a string
            string updatedJson = json.ToString();

            // return updatedJson;
            return updatedJson;
        }
    }
}