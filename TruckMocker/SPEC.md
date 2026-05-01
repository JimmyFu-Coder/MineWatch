# TruckMocker 模拟器规格文档

## 概述

TruckMocker 是一个 MQTT 模拟器，用于模拟卡车车队实时发送 GPS 遥测数据到 AWS IoT Core（或本地 mosquitto），供 MineWatch.Api 后端测试使用。

---

## 数据来源

**数据集**：`data/cleaned_truck_data.csv`

**字段**：
| 字段 | 说明 |
|------|------|
| `vehicle_no` | 卡车编号（作为 deviceId） |
| `Data_Ping_time` | GPS ping 时间 |
| `Curr_lat` | 纬度 |
| `Curr_lon` | 经度 |
| `ontime` | 准点状态（G=准点，NULL=延误） |

---

## 模拟行为

### 1. 数据加载
- 读取 CSV 文件，只读取有用列
- 按 `vehicle_no` 分组聚合
- 每组内按 `Data_Ping_time` 排序
- `Data_Ping_time` 格式处理（`0808.0` → `08:08`）

### 2. MQTT 发送
- 连接 MQTT Broker（地址可配置）
- 发布到 Topic `devices/{vehicle_no}/telemetry`
- Payload 格式：
```json
{
  "deviceId": "KA590408",
  "timestamp": "2026-05-01T10:30:00Z",
  "location": {
    "latitude": 12.6635,
    "longitude": 78.6498,
    "accuracy": 5.0
  },
  "speed": 45.2,
  "temperature": 32.5,
  "fuelLevel": 75.0,
  "eventType": "driving",
  "ontime": "G"
}
```

### 3. 发送频率模拟
- **随机间隔**：每次发送间隔在 `MinIntervalMs ~ MaxIntervalMs` 之间随机
- **丢包率**：每次发送有 `LossRate` 概率被丢弃（不发送）
- **突发模式**：每次发送有 `BurstProbability` 概率连续发送 2-3 条
- **车辆启动延迟**：每辆车在 `0 ~ StartDelayMaxSeconds` 之间随机延迟后开始发送

### 4. 数据播完后的行为
支持三种模式（可配置）：
| 模式 | 说明 |
|------|------|
| `Loop` | 播完后从头循环重播 |
| `Stop` | 播完后停止 |
| `Wait` | 播完后等待，不重播也不停止 |

### 5. 日志输出
- 每条发送的 Payload（可选，开启 `-v` verbose 模式）
- 连接/重连事件
- 丢包事件
- 每辆车启动/播完状态

### 6. 数据丰富（GPS 传感器模拟）
| 字段 | 生成逻辑 |
|------|----------|
| `accuracy` | 随机 3-10m |
| `speed` | 根据相邻 ping 位置差/时间差计算 |
| `temperature` | 印度 Tamil Nadu 区域，基础 30°C + 随机波动 ±5°C |
| `fuelLevel` | 随机 20-90%，部分车基于 `ontime` 状态调整 |
| `eventType` | 基于 `ontime`：G=driving，NULL=idle 或 loading |

---

## 配置项

```json
{
  "Mqtt": {
    "Broker": "localhost",
    "Port": 1883,
    "TopicPrefix": "devices"
  },
  "Simulator": {
    "MinIntervalMs": 1000,
    "MaxIntervalMs": 3000,
    "LossRate": 0.05,
    "BurstProbability": 0.1,
    "StartDelayMaxSeconds": 30,
    "CompletionBehavior": "Loop"
  },
  "DataSources": {
    "CsvPath": "data/cleaned_truck_data.csv"
  }
}
```

---

## 数据流

```
CSV ──读取+分组──▶ 按 vehicle_no 聚合
                         │
                    每车一个 Task
                         │
                    随机启动延迟
                         │
                    按顺序取 ping
                         │
                    随机间隔 + 丢包判断
                         │
                    MQTT Publish
                         │
                    ◀── 循环 / 停止 / 等待
```

---

## Payload 示例

```json
{
  "deviceId": "KA590408",
  "timestamp": "2026-05-01T10:30:01.234Z",
  "location": {
    "latitude": 12.6635,
    "longitude": 78.6498,
    "accuracy": 5.0
  },
  "speed": 45.2,
  "temperature": 33.1,
  "fuelLevel": 72.5,
  "eventType": "driving",
  "ontime": "G",
  "bookingId": "MVCV0000927/082021"
}
```

---

## 依赖

- MQTTnet（MQTT 客户端）
- CsvHelper（CSV 读取）
- Microsoft.Extensions.Configuration（配置）
- Microsoft.Extensions.Hosting（后台服务）

---

## 文件结构

```
TruckMocker/
├── data/
│   └── cleaned_truck_data.csv
├── Program.cs
├── TruckMocker.csproj
├── appsettings.json
└── SPEC.md
```
