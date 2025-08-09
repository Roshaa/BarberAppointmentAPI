namespace BarberBookmentAPI.DTO.Structs
{
    public struct AppointmentDto
    {
        public DateTime Date { get; set; }
        public string BarberId { get; set; }
        public string Client { get; set; }
    }
}
