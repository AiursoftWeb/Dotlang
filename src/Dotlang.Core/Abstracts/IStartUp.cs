using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Dotlang.Core.Abstracts;

public interface IStartUp
{
    public void ConfigureServices(IServiceCollection services);
}
