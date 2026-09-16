using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.WeekContexts;

/// <summary>
/// Use case: get the current household's week planning context, creating a default one on first access.
/// </summary>
public sealed class GetWeekContextQuery(IWeekContextRepository weekContextRepository)
{
    private readonly IWeekContextRepository _weekContextRepository = weekContextRepository;

    public async Task<WeekContextDto> ExecuteAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var weekContext = await _weekContextRepository.GetForHouseholdAsync(householdId, cancellationToken);

        return WeekContextMapper.ToDto(weekContext);
    }
}

/// <summary>
/// Use case: replace the current household's week planning context.
/// </summary>
public sealed class SaveWeekContextCommand(IWeekContextRepository weekContextRepository)
{
    private readonly IWeekContextRepository _weekContextRepository = weekContextRepository;

    public async Task<WeekContextDto> ExecuteAsync(Guid householdId, WeekContextDto payload, CancellationToken cancellationToken = default)
    {
        var weekContext = await _weekContextRepository.GetForHouseholdAsync(householdId, cancellationToken);

        weekContext.Replace(
            WeekContextMapper.ParseAlternatingWeekConfig(payload.AlternatingWeekConfig),
            WeekContextMapper.ParseWeekModeOverrides(payload.WeekModeOverrides),
            WeekContextMapper.ParseDays(payload.Days));

        await _weekContextRepository.SaveAsync(weekContext, cancellationToken);

        return WeekContextMapper.ToDto(weekContext);
    }
}
