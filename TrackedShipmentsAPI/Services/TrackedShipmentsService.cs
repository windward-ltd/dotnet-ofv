using TrackedShipmentsAPI.Models;
using System.Text.Json;

namespace TrackedShipmentsAPI.Services
{
    public class TrackedShipmentsService
    {
        private readonly HttpClient _httpClient;

        public TrackedShipmentsService()
        {
            _httpClient = new HttpClient();
        }

        private Dictionary<string, string> GetCredentials()
        {
            // TODO: Implement the method logic
            throw new NotImplementedException("Method GetCredentials not implemented yet.");

            return new Dictionary<string, string>
            {
                { "clientId", "" },
                { "clientSecret", "" }
            };
        }

        private async Task<string> CreateToken()
        {
            Dictionary<string, string> credentials = GetCredentials();

            string query = @"
                mutation PublicAPIToken($clientId: String!, $clientSecret: String!) {
                    publicAPIToken(clientId: $clientId, clientSecret: $clientSecret)
                }";

            var variables = new
            {
                clientId = credentials["clientId"],
                clientSecret = credentials["clientSecret"]
            };

            var response = await SendGraphQlRequest(query, variables);

            if (response?.error != null)
            {
                throw new Exception("Failed to create token");
            }

            return response?.data?.publicAPIToken ?? throw new Exception("Response data is null");
        }

        public async Task<dynamic> UpsertTrackedShipments(List<Dictionary<string, object?>> shipments)
        {
            string token = await CreateToken();

            string query = @"
                mutation UpsertTrackedShipments($shipments: [TrackedShipmentInput!]!) {
                    upsertTrackedShipments(shipments: $shipments) {
                        id
                        shipment {
                            containerNumber
                            carrierBookingReference
                            bol
                            scac
                        }
                        metadata {
                            jobNumber
                        }
                    }
                }";

            var variables = new
            {
                shipments
            };

            var response = await SendGraphQlRequest(query, variables, token);

            if (response?.Errors != null)
            {
                throw new Exception("Failed to upsert tracked shipments: ", response?.Errors);
            }

            var trackedShipments = Newtonsoft.Json.JsonConvert.SerializeObject(response);

            return trackedShipments ?? throw new Exception("Response data is null");
        }

        public async Task<dynamic> TrackedShipmentsByIds(string[] trackedShipmentIds)
        {
            string token = await CreateToken();

            string currentQuery = @"
                query TrackedShipmentsByIds($ids: [ObjectId!]!) {
                    trackedShipmentsByIds(ids: $ids) {
                        id
                        shipment {
                            id
                            bol
                            carrierBookingReference
                            initialCarrierETA
                            initialCarrierETD
                            container {
                                isoCode
                                number
                            }
                            carrier {
                                longName
                                code
                            }
                            status {
                                actualArrivalAt
                                estimatedArrivalAt
                                events {
                                    description
                                    publisherCode
                                    timestamps {
                                        datetime
                                        code
                                    }
                                    port {
                                        properties {
                                            country
                                            locode
                                            name
                                            timezone
                                            centroid {
                                                geometry
                                            }
                                        }
                                    }
                                    vessel {
                                        imo
                                    }
                                }
                                currentEvent {
                                    description
                                    vessel {
                                        name
                                    }
                                }
                                milestones {
                                    type
                                    utcOffset
                                    port {
                                        properties {
                                            name
                                            locode
                                            country
                                            timezone
                                            centroid {
                                                geometry
                                            }
                                        }
                                    }
                                    arrival {
                                        vessel {
                                            imo
                                            name
                                        }
                                        timestamps {
                                            carrier {
                                                code
                                                datetime
                                            }
                                            predicted {
                                                code
                                                datetime
                                            }
                                        }
                                    }
                                    departure {
                                        voyage
                                        vessel {
                                            imo
                                            name
                                        }
                                        timestamps {
                                            carrier {
                                                code
                                                datetime
                                            }
                                            predicted {
                                                code
                                                datetime
                                            }
                                        }
                                    }
                                }
                                predicted {
                                    datetime
                                    code
                                }
                            }
                        }
                    }
                }";

            var variables = new
            {
                ids = trackedShipmentIds
            };

            var response = await SendGraphQlRequest(currentQuery, variables, token);

            if (response?.Errors != null)
            {
                throw new Exception("Failed to upsert tracked shipments: ", response?.Errors);
            }

            return response ?? throw new Exception("Response data is null");
        }

        private async Task<dynamic> SendGraphQlRequest(string query, object variables, string? token = null)
        {
            string endpoint = "https://graphql.wnwd.com";

            var request = new
            {
                query,
                variables
            };

            StringContent httpContent = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(request) ?? throw new ArgumentNullException(nameof(request)),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.PostAsync(endpoint, httpContent);
            var content = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(content))
            {
                throw new Exception("Empty response content");
            }

            var deserializedContent = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(content);


            if (deserializedContent == null)
            {
                throw new Exception("Failed to deserialize response content");
            }

            return deserializedContent;
        }

        public async Task<string> TestCreateToken()
        {
            try
            {
                var token = await CreateToken();
                Console.WriteLine($"Token: {token}");
                return token;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return "";
            }
        }
    }
}
