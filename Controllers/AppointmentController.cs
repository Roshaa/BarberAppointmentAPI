using BarberAppointmentAPI.Services;
using BarberBookmentAPI.DTO.Structs;
using Microsoft.AspNetCore.Mvc;

namespace BarberAppointmentAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {

        private readonly DynamodbService _dynamoDbService;

        public AppointmentController(DynamodbService dynamoDbService)
        {
            _dynamoDbService = dynamoDbService;
        }

        [HttpGet("availableDays")]
        public async Task<IActionResult> GetDays(int daysAhead)
        {
            var response = await _dynamoDbService.DaysWithAvailableAppointments(daysAhead);
            return Ok(response);
        }

        [HttpGet("availableHours")]
        public async Task<IActionResult> GetTimes(string day) //dynamodb saves as strings...
        {
            var response = await _dynamoDbService.AvailableHoursOfADay(day);
            return Ok(response);
        }

        [HttpGet("availableBarbers")]
        public async Task<IActionResult> GetBarbers(string day, string hour)
        {
            var a = await _dynamoDbService.AvailableBarbersHour(day, hour);
            return Ok(a);
        }

        [HttpPost("createAppointment")]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentDto appointment)
        {
            try
            {
                if (!_dynamoDbService.ValidateBarber(ref appointment)) return BadRequest("Invalid Request");
                if (!_dynamoDbService.ValidateDateValue(appointment.Date)) return BadRequest("Invalid Request");
                if (!await _dynamoDbService.VerifyAvailableDate(appointment)) return BadRequest("Invalid Request");

                await _dynamoDbService.PutAppointment(appointment);
                return StatusCode(201, "Sucessfully created appointment");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Unknown internal error");
                throw;
            }
            return Ok();
        }


    }
}
