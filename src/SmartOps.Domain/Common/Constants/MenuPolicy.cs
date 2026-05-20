namespace SmartOps.Domain.Common.Constants;

public static class MenuPolicy
{
    public static string For(string menuCode, MenuPermissionAction action) =>
        $"Menu:{menuCode}:{action}";

    public static string View(string menuCode) => For(menuCode, MenuPermissionAction.View);

    public static string Add(string menuCode) => For(menuCode, MenuPermissionAction.Add);

    public static string Edit(string menuCode) => For(menuCode, MenuPermissionAction.Edit);

    public static string Delete(string menuCode) => For(menuCode, MenuPermissionAction.Delete);

    public static string Export(string menuCode) => For(menuCode, MenuPermissionAction.Export);
}
