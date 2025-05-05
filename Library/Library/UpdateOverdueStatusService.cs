using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Library.Data;

public class UpdateOverdueStatusService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public UpdateOverdueStatusService(IServiceProvider serviceProvider)
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
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var now = DateTime.UtcNow;

                    // Only update records where the value might change
                    var records = await context.BorrowRecords
                        .Where(br =>
                            (!br.IsReturned && br.DueDate <= now && br.Overdue != true) ||  // should be true but isn't
                            (br.Overdue == true && (br.IsReturned || br.DueDate > now))     // should be false but is true
                        )
                        .ToListAsync();

                    foreach (var record in records)
                    {
                        var isOverdue = !record.IsReturned && record.DueDate <= now;
                        record.Overdue = isOverdue;
                    }

                    await context.SaveChangesAsync();
                }

                // Delay between updates (e.g., every 5 minutes)
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateOverdueStatusService Error]: {ex.Message}");
        }
    }
}
