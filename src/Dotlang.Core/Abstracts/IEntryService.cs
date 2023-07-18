namespace Aiursoft.Dotlang.Core.Abstracts;

public interface IEntryService
{
    public Task OnServiceStartedAsync(string path, bool shouldTakeAction);
}
