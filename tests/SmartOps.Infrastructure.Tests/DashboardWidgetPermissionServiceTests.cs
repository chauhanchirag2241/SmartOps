using Moq;
using Xunit;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Constants;
using SmartOps.Infrastructure.Modules.Authorization.Services;

namespace SmartOps.Infrastructure.Tests;

public sealed class DashboardWidgetPermissionServiceTests
{
    [Fact]
    public async Task GetVisibleWidgetsAsync_ReturnsOnlyRoleWidgetsWithMenuView()
    {
        Guid userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        currentUser.Setup(c => c.UserId).Returns(userId);

        var widgetRepo = new Mock<IDashboardWidgetRepository>();
        widgetRepo
            .Setup(r => r.GetUserWidgetCodesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                DashboardWidgetCodes.StudentsStat,
                DashboardWidgetCodes.SalaryDisbursed,
                DashboardWidgetCodes.EmployeesStat
            ]);

        widgetRepo
            .Setup(r => r.GetWidgetTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Template(DashboardWidgetCodes.StudentsStat, MenuCodes.Students, 1),
                Template(DashboardWidgetCodes.SalaryDisbursed, MenuCodes.SalaryPayroll, 5),
                Template(DashboardWidgetCodes.EmployeesStat, MenuCodes.Employees, 2),
            ]);

        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(p => p.EnsureLoadedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        permissionService.Setup(p => p.HasViewAccess(MenuCodes.Students)).Returns(true);
        permissionService.Setup(p => p.HasViewAccess(MenuCodes.SalaryPayroll)).Returns(false);
        permissionService.Setup(p => p.HasViewAccess(MenuCodes.Employees)).Returns(true);

        var sut = new DashboardWidgetPermissionService(
            currentUser.Object,
            widgetRepo.Object,
            permissionService.Object);

        IReadOnlyList<DashboardWidgetLayoutItemDto> result = await sut.GetVisibleWidgetsAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, w => w.Code == DashboardWidgetCodes.StudentsStat);
        Assert.Contains(result, w => w.Code == DashboardWidgetCodes.EmployeesStat);
        Assert.DoesNotContain(result, w => w.Code == DashboardWidgetCodes.SalaryDisbursed);
    }

    [Fact]
    public async Task GetVisibleWidgetsAsync_DeduplicatesWidgetTemplatesByCode()
    {
        Guid userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        currentUser.Setup(c => c.UserId).Returns(userId);

        var widgetRepo = new Mock<IDashboardWidgetRepository>();
        widgetRepo
            .Setup(r => r.GetUserWidgetCodesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DashboardWidgetCodes.AttendanceRate]);

        var duplicate = Template(DashboardWidgetCodes.AttendanceRate, MenuCodes.Attendance, 8);
        widgetRepo
            .Setup(r => r.GetWidgetTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate, duplicate, duplicate]);

        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(p => p.EnsureLoadedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        permissionService.Setup(p => p.HasViewAccess(MenuCodes.Attendance)).Returns(true);

        var sut = new DashboardWidgetPermissionService(
            currentUser.Object,
            widgetRepo.Object,
            permissionService.Object);

        IReadOnlyList<DashboardWidgetLayoutItemDto> result = await sut.GetVisibleWidgetsAsync();

        Assert.Single(result);
        Assert.Equal(DashboardWidgetCodes.AttendanceRate, result[0].Code);
    }

    [Fact]
    public async Task GetVisibleWidgetsAsync_AccountantScenario_ExcludesStudentsWithoutMenu()
    {
        Guid userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.IsAuthenticated).Returns(true);
        currentUser.Setup(c => c.UserId).Returns(userId);

        var widgetRepo = new Mock<IDashboardWidgetRepository>();
        widgetRepo
            .Setup(r => r.GetUserWidgetCodesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                DashboardWidgetCodes.SalaryStatus,
                DashboardWidgetCodes.SalaryDisbursed,
                DashboardWidgetCodes.StudentsStat
            ]);

        widgetRepo
            .Setup(r => r.GetWidgetTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Template(DashboardWidgetCodes.StudentsStat, MenuCodes.Students, 1),
                Template(DashboardWidgetCodes.SalaryStatus, MenuCodes.SalaryPayroll, 5),
                Template(DashboardWidgetCodes.SalaryDisbursed, MenuCodes.SalaryPayroll, 7),
            ]);

        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(p => p.EnsureLoadedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        permissionService.Setup(p => p.HasViewAccess(MenuCodes.Students)).Returns(false);
        permissionService.Setup(p => p.HasViewAccess(MenuCodes.SalaryPayroll)).Returns(true);

        var sut = new DashboardWidgetPermissionService(
            currentUser.Object,
            widgetRepo.Object,
            permissionService.Object);

        IReadOnlyList<DashboardWidgetLayoutItemDto> result = await sut.GetVisibleWidgetsAsync();

        Assert.Equal(2, result.Count);
        Assert.True(result.All(w =>
            w.Code == DashboardWidgetCodes.SalaryStatus || w.Code == DashboardWidgetCodes.SalaryDisbursed));
    }

    private static RoleDashboardWidgetPermissionDto Template(string code, string menu, int order) =>
        new()
        {
            WidgetCode = code,
            WidgetName = code,
            Category = "Test",
            RequiredMenuCode = menu,
            DisplayOrder = order,
            DefaultSize = "stat",
            CanView = true
        };
}
