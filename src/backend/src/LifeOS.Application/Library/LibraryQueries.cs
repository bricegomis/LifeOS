using LifeOS.Application.Common.Interfaces;

namespace LifeOS.Application.Library;

public sealed class GetActivitiesQuery(IActivityRepository activityRepository)
{
    private readonly IActivityRepository _activityRepository = activityRepository;

    public async Task<IReadOnlyList<ActivityDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var activities = await _activityRepository.GetAllAsync(cancellationToken);

        return activities
            .Select(activity => new ActivityDto(activity.Id, activity.Name, activity.Icon, activity.DefaultDurationMinutes))
            .ToList();
    }
}
