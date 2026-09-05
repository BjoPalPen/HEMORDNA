using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;
using Hemordna.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Hemordna.Api;

internal static class DevelopmentDataSeeder
{
    private const string DemoEmail = "demo@hemordna.local";
    private const string DemoPassword = "Hemordna-demo-2026!";
    private const string DemoName = "Demo";

    internal static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<HemordnaUser>>();
        var user = await users.FindByEmailAsync(DemoEmail);

        if (user is null)
        {
            user = new HemordnaUser
            {
                UserName = DemoEmail,
                Email = DemoEmail,
                DisplayName = DemoName
            };

            var result = await users.CreateAsync(user, DemoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create the development demo user: "
                    + string.Join(", ", result.Errors.Select(error => error.Description)));
            }
        }

        var cancellationToken = CancellationToken.None;
        var memberships = scope.ServiceProvider.GetRequiredService<IHouseholdMembershipQuery>();
        var membership = await memberships.FindByUserIdAsync(user.Id, cancellationToken);
        var households = scope.ServiceProvider.GetRequiredService<IHouseholdRepository>();
        var household = membership is null
            ? await scope.ServiceProvider.GetRequiredService<CreateHousehold>().HandleAsync(
                "Demohemmet", user.Id, DemoName, cancellationToken)
            : await households.FindByIdAsync(membership.HouseholdId, cancellationToken)
                ?? throw new InvalidOperationException("The demo user's household was not found.");

        var addMember = scope.ServiceProvider.GetRequiredService<AddHouseholdMember>();
        var demoMember = household.Members.First(member => member.UserId == user.Id);
        if (household.Members.All(member => member.DisplayName != "Alex"))
        {
            await addMember.HandleAsync(household.Id, "Alex", WeeklyTimeBudget.Uniform(30), cancellationToken);
        }

        // A third member makes this a real family to test against, not just a couple, and
        // gives rotation something more interesting to cycle through than two people.
        if (household.Members.All(member => member.DisplayName != "Charlie"))
        {
            await addMember.HandleAsync(household.Id, "Charlie", WeeklyTimeBudget.Uniform(15), cancellationToken);
        }

        // A brand-new household starts with no budget for its creator by design (see
        // CreateHousehold) - the seed gives the demo account a normal week so "Min dag" has
        // something to plan. Only when still untouched, so a tester's own change survives a restart.
        if (demoMember.WeeklyTimeBudget.TotalWeeklyMinutes == 0)
        {
            var setWeeklyBudget = scope.ServiceProvider.GetRequiredService<SetMemberWeeklyBudget>();
            await setWeeklyBudget.HandleAsync(
                household.Id, demoMember.Id, WeeklyTimeBudget.Uniform(30), cancellationToken);
        }

        var addArea = scope.ServiceProvider.GetRequiredService<AddArea>();
        var kitchen = household.Areas.FirstOrDefault(area => area.Name == "Kök")
            ?? await addArea.HandleAsync(household.Id, "Kök", cancellationToken);
        var bathroom = household.Areas.FirstOrDefault(area => area.Name == "Badrum")
            ?? await addArea.HandleAsync(household.Id, "Badrum", cancellationToken);

        var createTask = scope.ServiceProvider.GetRequiredService<CreateTaskDefinition>();
        var taskDefinitions = scope.ServiceProvider.GetRequiredService<ITaskDefinitionRepository>();
        var existingTasks = await taskDefinitions.ListByHouseholdAsync(household.Id, cancellationToken);
        var washDishes = existingTasks.FirstOrDefault(task => task.Name == "Diska");

        if (washDishes is null)
        {
            washDishes = await createTask.HandleAsync(
                household.Id,
                new NewTaskDefinition(
                    "Diska", 20, "Töm maskinen och plocka undan.", kitchen?.Id,
                    TaskPriority.Normal, demoMember.Id),
                cancellationToken);
        }

        if (existingTasks.All(task => task.Name != "Rengör handfatet"))
        {
            await createTask.HandleAsync(
                household.Id,
                new NewTaskDefinition(
                    "Rengör handfatet", 15, AreaId: bathroom?.Id,
                    Priority: TaskPriority.Low, DefaultResponsibleMemberId: demoMember.Id),
                cancellationToken);
        }

        var scheduleTask = scope.ServiceProvider.GetRequiredService<ScheduleTaskOccurrence>();

        if (washDishes is not null && existingTasks.All(task => task.Name != "Diska"))
        {
            await scheduleTask.HandleAsync(
                household.Id,
                washDishes.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                demoMember.Id,
                cancellationToken);
        }

        // A recurring, rotating task, so logging in actually exercises EnsureOccurrencesGenerated
        // and RotationPicker instead of only ever showing hand-scheduled work. The recurrence
        // starts today so it is visible the first time anyone loads Min dag, not next week.
        if (existingTasks.All(task => task.Name != "Dammsug vardagsrum"))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var vacuum = await createTask.HandleAsync(
                household.Id,
                new NewTaskDefinition(
                    "Dammsug vardagsrum", 15, AreaId: null,
                    Priority: TaskPriority.Normal, HasRotatingResponsibility: true,
                    Recurrence: RecurrenceRule.Weekly(today, today.DayOfWeek)),
                cancellationToken);

            if (vacuum is not null)
            {
                await scheduleTask.HandleAsync(household.Id, vacuum.Id, today, assignToMemberId: null, cancellationToken);
            }
        }

        // A week's worth of history, mostly done, so the household overview's weekly matrix
        // and "senaste händelser" have something real to show on first login instead of an
        // empty week. Only seeded once - later runs must not keep completing today's copy.
        if (existingTasks.All(task => task.Name != "Torka av köksbänken"))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var weekAgo = today.AddDays(-6);

            var counterWipe = await createTask.HandleAsync(
                household.Id,
                new NewTaskDefinition(
                    "Torka av köksbänken", 5, AreaId: kitchen?.Id,
                    Priority: TaskPriority.Low, DefaultResponsibleMemberId: demoMember.Id,
                    Recurrence: RecurrenceRule.Daily(weekAgo)),
                cancellationToken);

            if (counterWipe is not null)
            {
                var completeTask = scope.ServiceProvider.GetRequiredService<CompleteTaskOccurrence>();

                for (var date = weekAgo; date <= today; date = date.AddDays(1))
                {
                    var occurrence = await scheduleTask.HandleAsync(
                        household.Id, counterWipe.Id, date, demoMember.Id, cancellationToken);

                    if (occurrence is not null && date < today)
                    {
                        await completeTask.HandleAsync(household.Id, occurrence.Id, demoMember.Id, cancellationToken);
                    }
                }
            }
        }

        // Exercises the preferences endpoint end to end, even without a settings page yet.
        var preferences = scope.ServiceProvider.GetRequiredService<IMemberPreferenceRepository>();
        if (await preferences.FindAsync(household.Id, demoMember.Id, cancellationToken) is null)
        {
            var setPreference = scope.ServiceProvider.GetRequiredService<SetMemberPreference>();
            await setPreference.HandleAsync(
                household.Id, demoMember.Id, PresentationMode.LargeText, MotivationLevel.Calm, cancellationToken);
        }
    }
}