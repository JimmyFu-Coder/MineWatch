namespace MineWatch.Api.DTOs;

public record LatestPositionResponse(
    Guid DeviceId,
    string VehicleNo,
    double Lat,
    double Lon,
    double Speed,
    double Heading,
    DateTime Timestamp);

public record HistoryResponse(
    string VehicleNo,
    List<HistoryPoint> Points,
    int TotalCount);

public record HistoryPoint(
    double Lat,
    double Lon,
    double Speed,
    double Heading,
    DateTime Timestamp);
