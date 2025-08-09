using Amazon;
using Amazon.DynamoDBv2;
using BarberAppointmentAPI.Services;

var builder = WebApplication.CreateBuilder(args);

DotNetEnv.Env.Load();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "eu-north-1";
    var config = new AmazonDynamoDBConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(region)
    };

    return new AmazonDynamoDBClient(config);
});

builder.Services.AddSingleton<DynamodbService>();
builder.Services.AddSingleton<ConfigService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var svc = scope.ServiceProvider.GetRequiredService<DynamodbService>();
    await svc.CreateTable();
}

app.Run();