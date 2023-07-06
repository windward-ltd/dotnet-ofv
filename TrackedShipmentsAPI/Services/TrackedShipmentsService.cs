using TrackedShipmentsAPI.Models;
using System.Text.Json;
using System;
using Microsoft.Extensions.Caching.Memory;

namespace TrackedShipmentsAPI.Services
{
    public class TrackedShipmentsService
    {
        private readonly HttpClient _httpClient;
        private static readonly IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());

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

        public async Task<string> GetCachedToken()
        {
            string? token = cache?.Get("Token") as string;

            if (token == null)
            {
                token = await CreateToken();

                cache?.Set("Token", token, TimeSpan.FromHours(1));
            }

            return token;
        }

        public async Task<dynamic> UpsertTrackedShipments(List<Dictionary<string, object?>> shipments)
        {
            string token = await GetCachedToken();

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

            if (response?.errors != null)
            {
                throw new Exception($"Failed to upsert tracked shipments: {response?.errors[0]?.message}");
            }

            var trackedShipments = Newtonsoft.Json.JsonConvert.SerializeObject(response);

            return trackedShipments ?? throw new Exception("Response data is null");
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
                var token = await GetCachedToken();
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
