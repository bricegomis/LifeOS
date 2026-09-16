using LifeOS.Application.ComposedMeals;
using LifeOS.Domain.ComposedMeals;
using LifeOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.ComposedMeals;

public sealed class EfComposedMealRepository(LifeOSDbContext dbContext) : IComposedMealRepository
{
    private readonly LifeOSDbContext _dbContext = dbContext;

    public async Task<ComposedMeal?> GetByIdAsync(Guid mealId, Guid householdId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComposedMeals
            .AsNoTracking()
            .Include(m => m.Parts)
            .FirstOrDefaultAsync(m => m.Id == mealId && m.HouseholdId == householdId, cancellationToken);
    }

    public async Task<IReadOnlyList<ComposedMeal>> GetAllForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ComposedMeals
            .AsNoTracking()
            .Where(m => m.HouseholdId == householdId)
            .Include(m => m.Parts)
            .ToListAsync(cancellationToken);
    }

    public async Task<ComposedMeal> AddAsync(ComposedMeal meal, CancellationToken cancellationToken = default)
    {
        await _dbContext.ComposedMeals.AddAsync(meal, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return meal;
    }

    public async Task<ComposedMeal> UpdateAsync(ComposedMeal meal, CancellationToken cancellationToken = default)
    {
        var existingMeal = await _dbContext.ComposedMeals
            .Include(m => m.Parts)
            .FirstOrDefaultAsync(m => m.Id == meal.Id, cancellationToken);

        if (existingMeal != null)
        {
            _dbContext.Entry(existingMeal).CurrentValues.SetValues(meal);

            var currentPartIds = meal.Parts.Select(p => p.Id).ToHashSet();
            var toRemove = existingMeal.Parts.Where(p => !currentPartIds.Contains(p.Id)).ToList();
            foreach (var rem in toRemove)
            {
                _dbContext.Set<ComposedMealPart>().Remove(rem);
            }

            var existingPartDict = existingMeal.Parts.ToDictionary(p => p.Id);
            foreach (var part in meal.Parts)
            {
                if (existingPartDict.TryGetValue(part.Id, out var existingPart))
                {
                    _dbContext.Entry(existingPart).CurrentValues.SetValues(part);
                }
                else
                {
                    existingMeal.Parts.Add(part);
                }
            }
        }
        else
        {
            _dbContext.ComposedMeals.Update(meal);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return meal;
    }

    public async Task<bool> DeleteAsync(Guid mealId, Guid householdId, CancellationToken cancellationToken = default)
    {
        var meal = await _dbContext.ComposedMeals
            .FirstOrDefaultAsync(m => m.Id == mealId && m.HouseholdId == householdId, cancellationToken);

        if (meal == null)
        {
            return false;
        }

        _dbContext.ComposedMeals.Remove(meal);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
