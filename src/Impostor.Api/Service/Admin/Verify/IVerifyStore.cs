namespace Impostor.Api.Service.Admin.Verify
{
    public interface IVerifyStore
    {
        void AddPending(string friendCode, string qqNumber);

        bool ValidateSecret(string secret);

        bool TryConfirm(string friendCode, string qqNumber);
    }
}
