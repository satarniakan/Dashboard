namespace Dashboard.Domain.Identity;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Colleague = "Colleague";
    public const string Salesperson = "Salesperson";
    public const string WarehouseUser = "WarehouseUser";
    public const string AccountingUser = "AccountingUser";
    public const string User = "User";
    public const string FirstAdminPhoneNumber = "09125993396"; // شماره موبایل خودت رو اینجا بذار
    public static readonly string[] All =
    {
        Admin, Colleague, Salesperson, WarehouseUser, AccountingUser, User
    };

    public static string ToPersian(string roleName) => roleName switch
    {
        Admin => "ادمین",
        Colleague => "همکار",
        Salesperson => "فروشنده",
        WarehouseUser => "کاربر انبار",
        AccountingUser => "کاربر حسابداری",
        User => "کاربر",
        _ => roleName
    };
}