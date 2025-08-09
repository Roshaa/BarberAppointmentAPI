using BarberBookmentAPI.Config;
using BarberBookmentAPI.DTO.Structs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class ConfigService
{
    private readonly string _path = "AppointConfig.yaml";
    private readonly RootConfig _config;

    public ConfigService()
    {
        _config = LoadConfigSync();
    }

    private RootConfig LoadConfigSync()
    {
        var yaml = File.ReadAllText(_path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<RootConfig>(yaml);
    }
    public int GetDaysUpToCreateAppointments() => _config.DaysUpToCreateAppointments;
    public ScheduleConfig GetScheduleConfig() => _config.Schedule;
    public List<BarberDto> GetBarbers() => _config.Barbers;
    public int PossibleAppointmentsInADay()
    {
        ScheduleConfig s = GetScheduleConfig();
        int minutes = (int)(new TimeOnly(s.EndMorning, 0) - new TimeOnly(s.StartMorning, 0)).TotalMinutes +
            (int)(new TimeOnly(s.EndAfternoon, 0) - new TimeOnly(s.StartAfternoon, 0)).TotalMinutes;

        return (minutes / s.MinutesInterval) * _config.Barbers.Count;
    }
}