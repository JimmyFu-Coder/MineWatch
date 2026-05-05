using TruckMocker.Models;
using TruckMocker.Services;
using TruckMocker.Models;
using TruckMocker.Services;

  // 1. 配置
  var config = new SimulationConfig
  {
      VehicleCount = 5,
      PointsPerVehicle = 300,
      FrequencyHz = 1,
      AvgSpeedMps = 30,                                                                                                         
      Bounds = new[]
      {                                                                                                                         
          (-32.265450, 116.023386),
          (-32.265093, 116.024874),
          (-32.267994, 116.024709),
          (-32.266975, 116.026851),
      }                                                                                                                         
  };
                                                                                                                                
  var mqttConfig = new MqttConfig { Server = "localhost", Port = 1883 };

  // 2. 生成轨迹
  var generator = new TrajectoryGenerator(config);
  var records = generator.Generate();
  Console.WriteLine($"Generated {records.Count} records for {config.VehicleCount} vehicles");
                                                                                                                                
  // 3. 发布 MQTT
  await using var publisher = new MqttPublisher(mqttConfig);                                                                    
  await publisher.ConnectAsync();
  Console.WriteLine($"Connected to MQTT broker");

  // 4. 循环发送
  var grouped = records.GroupBy(r => r.VehicleNo).ToList();
  var totalSent = 0;

  var tasks = grouped.Select(async vehicleGroup =>
  {
      var vehicleId = vehicleGroup.Key;
      var sent = 0;

      foreach (var record in vehicleGroup.OrderBy(r => r.Timestamp))
      {
          await publisher.PublishAsync(vehicleId, record);
          sent++;
          Interlocked.Add(ref totalSent, 1);

          if (config.FrequencyHz > 0)
              await Task.Delay(1000 / config.FrequencyHz);
      }                                                                                                                         
   
      Console.WriteLine($"[{vehicleId}] sent {sent} messages");                                                                 
      return sent;
  }).ToList();

  await Task.WhenAll(tasks);
  Console.WriteLine($"Sent {totalSent} messages to broker");
