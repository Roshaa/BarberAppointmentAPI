using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using BarberBookmentAPI.Config;
using BarberBookmentAPI.DTO.Structs;
using System.Globalization;

namespace BarberAppointmentAPI.Services
{
    public class DynamodbService
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly ConfigService _config;
        private readonly string DynamoTable = "Appointments";

        public DynamodbService(IAmazonDynamoDB dynamoDb, ConfigService config)
        {
            _dynamoDb = dynamoDb;
            _config = config;
        }

        // Table creation, used on program.cs
        public async Task CreateTable()
        {
            var currentTables = await _dynamoDb.ListTablesAsync();

            if (!currentTables.TableNames.Contains("Appointments"))
            {
                var request = new CreateTableRequest
                {
                    TableName = "Appointments",
                    AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition("id", "S"),   // Table PK
                new AttributeDefinition("day", "S"),  // GSI PK
                new AttributeDefinition("hour", "S")  // GSI SK
            },
                    KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement("id", "HASH")
            },
                    GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new GlobalSecondaryIndex
                {
                    IndexName = "DateIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement("day", "HASH"),
                        new KeySchemaElement("hour", "RANGE")
                    },
                    Projection = new Projection
                    {
                        ProjectionType = ProjectionType.ALL
                    }
                }
            },
                    BillingMode = BillingMode.PAY_PER_REQUEST
                };

                var response = await _dynamoDb.CreateTableAsync(request);
            }
        }
        //////////////////////////////////

        #region GETS
        public async Task<List<string>> DaysWithAvailableAppointments(int daysAhead)
        {
            List<string> availableDays = new List<string>();
            int possibleAppointmentsInADay = _config.PossibleAppointmentsInADay();

            //Results for 1 week
            for (int i = 0; i < 8; i++)
            {
                var dayStr = DateTime.Now.AddDays(i).Date.ToString("yyyyMMdd");

                QueryRequest request = new QueryRequest
                {
                    TableName = DynamoTable,
                    IndexName = "DateIndex",
                    KeyConditionExpression = "#day = :dayVal",
                    ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        ["#day"] = "day"
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":dayVal"] = new AttributeValue { S = dayStr }
                    }
                };

                QueryResponse response = await _dynamoDb.QueryAsync(request);
                if (possibleAppointmentsInADay > response.Count) availableDays.Add(DateTime.Now.Date.AddDays(i).ToString("MMdd"));
            }

            return availableDays;
        }
        public async Task<List<string>> AvailableHoursOfADay(string day)
        {
            ScheduleConfig s = _config.GetScheduleConfig();
            int capacity = _config.GetBarbers().Count;
            DateTime date = DateTime.ParseExact(day, "yyyyMMdd", CultureInfo.InvariantCulture);

            List<string> slots = new List<string>();

            void AddSlots(int startHour, int endHour)
            {
                for (var t = date.Date.AddHours(startHour); t < date.Date.AddHours(endHour); t = t.AddMinutes(s.MinutesInterval))
                {
                    if (date.Date == DateTime.Today && t <= DateTime.Now) continue;
                    slots.Add(t.ToString("HHmm"));
                }
            }

            AddSlots(s.StartMorning, s.EndMorning);
            AddSlots(s.StartAfternoon, s.EndAfternoon);

            QueryRequest req = new QueryRequest
            {
                TableName = DynamoTable,
                IndexName = "DateIndex",
                KeyConditionExpression = "#day = :d",
                ProjectionExpression = "#hour",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#day"] = "day",
                    ["#hour"] = "hour"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":d"] = new AttributeValue { S = day }
                }
            };

            QueryResponse resp = await _dynamoDb.QueryAsync(req);

            var taken = resp.Items
                .Where(it => it.TryGetValue("hour", out var v) && !string.IsNullOrEmpty(v.S))
                .GroupBy(it => it["hour"].S)
                .ToDictionary(g => g.Key, g => g.Count());

            return slots
                .Where(h => !taken.TryGetValue(h, out var c) || c < capacity)
                .OrderBy(h => h)
                .Select(h => $"{h[..2]}:{h[2..]}")
                .ToList();
        }
        public async Task<BarberDto[]> AvailableBarbersHour(string day, string hour)
        {
            QueryRequest req = new QueryRequest
            {
                TableName = DynamoTable,
                IndexName = "DateIndex",
                KeyConditionExpression = "#day = :d AND #hour = :h",
                ProjectionExpression = "#barberId",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#day"] = "day",
                    ["#hour"] = "hour",
                    ["#barberId"] = "barberId"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":d"] = new AttributeValue { S = day },
                    [":h"] = new AttributeValue { S = hour }
                }
            };

            QueryResponse resp = await _dynamoDb.QueryAsync(req);
            List<BarberDto> barbers = _config.GetBarbers();

            HashSet<string> bookedIds = resp.Items.Select(i => i["barberId"].S).ToHashSet();

            return barbers.Where(b => !bookedIds.Contains(b.BarberId)).ToArray();
        }

        #endregion

        #region POSTS
        //DynamodDB does not have a "post", but since we are not updating the appointments we will just use post to understand
        public async Task PutAppointment(AppointmentDto appointmentDTO)
        {
            Guid g = Guid.NewGuid();

            var item = new Dictionary<string, AttributeValue>
            {
                { "id", new AttributeValue { S = g.ToString() } },
                { "day", new AttributeValue { S = appointmentDTO.Date.ToString("yyyyMMdd") } },
                { "hour", new AttributeValue { S = appointmentDTO.Date.ToString("HHmm") } },
                { "barberId", new AttributeValue { S = appointmentDTO.BarberId } },
                { "client", new AttributeValue { S = appointmentDTO.Client } }
            };

            var request = new PutItemRequest
            {
                TableName = DynamoTable,
                Item = item
            };

            await _dynamoDb.PutItemAsync(request);
        }
        #endregion


        #region post_appointment_validations

        public bool ValidateBarber(ref AppointmentDto appointmentDTO)
        {
            var a = _config.GetBarbers().Select(b => b.BarberId);
            if (!a.Contains(appointmentDTO.BarberId)) return false;

            return true;
        }
        public bool ValidateDateValue(DateTime date)
        {
            ScheduleConfig schedule = _config.GetScheduleConfig();

            TimeSpan startM = new TimeSpan(schedule.StartMorning, 0, 0);
            TimeSpan endM = new TimeSpan(schedule.EndMorning, 0, 0);
            TimeSpan startA = new TimeSpan(schedule.StartAfternoon, 0, 0);
            TimeSpan endA = new TimeSpan(schedule.EndAfternoon, 0, 0);

            bool inWindow =
                (date.TimeOfDay >= startM && date.TimeOfDay < endM && date.TimeOfDay != endM) ||
                (date.TimeOfDay >= startA && date.TimeOfDay < endA && date.TimeOfDay != endA);

            bool onInterval =
                date.Second == 0 &&
                date.Millisecond == 0 &&
                (date.Minute % schedule.MinutesInterval) == 0;

            return inWindow && onInterval;
        }
        public async Task<bool> VerifyAvailableDate(AppointmentDto appointmentDTO)
        {
            if (appointmentDTO.Date.Date == DateTime.Today && appointmentDTO.Date.TimeOfDay <= DateTime.Now.TimeOfDay)
                return false;

            string dayStr = appointmentDTO.Date.ToString("yyyyMMdd");
            string hourStr = appointmentDTO.Date.ToString("HHmm");

            QueryRequest req = new QueryRequest
            {
                TableName = DynamoTable,
                IndexName = "DateIndex",
                KeyConditionExpression = "#day = :dayVal AND #hour = :hourVal",
                FilterExpression = "#barberId = :barberIdVal",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#day"] = "day",
                    ["#hour"] = "hour",
                    ["#barberId"] = "barberId"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":dayVal"] = new AttributeValue { S = dayStr },
                    [":hourVal"] = new AttributeValue { S = hourStr },
                    [":barberIdVal"] = new AttributeValue { S = appointmentDTO.BarberId }
                }
            };

            var resp = await _dynamoDb.QueryAsync(req);
            return resp.Count == 0;
        }

        #endregion




    }
}
