namespace BusInfo.Services
{
    public interface IEmailTemplateService
    {
        string GetPasswordResetTemplate(string resetLink);
        string GetAccountDeletionTemplate(int gracePeriodDays);
        string GetAccountReactivationTemplate();
    }
}
