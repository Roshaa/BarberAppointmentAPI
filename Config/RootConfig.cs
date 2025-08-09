using BarberBookmentAPI.DTO.Structs;

namespace BarberBookmentAPI.Config
{
    public class RootConfig
    {
        public ScheduleConfig Schedule { get; set; }
        public List<BarberDto> Barbers { get; set; }
        public int DaysUpToCreateAppointments { get; set; }
        public int PossibleAppointmentsInADay { get; set; }
    }
}
