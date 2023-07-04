using Microsoft.AspNetCore.Mvc;
using TrackedShipmentsAPI.Services;
using TrackedShipmentsAPI.Models;
using System.Text.Json;

namespace TrackedShipmentsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackedShipmentsController : ControllerBase
    {
        private readonly TrackedShipmentsService _service;

        public TrackedShipmentsController()
        {
            _service = new TrackedShipmentsService();
        }

        [HttpPost("testCreateToken")]
        public async Task<IActionResult> TestCreateToken()
        {
            try
            {
                string token = await _service.TestCreateToken();
                return Ok(token);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }

        [HttpPost("upsertTrackedShipments")]
        public async Task<IActionResult> UpsertTrackedShipments(List<TrackedShipmentModel> shipments)
        {
            try
            {
                List<Dictionary<string, object?>> shipmentDictionaries = shipments.Select(shipment => new Dictionary<string, object?>
                {
                    { "containerNumber", shipment?.containerNumber },
                    { "scac", shipment?.scac },
                    { "bol", shipment?.bol },
                    { "carrierBookingReference", shipment?.carrierBookingReference },
                    { "metadata", new Dictionary<string, object>
                        {
                            { "jobNumber", shipment?.metadata?.jobNumber ?? Guid.NewGuid().ToString() }
                        }
                    }
                }).ToList();

                dynamic result = await _service.UpsertTrackedShipments(shipmentDictionaries);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }
    }
}
