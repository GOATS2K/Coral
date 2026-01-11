using AutoMapper;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Auth;
using Microsoft.EntityFrameworkCore;

namespace Coral.Services;

public interface IDeviceService
{
    Task<List<DeviceDto>> GetUserDevicesAsync(Guid userId, Guid? currentDeviceId = null);
    Task<bool> DeleteDeviceAsync(Guid userId, Guid deviceId);
}

public class DeviceService : IDeviceService
{
    private readonly CoralDbContext _context;
    private readonly IMapper _mapper;
    private readonly ISessionCacheService _sessionCache;

    public DeviceService(CoralDbContext context, IMapper mapper, ISessionCacheService sessionCache)
    {
        _context = context;
        _mapper = mapper;
        _sessionCache = sessionCache;
    }

    public async Task<List<DeviceDto>> GetUserDevicesAsync(Guid userId, Guid? currentDeviceId = null)
    {
        var devices = await _context.Devices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync();

        return devices.Select(d =>
        {
            var dto = _mapper.Map<DeviceDto>(d);
            return dto with { IsCurrent = d.Id == currentDeviceId };
        }).ToList();
    }

    public async Task<bool> DeleteDeviceAsync(Guid userId, Guid deviceId)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);

        if (device == null)
            return false;

        _sessionCache.InvalidateSession(deviceId);
        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();
        return true;
    }
}
