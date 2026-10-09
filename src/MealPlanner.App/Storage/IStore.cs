namespace MealPlanner.App.Storage;

public interface IStore
{
    Task<AppData?> LoadAsync();
    Task SaveAsync(AppData data);
}
