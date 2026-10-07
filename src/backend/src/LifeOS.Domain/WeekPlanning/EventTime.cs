namespace LifeOS.Domain.WeekPlanning;

public static class EventTime
{
    public static void Validate(int? startMinute, int? endMinute)
    {
        if (startMinute is null && endMinute is null) return;
        if (startMinute is null || endMinute is null || startMinute < 0 || endMinute > 1440 || endMinute <= startMinute)
            throw new ArgumentException("Créneau invalide : début/fin entre 0 et 1440, fin après début.");
    }
}
