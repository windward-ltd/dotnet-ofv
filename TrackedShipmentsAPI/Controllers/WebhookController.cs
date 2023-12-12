using Microsoft.AspNetCore.Mvc;
using TrackedShipmentsAPI.Services;
using TrackedShipmentsAPI.Models;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TrackedShipmentsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly WebhookService _service;
        private readonly TrackedShipmentsService _tsService;

        public WebhookController()
        {
            _service = new WebhookService();
            _tsService = new TrackedShipmentsService();
        }

        [HttpPost("shipmentUpdate")]
        public IActionResult shipmentUpdate([FromBody] JsonElement webhook)
        {
            try
            {
                dynamic? deserializedWebhookObject = JsonConvert.DeserializeObject<JObject>(webhook.GetRawText(), new JsonSerializerSettings {
                    DateParseHandling = DateParseHandling.None
                });

                string? sentAt = deserializedWebhookObject?.data?.metadata?.sentAt;

                var enrichedData = _service.AddDataToJSON(deserializedWebhookObject?.data, sentAt);
                var result = _service.JsonToXML(enrichedData);

                return Content(result, "application/xml");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }
    }
}