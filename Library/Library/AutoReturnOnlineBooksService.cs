using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Data;
using System.Threading.Tasks;
using Library.Data;

public class AutoReturnOnlineBooksService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public AutoReturnOnlineBooksService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // Make sure this name matches

                    var now = DateTime.UtcNow;

                    var overdueOnlineRecords = await context.BorrowRecords
                        .Where(br => !br.IsReturned && br.IsOnline && br.DueDate <= now)
                        .ToListAsync();

                    foreach (var record in overdueOnlineRecords)
                    {
                        record.IsReturned = true;
                        record.ReturnTime = record.DueDate;
                    }

                    await context.SaveChangesAsync();
                }

                await Task.Delay(TimeSpan.FromHours(0.001), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BackgroundService Error]: {ex.Message}");
        }
    }

}
