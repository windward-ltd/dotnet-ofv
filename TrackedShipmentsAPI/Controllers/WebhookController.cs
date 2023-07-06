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
        public async Task<IActionResult> shipmentUpdate([FromBody] JsonElement webhook)
        {
            try
            {

                dynamic webhookObject = JObject.Parse(webhook.GetRawText());

                string trackedShipmentId = webhook.GetProperty("data").GetProperty("shipment").GetProperty("identifiers").GetProperty("trackedShipmentId").ToString();
                
                string sentAt = webhook.GetProperty("data").GetProperty("metadata").GetProperty("sentAt").ToString();
                string[] trackedShipmentIds = { trackedShipmentId };
                var trackedShipmentsDataById = await _tsService.TrackedShipmentsByIds(trackedShipmentIds);

                var enrichedData = _service.AddDataToJSON(trackedShipmentsDataById?.data?.trackedShipmentsByIds?[0], sentAt, webhookObject?.data);
                var result = _service.JsonToXML(enrichedData);

                var xmlString = result.OuterXml;

                return Content(xmlString, "application/xml");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }
    }
}